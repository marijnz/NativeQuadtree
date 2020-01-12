using System;
using Unity.Mathematics;

namespace QuadTree
{
	[Serializable]
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
			return Contains(b.Center + new float2(-b.Extents.x, -b.Extents.y)) &&
			       Contains(b.Center + new float2(-b.Extents.x, b.Extents.y)) &&
			       Contains(b.Center + new float2(b.Extents.x, -b.Extents.y)) &&
			       Contains(b.Center + new float2(b.Extents.x, b.Extents.y));
		}
	}
}