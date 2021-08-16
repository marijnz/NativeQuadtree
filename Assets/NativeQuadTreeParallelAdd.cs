using System;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;

namespace NativeQuadTree
{
    public unsafe partial struct NativeQuadTree<T> where T : unmanaged
    {
        public static JobHandle SetupBulkAddJobChain(NativeReference<NativeQuadTree<T>> tree, NativeArray<QuadElement<T>> quadElementArray, JobHandle dependency)
        {
            // this whole file essentially splits out and tries to parallelize NativeQuadTree.ClearAndBulkInsert(incomingElements)!
            
            BulkAddInitialiseJob init = new BulkAddInitialiseJob
            {
                Elements = quadElementArray,
                QuadTree = tree
            };

            PrepairMortonCodesJob mortonCreate = new PrepairMortonCodesJob(quadElementArray, tree);
            
            IndexMortonCodesJob mortonIndex = new IndexMortonCodesJob
            {
                MortonCodes = mortonCreate.MortonCodes,
                QuadTree = tree
            };
            
            RecursivePrepareLeavesJob prepairLeaves = new RecursivePrepareLeavesJob
            {
                QuadTree = tree
            };
            
            AddElementsToLeafNodesJob leafJob = new AddElementsToLeafNodesJob
            {
                Elements = quadElementArray,
                MortonCodes = mortonCreate.MortonCodes,
                QuadTree = tree
            };

            const int threadBucketSize = 100;
            
            JobHandle initHandle = init.Schedule(dependency);
            JobHandle morton1Handle = mortonCreate.ScheduleBatch(quadElementArray.Length, threadBucketSize, initHandle);
            //JobHandle morton1Handle = mortonCreate.Schedule(initHandle);
            JobHandle morton2Handle = mortonIndex.ScheduleBatch(quadElementArray.Length, threadBucketSize, morton1Handle);
            //JobHandle morton2Handle = mortonIndex.Schedule(morton1Handle);
            JobHandle prepairLeavesHandle = prepairLeaves.Schedule(morton2Handle);
            JobHandle populateHandle = leafJob.Schedule(prepairLeavesHandle);
            mortonCreate.MortonCodes.Dispose(populateHandle);

            return populateHandle;
        }

        [BurstCompile]
        private struct BulkAddInitialiseJob : IJob
        {
            [ReadOnly]
            public NativeArray<QuadElement<T>> Elements;
            public NativeReference<NativeQuadTree<T>> QuadTree;
            
            public void Execute()
            {
                NativeQuadTree<T> quadTree = QuadTree.Value;
                quadTree.InitialiseBulkInsert(Elements);
                QuadTree.Value = quadTree;
            }
        }

        [BurstCompile]
        private struct PrepairMortonCodesJob : IJobParallelForBatch
        {
            [ReadOnly]
            public NativeArray<QuadElement<T>> Elements;
            [ReadOnly]
            public NativeReference<NativeQuadTree<T>> QuadTree;
            [WriteOnly]
            public NativeArray<int> MortonCodes;

            public PrepairMortonCodesJob(NativeArray<QuadElement<T>> elements, NativeReference<NativeQuadTree<T>> quadTree) : this()
            {
                Elements = elements;
                QuadTree = quadTree;
                MortonCodes = new NativeArray<int>(Elements.Length, Allocator.TempJob);
            }

            public void Execute()
            {
                NativeQuadTree<T> quadTree = QuadTree.Value;
                quadTree.PrepairMortonCodesInitial(Elements, MortonCodes);
            }

            public void Execute(int startIndex, int count)
            {
                NativeQuadTree<T> quadTree = QuadTree.Value;
                float2 depthExtentsScaling = LookupTables.DepthLookup[quadTree.maxDepth] / quadTree.bounds.Extents;
                for (int i = startIndex; i < startIndex + count; i++)
                {
                    float2 incPos = Elements[i].pos;
                    incPos -= quadTree.bounds.Center; // Offset by center
                    incPos.y = -incPos.y; // World -> array
                    float2 pos = (incPos + quadTree.bounds.Extents) * .5f; // Make positive
                    // Now scale into available space that belongs to the depth
                    pos *= depthExtentsScaling;
                    // And interleave the bits for the morton code
                    MortonCodes[i] = (LookupTables.MortonLookup[(int) pos.x] | (LookupTables.MortonLookup[(int) pos.y] << 1));
                }
            }
        }

        [BurstCompile]
        private struct IndexMortonCodesJob : IJobParallelForBatch
        {
            public NativeArray<int> MortonCodes;
            [ReadOnly]
            public NativeReference<NativeQuadTree<T>> QuadTree;
            
            public void Execute()
            {
                NativeQuadTree<T> quadTree = QuadTree.Value;
                quadTree.PrepairMortonCodesIndex(MortonCodes);
            }

            public void Execute(int startIndex, int count)
            {
                NativeQuadTree<T> quadTree = QuadTree.Value;
                // Index total child element count per node (total, so parent's counts include those of child nodes)
                for (int i = startIndex; i < startIndex + count; i++)
                {
                    int atIndex = 0;
                    for (int depth = 0; depth <= quadTree.maxDepth; depth++)
                    {
                        // Increment the node on this depth that this element is contained in
                        (*(int*) ((IntPtr) quadTree.lookup->Ptr + atIndex * sizeof (int)))++;
                        atIndex = quadTree.IncrementIndex(depth, MortonCodes, i, atIndex);
                    }
                }
            }
        }

        [BurstCompile]
        private struct RecursivePrepareLeavesJob : IJob
        {
            public NativeReference<NativeQuadTree<T>> QuadTree;
            
            public void Execute()
            {
                NativeQuadTree<T> quadTree = QuadTree.Value;
                quadTree.RecursivePrepareLeaves(1, 1);
                QuadTree.Value = quadTree;
            }
        }

        [BurstCompile]
        private struct AddElementsToLeafNodesJob : IJob
        {
            [ReadOnly]
            public NativeArray<QuadElement<T>> Elements;
            [ReadOnly]
            public NativeArray<int> MortonCodes;
            public NativeReference<NativeQuadTree<T>> QuadTree;
            
            public void Execute()
            {
                NativeQuadTree<T> quadTree = QuadTree.Value;
                quadTree.AddElementsToLeafNodes(MortonCodes, Elements);
                QuadTree.Value = quadTree;
            }

            public void Execute(int startIndex, int count)
            {
                NativeQuadTree<T> quadTree = QuadTree.Value;
                for (int i = startIndex; i < count; i++)
                {
                    int atIndex = 0;
                    for (int depth = 0; depth <= quadTree.maxDepth; depth++)
                    {
                        QuadNode node = UnsafeUtility.ReadArrayElement<QuadNode>(quadTree.nodes->Ptr, atIndex);
                        if(node.isLeaf)
                        {
                            // We found a leaf, add this element to it and move to the next element
                            UnsafeUtility.WriteArrayElement(quadTree.elements->Ptr, node.firstChildIndex + node.count, Elements[i]);
                            node.count++;
                            UnsafeUtility.WriteArrayElement(quadTree.nodes->Ptr, atIndex, node);
                            break;
                        }

                        // No leaf found, we keep going deeper until we find one
                        atIndex = quadTree.IncrementIndex(depth, MortonCodes, i, atIndex);
                    }
                }
            }
        }
    }
}