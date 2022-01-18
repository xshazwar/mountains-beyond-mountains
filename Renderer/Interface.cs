using Unity.Collections;

namespace xshazwar.Renderer {

    public interface IRenderTiles{
        
        public bool isReady();
        public int requestTileId();
        public void hideBillboard(int tokenID);
        public void unhideBillboard(int tokenID);
        public void releaseTile(int tokenID);
        public void RegisterTileUpdated(int id);
        public NativeSlice<float> getTileHeights(int id);
        public void setBillboardPosition(int id, float x_pos, float z_pos, float y_off, bool waitForHeight=true);
        
    }

}