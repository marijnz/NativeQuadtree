using System;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;

namespace NativeQuadTree
{
	public unsafe partial struct NativeQuadTree<T> where T : unmanaged
	{
		struct QuadTreeRangeQuery
		{
			NativeQuadTree<T> tree;

			UnsafeList* fastResults;
			int count;

			AABB2D bounds;

			public void Query(NativeQuadTree<T> tree, AABB2D bounds, NativeList<QuadElement<T>> results)
			{
				this.tree = tree;
				this.bounds = bounds;
				count = 0;

				// Get pointer to inner list data for faster writing
				fastResults = (UnsafeList*) NativeListUnsafeUtility.GetInternalListDataPtrUnchecked(ref results);

				// Query root node
				QueryNode(0, bounds.Contains(tree.bounds));

				// Query rest of tree
				RecursiveRangeQuery(tree.bounds, false, 1, 1);

				fastResults->Length = count;
			}

			public void RecursiveRangeQuery(AABB2D parentBounds, bool parentContained, int prevOffset, int depth)
			{
				if(count + 4 * tree.maxLeafElements > fastResults->Capacity)
				{
					fastResults->Resize<QuadElement<T>>(math.max(fastResults->Capacity * 2, count + 4 * tree.maxLeafElements));
				}

				var depthSize = LookupTables.DepthSizeLookup[tree.maxDepth - depth+1];
				for (int l = 0; l < 4; l++)
				{
					var childBounds = GetChildBounds(parentBounds, l);

					var contained = parentContained;
					if(!contained)
					{
						if(bounds.Contains(childBounds))
						{
							contained = true;
						}
						else if(!bounds.Intersects(childBounds))
						{
							continue;
						}
					}

					var at = prevOffset + l * depthSize;
					QueryNode(at, contained);

					var elementCount = UnsafeUtility.ReadArrayElement<int>(tree.lookup->Ptr, at);
					var goDeeper = elementCount > tree.maxLeafElements && depth < tree.maxDepth;
					if(goDeeper)
					{
						RecursiveRangeQuery(childBounds, contained, at+1, depth+1);
					}
				}
			}

			private void QueryNode(int at, bool contained) {
				var node = UnsafeUtility.ReadArrayElement<QuadNode>(tree.nodes->Ptr, at);

				if (node.count > 0) {
					if (contained) {
						var index = (void*) ((IntPtr) tree.elements->Ptr + node.firstChildIndex * UnsafeUtility.SizeOf<QuadElement<T>>());

						UnsafeUtility.MemCpy((void*) ((IntPtr) fastResults->Ptr + count * UnsafeUtility.SizeOf<QuadElement<T>>()), index, node.count * UnsafeUtility.SizeOf<QuadElement<T>>());
						count += node.count;
					}
					else {
						var actualElementCount = node.count;
						for (int k = 0; k < actualElementCount; k++) {
							var element = UnsafeUtility.ReadArrayElement<QuadElement<T>>(tree.elements->Ptr, node.firstChildIndex + k);
							if (bounds.Intersects(element.bounds)) {
								UnsafeUtility.WriteArrayElement(fastResults->Ptr, count++, element);
							}
						}
					}
				}
			}

			public static AABB2D GetChildBounds(AABB2D parentBounds, int childZIndex)
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
}