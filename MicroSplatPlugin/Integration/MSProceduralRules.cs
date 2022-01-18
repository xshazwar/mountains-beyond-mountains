using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

using JBooth.MicroSplat;

namespace xshazwar.integration.microsplat {
    public class MSProceduralRules : MonoBehaviour
    {
        #if __MICROSPLAT__
        public MicroSplatProceduralTextureConfig procTexCfg;

        public void setBuffersFromRules(MaterialPropertyBlock m){
            if (procTexCfg == null){
                Debug.LogError("No procedural texture configuration set.");
                throw new Exception("No procedural texture configuration set.");
            }
            m.SetTexture("_ProcTexCurves", procTexCfg.GetCurveTexture());
            m.SetTexture("_ProcTexParams", procTexCfg.GetParamTexture());
            m.SetInt("_PCLayerCount", procTexCfg.layers.Count);
            Debug.Log("procs set on material");
        }
        #endif
    }
}
