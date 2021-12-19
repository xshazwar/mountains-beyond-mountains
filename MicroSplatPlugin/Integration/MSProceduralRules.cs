using System.Collections;
using System.Collections.Generic;
using UnityEngine;

using JBooth.MicroSplat;

namespace xshazwar {
    public class MSProceduralRules : MonoBehaviour
    {
        public MicroSplatProceduralTextureConfig procTexCfg;

        public void setBuffersFromRules(MaterialPropertyBlock m){
            m.SetTexture("_ProcTexCurves", procTexCfg.GetCurveTexture());
            m.SetTexture("_ProcTexParams", procTexCfg.GetParamTexture());
            m.SetInt("_PCLayerCount", procTexCfg.layers.Count);
            Debug.Log("procs set on material");
        }
    }
}
