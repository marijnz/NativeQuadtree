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
        public bool Intersects(Circle2D b)
        {
            return math.distance(Center, b.Center) <= Radious + b.Radious;
        }
    }
}