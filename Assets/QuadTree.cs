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
			1,
			1+2*2,
			1+2*2+4*4,
			1+2*2+4*4+8*8,
			1+2*2+4*4+8*8+16+16,
			1+2*2+4*4+8*8+16+16+32*32,
			1+2*2+4*4+8*8+16+16+32*32+64*64,
			1+2*2+4*4+8*8+16+16+32*32+64*64+128*128,
		};

		static readonly int[] depthLookup =
		{
			0,
			2,
			4,
			8,
			16,
			32,
			64,
			128,
			256,
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

		struct RangeQueryInstruction
		{
			public NativeList<QuadElement<T>> Results;
			public AABB2D Bounds;
		}

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

		/// <summary>
		/// Create a new QuadTree.
		/// - Ensure the bounds are not way bigger than needed, otherwise the buckets are very off. Probably best to calculate bounds
		/// - The higher the depth, the larger the overhead, it especially goes up at a depth of 7/8
		/// </summary>
		public NativeQuadTree(AABB2D bounds, int maxDepth = 6, short maxLeafElements = 16,
			int initialElementsCapacity = 300000, int initialNodesCapacity = 300000) : this()
		{
			CollectionHelper.CheckIsUnmanaged<T>();

			this.bounds = bounds;
			this.maxDepth = maxDepth;
			this.maxLeafElements = maxLeafElements;
			elements = UnsafeList.Create(UnsafeUtility.SizeOf<QuadElement<T>>(), UnsafeUtility.AlignOf<QuadElement<T>>(), initialElementsCapacity, Allocator.Persistent);

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

		public void BulkInsert(NativeArray<QuadElement<T>> incomingElements)
		{
			var mortonCodes = new NativeArray<int>(incomingElements.Length, Allocator.Temp);

			// Remapping values to range of depth
			var depthRemapMult = (depthLookup[maxDepth] / (bounds.Extents.x ));
			for (var i = 0; i < incomingElements.Length; i++)
			{
				var incPos = incomingElements[i].pos;
				incPos.y = -incPos.y; // world -> array
				var pos = (int2) ((incPos + bounds.Extents) * .5f * depthRemapMult);
				mortonCodes[i] = mortonLookup[pos.x] | (mortonLookup[pos.y] << 1);
			}

			// Index total child element count per node (so including those of child nodes)
			for (var i = 0; i < mortonCodes.Length; i++)
			{
				var mortonCode = mortonCodes[i];

				for (int depth = maxDepth; depth >= 0; depth--)
				{
					int level = mortonCode >> ((maxDepth - depth) *2);

					// Offset by depth and add morton index
					var index = depthSizeLookup[depth] + level;

					// +1 to that node lookup
					(*(int*) ((IntPtr) lookup->Ptr + index * sizeof (int)))++;
				}
			}

			RecursiveAlloc(0, 0);

			// Add elements to nodes
			for (var i = 0; i < mortonCodes.Length; i++)
			{
				var mortonCode = mortonCodes[i];

				for (int depth = maxDepth; depth >= 0; depth--)
				{
					int level = mortonCode >> ((maxDepth - depth) *2);

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
			mortonCodes.Dispose();
		}

		void RecursiveAlloc(int atNode, int depth)
		{
			var totalOffset = depthSizeLookup[++depth];

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

		public void RangeQuery(AABB2D bounds, NativeList<QuadElement<T>> results)
		{
			var query = new RangeQueryInstruction
			{
				Bounds = bounds,
				Results = results
			};
			RecursiveRangeQuery(ref query, this.bounds, false, 0, 0);
		}

		void RecursiveRangeQuery(ref RangeQueryInstruction query, AABB2D fromBounds, bool parentContained, int atNode, int depth)
		{
			var totalOffset = depthSizeLookup[++depth];

			for (int l = 0; l < 4; l++)
			{
				var childBounds = GetChildBounds(fromBounds, l);

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
						var index = (void*) ((IntPtr) elements->Ptr + node.firstChildIndex * UnsafeUtility.SizeOf<QuadElement<T>>());
						query.Results.AddRange(index, node.count);
					}
					else
					{
						for (int k = 0; k < node.count; k++)
						{
							var element = UnsafeUtility.ReadArrayElement<QuadElement<T>>(elements->Ptr, node.firstChildIndex + k);
							if(query.Bounds.Contains(element.pos))
							{
								query.Results.Add(element);
							}
						}
					}
				}
			}
		}

		void RecursiveAssert(int atNode, int parentSize, int totalOffset, int depth)
		{
			var totalWidthThisDepth = parentSize * 2;
			int oneNodeSize = totalWidthThisDepth / 2;

			int offset = oneNodeSize * atNode;

			for (int l = 0; l < 4; l++)
			{
				var at = totalOffset + offset + l;

				var node = UnsafeUtility.ReadArrayElement<QuadNode>(nodesQuick->Ptr, at);

				if(node.count != node.elementsCapacity)
				{
					Debug.Assert(node.count == node.elementsCapacity);
				}
			}

			if(depth < maxDepth)
			{
				for (int l = 0; l < 4; l++)
				{
					RecursiveAssert(l, totalWidthThisDepth, totalOffset +
					                                        (totalWidthThisDepth*totalWidthThisDepth), depth+1);
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

		public static void Draw(NativeQuadTree<T> tree, NativeList<QuadElement<T>> results, AABB2D range,
			Color[][] texture)
		{
			var widthMult = texture.Length / tree.bounds.Extents.x * 2 / 2 / 2;
			var heightMult = texture[0].Length / tree.bounds.Extents.y * 2 / 2 / 2;

			var widthAdd = tree.bounds.Center.x + tree.bounds.Extents.x;
			var heightAdd = tree.bounds.Center.y + tree.bounds.Extents.y;

			for (int i = 0; i < tree.nodesQuick->Capacity; i++)
			{
				var node = UnsafeUtility.ReadArrayElement<QuadNode>(tree.nodesQuick->Ptr, i);

				if(node.count > 0)
				{
					for (int k = 0; k < node.count; k++)
					{
						var element =
							UnsafeUtility.ReadArrayElement<QuadElement<T>>(tree.elements->Ptr, node.firstChildIndex + k);

						texture[(int) ((element.pos.x + widthAdd) * widthMult)]
							[(int) ((element.pos.y + heightAdd) * heightMult)] = Color.red;
					}
				}
			}

			foreach (var element in results)
			{
				texture[(int) ((element.pos.x + widthAdd) * widthMult)]
					[(int) ((element.pos.y + heightAdd) * heightMult)] = Color.green;
			}

			DrawBounds(texture, range, tree);
		}

		static void DrawBounds(Color[][] texture, AABB2D bounds, NativeQuadTree<T> tree)
		{
			var widthMult = texture.Length / tree.bounds.Extents.x * 2 / 2 / 2;
			var heightMult = texture[0].Length / tree.bounds.Extents.y * 2 / 2 / 2;

			var widthAdd = tree.bounds.Center.x + tree.bounds.Extents.x;
			var heightAdd = tree.bounds.Center.y + tree.bounds.Extents.y;

			var top = new float2(bounds.Center.x, bounds.Center.y - bounds.Extents.y);
			var left = new float2(bounds.Center.x - bounds.Extents.x, bounds.Center.y);

			for (int leftToRight = 0; leftToRight < bounds.Extents.x * 2; leftToRight++)
			{
				var poxX = left.x + leftToRight;
				texture[(int) ((poxX + widthAdd) * widthMult)][(int) ((bounds.Center.y + heightAdd + bounds.Extents.y) * heightMult)] = Color.blue;
				texture[(int) ((poxX + widthAdd) * widthMult)][(int) ((bounds.Center.y + heightAdd - bounds.Extents.y) * heightMult)] = Color.blue;
			}

			for (int topToBottom = 0; topToBottom < bounds.Extents.y * 2; topToBottom++)
			{
				var posY = top.y + topToBottom;
				texture[(int) ((bounds.Center.x + widthAdd + bounds.Extents.x) * widthMult)][(int) ((posY + heightAdd) * heightMult)] = Color.blue;
				texture[(int) ((bounds.Center.x + widthAdd - bounds.Extents.x) * widthMult)][(int) ((posY + heightAdd) * heightMult)] = Color.blue;
			}
		}

		static void DrawDivider(Color[][] texture, AABB2D bounds, NativeQuadTree<T> tree)
		{
			var widthMult = texture.Length / tree.bounds.Extents.x * 2 / 2 / 2;
			var heightMult = texture[0].Length / tree.bounds.Extents.y * 2 / 2 / 2;

			var widthAdd = tree.bounds.Center.x + tree.bounds.Extents.x;
			var heightAdd = tree.bounds.Center.y + tree.bounds.Extents.y;

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
		}
	}
}
