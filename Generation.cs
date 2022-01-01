using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Linq;
using System.Text;
using System.Threading;
using UnityEngine;
using UnityEngine.Profiling;

using Den.Tools;
using Den.Tools.Tasks;
using MapMagic.Core;
using Den.Tools.Matrices;
using MapMagic.Products;
using MapMagic.Nodes;
using MapMagic.Nodes.MatrixGenerators;
using MapMagic.Terrains;

using xshazwar.Renderer;

namespace xshazwar {
        
        public interface IHandlePosition
        {
            public Action<Vector2, Vector2> OnRangeUpdated {get; set;}
        }

        public interface IReportStatus {
            public Action<Coord> OnTileRendered {get; set;}
            public Action<Coord> OnTileReleased {get; set;}
        }

        public enum Resolution {_6=6, _8=8, _17=17, _33=33, _65=65, _129=129, _257=257, _513=513, _1025=1025, _2049=2049 };
        public class BillboardLoD{            
            public int id;
            public Coord coord;
            public Resolution resolution;
            public int margin;
            public Vector2D tileSize;
            public TileData data;
            public StopToken stop;
            public BillboardLoD(){
                this.data = new TileData();
            }

            public static BillboardLoD CloneSettings(BillboardLoD o, int newID, Coord coord){
                return new BillboardLoD(newID, coord, o.resolution, o.margin, o.tileSize);
            }

            public BillboardLoD(int id): this(){
                this.id = id;
            }
            public BillboardLoD(int id, Coord coord, Resolution resolution, int margin, Vector2D tileSize): this(id){
                setParams(coord, resolution, margin, tileSize);
            }
            public void setParams(Coord c, Resolution resolution, int margin, Vector2D tileSize){
                this.coord = c;
                this.resolution = resolution;
                this.margin = margin;
                this.tileSize = tileSize;
                this.data.area = new Area(c, (int)resolution, margin, tileSize);
            }

            public void recycle(int id, Coord coord){
                this.id = id;
                this.coord = coord;
                this.data = new TileData();
                this.data.area = new Area(coord, (int)resolution, margin, tileSize);
            }
        }

        public class MapMagicListener: IHandlePosition {
            HashSet<Coord> active; 
            Vector2 xRange;
            Vector2 zRange;
            Vector2 xRangePrevious;
            Vector2 zRangePrevious;

            public Action<Vector2, Vector2> OnRangeUpdated {get; set;}
            public Action<Coord> OnTileActivated;
            public Action<Coord> OnTileRemoved;

            public MapMagicListener(){
                active = new HashSet<Coord>();
                xRange = new Vector2();
                zRange = new Vector2();
                xRangePrevious = new Vector2();
                zRangePrevious = new Vector2();
                TerrainTile.OnLodSwitched += LodSwitched;
            }

            public void CalcActive(){
                xRangePrevious = xRange;
                zRangePrevious = zRange;
                xRange.x = active.Select(v => v.x).Min();
                xRange.y = active.Select(v => v.x).Max();
                zRange.x = active.Select(v => v.z).Min();
                zRange.y = active.Select(v => v.z).Max();
                if (xRangePrevious != xRange || zRangePrevious != zRange){
                    OnRangeUpdated?.Invoke(xRange, zRange);
                }
            }

            // public static Action<TerrainTile, bool, bool> OnLodSwitched;
            public void LodSwitched(TerrainTile tile, bool isMain, bool isDraft){
                if (!isMain && !isDraft){
                    lock(active){
                        active.Remove(tile.coord);
                        OnTileRemoved?.Invoke(tile.coord);
                        CalcActive();
                    }
                }else{
                    lock(active){
                        active.Add(tile.coord);
                        OnTileActivated?.Invoke(tile.coord);
                        CalcActive();
                    }
                }               
            }

            public void Disconnect(){
                TerrainTile.OnLodSwitched -= LodSwitched;
                active = new HashSet<Coord>();
            }
        }

        public enum TileStatus {
            ACTIVE, // dont care if draft or main really
            BB,
            CULLED

        };
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
            Graph graph;
            MapMagicListener listener;
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
            
