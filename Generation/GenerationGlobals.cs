using UnityEngine;

namespace xshazwar.Generation {
    public class GenerationGlobals : MonoBehaviour {
        public Camera camera;
        public float tileSize;
        public float height = 1000f;
        public ComputeShader cullingComputeShaders;
        public Material material;
    }
}