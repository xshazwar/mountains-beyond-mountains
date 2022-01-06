using System;
// using System.Collections;
// using System.Collections.Generic;
// using System.Collections.Concurrent;
// using System.Linq;
// using System.Text;
// using System.Threading;
using UnityEngine;
using UnityEngine.Profiling;

namespace xshazwar.Generation {
    public class Coord: IEquatable<Coord>{
        public int x;
        public int z;
        public Coord(int x, int z){
            this.x = x;
            this.z = z;
        }
        public override bool Equals(object obj) => obj is Coord other && this.Equals(other);
        public bool Equals(Coord p) => x == p.x && z == p.z;



    }
    public enum Resolution {_6=6, _8=8, _17=17, _33=33, _65=65, _129=129, _257=257, _513=513, _1025=1025, _2049=2049 };

    public enum TileStatus {
        ACTIVE, // dont care if draft or main really
        BB,
        CULLED

    };
}