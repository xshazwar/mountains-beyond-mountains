using System.Collections;
using System.Collections.Generic;
using UnityEngine;

#if __MICROSPLAT__
using JBooth.MicroSplat;
#endif

namespace xshazwar {
    public class MSProceduralRules : MonoBehaviour
    {
        #if __MICROSPLAT__
        public MicroSplatProceduralTextureConfig procTexCfg;

        public void setBuffersFromRules(MaterialPropertyBlock m){
            m.SetTexture("_ProcTexCurves", procTexCfg.GetCurveTexture());
            m.SetTexture("_ProcTexParams", procTexCfg.GetParamTexture());
            m.SetInt("_PCLayerCount", procTexCfg.layers.Count);
            Debug.Log("procs set on material");
        }
        #endif
    }
}
