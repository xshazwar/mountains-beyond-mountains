// Requires MM2

#if MAPMAGIC2
using System;
using MapMagic.Core;

using UnityEngine;

using xshazwar.Generation;

namespace xshazwar.Generation.MapMagic
{
    [RequireComponent(typeof(GenerationLocals))]
    public class MapMagicHeights : MonoBehaviour, IProvideHeights {
        // wraps the TerrainRenderer Class because I'm too lazy to refactor it right now
 
        public GenerationGlobals globals;
        public GenerationLocals locals;
        public MapMagicObject mapmagic;
        MapMagicSource source;

        #if UNITY_EDITOR
        void OnValidate(){
            if (mapmagic == null){
                throw new Exception("Requires MapMagicObject");
            }
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
        }
        #endif

        void OnEnable(){
            if(globals == null){
                globals = gameObject.transform.parent.gameObject.GetComponent<GenerationGlobals>();
            }
            if(locals == null){
                locals = GetComponent<GenerationLocals>();
            }
            source = new MapMagicSource(
                mapmagic,
                locals.resolution,
                locals.meshOverlap,
                globals.tileSize
            );
            
        }

        // teardown

        void OnDisable(){
            source = null;
        }

        // interfaces
        public void GetHeights(GridPos pos, ref float[] values){
            source.GetHeights(pos, ref values);
        }

    }
}
#endif