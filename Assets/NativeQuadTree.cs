﻿using System;
using System.Runtime.CompilerServices;
using JetBrains.Annotations;
using Mono.Cecil.Cil;
using NativeQuadTree.Jobs.Internal;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;
using UnityEngine.Assertions;

namespace NativeQuadTree
{
	/// <summary>
	/// A QuadTree aimed to be used with Burst, supports fast bulk insertion and querying.
	///
	/// TODO:
	/// - Better test coverage
	/// - Automated depth / bounds / max leaf elements calculation
	/// </summary>
	public unsafe struct NativeQuadTree<T> : IDisposable where T : unmanaged
	{
#if ENABLE_UNITY_COLLECTIONS_CHECKS && !NATIVE_QUAD_TREE_ECS_USAGE
		// Safety
		AtomicSafetyHandle safetyHandle;
		[NativeSetClassTypeToNullOnSchedule]
		DisposeSentinel disposeSentinel;
#endif
		// Data
		[NativeDisableUnsafePtrRestriction]
		public UnsafeList* elements;

		[NativeDisableUnsafePtrRestriction]
		public UnsafeList* lookup;

		[NativeDisableUnsafePtrRestriction]
		public UnsafeList* nodes;

		public int EntryCount => elementsCount;
		private int elementsCount;

		public int MaxDepth => m_maxDepth;
		private int m_maxDepth;
		public ushort MaxLeafElements => maxLeafElements;
		private ushort maxLeafElements;

		internal AABB2D bounds; // NOTE: Currently assuming uniform

		/// <summary>
		/// Create a new QuadTree.
		/// - Ensure the bounds are not way bigger than needed, otherwise the buckets are very off. Probably best to calculate bounds
		/// - The higher the depth, the larger the overhead, it especially goes up at a depth of 7/8
		/// </summary>
		public NativeQuadTree(AABB2D bounds, Allocator allocator = Allocator.Temp, int maxDepth = 6, ushort maxLeafElements = 16,
			int initialElementsCapacity = 256
		) : this()
		{
			this.bounds = bounds;
			m_maxDepth = maxDepth;
			this.maxLeafElements = maxLeafElements;
			elementsCount = 0;

			if(maxDepth > 8)
			{
				// Currently no support for higher depths, the morton code lookup tables would have to support it
				throw new InvalidOperationException();
			}

#if ENABLE_UNITY_COLLECTIONS_CHECKS && !NATIVE_QUAD_TREE_ECS_USAGE
			//CollectionHelper.CheckIsUnmanaged<T>();
			DisposeSentinel.Create(out safetyHandle, out disposeSentinel, 1, allocator);
#endif
#if UNITY_ASSERTIONS
			// make sure that bounds are valid
			Assert.IsFalse(bounds.Extents.x == 0, "bounds can't be empty! X axis must be greater than 0");
			Assert.IsFalse(bounds.Extents.y == 0, "bounds can't be empty! Y axis must be greater than 0");
			Assert.IsFalse(float.IsInfinity(bounds.Extents.x), "bounds can't be infinite! X axis is infinity");
			Assert.IsFalse(float.IsInfinity(bounds.Extents.y), "bounds can't be infinite! Y axis is infinity");
#endif

			// Allocate memory for every depth, the nodes on all depths are stored in a single continuous array
			int totalSize = LookupTables.DepthSizeLookup[maxDepth+1];

			lookup = UnsafeList.Create(UnsafeUtility.SizeOf<int>(),
				UnsafeUtility.AlignOf<int>(),
				totalSize,
				allocator,
				NativeArrayOptions.ClearMemory);
			lookup->Length = totalSize;

			nodes = UnsafeList.Create(UnsafeUtility.SizeOf<QuadNode>(),
				UnsafeUtility.AlignOf<QuadNode>(),
				totalSize,
				allocator,
				NativeArrayOptions.ClearMemory);
			nodes->Length = totalSize;

			elements = UnsafeList.Create(UnsafeUtility.SizeOf<QuadElement<T>>(),
				UnsafeUtility.AlignOf<QuadElement<T>>(),
				initialElementsCapacity,
				allocator);
		}

