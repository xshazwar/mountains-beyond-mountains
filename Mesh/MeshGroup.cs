using xshazwar.Meshes;
using xshazwar.Meshes.Generators;
using xshazwar.Meshes.Streams;
using UnityEngine;
using UnityEngine.Rendering;

namespace xshazwar {
    public class MeshGroup : MonoBehaviour {
        ExternGeneration Generator;
        int maxNewPerFrame = 4;
        bool drawMesh = false;
    }
}