﻿using System;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;
using UnityEngine;

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
#if ENABLE_UNITY_COLLECTIONS_CHECKS
		// Safety
		AtomicSafetyHandle safetyHandle;
		[NativeSetClassTypeToNullOnSchedule]
		DisposeSentinel disposeSentinel;
#endif
		// Data
		[NativeDisableUnsafePtrRestriction]
		UnsafeList<QuadElement<T>>* elements;

		[NativeDisableUnsafePtrRestriction]
		UnsafeList<int>* lookup;

		[NativeDisableUnsafePtrRestriction]
		UnsafeList<QuadNode>* nodes;

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
			// TODO: Find out what the equivalent of this is in latest entities
            // CollectionHelper.CheckIsUnmanaged<T>();
			DisposeSentinel.Create(out safetyHandle, out disposeSentinel, 1, allocator);
#endif

			// Allocate memory for every depth, the nodes on all depths are stored in a single continuous array
			var totalSize = LookupTables.DepthSizeLookup[maxDepth+1];

			lookup = UnsafeList<int>.Create(
				totalSize,
				allocator,
				NativeArrayOptions.ClearMemory);

			nodes = UnsafeList<QuadNode>.Create(
				totalSize,
				allocator,
				NativeArrayOptions.ClearMemory);

			elements = UnsafeList<QuadElement<T>>.Create(
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
				elements->Resize(math.max(incomingElements.Length, elements->Capacity*2));
			}

			// Prepare morton codes
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

			// Index total child element count per node (total, so parent's counts include those of child nodes)
			for (var i = 0; i < mortonCodes.Length; i++)
			{
				int atIndex = 0;
				for (int depth = 0; depth <= maxDepth; depth++)
				{
					// Increment the node on this depth that this element is contained in
					(*(int*) ((IntPtr) lookup->Ptr + atIndex * sizeof (int)))++;
					atIndex = IncrementIndex(depth, mortonCodes, i, atIndex);
				}
			}

			// Prepare the tree leaf nodes
			RecursivePrepareLeaves(1, 1);

			// Add elements to leaf nodes
			for (var i = 0; i < incomingElements.Length; i++)
			{
				int atIndex = 0;

				for (int depth = 0; depth <= maxDepth; depth++)
				{
					var node = UnsafeUtility.ReadArrayElement<QuadNode>(nodes->Ptr, atIndex);
					if(node.isLeaf)
					{
						// We found a leaf, add this element to it and move to the next element
						UnsafeUtility.WriteArrayElement(elements->Ptr, node.firstChildIndex + node.count, incomingElements[i]);
						node.count++;
						UnsafeUtility.WriteArrayElement(nodes->Ptr, atIndex, node);
						break;
					}
					// No leaf found, we keep going deeper until we find one
					atIndex = IncrementIndex(depth, mortonCodes, i, atIndex);
				}
			}

			mortonCodes.Dispose();
		}

		int IncrementIndex(int depth, NativeArray<int> mortonCodes, int i, int atIndex)
		{
			var atDepth = math.max(0, maxDepth - depth);
			// Shift to the right and only get the first two bits
			int shiftedMortonCode = (mortonCodes[i] >> ((atDepth - 1) * 2)) & 0b11;
			// so the index becomes that... (0,1,2,3)
			atIndex += LookupTables.DepthSizeLookup[atDepth] * shiftedMortonCode;
			atIndex++; // offset for self
			return atIndex;
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
			UnsafeUtility.MemClear(lookup->Ptr, lookup->Capacity * UnsafeUtility.SizeOf<int>());
			UnsafeUtility.MemClear(nodes->Ptr, nodes->Capacity * UnsafeUtility.SizeOf<QuadNode>());
			UnsafeUtility.MemClear(elements->Ptr, elements->Capacity * UnsafeUtility.SizeOf<QuadElement<T>>());
			elementsCount = 0;
		}

		public void Dispose()
        {
			UnsafeList<QuadElement<T>>.Destroy(elements);
			elements = null;
			UnsafeList<int>.Destroy(lookup);
			lookup = null;
			UnsafeList<QuadNode>.Destroy(nodes);
			nodes = null;
#if ENABLE_UNITY_COLLECTIONS_CHECKS
			DisposeSentinel.Dispose(ref safetyHandle, ref disposeSentinel);
#endif
		}
	}
}
