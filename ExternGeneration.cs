// using System;
// using System.Collections;
// using System.Collections.Generic;
// using System.Threading.Tasks;
// using UnityEngine;
// using UnityEngine.Profiling;

// using Den.Tools;
// using Den.Tools.Tasks;
// using MapMagic.Core;
// using Den.Tools.Matrices;
// using MapMagic.Products;
// using MapMagic.Nodes;
// using MapMagic.Nodes.MatrixGenerators;
// using MapMagic.Terrains;

// using xshazwar.Generation;
// using xshazwar.Renderer;

// namespace xshazwar {
//     public class ExternGeneration : MonoBehaviour, IReportStatus {

//         public MapMagicObject mapMagic;
//         public ExternGeneration leader;
//         public Camera camera;
//         public Generation.Resolution resolution = Generation.Resolution._65;
//         public ComputeShader cullShader;
//         public int downscale = 1;
//         public int margin = 2;
//         public int startDistance = 0;
//         public int endDistance = 4;
//         public Material material;
//         public Color debugColor;
//         public Generation.Generator generator;
//         private TerrainRenderer renderer;

//         #if UNITY_EDITOR
//         void OnValidate(){
//             // mapMagic = gameObject.transform.parent.gameObject.GetComponent<MapMagicObject>();
//             // if(cullShader == null){
//             //     throw new Exception("Set the culling ComputeShader!");
//             // }
//         }
//         #endif
        
//         void OnEnable(){
//             if (camera == null){
//                 camera = Camera.main;
//             }
//             renderer = new TerrainRenderer(Instantiate(cullShader), material, endDistance, downscale, (int) resolution, margin, mapMagic.tileSize.x, mapMagic.globals.height, startDistance, debugColor);
//             MSProceduralRules procRules = gameObject.GetComponent<MSProceduralRules>();
// #if __MICROSPLAT__
//             if (procRules != null && procRules.procTexCfg != null){
//                 procRules.setBuffersFromRules(renderer.materialProps);
//                 Debug.Log("Set procedural rules");
//             }
// #endif
//             // generator = new Generation.Generator(mapMagic, leader?.generator, renderer, tracker, camera, resolution, margin, (Vector2)mapMagic.tileSize, endDistance, startDistance, camera.gameObject.transform.position);
//         }

//         void Start(){
//             if(camera == null){
//                 throw new Exception("Attach the main camera!");
//             }          
//         }

//         void Update(){
//             generator.Update();
//             renderer.UpdateFunctionOnGPU(camera);
//         }

//         void OnDisable(){
//             generator?.Disconnect();
//             renderer?.flush();
//             renderer = null;
//         }

//         public Action<GridPos> OnTileRendered {get; set;}
//         public Action<GridPos> OnTileReleased {get; set;}
        
//     }
// }