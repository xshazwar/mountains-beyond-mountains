using System;
using UnityEngine;

namespace xshazwar.Generation {
    
    [RequireComponent(typeof(GenerationGlobals))]
    public class SimplePositionReporter : MonoBehaviour, IHandlePosition  {

        private CameraPosition reporter;
        public Action<GridPos> OnRangeUpdated {get; set;}
        
        void NewRange(GridPos coord){
            OnRangeUpdated?.Invoke(coord);
        }
        void OnEnable(){
            GenerationGlobals globals = GetComponent<GenerationGlobals>();
            reporter = new CameraPosition(globals.camera, globals.tileSize);
            reporter.OnRangeUpdated += NewRange;
        }
        void Start(){}
        void Update(){
            reporter.Poll();
        }
        void OnDisable(){
            reporter.OnRangeUpdated -= NewRange;
        }
    }
}