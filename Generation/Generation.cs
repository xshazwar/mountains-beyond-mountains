using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Profiling;

using MapMagic.Core;

using xshazwar.Renderer;

namespace xshazwar.Generation {

        public class TileToken{
            public int? id;
            public Coord coord;
            public TileStatus status;

            public TileToken(int? id, Coord coord, TileStatus status){
                this.id = id;
                this.coord = coord;
                this.status = status;
            }

            public TileToken(Coord coord, TileStatus status): this(null, coord, status){}

            public TileToken(TileToken t): this(t.id, t.coord, t.status){}

            public override int GetHashCode(){
                return coord.x * 1000000 + coord.z;
            }

            public void UpdateWith(TileToken t){
                id = t.id;
                status = t.status;
            }

            public void Recycle(int? id, Coord coord, TileStatus status){
                this.id = id;
                this.coord = coord;
                this.status = status;
            }

            public void Recycle(Coord coord, TileStatus status){
                Recycle(null, coord, status);
            }


        }

        public class Generator : IHandlePosition, IReportStatus {
            MapMagicObject mm;    
            MapMagicListener listener;
            MapMagicSource source;
            Generator leader;
            float size;
            int range;
            int ignoreSize;
            public Action<Vector2, Vector2> OnRangeUpdated {get; set;}
            public Action<Coord> OnTileRendered {get; set;}
            public Action<Coord> OnTileReleased {get; set;}
            BillboardLoD prototype;
            private Coord position;
            private Coord newPosition;
            private Vector2 xRange;
            private Vector2 zRange;
            private ConcurrentQueue<BillboardLoD> billboards;
            private ConcurrentQueue<TileToken> tokens; //cache

            private ConcurrentQueue<TileToken> changeQueue;

            private Dictionary<Coord, TileToken> currentState;
            private TerrainRenderer renderer;
            
            public Generator(MapMagicObject mm, Generator leader, TerrainRenderer renderer, Resolution resolution, int margin, Vector2 tileSize, int range, int ignoreSize = 0, Vector3 startingPosition = new Vector3()){
                
                this.mm = mm;
                this.leader = leader;
                size = tileSize.x;
                this.range = range;
                this.ignoreSize = ignoreSize;
                position = new Generation.Coord(0, 0);
                newPosition = new Generation.Coord(0, 0);
                Generation.Coord zero = new Generation.Coord(0, 0);
                prototype = new BillboardLoD(-100, zero, resolution, margin, tileSize);
                billboards = new ConcurrentQueue<BillboardLoD>();
                tokens = new ConcurrentQueue<TileToken>();
                changeQueue = new ConcurrentQueue<TileToken>();
                currentState = new Dictionary<Coord, TileToken>();
                this.renderer = renderer;
                source = new MapMagicSource(mm);
                if (leader != null){
                    leader.OnRangeUpdated += RangeUpdate;
                    leader.OnTileRendered += TileActivated;
                    leader.OnTileReleased += TileRemoved;
                }
                else{
                    listener = new MapMagicListener();
                    listener.OnRangeUpdated += RangeUpdate;
                    listener.OnTileActivated += TileActivated;
                    listener.OnTileRemoved += TileRemoved;
                    avoidStartingMMTiles();
                }
                
                newPosition.x = (int) Math.Floor(startingPosition.x / size);
                newPosition.z = (int) Math.Floor(startingPosition.z/ size);
                OnUpdate();
            }

            public void OnNewTileset(Vector2 xRange, Vector2 zRange){
                this.xRange = xRange;
                this.zRange = zRange;
            }

            public void Update(){
                newPosition.x = (int)(xRange.x + xRange.y) / 2;
                newPosition.z = (int)(zRange.x + zRange.y) / 2;
                if (!position.Equals(newPosition) || changeQueue.Count > 0){
                    OnUpdate();
                    position = newPosition;
                    OnRangeUpdated?.Invoke(this.xRange, this.zRange);
                }
            }
            public void OnUpdate(){
                if (!renderer.isReady){
                    Debug.Log("Renderer not ready for work...");
                    return;
                }
                if (!position.Equals(newPosition)){
                    UnityEngine.Profiling.Profiler.BeginSample("Cull Grid");
                    CullGrid();
                    UnityEngine.Profiling.Profiler.EndSample();
                }
                UnityEngine.Profiling.Profiler.BeginSample("StartJobs");
                StartJobs();
                UnityEngine.Profiling.Profiler.EndSample();
                UnityEngine.Profiling.Profiler.BeginSample("Fill Grid");
                FillGrid();
                UnityEngine.Profiling.Profiler.EndSample();
                UnityEngine.Profiling.Profiler.BeginSample("Service Queue");
                ServiceQueue();
                UnityEngine.Profiling.Profiler.EndSample();
            }

