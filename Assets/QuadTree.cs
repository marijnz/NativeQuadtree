using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;
using UnityEngine;

namespace QuadTree
{
	// Represents an element node in the quadtree.
	public struct QuadElement<T>
	{
		public float2 pos;
		public T element;
	}

	public unsafe struct NativeQuadTree<T> : IDisposable where T : unmanaged
	{
		struct QuadNode
		{
			// Points to the first child if this node is a branch or the first
			// element if this node is a leaf.
			public int firstChildIndex;

			// Stores the number of elements in the leaf
			public short count;

			// How many is allocated? Use to free up memory. -1 if not a leaf.
			public short elementsCapacity;
		}

		[NativeDisableUnsafePtrRestriction]
		UnsafeList* nodes;
		int nodesCount;
		[NativeDisableUnsafePtrRestriction]
		UnsafeList* elements;
		int elementsCount; // TODO clean up wasted space - space can be reused if leaf becomes parent node

		int maxDepth;
		short maxLeafElements;

		AABB2D bounds;

		public NativeQuadTree(AABB2D bounds, int maxDepth = 8, short maxLeafElements = 8,
			int initialElementsCapacity = 50000, int initialNodesCapacity = 50000)
		{
			CollectionHelper.CheckIsUnmanaged<T>();

			this.bounds = bounds;
			this.maxDepth = maxDepth;
			this.maxLeafElements = maxLeafElements;
			elements = UnsafeList.Create(UnsafeUtility.SizeOf<QuadElement<T>>(), UnsafeUtility.AlignOf<QuadElement<T>>(), initialElementsCapacity, Allocator.Persistent);
			nodes = UnsafeList.Create(UnsafeUtility.SizeOf<QuadNode>(), UnsafeUtility.AlignOf<QuadNode>(), initialNodesCapacity, Allocator.Persistent);
			nodesCount = 0;
			elementsCount = 0;

			AllocQuadNode(); // Root node
		}

		public void InsertElement(QuadElement<T> element)
		{
			InsertElement(bounds, 0, element, 0);
		}

		void InsertElement(AABB2D nodeBounds, int nodeIndex, QuadElement<T> element, int depth) {
			var node = UnsafeUtility.ReadArrayElement<QuadNode>(nodes->Ptr, nodeIndex);

			Debug.Assert(nodeBounds.Contains(element.pos));

			if(depth > 150)
			{
				throw new Exception();
			}

			if(node.elementsCapacity != -1)
			{
				// We are in a leaf
				if(node.count < node.elementsCapacity)
				{
					// There's space for another element, so add it
					UnsafeUtility.WriteArrayElement(elements->Ptr, node.firstChildIndex + node.count, element);
					node.count++;
					UnsafeUtility.WriteArrayElement(nodes->Ptr, nodeIndex, node);
				}
				else if(depth >= maxDepth)
				{
					// We hit max capacity of elements but are at max depth, so expand
					ExpandQuadNodeElements(ref node);

					// Now there's space, so add it
					UnsafeUtility.WriteArrayElement(elements->Ptr, node.firstChildIndex + node.count, element);
					node.count++;
					UnsafeUtility.WriteArrayElement(nodes->Ptr, nodeIndex, node);
				}
				else
				{
					// No space for another element and we didn't hit max depth yet, so subdivide

					// Allocate child nodes
					node.elementsCapacity = -1;
					// TODO: free up capacity memory
					var elementsFirstChild = node.firstChildIndex;

					EnsureNodesCapacity(nodesCount+4);
					EnsureElementsCapacity(elementsCount + (maxLeafElements * 4));

					node.firstChildIndex = AllocQuadNode(); // Top left
					AllocQuadNode(); // Top right
					AllocQuadNode(); // Bottom left
					AllocQuadNode(); // Bottom right

					UnsafeUtility.WriteArrayElement(nodes->Ptr, nodeIndex, node);

					// Trickle elements down to newly created child nodes
					for (int i = 0; i < maxLeafElements; i++)
					{
						int index = elementsFirstChild + i;
						var leafElement = UnsafeUtility.ReadArrayElement<QuadElement<T>>(elements->Ptr,
							index);
						InsertElement(nodeBounds, nodeIndex, leafElement, depth+1);
					}

					// And add the element..
					InsertElement(nodeBounds, nodeIndex, element, depth+1);
				}
			}
			else
			{
				// We are not in a leaf
				var pos = element.pos;
				if(pos.x < nodeBounds.Center.x)
				{
					if(pos.y > nodeBounds.Center.y)
						InsertElement(GetTopLeft(nodeBounds), node.firstChildIndex, element, depth+1);
					else
						InsertElement(GetBottomLeft(nodeBounds), node.firstChildIndex+1, element, depth+1);
				}
				else
				{
					if(pos.y > nodeBounds.Center.y)
						InsertElement(GetTopRight(nodeBounds), node.firstChildIndex+2, element, depth+1);
					else
						InsertElement(GetBottomRight(nodeBounds), node.firstChildIndex+3, element, depth+1);
				}
			}
		}

