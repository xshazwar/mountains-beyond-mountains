using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;

using UnityEngine;

namespace xshazwar.Renderer
{
    public struct OffsetData {
        
        public float x;
        public float y_offset;
        public float z;
        

        public OffsetData(float x, float z, float off){
            this.x = x;
            this.y_offset = off;
            this.z = z;
        }


        public static int stride(){
            return 3 * 4; // 3 * 4bytes float
        }

        public static void frustrumFromMatrix(Matrix4x4 mat, ref Vector4[] planes){
            // Vector4[] planes = new Vector4[6];
            //left
            planes[0] = new Vector4(mat.m30 + mat.m00, mat.m31 + mat.m01, mat.m32 + mat.m02, mat.m33 + mat.m03);
            // right
            planes[1] = new Vector4(mat.m30 - mat.m00, mat.m31 - mat.m01, mat.m32 - mat.m02, mat.m33 - mat.m03);
            // bottom
            planes[2] = new Vector4(mat.m30 + mat.m10, mat.m31 + mat.m11, mat.m32 + mat.m12, mat.m33 + mat.m13);
            // top
            planes[3] = new Vector4(mat.m30 - mat.m10, mat.m31 - mat.m11, mat.m32 - mat.m12, mat.m33 - mat.m13);
            // near
            planes[4] = new Vector4(mat.m30 + mat.m20, mat.m31 + mat.m21, mat.m32 + mat.m22, mat.m33 + mat.m23);
            // far
            planes[5] = new Vector4(mat.m30 - mat.m20, mat.m31 - mat.m21, mat.m32 - mat.m22, mat.m33 - mat.m23);
            // normalize
            for (uint i = 0; i < 6; i++)
            {
                planes[i].Normalize();
            }
        }
    }
}