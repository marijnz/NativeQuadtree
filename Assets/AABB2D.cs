using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using Unity.Mathematics;

namespace NativeQuadTree
{
	[Serializable, DebuggerDisplay("Center: {Center}, Extents: {Extents}")]
	public struct AABB2D {
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

		public bool Contains(float2 point) {
			if (point[0] < Center[0] - Extents[0]) {
				return false;
			}
			if (point[0] > Center[0] + Extents[0]) {
				return false;
			}

			if (point[1] < Center[1] - Extents[1]) {
				return false;
			}
			if (point[1] > Center[1] + Extents[1]) {
				return false;
			}

			return true;
		}

		public bool Contains(AABB2D b) {
			return Contains(b.Center + -b.Extents) &&
			       Contains(b.Center + new float2(-b.Extents.x, b.Extents.y)) &&
			       Contains(b.Center + new float2(b.Extents.x, -b.Extents.y)) &&
			       Contains(b.Center + b.Extents);
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool Contains(Circle2D b)
		{
			float closestX = math.clamp(b.Center.x, Center.x - Extents.x, Center.x + Extents.x);
			if(math.distance(closestX, b.Center.x) < b.Radious)
			{
				return true;
			}
			
			float closestY = math.clamp(b.Center.y, Center.y - Extents.y, Center.y + Extents.y);
			if(math.distance(closestY, b.Center.y) < b.Radious)
			{
				return true;
			}

			return math.distance(Center, b.Center + -Extents) <= b.Radious &&
			       math.distance(Center, b.Center + new float2(-Extents.x, Extents.y)) <= b.Radious &&
			       math.distance(Center, b.Center + new float2(Extents.x, -Extents.y)) <= b.Radious &&
			       math.distance(Center, b.Center + Extents.x) <= b.Radious;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool Intersects(Circle2D b)
		{
			float2 squareEdgePoint = new float2
			{
				x = math.clamp(b.Center.x, Center.x - Extents.x, Center.x + Extents.x),
				y = math.clamp(b.Center.y, Center.y - Extents.y, Center.y + Extents.y)
			};

			return math.distance(squareEdgePoint, Center) <= b.Radious; 
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