using Unity.Mathematics;

namespace NativeQuadTree.Helpers
{
    public static class ArraySizeHelpers
    {
        /// <summary>
        /// Calculates an estimate for the amount of results from an entityQuery assuming perfect uniform entry distribution inside <paramref name="tree"/> 
        /// </summary>
        /// <param name="tree">NativeQuadTree that will be queried against</param>
        /// <param name="queryShape">shape that will be used as range filter</param>
        /// <returns>estimated array size</returns>
        public static int EstimateResultSize<T>(this NativeQuadTree<T> tree, Circle2D queryShape) where T : unmanaged
        {
            if(tree.EntryCount == 0) return 0;

            float boundsArea = tree.bounds.Size.x * tree.bounds.Size.y;
            float shapeArea = (math.PI * queryShape.Radious) * (math.PI * queryShape.Radious);

            float itemsPerUnit = boundsArea / tree.EntryCount;
            return (int) (shapeArea * itemsPerUnit);
        }
        
        /// <summary>
        /// Calculates an estimate for the amount of results from an entityQuery assuming perfect uniform entry distribution inside <paramref name="tree"/> 
        /// </summary>
        /// <param name="tree">NativeQuadTree that will be queried against</param>
        /// <param name="queryShape">shape that will be used as range filter</param>
        /// <returns>estimated array size</returns>
        public static int EstimateResultSize<T>(this NativeQuadTree<T> tree, AABB2D queryShape) where T : unmanaged
        {
            if(tree.EntryCount == 0) return 0;
            
            float boundsArea = tree.bounds.Size.x * tree.bounds.Size.y;
            float shapeArea = queryShape.Size.x * queryShape.Size.y;

            float itemsPerUnit = boundsArea / tree.EntryCount;
            return (int) (shapeArea * itemsPerUnit);
        }
    }
}