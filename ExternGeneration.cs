using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Profiling;

using Den.Tools;
using Den.Tools.Tasks;
using MapMagic.Core;
using Den.Tools.Matrices;
using MapMagic.Products;
using MapMagic.Nodes;
using MapMagic.Nodes.MatrixGenerators;
using MapMagic.Terrains;

// using Sirenix.OdinInspector;

namespace xshazwar {
    public class ExternGeneration : MonoBehaviour {

        public MapMagicObject mapMagic;
        public ExternGeneration leader;
        public Camera camera;
        public Resolution resolution = Resolution._65;
        public int downscale = 1;
        public int margin = 2;
        public int startDistance = 0;
        public int endDistance = 4;
        public Material material;
        public Color debugColor;
        public Generator generator;
        private TerrainRenderer renderer;

        #if UNITY_EDITOR
        void OnValidate(){
            // mapMagic = gameObject.transform.parent.gameObject.GetComponent<MapMagicObject>();
        }
        #endif
        
        void OnEnable(){
            if (camera == null){
                camera = Camera.main;
            }
            renderer = new TerrainRenderer(material, endDistance, downscale, (int) resolution, margin, mapMagic.tileSize.x, mapMagic.globals.height, startDistance, debugColor);
            MSProceduralRules procRules = gameObject.GetComponent<MSProceduralRules>();
            if (procRules != null && procRules.procTexCfg != null){
                procRules.setBuffersFromRules(renderer.materialProps);
                Debug.Log("Set procedural rules");
            }
            generator = new Generator(mapMagic, leader?.generator, renderer, resolution, margin, mapMagic.tileSize, endDistance, startDistance, camera.gameObject.transform.position);
        }

        void Start(){
            if(camera == null){
                throw new Exception("Attach the main camera!");
            }          
        }

        // [Button]
        // public void Debug(){
        //     renderer.ReportActive();
        // }
        void Update(){
            generator.Update();
            renderer.UpdateFunctionOnGPU(camera);
        }

        void OnDisable(){
            generator?.Disconnect();
            renderer?.flush();
            renderer = null;
        }
        
    }
}