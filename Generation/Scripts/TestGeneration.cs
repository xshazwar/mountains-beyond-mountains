using System;
using Unity.Collections;
using UnityEngine;
using UnityEngine.Profiling;

using xshazwar.Generation.Base;
using xshazwar.Renderer;

namespace xshazwar.Generation {

    public class TestGeneration : BaseGenerator {

        public override void RequestTile(TileToken token){
            int nextID = _renderer.requestTileId();
            token.id = nextID;
            NativeSlice<float> data = _renderer.getTileHeights(nextID);
            _heightSource.GetHeights(token.coord, data);
            _renderer.RegisterTileUpdated(nextID);
            _renderer.setBillboardPosition(nextID, token.coord.x * size, token.coord.z * size, 0f, false);
            changeQueue.Enqueue(token);
            OnTileRendered?.Invoke(token.coord);
        }

    }

}