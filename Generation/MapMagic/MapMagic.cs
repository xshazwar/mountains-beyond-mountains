// Requires MM2
#if MAPMAGIC2

using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Linq;

using UnityEngine;
using Unity.Collections;

using Den.Tools;
using Den.Tools.Tasks;
using MapMagic.Core;
using Den.Tools.Matrices;
using MapMagic.Products;
using MapMagic.Nodes;
using MapMagic.Nodes.MatrixGenerators;
using MapMagic.Terrains;

using xshazwar.Generation;

namespace xshazwar.Generation.MapMagic {
    
    public class BillboardLoD{            
        public int id;
        public Den.Tools.Coord coord;
        public Resolution resolution;
        public int margin;
        public Vector2D tileSize;
        public TileData data;
        public StopToken stop;
        public BillboardLoD(){
            this.data = new TileData();
        }

        public static BillboardLoD CloneSettings(BillboardLoD o, int newID, GridPos coord){
            return new BillboardLoD(newID, coord, o.resolution, o.margin, o.tileSize.x);
        }

        public BillboardLoD(int id): this(){
            this.id = id;
        }
        public BillboardLoD(int id, GridPos c, Resolution resolution, int margin, float tileSize): this(id){
            setParams(c, resolution, margin, new Vector2D(tileSize, tileSize));
        }
        public void setParams(GridPos c, Resolution resolution, int margin, Vector2D tileSize){
            this.coord = new Den.Tools.Coord(c.x, c.z);
            this.resolution = resolution;
            this.margin = margin;
            this.tileSize = tileSize;
            this.data.area = new Area(coord, (int)resolution, margin, tileSize);
        }

        public void recycle(int id, GridPos c){
            this.id = id;
            this.coord = new Den.Tools.Coord(c.x, c.z);
            this.data = new TileData();
            this.data.area = new Area(coord, (int)resolution, margin, tileSize);
        }
    }
    
    public class MapMagicSource: IProvideHeights {
        MapMagicObject mm;
        BillboardLoD prototype;
        private ConcurrentQueue<BillboardLoD> billboards;

        public MapMagicSource(MapMagicObject mm, Resolution resolution, int margin, float tileSize){
            this.mm = mm;
            billboards = new ConcurrentQueue<BillboardLoD>();
            prototype = new BillboardLoD(-100, new GridPos(0, 0), resolution, margin, tileSize);
        }

        public IEnumerable<GridPos> GetStartingTileCoords(){
            foreach(TerrainTile tile in mm.tiles.All()){
                yield return new GridPos(tile.coord.x, tile.coord.z);
            }
        }
        
        public void GetHeights(GridPos pos, ref float[] values){
            BillboardLoD lod;
            if (!billboards.TryDequeue(out lod)){
                lod = BillboardLoD.CloneSettings(prototype, -1, pos);
            }else{
                lod.recycle(-1, pos);
            }
            StartGenerate(lod, ref values);
            billboards.Enqueue(lod);
        }

        public void StartGenerate(BillboardLoD lod, ref float[] values){
            lod.data.globals = mm.globals;
            lod.data.random = mm.graph.random;
            GenerateHeights(lod, ref values);
        }
        public void GenerateHeights(BillboardLoD lod, ref float[] values){
                lod.stop = new StopToken();
                mm.graph.Generate(lod.data, lod.stop);
                this.FinalizeHeights(lod, ref values);
            }
        public void FinalizeHeights(BillboardLoD lod, ref float[] values){
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

                    values[a] += val * biomeVal;
                }
            }
        }
    }
    
    public class MapMagicListener: IHandlePosition, IReportStatus {
        HashSet<GridPos> active; 
        Vector2 xRange;
        Vector2 zRange;
        Vector2 xRangePrevious;
        Vector2 zRangePrevious;

        public Action<GridPos> OnRangeUpdated {get; set;}
        public Action<GridPos> OnTileRendered {get; set;}
        public Action<GridPos> OnTileReleased {get; set;}

        public MapMagicListener(){
            active = new HashSet<GridPos>();
            xRange = new Vector2();
            zRange = new Vector2();
            xRangePrevious = new Vector2();
            zRangePrevious = new Vector2();
            TerrainTile.OnLodSwitched += LodSwitched;
        }

        public void CalcActive(){
            if (active.Count == 0){
                return;
            }
            xRangePrevious = xRange;
            zRangePrevious = zRange;
            xRange.x = active.Select(v => v.x).Min();
            xRange.y = active.Select(v => v.x).Max();
            zRange.x = active.Select(v => v.z).Min();
            zRange.y = active.Select(v => v.z).Max();
            if (xRangePrevious != xRange || zRangePrevious != zRange){
                GridPos center = new GridPos(
                    (int) (xRange.x + xRange.y) / 2,
                    (int) (zRange.x + zRange.y) / 2
                );
                OnRangeUpdated?.Invoke(center);
            }
        }

        // public static Action<TerrainTile, bool, bool> OnLodSwitched;
        public void LodSwitched(TerrainTile tile, bool isMain, bool isDraft){
            GridPos coord = new GridPos(tile.coord.x, tile.coord.z);
            if (!isMain && !isDraft){
                lock(active){
                    active.Remove(coord);
                    OnTileReleased?.Invoke(coord);
                    CalcActive();
                }
            }else{
                lock(active){
                    active.Add(coord);
                    OnTileRendered?.Invoke(coord);
                    CalcActive();
                }
            }               
        }

        public void Disconnect(){
            TerrainTile.OnLodSwitched -= LodSwitched;
            active = new HashSet<GridPos>();
        }
    }
}
#endif