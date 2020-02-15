using System;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;

namespace NativeQuadTree
{
	// Represents an element node in the quadtree.
	public struct QuadElement<T> where T : unmanaged
	{
		public float2 pos;
		public T element;
	}

	struct QuadNode
	{
		// Points to this node's first child index in elements
		public int firstChildIndex;

		// Number of elements in the leaf
		public short count;
		public bool isLeaf;
	}

	/// <summary>
	/// A QuadTree aimed to be used with Burst, supports fast bulk insertion and querying.
	///
	/// TODO:
	/// - Better test coverage
	/// - Automated depth / bounds / max leaf elements calculation
	/// </summary>
	public unsafe partial struct NativeQuadTree<T> : IDisposable where T : unmanaged
	{
		// Safety
		AtomicSafetyHandle safetyHandle;
		[NativeSetClassTypeToNullOnSchedule]
		DisposeSentinel disposeSentinel;

		// Data
		[NativeDisableUnsafePtrRestriction]
		UnsafeList* elements;

		[NativeDisableUnsafePtrRestriction]
		UnsafeList* lookup;

		[NativeDisableUnsafePtrRestriction]
		UnsafeList* nodes;

		int elementsCount;

		int maxDepth;
		short maxLeafElements;

		AABB2D bounds; // NOTE: Currently assuming uniform

		/// <summary>
		/// Create a new QuadTree.
		/// - Ensure the bounds are not way bigger than needed, otherwise the buckets are very off. Probably best to calculate bounds
		/// - The higher the depth, the larger the overhead, it especially goes up at a depth of 7/8
		/// </summary>
		public NativeQuadTree(AABB2D bounds, Allocator allocator = Allocator.Temp, int maxDepth = 4, short maxLeafElements = 16,
			int initialElementsCapacity = 256
		) : this()
		{
			this.bounds = bounds;
			this.maxDepth = maxDepth;
			this.maxLeafElements = maxLeafElements;
			elementsCount = 0;

			if(maxDepth > 8)
			{
				// Currently no support for higher depths, the morton code lookup tables would have to support it
				throw new InvalidOperationException();
			}

#if ENABLE_UNITY_COLLECTIONS_CHECKS
			CollectionHelper.CheckIsUnmanaged<T>();
			DisposeSentinel.Create(out safetyHandle, out disposeSentinel, 1, allocator);
#endif

			// Allocate memory for every depth, the nodes on all depths are stored in a single continuous array
			var totalSize = LookupTables.DepthSizeLookup[maxDepth+1];

			lookup = UnsafeList.Create(UnsafeUtility.SizeOf<int>(),
				UnsafeUtility.AlignOf<int>(),
				totalSize,
				allocator,
				NativeArrayOptions.ClearMemory);

			nodes = UnsafeList.Create(UnsafeUtility.SizeOf<QuadNode>(),
				UnsafeUtility.AlignOf<QuadNode>(),
				totalSize,
				allocator,
				NativeArrayOptions.ClearMemory);

			elements = UnsafeList.Create(UnsafeUtility.SizeOf<QuadElement<T>>(),
				UnsafeUtility.AlignOf<QuadElement<T>>(),
				initialElementsCapacity,
				allocator);
		}

