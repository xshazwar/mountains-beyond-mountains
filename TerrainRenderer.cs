using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;

using UnityEngine;
using UnityEngine.Rendering;

namespace xshazwar
{
    public struct OffsetData {
        
        public float x;
        public float y_offset;
        public float z;
        public float id;
        

        public OffsetData(int id, float x, float z, float off){
            this.id = (float) id;
            this.x = x;
            this.z = z;
            this.y_offset = off;
        }


        public static int stride(){
            return 4 * 4; // 5 * 4bytes float
        }

        public void AssignElement(ref Vector3 v, float x, float y, float z){
            v.x = x;
            v.y = y;
            v.z = z;
        }
        public void corners(float tileSize, float height, ref Vector3[] _corners){  // WS tile corner positions in the x, z plane. We don't cull based on height ATM
            AssignElement(ref _corners[0], this.x, 0f, this.z);
            AssignElement(ref _corners[1], this.x + tileSize, 0f, this.z);
            AssignElement(ref _corners[2], this.x + tileSize, 0f, this.z + tileSize);
            AssignElement(ref _corners[3], this.x , 0f, this.z + tileSize);
            AssignElement(ref _corners[4], this.x, height, this.z);
            AssignElement(ref _corners[5], this.x + tileSize, height, this.z);
            AssignElement(ref _corners[6], this.x + tileSize, height, this.z + tileSize);
            AssignElement(ref _corners[7], this.x , height, this.z + tileSize);
        }

        public bool visible(Vector3 r){ // relative position
            if ( r.x < 0f || r.x > 1f ||  r.y < 0f || r.y > 1f || r.z < 0){
                return false;
            }
            return true;
        }
        public bool visibleIn(Camera camera, float tileSize, float height, ref Vector3[] _corners){
            this.corners(tileSize, height, ref _corners);
            foreach (Vector3 p in _corners){
                if(this.visible(camera.WorldToViewportPoint(p))){
                    return true;
                }
            }
            return false;
        }
    }
    public class TerrainRenderer {


        public Mesh mesh;
        public int meshResolution = 65;
        public int meshDownscale = 4;
        public int meshOverlap = 3;
        public int terrainRange = 3;
        public int terrainCount = 9;
        public int terrainBufferSize = 0;
        public float height = 1000f;
        public float tileSize = 1000f;
        private int range;


        // Buffers

        public ComputeBuffer fovBuffer;
        public ComputeBuffer offsetBuffer;
        public ComputeBuffer terrainBuffer;

        // CPU Side Buffer Sources
        OffsetData[] offset_data_arr;
        float[] all_heights_arr;
        int[] fov_array;

        public Material material;
        public MaterialPropertyBlock materialProps;
        public Bounds bounds;

        int tileHeightElements = 0;
        private ConcurrentQueue<int> heightsUpdates = new ConcurrentQueue<int>();
        private ConcurrentQueue<int> offsetUpdates = new ConcurrentQueue<int>();
        ConcurrentQueue<int> billboardIds = new ConcurrentQueue<int>();

        public bool isReady = false;

        private int minAvailableTiles = 10000;

        private Vector3 __extent = new Vector3(100000f, 5000f, 100000f);
        public void setBounds(){
            setBounds(Vector3.zero, __extent);
        }
        public void setBounds(Vector3 center){
            setBounds(center, __extent);
        }
        public void setBounds(Vector3 center, Vector3 extent){
            bounds = new Bounds(center, extent);
        }

