using System;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;

namespace marijnz.NativeQuadTree
{
	public struct QuadElement
	{
		public float2 pos;
		public int id;
	}

	/// <summary>
	/// A QuadTree aimed to be used by Burst, using morton code for very fast bulk insertion.
	///
	/// TODO:
	/// - Safety checks with AtomicSafetyHandle / DisposeSentinel
	/// - Better test coverage
	/// - Automated depth / bounds / max leaf elements calculation
	/// </summary>
	public unsafe partial struct NativeQuadTree : IDisposable
	{
		struct QuadNode
		{
			// Points to this node's first child index in elements
			public int firstChildIndex;

			// Number of elements in the leaf
			public short count;

			// Capacity of elements in the leaf. TODO: not really needed anymore
			public short elementsCapacity;
		}

		[NativeDisableUnsafePtrRestriction]
		UnsafeList* elements;

		[NativeDisableUnsafePtrRestriction]
		UnsafeList* lookup;

		int elementsCount;

		int maxDepth;
		short maxLeafElements;

		AABB2D bounds; // NOTE: Currently assuming uniform

		[NativeDisableUnsafePtrRestriction]
		UnsafeList* nodesQuick;

		/// <summary>
		/// Create a new QuadTree.
		/// - Ensure the bounds are not way bigger than needed, otherwise the buckets are very off. Probably best to calculate bounds
		/// - The higher the depth, the larger the overhead, it especially goes up at a depth of 7/8
		/// </summary>
		public NativeQuadTree(AABB2D bounds, int maxDepth = 6, short maxLeafElements = 16,
			int initialElementsCapacity = 300000) : this()
		{
			this.bounds = bounds;
			this.maxDepth = maxDepth;
			this.maxLeafElements = maxLeafElements;

			elements = UnsafeList.Create(UnsafeUtility.SizeOf<QuadElement>(), UnsafeUtility.AlignOf<QuadElement>(), initialElementsCapacity, Allocator.Persistent);
			elementsCount = 0;

			if(maxDepth > 8)
			{
				throw new InvalidOperationException();
			}

			const int totalSize = 1+2*2+4*4+8*8+16+16+32*32+64*64+128*128+256*256+512*512;

			lookup = UnsafeList.Create(UnsafeUtility.SizeOf<int>(),
				UnsafeUtility.AlignOf<QuadNode>(),
				totalSize,
				Allocator.Persistent,
				NativeArrayOptions.ClearMemory);

			nodesQuick = UnsafeList.Create(UnsafeUtility.SizeOf<QuadNode>(),
				UnsafeUtility.AlignOf<QuadNode>(),
				totalSize,
				Allocator.Persistent,
				NativeArrayOptions.ClearMemory);
		}

		public void BulkInsert(NativeArray<QuadElement> incomingElements)
		{
			var mortonCodes = new NativeArray<int>(incomingElements.Length, Allocator.Temp);

			// Remapping values to range of depth
			var depthRemapMult = LookupTables.DepthLookup[maxDepth] / bounds.Extents.x;
			for (var i = 0; i < incomingElements.Length; i++)
			{
				var incPos = incomingElements[i].pos;
				incPos.y = -incPos.y; // world -> array
				var pos = (int2) ((incPos + bounds.Extents) * .5f * depthRemapMult);
				mortonCodes[i] = LookupTables.MortonLookup[pos.x] | (LookupTables.MortonLookup[pos.y] << 1);
			}

			// Index total child element count per node (so including those of child nodes)
			for (var i = 0; i < mortonCodes.Length; i++)
			{
				var mortonCode = mortonCodes[i];

				for (int depth = maxDepth; depth >= 0; depth--)
				{
					int level = mortonCode >> ((maxDepth - depth) *2);

					// Offset by depth and add morton index
					var index = LookupTables.DepthSizeLookup[depth] + level;

					// +1 to that node lookup
					(*(int*) ((IntPtr) lookup->Ptr + index * sizeof (int)))++;
				}
			}

			// Allocate the tree leave nodes
			RecursiveAlloc(0, 0);

			// Add elements to leave nodes
			for (var i = 0; i < mortonCodes.Length; i++)
			{
				var mortonCode = mortonCodes[i];

				for (int depth = maxDepth; depth >= 0; depth--)
				{
					int level = mortonCode >> ((maxDepth - depth) *2);

					// Offset by depth and add morton index
					var index = LookupTables.DepthSizeLookup[depth] + level;
					var node = UnsafeUtility.ReadArrayElement<QuadNode>(nodesQuick->Ptr, index);
					if(node.elementsCapacity > 0)
					{
						UnsafeUtility.WriteArrayElement(elements->Ptr, node.firstChildIndex + node.count, incomingElements[i]);
						node.count++;
						UnsafeUtility.WriteArrayElement(nodesQuick->Ptr, index, node);
						break;
					}
				}
			}

			mortonCodes.Dispose();
		}

