using System;
using Unity.Mathematics;

namespace NativeQuadTree.Jobs.Internal
{
    public static class RangeQueryHelpers
    {
        internal static AABB2D GetChildBounds(AABB2D parentBounds, int childZIndex)
        {
            var half = parentBounds.Extents.x * .5f;

            switch (childZIndex)
            {
                case 0: return new AABB2D(new float2(parentBounds.Center.x - half, parentBounds.Center.y + half), half);
                case 1: return new AABB2D(new float2(parentBounds.Center.x + half, parentBounds.Center.y + half), half);
                case 2: return new AABB2D(new float2(parentBounds.Center.x - half, parentBounds.Center.y - half), half);
                case 3: return new AABB2D(new float2(parentBounds.Center.x + half, parentBounds.Center.y - half), half);
                default: throw new Exception();
            }
        }
    }
}