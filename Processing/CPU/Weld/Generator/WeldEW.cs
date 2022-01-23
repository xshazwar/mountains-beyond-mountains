using Unity.Collections;

namespace xshazwar.processing.cpu.weld{

    public struct WeldEW: IGenerateWelds {
        public int Resolution {get; set;}
        public int MarginWidth {get; set;}
        public WeldSide side {get; set;}
        public int JobLength => Resolution;
        public int[] cRange => new int[] {0, 2 * MarginWidth, 1};
        public int tOff => side == WeldSide.W ? 0 : Resolution - 2 * MarginWidth; 
        public int sOff => side == WeldSide.E ? 0 : Resolution - 2 * MarginWidth; 
        public void Execute(int i, NativeSlice<float> source, NativeSlice<float> target){
            for (int c = cRange[0]; c < cRange[1]; c += cRange[2]){
                target[ (i * Resolution) + c + tOff] = source[(i * Resolution) + c + sOff];
            }
            // W:
                // i => (margin, res - margin)
                // c => 0; 2 * margin; +=1
                // T => : (i * res) + c
                // S => : (i * res) + c + res - 2 * margin
            // E:
                // i => (margin, res - margin)
                // c => 0; 2 * margin; +=1
                // T => : (i * res) + c + res - 2 * margin
                // S => : (i * res) + c
            // N:
                // i => (margin, res - margin)
                // c => 0; 2 * margin * resolution; +=resolution
                // T => : i + c
                // S => : i + c + (res * res - (res * 2 * margin) + margin)
            // S:
                // i => (margin, res - margin)
                // c => 0; 2 * margin * resolution; +=resolution
                // T => : i + c + (res * res - (res * 2 * margin) + margin)
                // S => : i + c
        }
    }

}