        public TerrainRenderer(Material _material, int range, int downscale, int resolution, int overlap, float _tileSize, float _height, int internalGap = 0, Color? color = null){
            tileSize = _tileSize;
            height = _height;
            material = _material;
            meshOverlap = overlap;
            meshDownscale = downscale;
            meshResolution = resolution + (2 * overlap);
            tileHeightElements = meshResolution * meshResolution;
            // to handle overdraw, we'll tell the GPU about the real size w/ overlap
            // float meshSize = tileSize * ((meshResolution * 1.0f / resolution));
            this.range = range;
            terrainRange = (2 * (range + 2));
            terrainCount = terrainRange * terrainRange - (2 * internalGap * 2 * internalGap);
            minAvailableTiles = terrainCount;
            for (int i = 0; i < terrainCount; i ++){
                billboardIds.Enqueue(i);
            }
            terrainBufferSize = terrainCount * tileHeightElements;
            setBounds();
            terrainBuffer = new ComputeBuffer(terrainBufferSize, 4);
            all_heights_arr = new float[terrainBufferSize];
            for (int i = 0; i < terrainBufferSize; i ++){
                all_heights_arr[i] = 0.001f;
            }
            terrainBuffer.SetData(all_heights_arr);

            offsetBuffer = new ComputeBuffer(terrainCount, OffsetData.stride());
            mesh = makeSquarePlanarMesh(meshResolution, meshDownscale);

            offset_data_arr = new OffsetData[terrainCount];
            for(int i = 0; i < terrainCount; i++){
                offset_data_arr[i].y_offset = -100000f;
            }

            fovBuffer = new ComputeBuffer(terrainCount, 4);
            fov_array = new int[terrainCount];

            float ws_bound = (range) * tileSize;
            materialProps = new MaterialPropertyBlock();
            if (color != null){
                materialProps.SetColor("_Tint", (Color) color);
            }
            materialProps.SetFloat("_Height", height);
            materialProps.SetFloat("_Mesh_Size", tileSize);
            materialProps.SetFloat("_Mesh_Res", resolution * 1.0f);
            materialProps.SetFloat("_MeshOverlap", meshOverlap * 1.0f);
            materialProps.SetFloat("_DownScale", meshDownscale);
            materialProps.SetBuffer("_Offset", offsetBuffer);
            materialProps.SetBuffer("_TerrainValues", terrainBuffer);
            materialProps.SetBuffer("_FOV", fovBuffer);

            Debug.Log("GPUTerrain Ready");
            isReady = true;
        }

        public int setCullingGetInstanceCount(Camera camera){
            UnityEngine.Profiling.Profiler.BeginSample("CullGPUTiles");
            float maxRange = (float)(range + 2) * tileSize;
            Vector2 pos = new Vector2(camera.gameObject.transform.position.x, camera.gameObject.transform.position.z);
            int c = 0;
            // reuse these 8 vectors for all camera calcs
            Vector3[] corners = new Vector3[8]; 
            for (int idx = 0; idx < terrainCount; idx ++){
                UnityEngine.Profiling.Profiler.BeginSample("ReadArray");
                OffsetData d = offset_data_arr[idx];
                UnityEngine.Profiling.Profiler.EndSample();
                // cull sunken
                UnityEngine.Profiling.Profiler.BeginSample("CullSunken");
                if (d.y_offset < -1f){
                    UnityEngine.Profiling.Profiler.EndSample();
                    continue;
                }
                UnityEngine.Profiling.Profiler.EndSample();
                UnityEngine.Profiling.Profiler.BeginSample("CullCameraFoV");
                if(!d.visibleIn(camera, tileSize, height, ref corners)){
                    UnityEngine.Profiling.Profiler.EndSample();
                    continue;
                }
                UnityEngine.Profiling.Profiler.EndSample();
                UnityEngine.Profiling.Profiler.BeginSample("Assign FoV Array");
                fov_array[c] = idx;
                c += 1;
                UnityEngine.Profiling.Profiler.EndSample();
            }
            UnityEngine.Profiling.Profiler.EndSample();
            UnityEngine.Profiling.Profiler.BeginSample("SetLiveGPUTiles");
            fovBuffer.SetData(fov_array, 0, 0, c);
            UnityEngine.Profiling.Profiler.EndSample();
            return c;
        }

        public void setBillboardPosition(int id, float x_pos, float z_pos, float y_off, bool waitForHeight=true){
            if (id > terrainCount || id < 0){
                Debug.LogError($"{id} =?> Invalid BillboardID");
                return;
            }
            // Debug.Log($"Moving {id} -> {x_pos}, {z_pos}");
            // TODO Find a better draw bounds solution
            // this changes the damned center as well...
            // setBounds(center: new Vector3(x_pos, 0f, z_pos));
            offset_data_arr[id] = new OffsetData(id, x_pos, z_pos, y_off); //start sunken
            if (!waitForHeight){
                offsetUpdates.Enqueue(id);
            }
        }

        public void setBillboardHeights(int id, float[] heights){
            if (heights.Length != tileHeightElements){
                throw new OverflowException("resolution mismatch!");
            }
            // in shader to pull 1 value @  v.vertex.x, v.vertex.z in tilespace we do:
            // int idx = _offset.id * (_Mesh_Res * _Mesh_Res) + (v.vertex.x * _Mesh_Res + v.vertex.z);
            // ergo the first idx is just ^^
            int idx = id * tileHeightElements;
            if (all_heights_arr.Length < idx + tileHeightElements){
                throw new OverflowException($"HeightBuffer Overflow for id : {id}");
            }
            heights.CopyTo(all_heights_arr, idx);
            heightsUpdates.Enqueue(idx);
        }

