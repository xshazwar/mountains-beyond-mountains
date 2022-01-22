using UnityEngine;

namespace xshazwar.Generation {
    public class GenerationLocals : MonoBehaviour {
        public Resolution resolution = Resolution._65;
        public int meshDownscale = 1;
        public int meshOverlap = 3;
        public int terrainStart = 0;
        public int terrainEnd = 3;
        public Material overrideMaterial;
        public Color debugColor;
    }
}