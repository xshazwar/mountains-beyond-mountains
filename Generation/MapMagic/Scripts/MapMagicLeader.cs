// Requires MM2

#if MAPMAGIC2
using System;
using MapMagic.Core;


using Unity.Collections;
using UnityEngine;

using xshazwar.Generation;

namespace xshazwar.Generation.MapMagic
{
    public class MapMagicLeader : MonoBehaviour, IReportStatus, IHandlePosition {
        // wraps the TerrainRenderer Class because I'm too lazy to refactor it right now
 
        MapMagicListener listener;
        public Action<GridPos> OnRangeUpdated {get; set;}
        public Action<GridPos> OnTileRendered {get; set;}
        public Action<GridPos> OnTileReleased {get; set;}

        void OnEnable(){
            listener = new MapMagicListener();
            listener.OnRangeUpdated += RangeUpdated;
            listener.OnTileRendered += TileRendered;
            listener.OnTileReleased += TileReleased;
        }

        // teardown

        void OnDisable(){
            listener.Disconnect();
            listener.OnRangeUpdated -= RangeUpdated;
            listener.OnTileRendered -= TileRendered;
            listener.OnTileReleased -= TileReleased;
            listener = null;
        }

        // callbacks
        public void RangeUpdated(GridPos p){
            OnRangeUpdated?.Invoke(p);
        }
        public void TileRendered(GridPos p){
            OnTileRendered?.Invoke(p);
        }
        public void TileReleased(GridPos p){
            OnTileReleased?.Invoke(p);
        }

    }
}
#endif