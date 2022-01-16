using UnityEngine;

namespace xshazwar.Generation {
    public class SimplePositionReporter : MonoBehaviour {

        public Camera camera;
        public int tileSize = 1000;
        public int range = 3;
        private CameraPosition reporter;
        
        void NewRange(Vector2 x, Vector2 z){
            Debug.Log($"New Position reported x:{x} z:{z}");
        }
        void OnEnable(){
            reporter= new CameraPosition(camera, tileSize, range);
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