            public void ServiceQueue(){
                TileToken tt;
                while (changeQueue.TryDequeue(out tt)){
                    try{
                        DoChange(currentState[tt.coord], tt);
                    }catch(KeyNotFoundException){
                        DoChange(null, tt);   
                    }
                }
            }

            public async void StartJobs(){

            }

            public bool inIgnore(int x, int z, int posX, int posZ, int ignore){
                return (
                    x > posX - ignore &&
                    x < posX + ignore &&
                    z > posZ - ignore &&
                    z < posZ + ignore);
            }

            public bool outOfRange(int x, int z, int posX, int posZ, int _range){
                return (x < posX - _range ||
                        x > posX + _range ||
                        z < posZ - _range ||
                        z > posZ + _range);
            }

            public int[] GetRange(int pos, int _range){
                int[] v = new int[(2 * _range) + 1];
                for (int i = 0; i <= 2 * _range; i++ ){
                    v[i] = i - _range;
                }
                return v;
            }

            public void FillGrid(){
                Coord t = new Generation.Coord(0,0);
                // enqueue new tiles
                int[] xRange = GetRange(newPosition.x, range);
                int[] zRange = GetRange(newPosition.z, range);

                foreach(int x in xRange){
                    foreach(int z in zRange){
                        // ignore exclusion zone && core tiles
                        UnityEngine.Profiling.Profiler.BeginSample("CheckIgnore");
                        if (inIgnore(x, z, newPosition.x, newPosition.z, ignoreSize)){
                            UnityEngine.Profiling.Profiler.EndSample();
                            continue;
                        }
                        UnityEngine.Profiling.Profiler.EndSample();
                        t.x = x; t.z = z;
                        UnityEngine.Profiling.Profiler.BeginSample("CheckKey");
                        if (!currentState.ContainsKey(t)){
                            UnityEngine.Profiling.Profiler.EndSample();
                            UnityEngine.Profiling.Profiler.BeginSample("GetToken");
                            TileToken nt = new TileToken(new Coord(t.x, t.z), TileStatus.BB);
                            UnityEngine.Profiling.Profiler.EndSample();
                            UnityEngine.Profiling.Profiler.BeginSample("UpdateState");
                            currentState[nt.coord] = nt;
                            UnityEngine.Profiling.Profiler.EndSample();
                            UnityEngine.Profiling.Profiler.BeginSample("Enqueue Token Gen");
                            EnqueueTileRequest(nt);
                            UnityEngine.Profiling.Profiler.EndSample();
                        }else{
                            UnityEngine.Profiling.Profiler.EndSample();}
                    }
                }
            }

            public void CullGrid(){
                List<TileToken> removal = currentState.Values.Where<TileToken>(t => (
                    t.id != null &&
                    (
                        outOfRange(t.coord.x, t.coord.z, newPosition.x, newPosition.z, range
                            )||(
                        inIgnore(t.coord.x, t.coord.z, newPosition.x, newPosition.z, ignoreSize)
                    ))
                )).ToList();
                foreach(TileToken update in removal){
                    TileToken cull = GetToken(update);
                    cull.status = TileStatus.CULLED;
                    changeQueue.Enqueue(cull);
                }
            }

            public void avoidStartingMMTiles(){
                foreach(Coord c in source.GetStartingTileCoords()){
                    TileActivated(c);
                }
                
            }
            public void EnqueueTileRequest(TileToken token){
                Task.Run( () => RequestTile(this.renderer, token) );
            }

            public TileToken GetToken(Coord coord, TileStatus status){
                return new TileToken(coord, status);
            }

            public TileToken GetToken(TileToken src){
                TileToken tt = GetToken(src.coord, src.status);
                tt.id = src.id;
                return tt;
            }