		public void ClearAndBulkInsert(NativeArray<QuadElement<T>> incomingElements)
		{
			InitialiseBulkInsert(incomingElements);

			// Prepare morton codes
			NativeArray<int> mortonCodes = PrepairMortonCodes(incomingElements);

			// Prepare the tree leaf nodes
			RecursivePrepareLeaves(1, 1);

			// Add elements to leaf nodes
			AddElementsToLeafNodes(mortonCodes, incomingElements);

			mortonCodes.Dispose();
		}

		internal void InitialiseBulkInsert(NativeArray<QuadElement<T>> incomingElements)
		{
			// Always have to clear before bulk insert as otherwise the lookup and node allocations need to account
			// for existing data.
			Clear();

#if ENABLE_UNITY_COLLECTIONS_CHECKS && !NATIVE_QUAD_TREE_ECS_USAGE
			AtomicSafetyHandle.CheckWriteAndBumpSecondaryVersion(safetyHandle);
#endif

			// Resize if needed
			if(elements->Capacity < elementsCount + incomingElements.Length)
			{
				elements->Resize<QuadElement<T>>(math.max(incomingElements.Length, elements->Capacity*2));
			}
			
			// this is needed so that future resize/move operations correctly copy the expected amount of data to the new location
			elements->Length = elements->Capacity;
		}

		[BurstCompatible]
		internal void AddElementsToLeafNodes(NativeArray<int> mortonCodes, NativeArray<QuadElement<T>> incomingElements)
		{
			for (int i = 0; i < incomingElements.Length; i++)
			{
				int atIndex = 0;
				for (int depth = 0; depth <= m_maxDepth; depth++)
				{
					QuadNode node = UnsafeUtility.ReadArrayElement<QuadNode>(nodes->Ptr, atIndex);
					if(node.isLeaf)
					{	
						// We found a leaf, add this element to it and move to the next element
						UnsafeUtility.WriteArrayElement(elements->Ptr, node.firstChildIndex + node.count, incomingElements[i]);
						node.count++;
						UnsafeUtility.WriteArrayElement(nodes->Ptr, atIndex, node);

#if UNITY_ASSERTIONS
						AssertLeafCapacityExceed(atIndex, node.count);
#endif
						break;
					}
					// No leaf found, we keep going deeper until we find one
					atIndex = IncrementIndex(depth, mortonCodes, i, atIndex);
				}
				
#if UNITY_ASSERTIONS
				AssertLeafCapacityExceed(i, atIndex);
#endif
			}
		}

		[BurstDiscard, StringFormatMethod("message")]
		private void AssertLeafCapacityExceed(int elementIndex, int nextNodeIndex)
		{
			Assert.IsFalse(elementIndex > nodes->Length, $"Failed to add element[{elementIndex}] to quad tree data!");
		}

