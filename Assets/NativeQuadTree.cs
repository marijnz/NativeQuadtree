using System;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;
using UnityEngine;

namespace NativeQuadTree
{
	// Represents an element node in the quadtree.
	public struct QuadElement<T> where T : unmanaged
	{
		public AABB2D bounds;
		public T element;
	}

	struct QuadNode
	{
		// Points to this node's first child index in elements
		public int firstChildIndex;

		// Number of elements in the leaf
		public short count;
		public bool canGoDeeper;
		public int expectedElementCount;
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
#if ENABLE_UNITY_COLLECTIONS_CHECKS
		// Safety
		AtomicSafetyHandle safetyHandle;
		[NativeSetClassTypeToNullOnSchedule]
		DisposeSentinel disposeSentinel;
#endif
		// Data
		[NativeDisableUnsafePtrRestriction]
		UnsafeList* elements;

		[NativeDisableUnsafePtrRestriction]
		UnsafeList* lookup;
		
		[NativeDisableUnsafePtrRestriction]
		UnsafeList* lookupMust;

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
		public NativeQuadTree(AABB2D bounds, Allocator allocator = Allocator.Temp, int maxDepth = 6, short maxLeafElements = 16,
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
			
			lookupMust = UnsafeList.Create(UnsafeUtility.SizeOf<int>(),
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
			
			var isMustAtLevel = new NativeArray<int>(incomingElements.Length, Allocator.Temp);

			// Prepare morton codes
			var mortonCodes = new NativeArray<int2>(incomingElements.Length, Allocator.Temp);
			var depthExtentsScaling = LookupTables.DepthLookup[maxDepth] / bounds.Extents;
			
			for (var i = 0; i < incomingElements.Length; i++) {
				int posMin, posMax;
				{
					var incPos = incomingElements[i].bounds.Min;
					incPos -= bounds.Center; // Offset by center
					incPos.y = -incPos.y; // World -> array
					var pos = (incPos + bounds.Extents) * .5f; // Make positive
					// Now scale into available space that belongs to the depth
					pos *= depthExtentsScaling;
					// And interleave the bits for the morton code
					posMin = (LookupTables.MortonLookup[(int) pos.x] | (LookupTables.MortonLookup[(int) pos.y] << 1));
				}
				
				{
					var incPos = incomingElements[i].bounds.Max;
					incPos -= bounds.Center; // Offset by center
					incPos.y = -incPos.y; // World -> array
					var pos = (incPos + bounds.Extents) * .5f; // Make positive
					// Now scale into available space that belongs to the depth
					pos *= depthExtentsScaling;
					// And interleave the bits for the morton code
					posMax = (LookupTables.MortonLookup[(int) pos.x] | (LookupTables.MortonLookup[(int) pos.y] << 1));
				}

				mortonCodes[i] = new int2(posMin, posMax);
			}

			// Index total child element count per node (total, so parent's counts include those of child nodes)
			for (var i = 0; i < mortonCodes.Length; i++)
			{
				int atIndex = 0;
				isMustAtLevel[i] = -1;
				for (int depth = 0; depth <= maxDepth; depth++)
				{
					// Increment the node on this depth that this element is contained in
					(*(int*) ((IntPtr) lookup->Ptr + atIndex * sizeof (int)))++;
					
					var atIndex2 = IncrementIndex(depth, mortonCodes, i, atIndex);
					var areSame = atIndex2.x == atIndex2.y;
					if (!areSame) {
						// Not the same, can't go deeper
						(*(int*) ((IntPtr) lookupMust->Ptr + atIndex * sizeof (int)))++;
						isMustAtLevel[i] = depth;
						break;
					} else {
						atIndex = atIndex2.x;
					}
				}
			}
			
			// Prepare root node
			WriteToNode(0, 0, 0, out var _);
			
			// Prepare the tree leaf nodes
			RecursivePrepareLeaves(1, 1);

			// Add elements to leaf nodes
			for (var i = 0; i < incomingElements.Length; i++)
			{
				int atIndex = 0;

				var mustAtDepth = isMustAtLevel[i];

				for (int depth = 0; depth <= maxDepth; depth++)
				{
					var node = UnsafeUtility.ReadArrayElement<QuadNode>(nodes->Ptr, atIndex);

					if (mustAtDepth == depth || (!node.canGoDeeper && node.expectedElementCount > 0)) {
						// We found a leaf, add this element to it and move to the next element
						UnsafeUtility.WriteArrayElement(elements->Ptr, node.firstChildIndex + node.count, incomingElements[i]);
						node.count++;
						UnsafeUtility.WriteArrayElement(nodes->Ptr, atIndex, node);
						break;
					}
					
					// No leaf found, we keep going deeper until we find one
					var atIndex2 = IncrementIndex(depth, mortonCodes, i, atIndex);
					var areSame = atIndex2.x == atIndex2.y;
					Debug.Assert(areSame);
					atIndex = atIndex2.x;
				}
			}

			mortonCodes.Dispose();
			isMustAtLevel.Dispose();
		}

		int2 IncrementIndex(int depth, NativeArray<int2> mortonCodes, int i, int atIndex)
		{
			var atDepth = math.max(0, maxDepth - depth);
			// Shift to the right and only get the first two bits
			int2 shiftedMortonCode = (mortonCodes[i] >> ((atDepth - 1) * 2)) & 0b11;
			// so the index becomes that... (0,1,2,3)
			int2 atIndex2 = atIndex; // TODO check if this is correct
			atIndex2 += LookupTables.DepthSizeLookup[atDepth] * shiftedMortonCode;
			atIndex2++; // offset for self
			return atIndex2;
		}

		void RecursivePrepareLeaves(int prevOffset, int depth)
		{
			for (int l = 0; l < 4; l++) {
				var at = WriteToNode(prevOffset, depth, l, out var goDeeper);
				if(goDeeper)
				{
					// There's more elements than allowed on this node so keep going deeper
					RecursivePrepareLeaves(at+1, depth+1);
				}
			}
		}

		private int WriteToNode(int prevOffset, int depth, int l, out bool goDeeper) {
			var at = prevOffset + l * LookupTables.DepthSizeLookup[maxDepth - depth + 1];

			var elementCount = UnsafeUtility.ReadArrayElement<int>(lookup->Ptr, at);
			var elementCountMust = UnsafeUtility.ReadArrayElement<int>(lookupMust->Ptr, at);

			goDeeper = elementCount > maxLeafElements && depth < maxDepth;

			if (!goDeeper || elementCountMust > 0) {
				// We either hit max depth or there's less than the max elements on this node, make it a leaf
				var node = new QuadNode {firstChildIndex = elementsCount, count = 0, canGoDeeper = goDeeper, expectedElementCount = elementCount};
				UnsafeUtility.WriteArrayElement(nodes->Ptr, at, node);

				var count = (goDeeper && elementCountMust > 0) ? elementCountMust : elementCount;
				elementsCount += count;
			}

			return at;
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
			UnsafeUtility.MemClear(lookup->Ptr, lookup->Capacity * UnsafeUtility.SizeOf<int>());
			UnsafeUtility.MemClear(nodes->Ptr, nodes->Capacity * UnsafeUtility.SizeOf<QuadNode>());
			UnsafeUtility.MemClear(elements->Ptr, elements->Capacity * UnsafeUtility.SizeOf<QuadElement<T>>());
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
