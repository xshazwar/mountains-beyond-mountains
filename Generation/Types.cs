using System;
using System.Collections.Concurrent;

using UnityEngine;
using UnityEngine.Profiling;

namespace xshazwar.Generation {
    
    public enum Resolution {_6=6, _8=8, _17=17, _33=33, _65=65, _129=129, _257=257, _513=513, _1025=1025, _2049=2049 };

    public enum TileStatus {
        ACTIVE,
        BB,
        CULLED
    };

    public class GridPos: IEquatable<GridPos>{
        public int x;
        public int z;
        public GridPos(int x, int z){
            this.x = x;
            this.z = z;
        }
        public override bool Equals(object obj) => obj is GridPos other && this.Equals(other);
        public bool Equals(GridPos p) => x == p.x && z == p.z;
        public override string ToString() => String.Join(", ", new {x, z} );
        public override int GetHashCode() =>  x * 10000000 + z;
    }

    public class TokenPool {
        protected readonly ConcurrentBag<TileToken> _objects;
        protected readonly Func<GridPos,TileStatus, TileToken> _objectGenerator;
        
        public TokenPool(){
            
            _objectGenerator = (GridPos c, TileStatus t) => new TileToken(c, t);
            _objects = new ConcurrentBag<TileToken>();

        }

        public TileToken Get(GridPos p, TileStatus t){
            _objects.TryTake(out TileToken item);
            if (item != null){
                item.Recycle(p, t);
                return item;
            }
            return _objectGenerator(p, t);
        } 
        public void Return(TileToken item) => _objects.Add(item);
    }

    public class TileToken{
        public int? id;
        public GridPos coord;
        public TileStatus status;

        public TileToken(int? id, GridPos coord, TileStatus status){
            this.id = id;
            this.coord = coord;
            this.status = status;
        }

        public TileToken(GridPos coord, TileStatus status): this(null, coord, status){}

        public TileToken(TileToken t): this(t.id, t.coord, t.status){}

        public override int GetHashCode(){
            return coord.x * 1000000 + coord.z;
        }

        public void UpdateWith(TileToken t){
            id = t.id;
            status = t.status;
        }

        public void Recycle(int? id, GridPos coord, TileStatus status){
            this.id = id;
            this.coord = coord;
            this.status = status;
        }

        public void Recycle(GridPos coord, TileStatus status){
            Recycle(null, coord, status);
        }
    }
}