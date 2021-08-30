using System;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;

namespace NativeQuadTree.Jobs.Internal
{
    public unsafe struct QuadTreeRectRangeQuery<T> where T : unmanaged
    {
        private NativeQuadTree<T> tree;

        [NativeDisableUnsafePtrRestriction]
        private UnsafeList* fastResults;
        private int count;

        private AABB2D bounds;

        public void Query(NativeQuadTree<T> tree, AABB2D bounds, NativeList<QuadElement<T>> results)
        {
            this.tree = tree;
            this.bounds = bounds;
            count = 0;

            // Get pointer to inner list data for faster writing
            fastResults = (UnsafeList*) NativeListUnsafeUtility.GetInternalListDataPtrUnchecked(ref results);

            RecursiveRangeQuery(tree.bounds, false, 1, 1);

            fastResults->Length = count;
        }

        public void RecursiveRangeQuery(AABB2D parentBounds, bool parentContained, int prevOffset, int depth)
        {
            if(count + 4 * tree.MaxLeafElements > fastResults->Capacity)
            {
                fastResults->Resize<QuadElement<T>>(math.max(fastResults->Capacity * 2, count + 4 * tree.MaxLeafElements));
            }

            var depthSize = LookupTables.DepthSizeLookup[tree.MaxDepth - depth + 1];
            for (int l = 0; l < 4; l++)
            {
                var childBounds = RangeQueryHelpers.GetChildBounds(parentBounds, l);

                var contained = parentContained;
                if(!contained)
                {
                    if(bounds.Contains(childBounds))
                    {
                        contained = true;
                    }
                    else if(!bounds.Intersects(childBounds))
                    {
                        continue;
                    }
                }


                var at = prevOffset + l * depthSize;
                var elementCount = UnsafeUtility.ReadArrayElement<int>(tree.lookup->Ptr, at);

                if(elementCount > tree.MaxLeafElements && depth < tree.MaxDepth)
                {
                    RecursiveRangeQuery(childBounds, contained, at + 1, depth + 1);
                }
                else if(elementCount != 0)
                {
                    var node = UnsafeUtility.ReadArrayElement<QuadNode>(tree.nodes->Ptr, at);

                    if(contained)
                    {
                        void* source = (void*) ((IntPtr) tree.elements->Ptr + node.firstChildIndex * UnsafeUtility.SizeOf<QuadElement<T>>());
                        void* destination = (void*) ((IntPtr) fastResults->Ptr + count * UnsafeUtility.SizeOf<QuadElement<T>>());
                        UnsafeUtility.MemCpy(destination, source, node.count * UnsafeUtility.SizeOf<QuadElement<T>>());
                        count += node.count;
                    }
                    else
                    {
                        for (int k = 0; k < node.count; k++)
                        {
                            var element = UnsafeUtility.ReadArrayElement<QuadElement<T>>(tree.elements->Ptr, node.firstChildIndex + k);
                            if(bounds.Contains(element.Pos))
                            {
                                UnsafeUtility.WriteArrayElement(fastResults->Ptr, count++, element);
                            }
                        }
                    }
                }
            }
        }
    }
}
