using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

using JBooth.MicroSplat;

namespace xshazwar.integration.microsplat {
    public class MSProceduralRules : MonoBehaviour
    {
        #if __MICROSPLAT_PROCTEX__
        public MicroSplatProceduralTextureConfig procTexCfg;
        public MicroSplatPropData propData;

        public void setBuffersFromRules(MaterialPropertyBlock m){
            if (procTexCfg == null || propData == null){
                Debug.LogError("Either procedural texture or propdata configuration not set.");
                throw new Exception("No procedural texture configuration set.");
            }
            m.SetTexture("_ProcTexCurves", procTexCfg.GetCurveTexture(propData));
            m.SetTexture("_ProcTexParams", procTexCfg.GetParamTexture());
            m.SetInt("_PCLayerCount", procTexCfg.layers.Count);
            Debug.Log("procs set on material");
        }
        #else
        public void setBuffersFromRules(MaterialPropertyBlock m){
        }
        #endif
    }
}