		[BurstDiscard, StringFormatMethod("message")]
		private void AssertCorrectAdd(int index, int nodeCount)
		{
			if(nodeCount > maxLeafElements)
			{
				// the allocation done in the constructor limits the amount of elements in each leaf
				Assert.IsTrue(false, "Quad Tree node " + index + " is filled with elements, consider allocating a larger leaf node size than " + maxLeafElements);
			}
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		private NativeArray<int> PrepairMortonCodes(NativeArray<QuadElement<T>> incomingElements)
		{
			NativeArray<int> mortonCodes = new NativeArray<int>(incomingElements.Length, Allocator.Temp);
			PrepairMortonCodesInitial(incomingElements, mortonCodes);
			PrepairMortonCodesIndex(mortonCodes);
			return mortonCodes;
		}

		/// <summary>
		/// Calculates the morton code for each item inside <paramref name="incomingElements"/>
		/// </summary>
		/// <param name="incomingElements">all items being stored</param>
		/// <param name="mortonCodes">temporary index for storing morton code indexes</param>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		internal void PrepairMortonCodesInitial(NativeArray<QuadElement<T>> incomingElements, NativeArray<int> mortonCodes)
		{
			float2 depthExtentsScaling = LookupTables.DepthLookup[m_maxDepth] / bounds.Extents;
			for (int i = 0; i < incomingElements.Length; i++)
			{
				float2 incPos = incomingElements[i].Pos;
				incPos -= bounds.Center; // Offset by center
				incPos.y = -incPos.y; // World -> array
				float2 pos = (incPos + bounds.Extents) * .5f; // Make positive
				// Now scale into available space that belongs to the depth
				pos *= depthExtentsScaling;
				// And interleave the bits for the morton code
				mortonCodes[i] = (LookupTables.MortonLookup[(int) pos.x] | (LookupTables.MortonLookup[(int) pos.y] << 1));
			}
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		internal void PrepairMortonCodesIndex(NativeArray<int> mortonCodes)
		{
			// Index total child element count per node (total, so parent's counts include those of child nodes)
			for (int i = 0; i < mortonCodes.Length; i++)
			{
				int atIndex = 0;
				for (int depth = 0; depth <= m_maxDepth; depth++)
				{
					// Increment the node on this depth that this element is contained in
					(*(int*) ((IntPtr) lookup->Ptr + (atIndex * sizeof (int))))++;
					atIndex = IncrementIndex(depth, mortonCodes, i, atIndex);
				}
			}
		}

		[BurstCompatible]
		internal int IncrementIndex(int depth, NativeArray<int> mortonCodes, int i, int atIndex)
		{
			int atDepth = math.max(0, m_maxDepth - depth);
			// Shift to the right and only get the first two bits
			int shiftedMortonCode = (mortonCodes[i] >> ((atDepth - 1) * 2)) & 0b11;
			// so the index becomes that... (0,1,2,3)
			atIndex += LookupTables.DepthSizeLookup[atDepth] * shiftedMortonCode;
			atIndex++; // offset for self
			return atIndex;
		}

		internal void RecursivePrepareLeaves(int prevOffset, int depth)
		{
#if UNITY_ASSERTIONS
			if(depth == 1)
			{
				int[] data = new int[lookup->Length];
				for (int i = 0; i < data.Length; i++)
				{
					data[i] = UnsafeUtility.ReadArrayElement<int>(lookup->Ptr, i);
				}
			}
#endif
			
			for (int l = 0; l < 4; l++)
			{
				int at = prevOffset + l * LookupTables.DepthSizeLookup[m_maxDepth - depth+1];
				int elementCount = UnsafeUtility.ReadArrayElement<int>(lookup->Ptr, at);

				if(elementCount > maxLeafElements && depth < m_maxDepth)
				{
					// There's more elements than allowed on this node so keep going deeper
					RecursivePrepareLeaves(at+1, depth+1);
				}
				else if(elementCount != 0)
				{
					// We either hit max depth or there's less than the max elements on this node, make it a leaf
					QuadNode node = new QuadNode {firstChildIndex = elementsCount, count = 0, isLeaf = true };
					UnsafeUtility.WriteArrayElement(nodes->Ptr, at, node);
					elementsCount += elementCount;
				}
			}
		}

		public void RangeQuery(AABB2D bounds, NativeList<QuadElement<T>> results)
		{
#if ENABLE_UNITY_COLLECTIONS_CHECKS && !NATIVE_QUAD_TREE_ECS_USAGE
			AtomicSafetyHandle.CheckReadAndThrow(safetyHandle);
#endif
			new QuadTreeRectRangeQuery<T>().Query(this, bounds, results);
		}

		public void RangeQuery(Circle2D bounds, NativeList<QuadElement<T>> results)
		{
#if ENABLE_UNITY_COLLECTIONS_CHECKS && !NATIVE_QUAD_TREE_ECS_USAGE
			AtomicSafetyHandle.CheckReadAndThrow(safetyHandle);
#endif
			new QuadTreeCircleRangeQuery<T>().Query(this, bounds, results);
		}

		public void Clear()
		{
#if ENABLE_UNITY_COLLECTIONS_CHECKS && !NATIVE_QUAD_TREE_ECS_USAGE
			AtomicSafetyHandle.CheckWriteAndBumpSecondaryVersion(safetyHandle);
#endif
			
			UnsafeUtility.MemClear(lookup->Ptr, lookup->Capacity * UnsafeUtility.SizeOf<int>());
			UnsafeUtility.MemClear(nodes->Ptr, nodes->Capacity * UnsafeUtility.SizeOf<QuadNode>());
			UnsafeUtility.MemClear(elements->Ptr, elements->Capacity * UnsafeUtility.SizeOf<QuadElement<T>>());
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
#if ENABLE_UNITY_COLLECTIONS_CHECKS && !NATIVE_QUAD_TREE_ECS_USAGE
			DisposeSentinel.Dispose(ref safetyHandle, ref disposeSentinel);
#endif
		}
	}
}
