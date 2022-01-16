using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Linq;

using UnityEngine;
using Unity.Collections;

namespace xshazwar.Generation {
    class CameraPosition : IHandlePosition{
        private Camera camera;
        private float tileSize;
        private Vector2 lastUpdate;
        private float updateDistance = 250f;
        public Action<GridPos> OnRangeUpdated {get; set;}
        private Vector2 _query;

        public CameraPosition(Camera camera, float tileSize){
            this.camera = camera;
            this.tileSize = tileSize;
            _query = new Vector2(0, 0);
            updatePosition();
        }

        public void Poll(){
            if (needUpdates()){
                updatePosition();
            }
        }

        private bool needUpdates(){
            _query.x = camera.gameObject.transform.position.x;
            _query.y = camera.gameObject.transform.position.z;
            if(Vector2.Distance(_query, lastUpdate) >= updateDistance){
                lastUpdate = _query;
                return true;
            }
            return false;
        }
        private void updatePosition(){
            float x = Mathf.Floor(lastUpdate.x / tileSize);  
            float z = Mathf.Floor(lastUpdate.y / tileSize);
            Debug.Log($"Tile {x},{z}");
            OnRangeUpdated?.Invoke(new GridPos((int) x, (int) z));
        }
    }
}