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

        public void AssignElement(ref Vector4 v, float x, float y, float z, float w){
            v.x = x;
            v.y = y;
            v.z = z;
            v.w = w;
        }
        public void corners(float tileSize, float height, ref Vector4[] _corners){
            AssignElement(ref _corners[0], this.x, 0f, this.z, 1f);
            AssignElement(ref _corners[1], this.x + tileSize, 0f, this.z, 1f);
            AssignElement(ref _corners[2], this.x + tileSize, 0f, this.z + tileSize, 1f);
            AssignElement(ref _corners[3], this.x , 0f, this.z + tileSize, 1f);

            AssignElement(ref _corners[4], this.x, height, this.z, 1f);
            AssignElement(ref _corners[5], this.x + tileSize, height, this.z, 1f);
            AssignElement(ref _corners[6], this.x + tileSize, height, this.z + tileSize, 1f);
            AssignElement(ref _corners[7], this.x , height, this.z + tileSize, 1f);
        }

        public bool visible(Vector3 r){ // relative position
            if ( r.x < 0f || r.x > 1f ||  r.y < 0f || r.y > 1f || r.z < 0){
                return false;
            }
            return true;
        }
        public bool visibleIn(ref Vector4[] planes, float tileSize, float height, ref Vector4[] _corners){
            this.corners(tileSize, height, ref _corners);
            int i = 0;
            foreach(Vector4 p in planes){
                i = 0;
                foreach (Vector4 c in _corners){
                    // if all negative return False
                    if (Vector4.Dot(p, c) < 0){
                        i++;
                    }
                }
                if (i == 8){ return false;}
            }
            return true;
        }
    }
}