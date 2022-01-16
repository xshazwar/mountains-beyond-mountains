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

        public enum Tracker {
            MM,
            SIMPLE
        }

        public class Generator : IHandlePosition, IReportStatus {
            MapMagicObject mm;  
            Tracker tracker;  
            MapMagicListener listener;
            CameraPosition cameraPosition;
            MapMagicSource source;
            Generator leader;
            float size;
            int range;
            int ignoreSize;
            public Action<Vector2, Vector2> OnRangeUpdated {get; set;}
            public Action<GridPos> OnTileRendered {get; set;}
            public Action<GridPos> OnTileReleased {get; set;}
            private TaskFactory taskFactory;
            BillboardLoD prototype;
            private GridPos position;
            private GridPos newPosition;
            private Vector2 xRange;
            private Vector2 zRange;
            private int[] xRangeTiles;
            private int[] zRangeTiles;
            private ConcurrentQueue<BillboardLoD> billboards;
            private ConcurrentQueue<TileToken> changeQueue;

            private Dictionary<GridPos, TileToken> currentState;
            private TerrainRenderer renderer;
            
            public Generator(
                MapMagicObject mm,
                Generator leader,
                TerrainRenderer renderer,
                Tracker tracker,
                Camera camera,
                Resolution resolution,
                int margin,
                Vector2 tileSize,
                int range,
                int ignoreSize = 0,
                Vector3 startingPosition = new Vector3()
            ){
                
                this.mm = mm;
                this.leader = leader;
                this.tracker = tracker;
                size = tileSize.x;
                this.range = range;
                this.ignoreSize = ignoreSize;
                position = new GridPos(0, 0);
                newPosition = new GridPos(0, 0);
                GridPos zero = new GridPos(0, 0);
                prototype = new BillboardLoD(-100, zero, resolution, margin, tileSize);
                billboards = new ConcurrentQueue<BillboardLoD>();
                changeQueue = new ConcurrentQueue<TileToken>();
                currentState = new Dictionary<GridPos, TileToken>();
                this.renderer = renderer;
                taskFactory = new TaskFactory(
                    new System.Threading.CancellationToken(),
                    TaskCreationOptions.LongRunning,
                    TaskContinuationOptions.DenyChildAttach,
                    new LimitedConcurrencyLevelTaskScheduler(16));

                source = new MapMagicSource(mm);
                if (leader != null){
                    leader.OnRangeUpdated += RangeUpdate;
                    leader.OnTileRendered += TileActivated;
                    leader.OnTileReleased += TileRemoved;
                }
                else{
                    listener = new MapMagicListener();
                    if (tracker == Tracker.MM){
                        listener.OnRangeUpdated += RangeUpdate;
                    }else{
                        cameraPosition = new CameraPosition(camera, (int)tileSize.x, range);
                        cameraPosition.OnRangeUpdated += RangeUpdate;
                        // cameraPosition.Poll();
                    }
                    listener.OnTileRendered += TileActivated;
                    listener.OnTileReleased += TileRemoved;
                    avoidStartingMMTiles();
                }
                xRangeTiles = new int[2 * range + 1];
                zRangeTiles = new int[2 * range + 1];
                newPosition.x = (int) Math.Floor(startingPosition.x / size);
                newPosition.z = (int) Math.Floor(startingPosition.z/ size);
                OnUpdate();
            }

            public void OnNewTileset(Vector2 xRange, Vector2 zRange){
                Debug.Log("OnNewTileset");
                this.xRange = xRange;
                this.zRange = zRange;
            }

            public void Update(){
                // cameraPosition?.Poll();
                newPosition.x = (int)(xRange.x + xRange.y) / 2;
                newPosition.z = (int)(zRange.x + zRange.y) / 2;
                if (!position.Equals(newPosition) || changeQueue.Count > 0){
                    OnUpdate();
                    position.x = newPosition.x; position.z = newPosition.z;
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

            public void GetRange(int pos, int _range, ref int[] v){
                for (int i = 0; i <= 2 * _range; i++ ){
                    v[i] = pos + i  - _range;
                }
            }

            public void FillGrid(){
                GridPos t = new GridPos(0,0);
                // enqueue new tiles
                GetRange(newPosition.x, range, ref xRangeTiles);
                GetRange(newPosition.z, range, ref zRangeTiles);
                foreach(int x in xRangeTiles){
                    foreach(int z in zRangeTiles){
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
                            TileToken nt = new TileToken(new GridPos(x, z), TileStatus.BB);
                            UnityEngine.Profiling.Profiler.EndSample();
                            UnityEngine.Profiling.Profiler.BeginSample("UpdateState");
                            currentState[nt.coord] = nt;
                            UnityEngine.Profiling.Profiler.EndSample();
                            UnityEngine.Profiling.Profiler.BeginSample("Enqueue Height Gen");
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
                foreach(GridPos c in source.GetStartingTileCoords()){
                    TileActivated(c);
                }
                
            }
            public void EnqueueTileRequest(TileToken token){
                taskFactory.StartNew( () => RequestTile(this.renderer, token) );
            }

            public TileToken GetToken(GridPos coord, TileStatus status){
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
                token.id = nextID;
                BillboardLoD lod;
                if (!billboards.TryDequeue(out lod)){
                    lod = BillboardLoD.CloneSettings(prototype, nextID, token.coord);
                }else{
                    lod.recycle(nextID, token.coord);
                }
                source.StartGenerate(lod);
                renderer.setBillboardHeights(lod.id, lod.data.heights.arr);
                renderer.setBillboardPosition(nextID, token.coord.x * size, token.coord.z * size, 0f, false);
                billboards.Enqueue(lod);
                changeQueue.Enqueue(token);
                OnTileRendered?.Invoke(token.coord);
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

            public void TileActivated(GridPos coord){
                changeQueue.Enqueue(new TileToken(coord, TileStatus.ACTIVE));
            }

            public void TileRemoved(GridPos coord){
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
                    if (tracker == Tracker.MM){
                        listener.OnRangeUpdated -= RangeUpdate;
                    }else{
                        
                        cameraPosition.OnRangeUpdated -= RangeUpdate;
                    }
                    listener.OnTileRendered -= TileActivated;
                    listener.OnTileReleased -= TileRemoved;
                    listener.Disconnect();
                }
                
            }
        }
}
