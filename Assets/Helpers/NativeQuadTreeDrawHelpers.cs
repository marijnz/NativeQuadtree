using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;
using UnityEngine;

namespace NativeQuadTree
{
	/// <summary>
	/// Editor drawing of the NativeQuadTree
	/// </summary>
	public unsafe struct NativeQuadTreeDrawHelpers<T> where T : unmanaged
	{
		public static void Draw(NativeQuadTree<T> tree, NativeList<QuadElement<T>> results, AABB2D range, Color[][] texture)
		{
			float widthMult = texture.Length / tree.bounds.Extents.x * 2 / 2 / 2;
			float heightMult = texture[0].Length / tree.bounds.Extents.y * 2 / 2 / 2;

			float widthAdd = tree.bounds.Center.x + tree.bounds.Extents.x;
			float heightAdd = tree.bounds.Center.y + tree.bounds.Extents.y;

			for (int i = 0; i < tree.nodes->Capacity; i++)
			{
				QuadNode node = UnsafeUtility.ReadArrayElement<QuadNode>(tree.nodes->Ptr, i);

				if(node.count > 0)
				{
					for (int k = 0; k < node.count; k++)
					{
						QuadElement<T> element =
							UnsafeUtility.ReadArrayElement<QuadElement<T>>(tree.elements->Ptr, node.firstChildIndex + k);

						texture[(int) ((element.Pos.x + widthAdd) * widthMult)]
							[(int) ((element.Pos.y + heightAdd) * heightMult)] = Color.red;
					}
				}
			}

			foreach (QuadElement<T> element in results)
			{
				texture[(int) ((element.Pos.x + widthAdd) * widthMult)]
					[(int) ((element.Pos.y + heightAdd) * heightMult)] = Color.green;
			}

			DrawBounds(texture, range, tree);
		}

		private static void DrawBounds(Color[][] texture, AABB2D bounds, NativeQuadTree<T> tree)
		{
			float widthMult = texture.Length / tree.bounds.Extents.x * 2 / 2 / 2;
			float heightMult = texture[0].Length / tree.bounds.Extents.y * 2 / 2 / 2;

			float widthAdd = tree.bounds.Center.x + tree.bounds.Extents.x;
			float heightAdd = tree.bounds.Center.y + tree.bounds.Extents.y;

			float2 top = new float2(bounds.Center.x, bounds.Center.y - bounds.Extents.y);
			float2 left = new float2(bounds.Center.x - bounds.Extents.x, bounds.Center.y);

			for (int leftToRight = 0; leftToRight < bounds.Extents.x * 2; leftToRight++)
			{
				float poxX = left.x + leftToRight;
				texture[(int) ((poxX + widthAdd) * widthMult)][(int) ((bounds.Center.y + heightAdd + bounds.Extents.y) * heightMult)] = Color.blue;
				texture[(int) ((poxX + widthAdd) * widthMult)][(int) ((bounds.Center.y + heightAdd - bounds.Extents.y) * heightMult)] = Color.blue;
			}

			for (int topToBottom = 0; topToBottom < bounds.Extents.y * 2; topToBottom++)
			{
				float posY = top.y + topToBottom;
				texture[(int) ((bounds.Center.x + widthAdd + bounds.Extents.x) * widthMult)][(int) ((posY + heightAdd) * heightMult)] = Color.blue;
				texture[(int) ((bounds.Center.x + widthAdd - bounds.Extents.x) * widthMult)][(int) ((posY + heightAdd) * heightMult)] = Color.blue;
			}
		}
	}
}