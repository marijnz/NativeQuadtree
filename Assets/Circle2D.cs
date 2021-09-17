using System.Diagnostics;
using System.Runtime.CompilerServices;
using NativeQuadTree;
using Unity.Mathematics;

namespace NativeQuadTree
{
    public struct Circle2D
    {
        public float2 Center;
        public float Radious;

        public Circle2D(float2 center, float radious) 
            : this()
        {
            Center = center;
            Radious = radious;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Contains(float2 point)
        {
            return math.distance(point, Center) <= Radious; 
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Contains(AABB2D b)
        {
            // check that all 4 points are inside circle
            return Contains(b.Center + -b.Extents) &&
                   Contains(b.Center + new float2(-b.Extents.x, b.Extents.y)) &&
                   Contains(b.Center + new float2(b.Extents.x, -b.Extents.y)) &&
                   Contains(b.Center + b.Extents);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Intersects(AABB2D a)
        {
            return Intersects(a, this);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static bool Intersects(AABB2D a, Circle2D b)
        {
            float2 squareEdgePoint = math.clamp(b.Center, a.Center - a.Extents, a.Center + a.Extents);
            float distance = math.distance(squareEdgePoint, b.Center);

            if(a.Contains(b.Center))
            {
                // inside box
                /*float length = math.max(a.Extents.x, a.Extents.y);
                return distance > b.Radious || length < b.Radious;*/
                return true;
            }
            else
            {
                // outside box
                return distance < b.Radious;
            }
        }
    }
}