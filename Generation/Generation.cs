// using System;
// using System.Collections;
// using System.Collections.Generic;
// using System.Collections.Concurrent;
// using System.Linq;
// using System.Text;
// using System.Threading.Tasks;
// using UnityEngine;
// using UnityEngine.Profiling;

// using MapMagic.Core;

// using xshazwar.Generation.Base;
// using xshazwar.Renderer;

// namespace xshazwar.Generation {

//     public class Generator : BaseGenerator {
//         private ConcurrentQueue<BillboardLoD> billboards;

//         public Generator(
//             MapMagicObject mm,
//             Generator leader,
//             TerrainRenderer renderer,
//             Camera camera,
//             Resolution resolution,
//             int margin,
//             Vector2 tileSize,
//             int range,
//             int ignoreSize = 0,
//             Vector3 startingPosition = new Vector3()
//         ){
            
//             this.mm = mm;
//             this.leader = leader;
//             this.tracker = tracker;
//             size = tileSize.x;
//             this.range = range;
//             this.ignoreSize = ignoreSize;
//             position = new GridPos(0, 0);
//             newPosition = new GridPos(0, 0);
//             GridPos zero = new GridPos(0, 0);
//             prototype = new BillboardLoD(-100, zero, resolution, margin, tileSize);
//             billboards = new ConcurrentQueue<BillboardLoD>();
//             changeQueue = new ConcurrentQueue<TileToken>();
//             currentState = new Dictionary<GridPos, TileToken>();
//             this.renderer = renderer;
//             taskFactory = new TaskFactory(
//                 new System.Threading.CancellationToken(),
//                 TaskCreationOptions.LongRunning,
//                 TaskContinuationOptions.DenyChildAttach,
//                 new LimitedConcurrencyLevelTaskScheduler(16));

//             source = new MapMagicSource(mm);
//             if (leader != null){
//                 leader.OnRangeUpdated += RangeUpdate;
//                 leader.OnTileRendered += TileActivated;
//                 leader.OnTileReleased += TileRemoved;
//             }
//             else{
//                 listener = new MapMagicListener();
//                 if (tracker == Tracker.MM){
//                     listener.OnRangeUpdated += RangeUpdate;
//                 }else{
//                     cameraPosition = new CameraPosition(camera, (int)tileSize.x, range);
//                     cameraPosition.OnRangeUpdated += RangeUpdate;
//                     // cameraPosition.Poll();
//                 }
//                 listener.OnTileRendered += TileActivated;
//                 listener.OnTileReleased += TileRemoved;
//                 avoidStartingMMTiles();
//             }
//             xRangeTiles = new int[2 * range + 1];
//             zRangeTiles = new int[2 * range + 1];
//             newPosition.x = (int) Math.Floor(startingPosition.x / size);
//             newPosition.z = (int) Math.Floor(startingPosition.z/ size);
//             OnUpdate();
//         }


//         public void avoidStartingMMTiles(){
//             foreach(GridPos c in source.GetStartingTileCoords()){
//                 TileActivated(c);
//             }
            
//         }
        
//         public override void RequestTile(TileToken token){
//             int nextID = renderer.requestTileId();
//             token.id = nextID;
//             BillboardLoD lod;
//             if (!billboards.TryDequeue(out lod)){
//                 lod = BillboardLoD.CloneSettings(prototype, nextID, token.coord);
//             }else{
//                 lod.recycle(nextID, token.coord);
//             }
//             source.StartGenerate(lod);
//             renderer.setBillboardHeights(lod.id, lod.data.heights.arr);
//             renderer.setBillboardPosition(nextID, token.coord.x * size, token.coord.z * size, 0f, false);
//             billboards.Enqueue(lod);
//             changeQueue.Enqueue(token);
//             OnTileRendered?.Invoke(token.coord);
//         }

//     }
// }