        public void hideBillboard(int id){
            // remove from view but do not deallocate
            setBillboardPosition(id, offset_data_arr[id].x, offset_data_arr[id].z, -10000f, false);
        }

        public void unhideBillboard(int id){
            // replace in last position (after hide)
            setBillboardPosition(id, offset_data_arr[id].x, offset_data_arr[id].z, 0f, false);
        }

        public void releaseTile(int id){
            billboardIds.Enqueue(id);
            hideBillboard(id);
        }

        public int requestTileId(){
            int i = 0;
            if(billboardIds.TryDequeue(out i)){
                minAvailableTiles = billboardIds.Count < minAvailableTiles ? billboardIds.Count : minAvailableTiles;
                return i;
            }
            // Debug.LogError("Too few tiles available!");
            return -1;
        }

        public void UpdateFunctionOnGPU (Camera camera) {
            if (!isReady || material == null){
                Debug.Log($"Renderer not ready {isReady} => {material}");
                return;
            }
            int idx = 0;
            // update our offsets
            while(offsetUpdates.TryDequeue(out idx)){
                offsetBuffer.SetData(offset_data_arr, idx, idx, 1);
            }
            // cull and count
            int count = setCullingGetInstanceCount(camera);
            // very cheap about .05 uS / copy @ resolution of 128
            while (heightsUpdates.TryDequeue(out idx)){
                UnityEngine.Profiling.Profiler.BeginSample("Writing Terrain Buffer");
                terrainBuffer.SetData(all_heights_arr, idx, idx, tileHeightElements);
                // also update position if we're setting the height... (only wait to trigger if waitForHeight)
                int offetIdx = idx / tileHeightElements;
                offsetBuffer.SetData(offset_data_arr, offetIdx, offetIdx, 1);
                UnityEngine.Profiling.Profiler.EndSample();
            }
            UnityEngine.Profiling.Profiler.BeginSample("DrawInstancedCall");
            if ( count > 0 ){  // Fixes nulls when "Looking" stright down (and probably up) BLAME: Twobob
                Graphics.DrawMeshInstancedProcedural(mesh, 0, material, bounds, count, materialProps);
            }
            UnityEngine.Profiling.Profiler.EndSample();

        }

        public void flush(){
            isReady = false;
            fov_array = null;
            offset_data_arr = null;
            all_heights_arr = null;
            try{
                fovBuffer.Release();
            }catch{}
            try{
                terrainBuffer.Release();
            }catch{}
            terrainBuffer = null;
            try{
                offsetBuffer.Release();
            }catch{}
            fovBuffer = null;
            offsetBuffer = null;
            heightsUpdates = null;
            offsetUpdates = null;
            Debug.Log($"{(terrainRange/2) - 1} >> {terrainRange}: currently available {billboardIds?.Count} / {terrainCount} -> low water {minAvailableTiles}");
            billboardIds = null;
            heightsUpdates = new ConcurrentQueue<int>();
            offsetUpdates = new ConcurrentQueue<int>();
            billboardIds = new ConcurrentQueue<int>();
            Debug.Log("GPUTerrain Flushed");
        }

        public static Mesh makeSquarePlanarMesh(int resolution, int downscaleFactor){
            Mesh _mesh = new Mesh();
            int res = (int) resolution / downscaleFactor;
            if (res >= 256){
                // we MUST use 32 bit indices or things will be horribly wrong
                _mesh.indexFormat = IndexFormat.UInt32;
            }
            int xSize = res -1;
            int ySize = res -1;
            Vector3[] vertices = new Vector3[(xSize + 1) * (ySize + 1)];
            for (int i = 0, y = 0; y <= ySize; y++) {
                for (int x = 0; x <= xSize; x++, i++) {
                    vertices[i] = new Vector3(x * downscaleFactor, 0, y * downscaleFactor);
                }
            }
            _mesh.vertices = vertices;

            int[] triangles = new int[xSize * ySize * 6];
            for (int ti = 0, vi = 0, y = 0; y < ySize; y++, vi++) {
                for (int x = 0; x < xSize; x++, ti += 6, vi++) {
                    triangles[ti] = vi;
                    triangles[ti + 3] = triangles[ti + 2] = vi + 1;
                    triangles[ti + 4] = triangles[ti + 1] = vi + xSize + 1;
                    triangles[ti + 5] = vi + xSize + 2;
                }
            }
            _mesh.triangles = triangles;
            return _mesh;
        }
    }
}
