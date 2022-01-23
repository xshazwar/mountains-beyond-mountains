using Unity.Collections;
using Unity.Jobs;
using UnityEngine;

namespace xshazwar.processing.cpu.weld {
    public enum WeldSide {
        // We dont't need to weld diagonal tiles as they should be covered by proxy already
        N,
        S,
        E,
        W
    }
    public interface IGenerateWelds {
        // total resolution including margin
        int Resolution {get; set;}
        int JobLength {get;}
        int MarginWidth {get; set;}

        // private int[] iRange;
        int[] cRange {get;}
        int tOff {get;}
        int sOff {get;}
        WeldSide side {get; set;}
        void Execute(int i, NativeSlice<float> source, NativeSlice<float> target); 
    }

    // public interface ImWeldData {
        

    //     void Setup(NativeSlice<float> target, NativeSlice<float> source);
    //     void SetValue(int idx, float value);

    // }

}