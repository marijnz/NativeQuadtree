using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using UnityEngine.Assertions;
using Debug = UnityEngine.Debug;

namespace NativeQuadTree.Helpers
{
    public static class ValidationHelpers
    {
        /// <summary>
        /// Check that all expected counters match the expected entry count from the source data that was added
        /// </summary>
        /// <param name="tree">tree to check</param>
        /// <param name="entries">Expected entries</param>
        [Conditional("UNITY_ASSERTIONS"), BurstDiscard]
        public static void ValidateNativeTreeContent<T>(NativeReference<NativeQuadTree<T>> tree, NativeArray<QuadElement<T>> entries) where T : unmanaged
        {
            Assert.AreEqual(entries.Length, tree.Value.EntryCount, "Tree length mismatch (Count)!");
            
            unsafe
            {
                UnsafeList* values = tree.Value.elements;
                int treeLength = values->Length; 
                Assert.IsTrue(entries.Length <= treeLength, "Tree length mismatch (Raw Data)!");
            }
            
            // validate that the node counts match the expected entity count
            
            int nodeCount = 0;
            int leafCount = 0;
            List<QuadNode> rawNodes = ExtractNodeValues(tree.Value);
            for (int i = 0; i < rawNodes.Count; i++)
            {
                if(rawNodes[i].isLeaf)
                {
                    nodeCount += rawNodes[i].count;
                    leafCount++;
                }
            }
            Assert.AreEqual(entries.Length, nodeCount, "Tree length mismatch (Nodes)!");
        }

        internal static List<QuadNode> ExtractNodeValues<T>(NativeQuadTree<T> tree) where T : unmanaged
        {
            unsafe
            {
                // validate that the node counts match the expected entity count
                List<QuadNode> rawNodes = new List<QuadNode>(tree.nodes->Length);
                UnsafeList* nodes = tree.nodes;
                for (int i = 0; i < nodes->Length; i++)
                {
                    // this converts to the actual nodes we can inspect to make future actions easier
                    QuadNode node = UnsafeUtility.ReadArrayElement<QuadNode>(nodes->Ptr, i);
                    rawNodes.Add(node);
                }

                return rawNodes;
            }
        }
        
        /// <summary>
        /// Check that all expected counters match the expected entry count from the source data that was added
        /// </summary>
        /// <param name="treeRef">tree to check</param>
        /// <param name="entries">Expected entries</param>
        [Conditional("UNITY_ASSERTIONS"), BurstDiscard]
        public static void BruteForceLocationHitCheck<T>(NativeReference<NativeQuadTree<T>> treeRef, NativeArray<QuadElement<T>> entries) where T : unmanaged
        {
            BruteForceLocationHitCheckRect(treeRef, entries);
            BruteForceLocationHitCheckCircle(treeRef, entries);
        }
        
        /// <summary>
        /// Check that all expected counters match the expected entry count from the source data that was added
        /// </summary>
        /// <param name="treeRef">tree to check</param>
        /// <param name="entries">Expected entries</param>
        [Conditional("UNITY_ASSERTIONS"), BurstDiscard]
        public static void BruteForceLocationHitCheckCircle<T>(NativeReference<NativeQuadTree<T>> treeRef, NativeArray<QuadElement<T>> entries) where T : unmanaged
        {
            NativeQuadTree<T> tree = treeRef.Value;

            for (int i = 0; i < entries.Length; i++)
            {
                QuadElement<T> entry = entries[i];
                Circle2D exactPosition = new Circle2D(entry.Pos, 0.0001f);

                NativeList<QuadElement<T>> resultArray = new NativeList<QuadElement<T>>(2, Allocator.TempJob);
                tree.RangeQuery(exactPosition, resultArray);

                if(resultArray.Length == 0)
                {
                    // use explicit if statement so that you can put a breakpoint here and diagnose the issue
                    Assert.IsTrue(false, "no results for entry query at " + i);
                }
                
                bool found = false;
                foreach (QuadElement<T> result in resultArray)
                {
                    if(Equals(result.Element, entry.Element))
                    {
                        // expected result was actually found in the data
                        found = true;
                        break;
                    }
                }

                resultArray.Dispose();
                Assert.IsTrue(found, "Missing expected quadTree entry " + i);
            }
        }
        
        /// <summary>
        /// Check that all expected counters match the expected entry count from the source data that was added
        /// </summary>
        /// <param name="treeRef">tree to check</param>
        /// <param name="entries">Expected entries</param>
        [Conditional("UNITY_ASSERTIONS"), BurstDiscard]
        public static void BruteForceLocationHitCheckRect<T>(NativeReference<NativeQuadTree<T>> treeRef, NativeArray<QuadElement<T>> entries) where T : unmanaged
        {
            NativeQuadTree<T> tree = treeRef.Value;

            for (int i = 0; i < entries.Length; i++)
            {
                QuadElement<T> entry = entries[i];
                AABB2D exactPosition = new AABB2D(entry.Pos, 0.0001f);

                NativeList<QuadElement<T>> resultArray = new NativeList<QuadElement<T>>(2, Allocator.TempJob);
                tree.RangeQuery(exactPosition, resultArray);

                if(resultArray.Length == 0)
                {
                    // use explicit if statement so that you can put a breakpoint here and diagnose the issue
                    Assert.IsTrue(false, "no results for entry query at " + i);
                }
                
                bool found = false;
                foreach (QuadElement<T> result in resultArray)
                {
                    if(Equals(result.Element, entry.Element))
                    {
                        // expected result was actually found in the data
                        found = true;
                        break;
                    }
                }

                resultArray.Dispose();
                Assert.IsTrue(found, "Missing expected quadTree entry " + i);
            }
        }

        [BurstDiscard, Conditional("UNITY_EDITOR")]
        public static void PrintDepthUtilisation<T>(NativeReference<NativeQuadTree<T>> treeRef) where T : unmanaged
        {
            PrintDepthUtilisation(treeRef.Value);
        }
        
        [BurstDiscard, Conditional("UNITY_EDITOR")]
        public static void PrintDepthUtilisation<T>(NativeQuadTree<T> tree) where T : unmanaged
        {
            StringBuilder builder = new StringBuilder("Depth Utilisation - Percentage of used nodes in a depth level with the total count of " +
                                                      "Elements stored inside the the depth level").AppendLine();
            List<QuadNode> rawNodes = ExtractNodeValues(tree);

            int previousDepthSize = LookupTables.DepthSizeLookup[1];
            for (int i = 2; i <= tree.MaxDepth + 1; i++)
            {
                int totalUnits = 0;
                int depthNodes = LookupTables.DepthSizeLookup[i];
                int usedNodes = 0;

                for (int j = previousDepthSize; j < depthNodes; j++)
                {
                    QuadNode quadNode = rawNodes[j];

                    totalUnits += quadNode.count;
                    if(quadNode.count > 0)
                    { 
                        usedNodes++;
                    }
                }

                builder.AppendLine($"Depth {i - 1}: {(usedNodes / (float)(depthNodes - previousDepthSize) * 100f):N3}% Utilised - {usedNodes} Nodes ({totalUnits} Elements)");
                previousDepthSize = depthNodes;
            }
            
            Debug.Log(builder.ToString());
        }
    }
}