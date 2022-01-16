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

namespace xshazwar.Generation {
    
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
            return new BillboardLoD(newID, coord, o.resolution, o.margin, new Vector2(o.tileSize.x, o.tileSize.z));
        }

        public BillboardLoD(int id): this(){
            this.id = id;
        }
        public BillboardLoD(int id, GridPos c, Resolution resolution, int margin, Vector2 tileSize): this(id){
            setParams(c, resolution, margin, new Vector2D(tileSize.x, tileSize.y));
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
        public MapMagicSource(MapMagicObject mm){
            this.mm = mm;
        }

        public void GetHeights(GridPos pos, ref NativeSlice<float> values){

        }
        public IEnumerable<GridPos> GetStartingTileCoords(){
            foreach(TerrainTile tile in mm.tiles.All()){
                yield return new GridPos(tile.coord.x, tile.coord.z);
            }
        }
        

        public void StartGenerate(BillboardLoD lod){
            lod.data.globals = mm.globals;
            lod.data.random = mm.graph.random;
            GetHeights(lod);
        }
        public void GetHeights(BillboardLoD lod){
                lod.stop = new StopToken();
                mm.graph.Generate(lod.data, lod.stop);
                this.FinalizeHeights(lod);
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
    }
    
    public class MapMagicListener: IHandlePosition, IReportStatus {
        HashSet<GridPos> active; 
        Vector2 xRange;
        Vector2 zRange;
        Vector2 xRangePrevious;
        Vector2 zRangePrevious;

        public Action<Vector2, Vector2> OnRangeUpdated {get; set;}
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
                OnRangeUpdated?.Invoke(xRange, zRange);
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