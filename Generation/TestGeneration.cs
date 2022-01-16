using System;
using UnityEngine;
using UnityEngine.Profiling;

using xshazwar.Generation.Base;
using xshazwar.Renderer;

namespace xshazwar.Generation {

    public class TestGeneration : BaseGenerator {

        public override void RequestTile(TileToken token){
            Debug.Log($"requesting {token.coord}");
            int nextID = _renderer.requestTileId();
            token.id = nextID;
            float[] data = new float[heightElementCount];// todo make a native slice of the renderer heights array
            _heightSource.GetHeights(token.coord, ref data);
            _renderer.setBillboardHeights(nextID, data);
            _renderer.setBillboardPosition(nextID, token.coord.x * size, token.coord.z * size, 0f, false);
            changeQueue.Enqueue(token);
            OnTileRendered?.Invoke(token.coord);
        }

    }

}