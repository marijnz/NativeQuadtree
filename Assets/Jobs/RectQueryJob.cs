using NativeQuadTree.Jobs.Internal;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;

namespace NativeQuadTree
{
    /// <summary>
    /// Example on how to do a range query, it's better to write your own and do many queries in a batch
    /// </summary>
    [BurstCompile]
    public struct RectQueryJob<T> : IJob where T : unmanaged
    {
        [ReadOnly]
        public AABB2D Bounds;
        [ReadOnly]
        public NativeReference<NativeQuadTree<T>> QuadTree;
        public NativeList<QuadElement<T>> Results;

        private QuadTreeRectRangeQuery<T> query;
        
        public RectQueryJob(AABB2D bounds, NativeReference<NativeQuadTree<T>> quadTree, NativeList<QuadElement<T>> results)
        {
            Bounds = bounds;
            QuadTree = quadTree;
            Results = results;

            query = new QuadTreeRectRangeQuery<T>();
        }

        public void Execute()
        {
            query.Query(QuadTree.Value, Bounds, Results);
        }
    }
}