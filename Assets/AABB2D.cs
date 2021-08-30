using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using PlasticGui.WorkspaceWindow.Items;
using Unity.Mathematics;
using UnityEngine;

namespace NativeQuadTree
{
    [Serializable, DebuggerDisplay("Center: {Center}, Extents: {Extents}")]
    public struct AABB2D
    {
        public float2 Center;
        public float2 Extents;

        public float2 Size => Extents * 2;
        public float2 Min => Center - Extents;
        public float2 Max => Center + Extents;

        public AABB2D(float2 center, float2 extents)
        {
            Center = center;
            Extents = extents;
        }
        
        public AABB2D(RectTransform rect)
        {
            Center = new float2(rect.position.x, rect.position.y);
            Extents = new float2((rect.rect.max - rect.rect.min) / 2f);
        }

        public bool Contains(float2 point)
        {
            if(point[0] < Center[0] - Extents[0])
                return false;

            if(point[0] > Center[0] + Extents[0])
                return false;

            if(point[1] < Center[1] - Extents[1])
                return false;

            if(point[1] > Center[1] + Extents[1])
                return false;

            return true;
        }

        public bool Contains(AABB2D b)
        {
            return Contains(b.Center + -b.Extents) &&
                   Contains(b.Center + new float2(-b.Extents.x, b.Extents.y)) &&
                   Contains(b.Center + new float2(b.Extents.x, -b.Extents.y)) &&
                   Contains(b.Center + b.Extents);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Contains(Circle2D b)
        {
            if(Contains(b.Center))
            {
                // inside box
                float2 squareEdgePoint = math.clamp(b.Center, Center - Extents, Center + Extents);
                float distance = math.distance(squareEdgePoint, Center);

                if(distance + b.Radious <= math.max(Extents.x, Extents.y))
                {
                    return true;
                }
                else
                {
                    // this could mean that the point is in the very corner of the square
                    float BL = math.distance(b.Center, Center + -Extents);
                    float TL = math.distance(b.Center, Center + new float2(-Extents.x, Extents.y));
                    float BR = math.distance(b.Center, Center + new float2(Extents.x, -Extents.y));
                    float TR = math.distance(b.Center, Center + Extents.x);
                    float closestCornerDistance = math.min(math.min(BL, TL), math.min(BR, TR));
                    return closestCornerDistance > b.Radious;
                }
            }
            return false;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Intersects(Circle2D b)
        {
            return Circle2D.Intersects(this, b);
        }

        public bool Intersects(AABB2D b)
        {
            //bool noOverlap = Min[0] > b.Max[0] ||
            //                 b.Min[0] > Max[0]||
            //                 Min[1] > b.Max[1] ||
            //                 b.Min[1] > Max[1];
//
            //return !noOverlap;

            return (math.abs(Center[0] - b.Center[0]) < (Extents[0] + b.Extents[0])) &&
                   (math.abs(Center[1] - b.Center[1]) < (Extents[1] + b.Extents[1]));
        }
    }
}