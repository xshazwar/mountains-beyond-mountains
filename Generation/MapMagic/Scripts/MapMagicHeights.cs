// Requires MM2

#if MAPMAGIC2
using System;
using MapMagic.Core;


using Unity.Collections;
using UnityEngine;

using xshazwar.Generation;

namespace xshazwar.Generation.MapMagic
{
    [RequireComponent(typeof(GenerationLocals))]
    public class MapMagicHeights : MonoBehaviour, IProvideHeights {
 
        public GenerationGlobals globals;
        public GenerationLocals locals;
        public MapMagicObject mapmagic;
        MapMagicSource source;

        #if UNITY_EDITOR
        void OnValidate(){
            Validate();
        }
        #endif

        public void Validate(){
            if (mapmagic == null){
                Debug.LogError("Requires MapMagicObject");
                // throw new Exception("Requires MapMagicObject");
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

        void OnEnable(){
            Validate();
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
        public void GetHeights(GridPos pos, NativeSlice<float> values){
            source.GetHeights(pos, values);
        }

    }
}
#endif