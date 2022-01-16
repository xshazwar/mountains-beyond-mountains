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
        private int tileSize;
        private int range;
        private float extent;
        private Vector2 lastUpdate;
        private float updateDistance = 250f;
        public Action<Vector2, Vector2> OnRangeUpdated {get; set;}
        private Vector2 _query;

        public CameraPosition(Camera camera, int tileSize, int range){
            this.camera = camera;
            this.tileSize = tileSize;
            this.range = range;
            this.extent = (float) range * tileSize;
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
            float x = Mathf.Floor(lastUpdate.x / (float) tileSize);  
            float z = Mathf.Floor(lastUpdate.y / (float) tileSize);
            Debug.Log($"Tile {x},{z}");
            OnRangeUpdated?.Invoke(new Vector2(x * tileSize - extent, x * tileSize + extent), new Vector2(z * tileSize - extent, z * tileSize + extent));
        }
    }
}