		void RecursiveAlloc(int atNode, int depth)
		{
			var totalOffset = LookupTables.DepthSizeLookup[++depth];

			for (int l = 0; l < 4; l++)
			{
				var at = totalOffset + atNode + l;

				var elementCount = UnsafeUtility.ReadArrayElement<int>(lookup->Ptr, at);

				if(elementCount > maxLeafElements && depth < maxDepth)
				{
					RecursiveAlloc((atNode + l) * 4, depth);
				}
				else if(elementCount != 0)
				{
					// Alloc node
					var node = new QuadNode {firstChildIndex = elementsCount, count = 0, elementsCapacity = (short) elementCount};
					UnsafeUtility.WriteArrayElement(nodesQuick->Ptr, at, node);
					elementsCount += elementCount;
				}
			}
		}

		struct RangeQueryRequest
		{
			public NativeList<QuadElement> ResultsOutput;
			public QuadElement* ResultsStack;
			public int Length;
			public AABB2D Bounds;
		}

		// 512 elements seems to be a reasonable trade off in overhead / performance gain but it can be changed.
		const int QueryStackSize = 512;

		public void RangeQuery(AABB2D bounds, NativeList<QuadElement> results)
		{
			var stack = stackalloc QuadElement[QueryStackSize];
			var query = new RangeQueryRequest
			{
				Bounds = bounds,
				ResultsStack = stack,
				ResultsOutput = results
			};

			RecursiveRangeQuery(ref query, this.bounds, false, 0, 0);

			results.AddRange(query.ResultsStack, query.Length);
		}

		void RecursiveRangeQuery(ref RangeQueryRequest query, AABB2D parentBounds, bool parentContained, int atNode, int depth)
		{
			if(query.Length + 4 * maxLeafElements > QueryStackSize)
			{
				// We're about to hit the stack limit, copy over to results and reset length
				query.ResultsOutput.AddRange(query.ResultsStack, query.Length);
				query.Length = 0;
			}

			var totalOffset = LookupTables.DepthSizeLookup[++depth];

			for (int l = 0; l < 4; l++)
			{
				var childBounds = GetChildBounds(parentBounds, l);

				var contained = parentContained;
				if(!contained)
				{
					if(query.Bounds.Contains(childBounds))
					{
						contained = true;
					}
					else if(!query.Bounds.Intersects(childBounds))
					{
						continue;
					}
				}

				var at = totalOffset + atNode + l;
				var elementCount = UnsafeUtility.ReadArrayElement<int>(lookup->Ptr, at);

				if(elementCount > maxLeafElements && depth < maxDepth)
				{
					RecursiveRangeQuery( ref query, childBounds, contained, (atNode + l) * 4, depth);
				}
				else if(elementCount != 0)
				{
					var node = UnsafeUtility.ReadArrayElement<QuadNode>(nodesQuick->Ptr, at);

					if(contained)
					{
						var index = (void*) ((IntPtr) elements->Ptr + node.firstChildIndex * sizeof(QuadElement));
						//query.Results.AddRange(index, node.count);
						UnsafeUtility.MemCpy((void*) ((IntPtr) query.ResultsStack + query.Length * sizeof(QuadElement)),
							index, node.count * sizeof(QuadElement));
						query.Length += node.count;
					}
					else
					{
						for (int k = 0; k < node.count; k++)
						{
							var element = UnsafeUtility.ReadArrayElement<QuadElement>(elements->Ptr, node.firstChildIndex + k);
							if(query.Bounds.Contains(element.pos))
							{
								query.ResultsStack[query.Length++] = (element);
							}
						}
					}
				}
			}
		}

		public void Dispose()
		{
			UnsafeList.Destroy(elements);
			elements = null;
			UnsafeList.Destroy(lookup);
			lookup = null;
			UnsafeList.Destroy(nodesQuick);
			nodesQuick = null;
		}

		static AABB2D GetChildBounds(AABB2D parentBounds, int childZIndex)
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
