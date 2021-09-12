using System.Diagnostics;
using NUnit.Framework;
using NativeQuadTree;
using NativeQuadTree.Helpers;
using NativeQuadTree.Jobs;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using Debug = UnityEngine.Debug;
using Random = UnityEngine.Random;

public class QuadTreeTests
{
    AABB2D Bounds => new AABB2D(0, 1000);

    float2[] GetValues()
    {
        Random.InitState(0);
        var values = new float2[20000];

        for (int x = 0; x < values.Length; x++)
        {
            var val = new int2((int) Random.Range(-900, 900), (int) Random.Range(-900, 900));
            values[x] = val;
        }

        return values;
    }

    [Test]
    public void InsertTriggerDivideBulk()
    {
        var values = GetValues();

        var elements = new NativeArray<QuadElement<int>>(values.Length, Allocator.Persistent);

        for (int i = 0; i < values.Length; i++)
        {
            elements[i] = new QuadElement<int>
            {
                Pos = values[i],
                Element = i
            };
        }

        NativeReference<NativeQuadTree<int>> data = new NativeReference<NativeQuadTree<int>>(Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
        data.Value = new NativeQuadTree<int>(Bounds, maxLeafElements: 1000);
        var job = new AddBulkJob<int>
        {
            Elements = elements,
            QuadTree = data
        };

        var s = Stopwatch.StartNew();

        job.Run();

        s.Stop();
        Debug.Log(s.Elapsed.TotalMilliseconds);

        QuadTreeDrawer.Draw(data.Value);
        job.QuadTree.Dispose();
        elements.Dispose();
    }

    [Test]
    public void RangeQueryAfterBulk()
    {
        var values = GetValues();

        NativeArray<QuadElement<int>> elements = new NativeArray<QuadElement<int>>(values.Length, Allocator.TempJob);

        for (int i = 0; i < values.Length; i++)
        {
            elements[i] = new QuadElement<int>
            {
                Pos = values[i],
                Element = i
            };
        }

        var quadTree = new NativeQuadTree<int>(Bounds, maxLeafElements: 1000);
        quadTree.ClearAndBulkInsert(elements);

        var queryJob = new RangeQueryJob<int>
        {
            QuadTree = quadTree,
            Bounds = new AABB2D(100, 140),
            Results = new NativeList<QuadElement<int>>(1000, Allocator.TempJob)
        };

        var s = Stopwatch.StartNew();
        queryJob.Run();
        s.Stop();
        Debug.Log(s.Elapsed.TotalMilliseconds + " result: " + queryJob.Results.Length);

        QuadTreeDrawer.DrawWithResults(queryJob);
        quadTree.Dispose();
        elements.Dispose();
        queryJob.Results.Dispose();
    }

    [Test]
    public void InsertTriggerDivideNonBurstBulk()
    {
        var values = GetValues();

        var positions = new NativeArray<float2>(values, Allocator.Persistent);
        var quadTree = new NativeQuadTree<int>(Bounds, maxLeafElements: 1000);

        NativeArray<QuadElement<int>> elements = new NativeArray<QuadElement<int>>(positions.Length, Allocator.Persistent);

        for (int i = 0; i < positions.Length; i++)
        {
            elements[i] = new QuadElement<int>
            {
                Pos = positions[i],
                Element = i
            };
        }

        var s = Stopwatch.StartNew();

        quadTree.ClearAndBulkInsert(elements);

        s.Stop();
        Debug.Log(s.Elapsed.TotalMilliseconds);

        QuadTreeDrawer.Draw(quadTree);
        
        quadTree.Dispose();
        positions.Dispose();
        elements.Dispose();
    }

    [Test]
    public void SimpleNativeQuery([NUnit.Framework.Range(0, 20)] int count)
    {
        NativeArray<QuadElement<int>> elements = new NativeArray<QuadElement<int>>(count, Allocator.TempJob);
        for (int i = 0; i < count; i++)
        {
            elements[i] = new QuadElement<int>
            {
                Pos = new float2(0.1f + (0.02f * i), 1f),
                Element = i
            };
        }

        const int size = 30;
        AABB2D bounds = new AABB2D(new float2(size, 4f), new float2(size, 4f));
        NativeQuadTree<int> quadTree = new NativeQuadTree<int>(bounds, Allocator.TempJob, maxDepth: 3, maxLeafElements: 20);
        quadTree.ClearAndBulkInsert(elements);

        NativeReference<NativeQuadTree<int>> treeRef = new NativeReference<NativeQuadTree<int>>(quadTree, Allocator.TempJob);
        ValidationHelpers.PrintDepthUtilisation(treeRef);
        ValidationHelpers.ValidateNativeTreeContent(treeRef, elements);
        ValidationHelpers.BruteForceLocationHitCheck(treeRef, elements);

        treeRef.Dispose();
        quadTree.Dispose();
        elements.Dispose();
    }

    [Test]
    public void MultiDepthTree()
    {
        NativeArray<QuadElement<int>> elements = new NativeArray<QuadElement<int>>(7, Allocator.TempJob);
        // nodes can all fit into a single quad
        elements[0] = new QuadElement<int>() { Pos = new float2(0.1f, 0f) };
        elements[1] = new QuadElement<int>() { Pos = new float2(0.2f, 0f) };
        elements[2] = new QuadElement<int>() { Pos = new float2(0.3f, 0f) };
        elements[3] = new QuadElement<int>() { Pos = new float2(0.4f, 0f) };
        elements[4] = new QuadElement<int>() { Pos = new float2(0.5f, 0f) };
        // these two nodes push the count above the max leaf amount and thus need to be stored inside a sub node
        elements[5] = new QuadElement<int>() { Pos = new float2(3f, 0f) };
        elements[6] = new QuadElement<int>() { Pos = new float2(3.5f, 0f) };

        const int size = 10;
        AABB2D bounds = new AABB2D(new float2(size, -1f), new float2(size, 4f));
        NativeQuadTree<int> quadTree = new NativeQuadTree<int>(bounds, Allocator.TempJob, maxDepth: 4, maxLeafElements: 5);
        quadTree.ClearAndBulkInsert(elements);

        NativeReference<NativeQuadTree<int>> treeRef = new NativeReference<NativeQuadTree<int>>(quadTree, Allocator.TempJob);
        ValidationHelpers.PrintDepthUtilisation(treeRef);
        ValidationHelpers.ValidateNativeTreeContent(treeRef, elements);
        ValidationHelpers.BruteForceLocationHitCheck(treeRef, elements);

        treeRef.Dispose();
        quadTree.Dispose();
        elements.Dispose();
    }

    [Test]
    public void MultiDepthTree2()
    {
        NativeArray<QuadElement<int>> elements = new NativeArray<QuadElement<int>>(7, Allocator.TempJob);
        // Depth 2 - data should be stored in depth 2 nodes
        elements[0] = new QuadElement<int>() { Pos = new float2(0.1f, 3f) }; // Morton code 10
        elements[1] = new QuadElement<int>() { Pos = new float2(0.2f, 3f) }; // Morton code 10
        elements[2] = new QuadElement<int>() { Pos = new float2(5.3f, 3f) }; // Morton code 11
        elements[3] = new QuadElement<int>() { Pos = new float2(5.4f, 3f) }; // Morton code 11
        // Depth 1 - data should be stored in depth 1 nodes
        elements[4] = new QuadElement<int>() { Pos = new float2(12.5f, 7f) }; // Morton code 12
        elements[5] = new QuadElement<int>() { Pos = new float2(12.0f, 7f) }; // Morton code 12
        // Depth 1 - data should be stored in depth 1 nodes
        elements[6] = new QuadElement<int>() { Pos = new float2(12.5f, 2f) }; // Morton code 14

        AABB2D bounds = new AABB2D(10f, 10f);
        NativeQuadTree<int> quadTree = new NativeQuadTree<int>(bounds, Allocator.TempJob, maxDepth: 2, maxLeafElements: 3);
        quadTree.ClearAndBulkInsert(elements);

        NativeReference<NativeQuadTree<int>> treeRef = new NativeReference<NativeQuadTree<int>>(quadTree, Allocator.TempJob);
        ValidationHelpers.PrintDepthUtilisation(treeRef);
        ValidationHelpers.ValidateNativeTreeContent(treeRef, elements);
        ValidationHelpers.BruteForceLocationHitCheck(treeRef, elements);

        treeRef.Dispose();
        quadTree.Dispose();
        elements.Dispose();
    }

    [Test]
    public void MultiDepthTree3()
    {
        NativeArray<QuadElement<int>> elements = new NativeArray<QuadElement<int>>(6, Allocator.TempJob);
        // Depth 2 - data should be stored in depth 2 nodes
        elements[0] = new QuadElement<int>() { Pos = new float2(0.1f, 3f) }; // Morton code 10
        elements[1] = new QuadElement<int>() { Pos = new float2(0.2f, 3f) }; // Morton code 10
        elements[2] = new QuadElement<int>() { Pos = new float2(5.3f, 3f) }; // Morton code 11
        elements[3] = new QuadElement<int>() { Pos = new float2(5.4f, 3f) }; // Morton code 11
        // Depth 1 - data should be stored in depth 1 nodes
        elements[4] = new QuadElement<int>() { Pos = new float2(12.5f, 7f) }; // Morton code 12
        // Depth 1 - data should be stored in depth 1 nodes
        elements[5] = new QuadElement<int>() { Pos = new float2(2.5f, 12f) }; // Morton code 4

        AABB2D bounds = new AABB2D(10f, 10f);
        NativeQuadTree<int> quadTree = new NativeQuadTree<int>(bounds, Allocator.TempJob, maxDepth: 2, maxLeafElements: 3);
        quadTree.ClearAndBulkInsert(elements);

        NativeReference<NativeQuadTree<int>> treeRef = new NativeReference<NativeQuadTree<int>>(quadTree, Allocator.TempJob);
        ValidationHelpers.PrintDepthUtilisation(treeRef);
        ValidationHelpers.ValidateNativeTreeContent(treeRef, elements);
        ValidationHelpers.BruteForceLocationHitCheck(treeRef, elements);
        
        treeRef.Dispose();
        quadTree.Dispose();
        elements.Dispose();
    }

    [Test]
    public void LargeUtilisation()
    {
        NativeArray<QuadElement<int>> elements = new NativeArray<QuadElement<int>>(600, Allocator.TempJob);
        for (int i = 0; i < elements.Length; i++)
        {
            elements[i] = new QuadElement<int>() { Pos = new float2(0.01f * i, 3f) };
        }

        AABB2D bounds = new AABB2D(5f, 10f);
        NativeQuadTree<int> quadTree = new NativeQuadTree<int>(bounds, Allocator.TempJob, maxDepth: 5, maxLeafElements: 600);
        quadTree.ClearAndBulkInsert(elements);

        NativeReference<NativeQuadTree<int>> treeRef = new NativeReference<NativeQuadTree<int>>(quadTree, Allocator.TempJob);
        ValidationHelpers.PrintDepthUtilisation(treeRef);
        ValidationHelpers.ValidateNativeTreeContent(treeRef, elements);
        ValidationHelpers.BruteForceLocationHitCheck(treeRef, elements);
        
        treeRef.Dispose();
        quadTree.Dispose();
        elements.Dispose();
    }

    [Test]
    public void LargeUtilisation2()
    {
        NativeArray<QuadElement<int>> elements = new NativeArray<QuadElement<int>>(1040, Allocator.TempJob);
        for (int i = 0; i < elements.Length; i++)
        {
            elements[i] = new QuadElement<int>() { Pos = new float2(0.01f * i, 3f) };
        }

        AABB2D bounds = new AABB2D(new float2(22f, 5f), new float2(44f, 6));
        NativeQuadTree<int> quadTree = new NativeQuadTree<int>(bounds, Allocator.TempJob, maxDepth: 5, maxLeafElements: 1000);
        quadTree.ClearAndBulkInsert(elements);

        NativeReference<NativeQuadTree<int>> treeRef = new NativeReference<NativeQuadTree<int>>(quadTree, Allocator.TempJob);
        ValidationHelpers.PrintDepthUtilisation(treeRef);
        ValidationHelpers.ValidateNativeTreeContent(treeRef, elements);
        ValidationHelpers.BruteForceLocationHitCheck(treeRef, elements);
        
        treeRef.Dispose();
        quadTree.Dispose();
        elements.Dispose();
    }

    [Test]
    public void LargeUtilisation3()
    {
        NativeArray<QuadElement<int>> elements = new NativeArray<QuadElement<int>>(400, Allocator.TempJob);
        for (int i = 0; i < elements.Length; i++)
        {
            elements[i] = new QuadElement<int>() { Pos = new float2(0.05f * i, 3f) };
        }

        AABB2D bounds = new AABB2D(new float2(22f, 5f), new float2(44f, 6));
        NativeQuadTree<int> quadTree = new NativeQuadTree<int>(bounds, Allocator.TempJob, maxDepth: 7, maxLeafElements: 300);
        quadTree.ClearAndBulkInsert(elements);

        NativeReference<NativeQuadTree<int>> treeRef = new NativeReference<NativeQuadTree<int>>(quadTree, Allocator.TempJob);
        ValidationHelpers.PrintDepthUtilisation(treeRef);
        ValidationHelpers.ValidateNativeTreeContent(treeRef, elements);
        ValidationHelpers.BruteForceLocationHitCheck(treeRef, elements);
        
        treeRef.Dispose();
        quadTree.Dispose();
        elements.Dispose();
    }
}