            public void DoChange(TileToken prev, TileToken update){
                UnityEngine.Profiling.Profiler.BeginSample("HandleChange");
                if(prev == null){
                    switch(update.status){
                        case TileStatus.CULLED:
                            // "null tile culled"
                            break;
                        case TileStatus.ACTIVE:
                            //"null tile ACTIVATED, ignore"
                            break;
                        case TileStatus.BB:
                            // "BB added to coord"
                            break;
                    }
                }else if(prev?.id == null){
                    switch(update.status){
                        case TileStatus.CULLED:
                            // "{prev?.coord}/{prev?.status} to be culled but no ID, punt"
                            changeQueue.Enqueue(update);
                            break;
                        case TileStatus.ACTIVE:
                            // $"{prev?.coord}/{prev?.status} -> {update.id} to be ACTIVATED but no ID, punt"
                            changeQueue.Enqueue(update);
                            break;
                        case TileStatus.BB:
                            if(update.id != null){
                                // $"BB updated @{prev.coord} -> @{update.id}"
                                currentState[update.coord].id = update.id;
                            }else{
                                // $"BB requested @{prev.coord} -> @{prev.id}"
                                // waiting on height for callback ^^
                            }
                            break;
                    }
                }else{
                    switch(update.status){
                        case TileStatus.CULLED:
                            if(prev.status == TileStatus.ACTIVE){
                                // $"active tile removed @{prev.coord} revert -> @{prev.id} "
                                currentState[update.coord].status = TileStatus.BB;
                                ShowTile(renderer, (int) prev.id);

                            }else if(prev.status == TileStatus.BB){
                                // "BB removed @{prev.coord} recycle -> @{prev.id}"
                                currentState.Remove(update.coord);
                                ReleaseTile(renderer, (int) prev.id);
                                OnTileReleased?.Invoke(update.coord);
                            }
                            break;
                        case TileStatus.ACTIVE:
                            // "TileActivated @{prev.coord} -> @{prev.id}"
                            currentState[update.coord].status = TileStatus.ACTIVE;
                            HideTile(renderer, (int) prev.id);
                            OnTileReleased?.Invoke(update.coord);
                            break;
                        case TileStatus.BB:
                            if(update.id != null){
                                // $"BB updated @{prev.coord} -> @{prev.id}"
                                currentState[update.coord].id = update.id;
                                ShowTile(renderer, (int) update.id);
                            }else{
                                // waiting on height for callback ^^
                                // $"BB requested @{prev.coord} -> @{prev.id}"
                            }
                            break;
                            
                    }
                }
                UnityEngine.Profiling.Profiler.EndSample();
                
            }

            public void RequestTile(TerrainRenderer renderer, TileToken token){
                int nextID = renderer.requestTileId();
                TileToken newToken = new TileToken(token.coord, TileStatus.BB);
                newToken.id = nextID;
                BillboardLoD lod;
                if (!billboards.TryDequeue(out lod)){
                    lod = BillboardLoD.CloneSettings(prototype, nextID, newToken.coord);
                }else{
                    lod.recycle(nextID, newToken.coord);
                }
                source.StartGenerate(lod);
                renderer.setBillboardHeights(lod.id, lod.data.heights.arr);
                renderer.setBillboardPosition(nextID, newToken.coord.x * size, newToken.coord.z * size, 0f, false);
                billboards.Enqueue(lod);
                changeQueue.Enqueue(newToken);
                OnTileRendered?.Invoke(newToken.coord);
            }

            public void ReleaseTile(TerrainRenderer renderer, int tokenID){
                renderer.releaseTile(tokenID);
            }

            public void HideTile(TerrainRenderer renderer, int tokenID){
                renderer.hideBillboard(tokenID);
            }

            public void ShowTile(TerrainRenderer renderer, int tokenID){
                renderer.unhideBillboard(tokenID);
            }

            public void TileActivated(Coord coord){
                changeQueue.Enqueue(new TileToken(coord, TileStatus.ACTIVE));
            }

            public void TileRemoved(Coord coord){
                changeQueue.Enqueue(new TileToken(coord, TileStatus.CULLED));
            }
            public void RangeUpdate(Vector2 xRange, Vector2 zRange){
                OnNewTileset(xRange, zRange);
            }
            public void Disconnect(){
                if (leader != null){
                    leader.OnRangeUpdated += RangeUpdate;
                    leader.OnTileRendered -= TileActivated;
                    leader.OnTileReleased -= TileRemoved;
                }
                else{
                    listener.OnRangeUpdated -= RangeUpdate;
                    listener.OnTileActivated -= TileActivated;
                    listener.OnTileRemoved -= TileRemoved;
                    listener.Disconnect();
                }
                
            }
        }
}
