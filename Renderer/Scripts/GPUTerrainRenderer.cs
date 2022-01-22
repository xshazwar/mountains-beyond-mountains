using System;
using Unity.Collections;
using UnityEngine;

#if __MICROSPLAT__
using xshazwar.integration.microsplat;
#endif

using xshazwar.Generation;

namespace xshazwar.Renderer
{
    [RequireComponent(typeof(GenerationLocals))]
    public class GPUTerrainRenderer : MonoBehaviour, IRenderTiles {
        // wraps the TerrainRenderer Class because I'm too lazy to refactor it right now
 
        public GenerationGlobals globals;
        public GenerationLocals locals;
        Camera camera;
        TerrainRenderer renderer;
        ComputeShader cullShader;

        #if UNITY_EDITOR
        void OnValidate(){
            Validate();
        }
        #endif

        public void Validate(){
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
            camera = globals.camera;
            cullShader = globals.cullingComputeShaders;
            if (cullShader == null){
                throw new Exception("Culling shader not set in globals");
            }
        }

        void OnEnable(){
            Validate();
            if (renderer == null){
                renderer = new TerrainRenderer(
                    Instantiate(cullShader),
                    locals.overrideMaterial,
                    locals.terrainEnd,
                    locals.meshDownscale,
                    (int) locals.resolution,
                    locals.meshOverlap,
                    globals.tileSize,
                    globals.height,
                    locals.terrainStart,
                    locals.debugColor
                );
            }
            #if __MICROSPLAT__
            MSProceduralRules procRules = gameObject.GetComponent<MSProceduralRules>();
            if (procRules != null){
                procRules.setBuffersFromRules(renderer.materialProps);
                Debug.Log("Set procedural rules");
            }
            #endif
        }

        // teardown

        void OnDisable(){
            renderer?.flush();
            renderer = null;
        }

        void Update(){
            renderer.UpdateFunctionOnGPU(camera);
        }

        // interfaces
        public bool isReady() {
            if (renderer == null){
                return false;
            }
            return renderer.isReady();
        }
        public int requestTileId(){
            return renderer.requestTileId();
        }
        public void RegisterTileUpdated(int id){
            renderer.RegisterTileUpdated(id);
        }
        public NativeSlice<float> getTileHeights(int id){
            return renderer.getTileHeights(id);
        }
        public void setBillboardPosition(int id, float x_pos, float z_pos, float y_off, bool waitForHeight=true){
            renderer.setBillboardPosition(id, x_pos, z_pos, y_off, waitForHeight);
        }
        public void hideBillboard(int id){
            renderer.hideBillboard(id);
        }
        public void unhideBillboard(int id){
            renderer.unhideBillboard(id);
        }
        public void releaseTile(int id){
            renderer.releaseTile(id);
        }
    }
}