using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine.Profiling;
using UnityEngine;

using xshazwar.Generation;
using xshazwar.Renderer;

namespace xshazwar.Generation.Base {
    
    [RequireComponent(typeof(GenerationGlobals))]
    public abstract class BaseGenerator : MonoBehaviour, IHandlePosition, IReportStatus {
        // signal propogation
        public Action<Vector2, Vector2> OnRangeUpdated {get; set;}
        public Action<GridPos> OnTileRendered {get; set;}
        public Action<GridPos> OnTileReleased {get; set;}

        // partner components
        public IReportStatus parent;
        public IHandlePosition positionSource;
        public IProvideHeights heightSource;
        public IRenderTiles renderer;

        // settings
        public float size;  // tileSize
        int range;          // tile draw diatance
        int ignoreSize;     // parent internal draw size

        // current state
        private GridPos position;
        private GridPos newPosition;
        private Vector2 xRange;
        private Vector2 zRange;
        private ConcurrentQueue<TileToken> changeQueue;
        private Dictionary<GridPos, TileToken> currentState;
        
        // gc avoidance cache 
        private int[] xRangeTiles;
        private int[] zRangeTiles;

        // multithreading
        private TaskFactory taskFactory;

        public void Init(){

        }

        // set communication sources
        public void SetParent(IReportStatus parent){
            this.parent = parent;
            this.parent.OnTileRendered += TileActivated;
            this.parent.OnTileReleased += TileRemoved;
        }

        public void SetPositionSource(IHandlePosition positionSource){
            this.positionSource = positionSource;
            this.positionSource.OnRangeUpdated += RangeUpdate;
        }

        public void SetDataSource(IProvideHeights heightSource){
            this.heightSource = heightSource;
        }

        public void SetRenderer(IRenderTiles renderer){
            this.renderer = renderer;
        }
        
        // action handlers
        protected void TileActivated(GridPos coord){
            changeQueue.Enqueue(new TileToken(coord, TileStatus.ACTIVE));
        }
        protected void TileRemoved(GridPos coord){
                changeQueue.Enqueue(new TileToken(coord, TileStatus.CULLED));
            }
        protected void RangeUpdate(Vector2 xRange, Vector2 zRange){
            OnNewTileset(xRange, zRange);
        }

        // TODO REFACTOR OUT
        public void OnNewTileset(Vector2 xRange, Vector2 zRange){
            Debug.Log("OnNewTileset");
            this.xRange = xRange;
            this.zRange = zRange;
        }
        // renderer control
        public void ReleaseTile(int tokenID){
            renderer.releaseTile(tokenID);
        }

        public void HideTile(int tokenID){
            renderer.hideBillboard(tokenID);
        }

        public void ShowTile(int tokenID){
            renderer.unhideBillboard(tokenID);
        }

        // !abstract methods!

        public abstract void RequestTile(TileToken token);

        // update loop handling

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

        public void FillGrid(){
            GridPos t = new GridPos(0,0);
            // enqueue new tiles
            GridHelpers.GetRange(newPosition.x, range, ref xRangeTiles);
            GridHelpers.GetRange(newPosition.z, range, ref zRangeTiles);
            foreach(int x in xRangeTiles){
                foreach(int z in zRangeTiles){
                    // ignore exclusion zone && core tiles
                    UnityEngine.Profiling.Profiler.BeginSample("CheckIgnore");
                    if (GridHelpers.inIgnore(x, z, newPosition.x, newPosition.z, ignoreSize)){
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
                    GridHelpers.outOfRange(t.coord.x, t.coord.z, newPosition.x, newPosition.z, range
                        )||(
                    GridHelpers.inIgnore(t.coord.x, t.coord.z, newPosition.x, newPosition.z, ignoreSize)
                ))
            )).ToList();
            foreach(TileToken update in removal){
                TileToken cull = new TileToken(update);
                cull.status = TileStatus.CULLED;
                changeQueue.Enqueue(cull);
            }
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
                            ShowTile((int) prev.id);

                        }else if(prev.status == TileStatus.BB){
                            // "BB removed @{prev.coord} recycle -> @{prev.id}"
                            currentState.Remove(update.coord);
                            ReleaseTile((int) prev.id);
                            OnTileReleased?.Invoke(update.coord);
                        }
                        break;
                    case TileStatus.ACTIVE:
                        // "TileActivated @{prev.coord} -> @{prev.id}"
                        currentState[update.coord].status = TileStatus.ACTIVE;
                        HideTile((int) prev.id);
                        OnTileReleased?.Invoke(update.coord);
                        break;
                    case TileStatus.BB:
                        if(update.id != null){
                            // $"BB updated @{prev.coord} -> @{prev.id}"
                            currentState[update.coord].id = update.id;
                            ShowTile((int) update.id);
                        }else{
                            // waiting on height for callback ^^
                            // $"BB requested @{prev.coord} -> @{prev.id}"
                        }
                        break;
                        
                }
            }
            UnityEngine.Profiling.Profiler.EndSample();
            
        }
        public void EnqueueTileRequest(TileToken token){
            taskFactory.StartNew( () => RequestTile(token) );
        }

        public void Disconnect(){
            if (parent != null){
                parent.OnTileRendered -= TileActivated;
                parent.OnTileReleased -= TileRemoved;
            }
            if (positionSource != null){
                positionSource.OnRangeUpdated -= RangeUpdate;
            }
        }
    }

    public static class GridHelpers {
        public static bool inIgnore(int x, int z, int posX, int posZ, int ignore){
                return (
                    x > posX - ignore &&
                    x < posX + ignore &&
                    z > posZ - ignore &&
                    z < posZ + ignore);
            }

        public static bool outOfRange(int x, int z, int posX, int posZ, int _range){
            return (x < posX - _range ||
                    x > posX + _range ||
                    z < posZ - _range ||
                    z > posZ + _range);
        }

        public static void GetRange(int pos, int _range, ref int[] v){
            for (int i = 0; i <= 2 * _range; i++ ){
                v[i] = pos + i  - _range;
            }
        }
    }
}