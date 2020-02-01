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

		// Capacity of elements in the leaf. TODO: not really needed anymore
		public short elementsCapacity;
	}

	/// <summary>
	/// A QuadTree aimed to be used by Burst, using morton code for very fast bulk insertion.
	///
	/// TODO:
	/// - Safety checks with AtomicSafetyHandle / DisposeSentinel
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
				throw new InvalidOperationException();
			}


#if ENABLE_UNITY_COLLECTIONS_CHECKS

			CollectionHelper.CheckIsUnmanaged<T>();
			DisposeSentinel.Create(out safetyHandle, out disposeSentinel, 1, allocator);
#endif

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
					var node = UnsafeUtility.ReadArrayElement<QuadNode>(nodes->Ptr, index);
					if(node.elementsCapacity > 0)
					{
						UnsafeUtility.WriteArrayElement(elements->Ptr, node.firstChildIndex + node.count, incomingElements[i]);
						node.count++;
						UnsafeUtility.WriteArrayElement(nodes->Ptr, index, node);
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
			new QuadTreeRangeQuery<T>().Query(this, bounds, results);
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
