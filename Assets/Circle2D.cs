using NativeQuadTree;
using Unity.Mathematics;

namespace NativeQuadTree
{
    public class Circle2D
    {
        public float2 Center;
        public float Radious;
        
        public bool Contains(float2 point)
        {
            return math.distance(point, Center) <= Radious; 
        }

        public bool Contains(AABB2D b) {
            return Contains(b.Center + new float2(-b.Extents.x, -b.Extents.y)) &&
                   Contains(b.Center + new float2(-b.Extents.x, b.Extents.y)) &&
                   Contains(b.Center + new float2(b.Extents.x, -b.Extents.y)) &&
                   Contains(b.Center + new float2(b.Extents.x, b.Extents.y));
        }

        public bool Intersects(AABB2D b)
        {
            float2 center = new float2()
            {
                x = math.clamp(Center.x, b.Center.x - b.Extents.x, b.Center.x + b.Extents.x),
                y = math.clamp(Center.y, b.Center.y - b.Extents.y, b.Center.y + b.Extents.y)
            };

            return Contains(center);
        }

        public bool Intersects(Circle2D b)
        {
            return math.distance(Center, b.Center) <= Radious + b.Radious;
        }
    }
}