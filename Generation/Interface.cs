using System;
using UnityEngine;
using Unity.Collections;
using UnityEngine.Profiling;

namespace xshazwar.Generation {

    public interface IHandlePosition{
        public Action<GridPos> OnRangeUpdated {get; set;}
    }

    public interface IReportStatus {
        public Action<GridPos> OnTileRendered {get; set;}
        public Action<GridPos> OnTileReleased {get; set;}
    }
    public interface IProvideHeights{
        public void GetHeights(GridPos pos, ref float[] values);
    }
}