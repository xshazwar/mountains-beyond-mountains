using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine.Profiling;
using UnityEngine;

using xshazwar;
using xshazwar.Generation;
using xshazwar.Renderer;

namespace xshazwar.Generation.Base {

    [RequireComponent(typeof(GenerationLocals))]
    public abstract class BaseGenerator : MonoBehaviour, IHandlePosition, IReportStatus {
        // settings
        public GenerationGlobals globals;
        public GenerationLocals locals;
        
        // signal propogation
        public Action<GridPos> OnRangeUpdated {get; set;}
        public Action<GridPos> OnTileRendered {get; set;}
        public Action<GridPos> OnTileReleased {get; set;}

        // partner components
        [SerializeField]
        [RequireInterface(typeof(IReportStatus))]
        public UnityEngine.Object parent;
        private IReportStatus _parent;
        
        [SerializeField]
        [RequireInterface(typeof(IHandlePosition))]
        public UnityEngine.Object positionSource;
        public IHandlePosition _positionSource;

        [SerializeField]
        [RequireInterface(typeof(IProvideHeights))]
        public UnityEngine.Object heightSource;
        public IProvideHeights _heightSource;

        [SerializeField]
        [RequireInterface(typeof(IRenderTiles))]
        public UnityEngine.Object renderer;
        public IRenderTiles _renderer;

        // settings
        protected float size;  // tileSize
        protected int range;          // tile draw diatance
        protected int ignoreSize;     // parent internal draw size
        protected int heightElementCount;

        // current state
        protected GridPos position;
        protected GridPos newPosition;
        protected ConcurrentQueue<TileToken> changeQueue;
        protected Dictionary<GridPos, TileToken> currentState;
        
        // gc avoidance cache 
        protected int[] xRangeTiles;
        protected int[] zRangeTiles;
        protected TokenPool tokenPool;

        // multithreading
        protected TaskFactory taskFactory;

        public void Init(){
            taskFactory = new TaskFactory(
                new System.Threading.CancellationToken(),
                TaskCreationOptions.LongRunning,
                TaskContinuationOptions.DenyChildAttach,
                new LimitedConcurrencyLevelTaskScheduler(12));
            tokenPool = new TokenPool();
            position = new GridPos(0, 0);
            newPosition = new GridPos(0, 0);
            changeQueue = new ConcurrentQueue<TileToken>();
            currentState = new Dictionary<GridPos, TileToken>();
            xRangeTiles = new int[2 * range + 1];
            zRangeTiles = new int[2 * range + 1];
            heightElementCount = (int) locals.resolution * (int) locals.resolution;
        }

        // set communication sources
        public void SetParent(){
            this._parent.OnTileRendered += TileActivated;
            this._parent.OnTileReleased += TileRemoved;
        }

        void SetPositionSource(){
            _positionSource.OnRangeUpdated += RangeUpdate;
        }
    
        // action handlers
        protected void TileActivated(GridPos coord){
            changeQueue.Enqueue(tokenPool.Get(coord, TileStatus.ACTIVE));
        }
        protected void TileRemoved(GridPos coord){
                changeQueue.Enqueue(tokenPool.Get(coord, TileStatus.CULLED));
            }
        protected void RangeUpdate(GridPos coord){
            this.newPosition.x = coord.x;
            this.newPosition.z = coord.z;
        }

        // _renderer control
        public void ReleaseTile(int tokenID){
            _renderer.releaseTile(tokenID);
        }

        public void HideTile(int tokenID){
            _renderer.hideBillboard(tokenID);
        }

        public void ShowTile(int tokenID){
            _renderer.unhideBillboard(tokenID);
        }

        // !abstract methods!

        public abstract void RequestTile(TileToken token);

        // setup

        #if UNITY_EDITOR
        protected void OnValidate(){
            Validate();
        }
        #endif

        protected void Validate(){
            if (globals == null){
                globals = gameObject.transform.parent.gameObject.GetComponent<GenerationGlobals>();
            }
            if(locals == null){
                locals = GetComponent<GenerationLocals>();
            }
            if(globals == null){
                throw new Exception("Requires Global Settings");
            }
            if(locals == null){
                throw new Exception("Requires Local Settings");
            }
            if(renderer == null){
                throw new Exception("Renderer Required");
            }
            _renderer = (IRenderTiles) renderer;
            if (positionSource == null){
                throw new Exception("Position Source Required");
            }
            _positionSource = (IHandlePosition) positionSource;
            if (heightSource == null){
                throw new Exception("Height Source Required");
            }
            _heightSource = (IProvideHeights) heightSource;
        }

        protected void OnEnable(){
            Validate();
            size = globals.tileSize;
            range = locals.terrainEnd;
            ignoreSize = locals.terrainStart;
            Init();
            SetPositionSource();
            if (this._parent != null){
                SetParent();
            }else{
                Debug.LogWarning("Unless this LoD is the first it should have a parent");
            }
                
        }

        // teardown

        protected void OnDisable(){
            Disconnect();
        }

        // update loop handling

        public void Update(){
            if (!position.Equals(newPosition) || changeQueue.Count > 0){
                OnUpdate();
                position.x = newPosition.x; position.z = newPosition.z;
                OnRangeUpdated?.Invoke(position);
            }
        }

        public void OnUpdate(){
            if (_renderer == null || !_renderer.isReady()){
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
                        TileToken nt = tokenPool.Get(new GridPos(x, z), TileStatus.BB);
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
                TileToken cull = tokenPool.Get(update.coord, update.status);
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
                            tokenPool.Return(update);
                        }
                        break;
                    case TileStatus.ACTIVE:
                        // "TileActivated @{prev.coord} -> @{prev.id}"
                        currentState[update.coord].status = TileStatus.ACTIVE;
                        HideTile((int) prev.id);
                        OnTileReleased?.Invoke(update.coord);
                        tokenPool.Return(update);
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
            if (_parent != null){
                _parent.OnTileRendered -= TileActivated;
                _parent.OnTileReleased -= TileRemoved;
            }
            if (_positionSource != null){
                _positionSource.OnRangeUpdated -= RangeUpdate;
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