		int AllocQuadNode()
		{
			var node = new QuadNode{ firstChildIndex = elementsCount, elementsCapacity = maxLeafElements};
			UnsafeUtility.WriteArrayElement(nodes->Ptr, nodesCount, node);
			nodesCount++;
			elementsCount += maxLeafElements;
			return nodesCount-1;
		}

		void ExpandQuadNodeElements(ref QuadNode quadNode)
		{
			var newCapacity = (short) (quadNode.elementsCapacity * 2);
			EnsureElementsCapacity(elementsCount + newCapacity);

			UnsafeUtility.MemCpy(
				(void*) ((IntPtr) elements->Ptr + elementsCount * UnsafeUtility.SizeOf<T>()),
				(void*) ((IntPtr) elements->Ptr + quadNode.firstChildIndex * UnsafeUtility.SizeOf<T>()),
				quadNode.elementsCapacity * UnsafeUtility.SizeOf<T>());

			//TODO cleanup old memory

			quadNode.elementsCapacity = newCapacity;
			elementsCount += newCapacity;
		}

		void EnsureNodesCapacity(int capacity)
		{
			if(nodes->Capacity < capacity)
			{
				nodes->SetCapacity<T>(math.max(nodes->Capacity*2, capacity));
			}
		}

		void EnsureElementsCapacity(int capacity)
		{
			if(elements->Capacity < capacity)
			{
				elements->SetCapacity<T>(math.max(elements->Capacity*2, capacity));
			}
		}

		public void Dispose()
		{
			UnsafeList.Destroy(nodes);
			nodes = null;
			UnsafeList.Destroy(elements);
			elements = null;
		}


		static AABB2D GetBottomRight(AABB2D nodeBounds)
		{
			return new AABB2D(new float2(
					nodeBounds.Center.x + nodeBounds.Extents.x / 2,
					nodeBounds.Center.y - nodeBounds.Extents.y / 2),
				nodeBounds.Extents / 2);
		}

		static AABB2D GetTopRight(AABB2D nodeBounds)
		{
			return new AABB2D(new float2(
					nodeBounds.Center.x + nodeBounds.Extents.x / 2,
					nodeBounds.Center.y + nodeBounds.Extents.y / 2),
				nodeBounds.Extents / 2);
		}

		static AABB2D GetTopLeft(AABB2D nodeBounds)
		{
			return new AABB2D(new float2(
					nodeBounds.Center.x - nodeBounds.Extents.x / 2,
					nodeBounds.Center.y + nodeBounds.Extents.y / 2),
				nodeBounds.Extents / 2);
		}

		static AABB2D GetBottomLeft(AABB2D nodeBounds)
		{
			return new AABB2D(new float2(
					nodeBounds.Center.x - nodeBounds.Extents.x / 2,
					nodeBounds.Center.y - nodeBounds.Extents.y / 2),
				nodeBounds.Extents / 2);
		}

		public static void Draw(NativeQuadTree<T> tree, Color[][] texture)
		{
			var widthMult = texture.Length / tree.bounds.Extents.x * 2 / 2 / 2;
			var heightMult = texture[0].Length / tree.bounds.Extents.y * 2 / 2 / 2;

			var widthAdd = tree.bounds.Center.x + tree.bounds.Extents.x;
			var heightAdd = tree.bounds.Center.y + tree.bounds.Extents.y;

			Draw(tree.bounds, 0);

			void Draw(AABB2D bounds, int nodeIndex)
			{
				var node = UnsafeUtility.ReadArrayElement<QuadNode>(tree.nodes->Ptr, nodeIndex);

				if(node.elementsCapacity != -1)
				{
					for (int i = 0; i < node.count; i++)
					{
						var element = UnsafeUtility.ReadArrayElement<QuadElement<T>>(tree.elements->Ptr, node.firstChildIndex + i);

						texture[(int) ((element.pos.x + widthAdd) * widthMult)]
							[(int)((element.pos.y + heightAdd) * heightMult)] = Color.red;
					}
				} else
				{
					var top = new float2(bounds.Center.x, bounds.Center.y - bounds.Extents.y);
					var left = new float2(bounds.Center.x - bounds.Extents.x, bounds.Center.y);

					for (int leftToRight = 0; leftToRight < bounds.Extents.x * 2; leftToRight++)
					{
						var poxX = left.x + leftToRight;
						texture[(int) ((poxX + widthAdd) * widthMult)]
							[(int)((bounds.Center.y + heightAdd) * heightMult)] = Color.blue;
					}

					for (int topToBottom = 0; topToBottom < bounds.Extents.y * 2; topToBottom++)
					{
						var posY = top.y + topToBottom;
						texture[(int) ((bounds.Center.x + widthAdd) * widthMult)]
							[(int)((posY + heightAdd) * heightMult)] = Color.blue;
					}

					Draw(GetTopLeft(bounds), node.firstChildIndex);
					Draw(GetBottomLeft(bounds), node.firstChildIndex+1);
					Draw(GetTopRight(bounds), node.firstChildIndex+2);
					Draw(GetBottomRight(bounds), node.firstChildIndex+3);
				}
			}
		}
	}
}
