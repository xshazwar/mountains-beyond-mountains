using System;
using UnityEngine;
using Unity.Collections;
using UnityEngine.Profiling;

namespace xshazwar.Generation {

    public interface IHandlePosition{
        public Action<Vector2, Vector2> OnRangeUpdated {get; set;}
    }

    public interface IReportStatus {
        public Action<Coord> OnTileRendered {get; set;}
        public Action<Coord> OnTileReleased {get; set;}
    }

    public interface IProvideHeights{
        public void GetHeights(Coord pos, ref NativeSlice<float> values);
    }
}