// using Unity.Collections;

// namespace xshazwar.processing.cpu.weld{

//     public struct WeldRow: IGenerateWelds {
//         public int JobLength => Resolution;
//         // int[] iRange => new int[]{0, Resolution, 1};
//         int[] cRange => GenCRange(side);

//         public static int[] GenCRange( WeldSide side){
//             return side <= WeldSide.S ?
//                 new int[] {0, 2 * MarginWidth * Resolution, Resolution}:
//                 new int[] {0, 2 * MarginWidth, 1};
//         }

//         public void Execute<S> (int i){
//             // W:
//                 // i => (margin, res - margin)
//                 // c => 0; 2 * margin; +=1
//                 // T => : (i * res) + c
//                 // S => : (i * res) + c + res - 2 * margin
//             // E:
//                 // i => (margin, res - margin)
//                 // c => 0; 2 * margin; +=1
//                 // T => : (i * res) + c + res - 2 * margin
//                 // S => : (i * res) + c
//             // N:
//                 // i => (margin, res - margin)
//                 // c => 0; 2 * margin * resolution; +=resolution
//                 // T => : i + c
//                 // S => : i + c + (res * res - (res * 2 * margin) + margin)
//             // S:
//                 // i => (margin, res - margin)
//                 // c => 0; 2 * margin * resolution; +=resolution
//                 // T => : i + c + (res * res - (res * 2 * margin) + margin)
//                 // S => : i + c
            

//         }
//     }

// }