            public Generator(MapMagicObject mm, Generator leader, TerrainRenderer renderer, Resolution resolution, int margin, Vector2D tileSize, int range, int ignoreSize = 0, Vector3 startingPosition = new Vector3()){
                
                this.mm = mm;
                this.leader = leader;
                this.graph = mm.graph;
                size = tileSize.x;
                this.range = range;
                this.ignoreSize = ignoreSize;
                Coord zero; zero.x = 0; zero.z = 0; 
                prototype = new BillboardLoD(-100, zero, resolution, margin, tileSize);
                billboards = new ConcurrentQueue<BillboardLoD>();
                tokens = new ConcurrentQueue<TileToken>();
                changeQueue = new ConcurrentQueue<TileToken>();
                currentState = new Dictionary<Coord, TileToken>();
                this.renderer = renderer;
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
                if (position != newPosition || changeQueue.Count > 0){
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
                if (position != newPosition){
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
                List<TileToken> updates = new List<TileToken>();
                while (changeQueue.TryDequeue(out tt)){
                    updates.Add(GetToken(tt));
                    
                }
                foreach(TileToken update in updates){
                    try{
                        DoChange(currentState[update.coord], update);
                    }catch(KeyNotFoundException){
                        DoChange(null, update);   
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

            public IEnumerable<int> inRange(int pos, int _range){
                for (int i = pos - _range; i <= pos + _range; i++ ){
                    yield return i;
                }
            }

            public void FillGrid(){
                Coord t;
                // enqueue new tiles
                foreach(int x in inRange(newPosition.x, range)){
                    foreach(int z in inRange(newPosition.z, range)){
                        // ignore exclusion zone && core tiles
                        if (inIgnore(x, z, newPosition.x, newPosition.z, ignoreSize)){
                            continue;
                        }
                        t.x = x; t.z = z;
                        if (!currentState.ContainsKey(t)){
                            UnityEngine.Profiling.Profiler.BeginSample("GetToken");
                            TileToken nt = GetToken(new Coord(t.x, t.z), TileStatus.BB);
                            UnityEngine.Profiling.Profiler.EndSample();
                            currentState[nt.coord] = nt;
                            UnityEngine.Profiling.Profiler.BeginSample("Enqueue Token Gen");
                            EnqueueTileRequest(this.renderer, nt);
                            UnityEngine.Profiling.Profiler.EndSample();
                        }
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
            public void EnqueueTileRequest(TerrainRenderer renderer, TileToken token){
                ThreadPool.QueueUserWorkItem( state => RequestTile(renderer, token));
            }

            public TileToken GetToken(Coord coord, TileStatus status){
                TileToken tt = new TileToken(coord, status);
                // TODO turn this back into a pool
                // TileToken tt;
                // if(!tokens.TryDequeue(out tt)){
                //     tt = new TileToken(coord, status);
                // }
                tt.Recycle(coord, status);
                return tt;
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
                TileToken newToken = GetToken(token.coord, TileStatus.BB);
                newToken.id = nextID;
                BillboardLoD lod;
                if (!billboards.TryDequeue(out lod)){
                    lod = BillboardLoD.CloneSettings(prototype, nextID, newToken.coord);
                }else{
                    lod.recycle(nextID, newToken.coord);
                }
                StartGenerate(lod);
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
                TileToken tt = GetToken(coord, TileStatus.ACTIVE);
                changeQueue.Enqueue(tt);
            }

            public void TileRemoved(Coord coord){
                TileToken tt = GetToken(coord, TileStatus.CULLED);
                changeQueue.Enqueue(tt);
            }
            
            public void StartGenerate(BillboardLoD lod){
                lod.data.globals = mm.globals;
				lod.data.random = graph.random;
                GetHeights(lod);
            }

            public void GetHeights(BillboardLoD lod){
                lod.stop = new StopToken();
                graph.Generate(lod.data, lod.stop);
                this.FinalizeHeights(lod);
            }

            public void RangeUpdate(Vector2 xRange, Vector2 zRange){
                OnNewTileset(xRange, zRange);
            }

            public void FinalizeHeights(BillboardLoD lod){
                TileData data = lod.data;
                if (data.heights == null || 
                    data.heights.rect.size != data.area.full.rect.size || 
                    data.heights.worldPos != (Vector3)data.area.full.worldPos || 
                    data.heights.worldSize != (Vector3)data.area.full.worldSize) 
                        data.heights = new MatrixWorld(data.area.full.rect, data.area.full.worldPos, data.area.full.worldSize, data.globals.height);
                data.heights.worldSize.y = data.globals.height;
                data.heights.Fill(0);	

                foreach ((HeightOutput200 output, MatrixWorld product, MatrixWorld biomeMask) 
                    in data.Outputs<HeightOutput200,MatrixWorld,MatrixWorld> (typeof(HeightOutput200), inSubs:true) )
                {
                    if (data.heights == null) //height output not generated or received null result
                        return;

                    float val;
                    float biomeVal;
                    for (int a=0; a<data.heights.arr.Length; a++)
                    {
                        if (lod.stop!=null && lod.stop.stop) return;

                        val = product.arr[a];
                        biomeVal = biomeMask!=null ? biomeMask.arr[a] : 1;

                        data.heights.arr[a] += val * biomeVal;
                    }
                }
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
