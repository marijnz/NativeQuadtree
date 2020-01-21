//#define NATIVE_QUAD_TREE_DEBUG


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
		static readonly ushort[] mortonLookup = {
		    0x0000, 0x0001, 0x0004, 0x0005, 0x0010, 0x0011, 0x0014, 0x0015,
		    0x0040, 0x0041, 0x0044, 0x0045, 0x0050, 0x0051, 0x0054, 0x0055,
		    0x0100, 0x0101, 0x0104, 0x0105, 0x0110, 0x0111, 0x0114, 0x0115,
		    0x0140, 0x0141, 0x0144, 0x0145, 0x0150, 0x0151, 0x0154, 0x0155,
		    0x0400, 0x0401, 0x0404, 0x0405, 0x0410, 0x0411, 0x0414, 0x0415,
		    0x0440, 0x0441, 0x0444, 0x0445, 0x0450, 0x0451, 0x0454, 0x0455,
		    0x0500, 0x0501, 0x0504, 0x0505, 0x0510, 0x0511, 0x0514, 0x0515,
		    0x0540, 0x0541, 0x0544, 0x0545, 0x0550, 0x0551, 0x0554, 0x0555,
		    0x1000, 0x1001, 0x1004, 0x1005, 0x1010, 0x1011, 0x1014, 0x1015,
		    0x1040, 0x1041, 0x1044, 0x1045, 0x1050, 0x1051, 0x1054, 0x1055,
		    0x1100, 0x1101, 0x1104, 0x1105, 0x1110, 0x1111, 0x1114, 0x1115,
		    0x1140, 0x1141, 0x1144, 0x1145, 0x1150, 0x1151, 0x1154, 0x1155,
		    0x1400, 0x1401, 0x1404, 0x1405, 0x1410, 0x1411, 0x1414, 0x1415,
		    0x1440, 0x1441, 0x1444, 0x1445, 0x1450, 0x1451, 0x1454, 0x1455,
		    0x1500, 0x1501, 0x1504, 0x1505, 0x1510, 0x1511, 0x1514, 0x1515,
		    0x1540, 0x1541, 0x1544, 0x1545, 0x1550, 0x1551, 0x1554, 0x1555,
		    0x4000, 0x4001, 0x4004, 0x4005, 0x4010, 0x4011, 0x4014, 0x4015,
		    0x4040, 0x4041, 0x4044, 0x4045, 0x4050, 0x4051, 0x4054, 0x4055,
		    0x4100, 0x4101, 0x4104, 0x4105, 0x4110, 0x4111, 0x4114, 0x4115,
		    0x4140, 0x4141, 0x4144, 0x4145, 0x4150, 0x4151, 0x4154, 0x4155,
		    0x4400, 0x4401, 0x4404, 0x4405, 0x4410, 0x4411, 0x4414, 0x4415,
		    0x4440, 0x4441, 0x4444, 0x4445, 0x4450, 0x4451, 0x4454, 0x4455,
		    0x4500, 0x4501, 0x4504, 0x4505, 0x4510, 0x4511, 0x4514, 0x4515,
		    0x4540, 0x4541, 0x4544, 0x4545, 0x4550, 0x4551, 0x4554, 0x4555,
		    0x5000, 0x5001, 0x5004, 0x5005, 0x5010, 0x5011, 0x5014, 0x5015,
		    0x5040, 0x5041, 0x5044, 0x5045, 0x5050, 0x5051, 0x5054, 0x5055,
		    0x5100, 0x5101, 0x5104, 0x5105, 0x5110, 0x5111, 0x5114, 0x5115,
		    0x5140, 0x5141, 0x5144, 0x5145, 0x5150, 0x5151, 0x5154, 0x5155,
		    0x5400, 0x5401, 0x5404, 0x5405, 0x5410, 0x5411, 0x5414, 0x5415,
		    0x5440, 0x5441, 0x5444, 0x5445, 0x5450, 0x5451, 0x5454, 0x5455,
		    0x5500, 0x5501, 0x5504, 0x5505, 0x5510, 0x5511, 0x5514, 0x5515,
		    0x5540, 0x5541, 0x5544, 0x5545, 0x5550, 0x5551, 0x5554, 0x5555
		};

		static readonly int[] depthSizeLookup =
		{
			0,
			2*2,
			2*2+4*4,
			2*2+4*4+8*8,
			2*2+4*4+8*8+16+16,
			2*2+4*4+8*8+16+16+32*32,
			2*2+4*4+8*8+16+16+32*32+64*64,
		};

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

		[NativeDisableUnsafePtrRestriction]
		UnsafeList* lookup;

		int elementsCount; // TODO clean up wasted space - space can be reused if leaf becomes parent node

		int maxDepth; //TODO ensure maxDepth shifts and depth checks are correct
		short maxLeafElements;

		AABB2D bounds; // This should be uniform

		[NativeDisableUnsafePtrRestriction]
		UnsafeList* nodesQuick;

		public NativeQuadTree(AABB2D bounds, int maxDepth = 6, short maxLeafElements = 8,
			int initialElementsCapacity = 300000, int initialNodesCapacity = 300000) : this()
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

			if(maxDepth > 6)
			{
				throw new InvalidOperationException();
			}

			const int totalSize = 2*2+4*4+8*8+16+16+32*32+64*64;

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

		public void BulkInsert(NativeArray<QuadElement<T>> incomingElements)
		{
			//TODO Remap values to 0-64 range

			// Index total child element count per node (so including those of child nodes)
			for (var i = 0; i < incomingElements.Length; i++)
			{
				var mortonCode = GetMortonCode(incomingElements[i]);

				for (int depth = maxDepth; depth >= 0; depth--)
				{
					int level = mortonCode >> (depth*2);

					// Offset by depth and add morton index
					var index = depthSizeLookup[depth] + level;

					// +1 to that node lookup
					(*(int*) ((IntPtr) lookup->Ptr + index * sizeof (int)))++;
				}

			}

			// Allocate nodes as needed
			RecursiveAlloc(0, 2, 0, 0);

			// Add elements to nodes
			for (var i = 0; i < incomingElements.Length; i++)
			{
				var mortonCode = GetMortonCode(incomingElements[i]);

				for (int depth = maxDepth; depth >= 0; depth--)
				{
					int level = mortonCode >> (depth*2);

					// Offset by depth and add morton index
					var index = depthSizeLookup[depth] + level;
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
		}

		void RecursiveAlloc(int atNode, int parentSize, int totalOffset, int depth)
		{
			var totalSizeOfThisDepth = parentSize * parentSize;
			int oneNodeSize = totalSizeOfThisDepth / 4;

			int offset = oneNodeSize * atNode;

			for (int l = 0; l < 4; l++)
			{
				var at = totalOffset + offset + l;

				var elementCount = UnsafeUtility.ReadArrayElement<int>(lookup->Ptr, at);

				if(elementCount > maxLeafElements && depth <= maxDepth)
				{
					totalOffset += (totalSizeOfThisDepth * totalSizeOfThisDepth);
					RecursiveAlloc(l, totalSizeOfThisDepth, totalOffset, ++depth);
				}
				else
				{
					AllocElementsForQuadNode(at, elementCount);
				}
			}
		}

		void AllocElementsForQuadNode(int nodeIndex, int count)
		{
			var node = new QuadNode{ firstChildIndex = elementsCount, count = 0, elementsCapacity = (short) count};
			UnsafeUtility.WriteArrayElement(nodesQuick->Ptr, nodeIndex, node);
			elementsCount += count;
		}

		public void InsertElement(QuadElement<T> element)
		{
			if(!bounds.Contains(element.pos))
			{
				return;
			}

			InsertElement(bounds, 0, element, 0);
		}

		int GetMortonCode(QuadElement<T> element)
		{
			var pos = (int2) element.pos;
			return mortonLookup[pos.x] | (mortonLookup[pos.y] << 1);
		}

		void InsertElement(AABB2D nodeBounds, int nodeIndex, QuadElement<T> element, int depth) {
			var node = UnsafeUtility.ReadArrayElement<QuadNode>(nodes->Ptr, nodeIndex);
			/*
#if NATIVE_QUAD_TREE_DEBUG
			Debug.Assert(nodeBounds.Contains(element.pos));
			if(!nodeBounds.Contains(element.pos))
			{
				//Debug.Log(nodeBounds + " " + element.pos + " " + ((element.element).ToString()));
				return;
			}

			if(depth > 150)
			{
				throw new Exception();
			}
#endif*/

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
			UnsafeList.Destroy(lookup);
			lookup = null;
		}

		static AABB2D GetBottomRight(AABB2D nodeBounds)
		{
			var half = nodeBounds.Extents.x * .5f;
			return new AABB2D(new float2(nodeBounds.Center.x + half, nodeBounds.Center.y - half), half);
		}

		static AABB2D GetTopRight(AABB2D nodeBounds)
		{
			var half = nodeBounds.Extents.x * .5f;
			return new AABB2D(new float2(nodeBounds.Center.x + half, nodeBounds.Center.y + half), half);
		}

		static AABB2D GetTopLeft(AABB2D nodeBounds)
		{
			var half = nodeBounds.Extents.x * .5f;
			return new AABB2D(new float2(nodeBounds.Center.x - half, nodeBounds.Center.y + half), nodeBounds.Extents / 2);
		}

		static AABB2D GetBottomLeft(AABB2D nodeBounds)
		{
			var half = nodeBounds.Extents.x * .5f;
			return new AABB2D(new float2(nodeBounds.Center.x - half, nodeBounds.Center.y - half), half);
		}

		public static void Draw(NativeQuadTree<T> tree, Color[][] texture)
		{
			DrawOld(tree, texture);

			//TODO draw bulk
		}

		static void DrawOld(NativeQuadTree<T> tree, Color[][] texture)
		{
			var widthMult = texture.Length / tree.bounds.Extents.x * 2 / 2 / 2;
			var heightMult = texture[0].Length / tree.bounds.Extents.y * 2 / 2 / 2;

			var widthAdd = tree.bounds.Center.x + tree.bounds.Extents.x;
			var heightAdd = tree.bounds.Center.y + tree.bounds.Extents.y;

			Draw(tree.bounds, 0);

			void Draw(AABB2D bounds, int nodeIndex)
			{
				var node = UnsafeUtility.ReadArrayElement<QuadNode>(tree.nodes->Ptr, nodeIndex);

				if (node.elementsCapacity != -1)
				{
					for (int i = 0; i < node.count; i++)
					{
						var element =
							UnsafeUtility.ReadArrayElement<QuadElement<T>>(tree.elements->Ptr, node.firstChildIndex + i);

						texture[(int) ((element.pos.x + widthAdd) * widthMult)]
							[(int) ((element.pos.y + heightAdd) * heightMult)] = Color.red;
					}
				}
				else
				{
					var top = new float2(bounds.Center.x, bounds.Center.y - bounds.Extents.y);
					var left = new float2(bounds.Center.x - bounds.Extents.x, bounds.Center.y);

					for (int leftToRight = 0; leftToRight < bounds.Extents.x * 2; leftToRight++)
					{
						var poxX = left.x + leftToRight;
						texture[(int) ((poxX + widthAdd) * widthMult)]
							[(int) ((bounds.Center.y + heightAdd) * heightMult)] = Color.blue;
					}

					for (int topToBottom = 0; topToBottom < bounds.Extents.y * 2; topToBottom++)
					{
						var posY = top.y + topToBottom;
						texture[(int) ((bounds.Center.x + widthAdd) * widthMult)]
							[(int) ((posY + heightAdd) * heightMult)] = Color.blue;
					}

					Draw(GetTopLeft(bounds), node.firstChildIndex);
					Draw(GetBottomLeft(bounds), node.firstChildIndex + 1);
					Draw(GetTopRight(bounds), node.firstChildIndex + 2);
					Draw(GetBottomRight(bounds), node.firstChildIndex + 3);
				}
			}
		}
	}
}
