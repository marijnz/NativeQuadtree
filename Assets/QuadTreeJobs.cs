using System.Diagnostics;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;

namespace NativeQuadTree
{
	/// <summary>
	/// Examples on jobs for the NativeQuadTree
	/// </summary>
	public static class QuadTreeJobs
	{
		/// <summary>
		/// Bulk insert many items into the tree
		/// </summary>
		[BurstCompile]
		public struct AddBulkJob<T> : IJob where T : unmanaged
		{
			[ReadOnly]
			public NativeArray<QuadElement<T>> Elements;

			public NativeReference<NativeQuadTree<T>> QuadTree;

			public void Execute()
			{
				NativeQuadTree<T> quadTree = QuadTree.Value;
				quadTree.ClearAndBulkInsert(Elements);
				QuadTree.Value = quadTree;

				ValidateData();
			}
			
			[BurstDiscard, Conditional("UNITY_ASSERTIONS")]
			private void ValidateData()
			{
				UnityEngine.Assertions.Assert.AreEqual(Elements.Length, QuadTree.Value.EntryCount, "Failed to populate entityData");
			}
		}

		/// <summary>
		/// Example on how to do a range query, it's better to write your own and do many queries in a batch
		/// </summary>
		[BurstCompile]
		public struct RangeQueryJob<T> : IJob where T : unmanaged
		{
			[ReadOnly]
			public AABB2D Bounds;

			[ReadOnly]
			public NativeQuadTree<T> QuadTree;

			public NativeList<QuadElement<T>> Results;

			public void Execute()
			{
				for (int i = 0; i < 1000; i++)
				{
					QuadTree.RangeQuery(Bounds, Results);
					Results.Clear();
				}
				QuadTree.RangeQuery(Bounds, Results);
			}
		}
	}
}