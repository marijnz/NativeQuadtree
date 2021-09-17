using System.Diagnostics;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;

namespace NativeQuadTree.Jobs
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
}