		public void ClearAndBulkInsert(NativeArray<QuadElement<T>> incomingElements)
		{
			// Always have to clear before bulk insert as otherwise the lookup and node allocations need to account
			// for existing data.
			Clear();

#if ENABLE_UNITY_COLLECTIONS_CHECKS
			AtomicSafetyHandle.CheckWriteAndBumpSecondaryVersion(safetyHandle);
#endif

			// Resize if needed
			if(elements->Capacity < elementsCount + incomingElements.Length)
			{
				elements->Resize<QuadElement<T>>(math.max(incomingElements.Length, elements->Capacity*2));
			}

			var mortonCodes = new NativeArray<int>(incomingElements.Length, Allocator.Temp);

			var depthExtentsScaling = LookupTables.DepthLookup[maxDepth] / bounds.Extents;
			for (var i = 0; i < incomingElements.Length; i++)
			{
				var incPos = incomingElements[i].pos;
				incPos -= bounds.Center; // Offset by center
				incPos.y = -incPos.y; // World -> array
				var pos = (incPos + bounds.Extents) * .5f; // Make positive
				// Now scale into available space that belongs to the depth
				pos *= depthExtentsScaling;
				// And interleave the bits for the morton code
				mortonCodes[i] = (LookupTables.MortonLookup[(int) pos.x] | (LookupTables.MortonLookup[(int) pos.y] << 1));
			}

			/*
 * Let's say there's 8 depth so there's a a morton code spanning 18 bits: 01001000100111010101
 *
 *  On depth 1				On depth 2				On depth 3
 *
 * 	bit shift 16			bit shift 14			etc..
 *  01|0010001001110101		0100|001001110101
 *
 * 	00 01					0000 0001 0100 0101
 *	10 11			 		0010 0011 0110 0111
 *							1000 1001 1100 1101
 *							1010 1011 1110 1111
 *
 *  the decimal representation within [] will be the index into the lookup array
 *  ( + the offset of all previous depth's data)
 */

			// Index total child element count per node (total, so parent's counts include those of child nodes)
			for (var i = 0; i < mortonCodes.Length; i++)
			{
				int atIndex = 0;
				for (int depth = 0; depth <= maxDepth; depth++)
				{
					// Increment the node on this depth that this element is contained in
					(*(int*) ((IntPtr) lookup->Ptr + atIndex * sizeof (int)))++;

					if(depth != maxDepth)
					{
						var atDepth = maxDepth - depth;

						// Then shift to right to get rid of lower morton code
						int shiftedMortonCode = mortonCodes[i] >> ((atDepth-1) * 2);
						int mask = 0b11;
						shiftedMortonCode = shiftedMortonCode & mask;

						// so the index becomes that... (0,1,2,3)
						atIndex += LookupTables.DepthSizeLookup[atDepth] * shiftedMortonCode;
						atIndex++; // offset for self

					}

				}
			}


			// Prepare the tree leaf nodes
			RecursivePrepareLeaves(1, 1);

			// Add elements to leaf nodes
			for (var i = 0; i < incomingElements.Length; i++)
			{
				int atIndex = 0;

				bool added = false;
				for (int depth = 0; depth <= maxDepth; depth++)
				{
					var node = UnsafeUtility.ReadArrayElement<QuadNode>(nodes->Ptr, atIndex);
					if(node.isLeaf)
					{
						// We found a leaf, add this element to it and move to the next element
						UnsafeUtility.WriteArrayElement(elements->Ptr, node.firstChildIndex + node.count, incomingElements[i]);
						node.count++;
						UnsafeUtility.WriteArrayElement(nodes->Ptr, atIndex, node);
						added = true;
						break;
					}
					// No leaf found, we keep going deeper until we find one

					if(depth != maxDepth)
					{
						var atDepth = maxDepth - depth;

						// Then shift to right to get rid of lower morton code
						int shiftedMortonCode = mortonCodes[i] >> ((atDepth-1) * 2);
						int mask = 0b11;
						shiftedMortonCode = shiftedMortonCode & mask;

						// so the index becomes that... (0,1,2,3)
						atIndex += LookupTables.DepthSizeLookup[atDepth] * shiftedMortonCode;
						atIndex++; // offset for self
					}

				}
				if(!added)
				{
					// wjat?
				}
			}

			mortonCodes.Dispose();
		}

		void RecursivePrepareLeaves(int prevOffset, int depth)
		{
			for (int l = 0; l < 4; l++)
			{
				var at = prevOffset + l * LookupTables.DepthSizeLookup[maxDepth - depth+1];

				var elementCount = UnsafeUtility.ReadArrayElement<int>(lookup->Ptr, at);

				if(elementCount > maxLeafElements && depth < maxDepth)
				{
					// There's more elements than allowed on this node so keep going deeper
					RecursivePrepareLeaves(at+1, depth+1);
				}
				else if(elementCount != 0)
				{
					// We either hit max depth or there's less than the max elements on this node, make it a leaf
					var node = new QuadNode {firstChildIndex = elementsCount, count = 0, isLeaf = true };
					UnsafeUtility.WriteArrayElement(nodes->Ptr, at, node);
					elementsCount += elementCount;
				}
			}
		}

		public void RangeQuery(AABB2D bounds, NativeList<QuadElement<T>> results)
		{
#if ENABLE_UNITY_COLLECTIONS_CHECKS
			AtomicSafetyHandle.CheckReadAndThrow(safetyHandle);
#endif
			new QuadTreeRangeQuery().Query(this, bounds, results);
		}

		public void Clear()
		{
#if ENABLE_UNITY_COLLECTIONS_CHECKS
			AtomicSafetyHandle.CheckWriteAndBumpSecondaryVersion(safetyHandle);
#endif
			lookup->Clear();
			nodes->Clear();
			elements->Clear();
			elementsCount = 0;
		}

		public void Dispose()
		{
			UnsafeList.Destroy(elements);
			elements = null;
			UnsafeList.Destroy(lookup);
			lookup = null;
			UnsafeList.Destroy(nodes);
			nodes = null;
#if ENABLE_UNITY_COLLECTIONS_CHECKS
			DisposeSentinel.Dispose(ref safetyHandle, ref disposeSentinel);
#endif
		}
	}
}
