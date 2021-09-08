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
        data.Value = new NativeQuadTree<int>(Bounds);
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

        var quadTree = new NativeQuadTree<int>(Bounds);
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
        var quadTree = new NativeQuadTree<int>(Bounds);

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
    public void SimpleNativeQuery([NUnit.Framework.Range(19, 21)] int count)
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
        NativeQuadTree<int> quadTree = new NativeQuadTree<int>(bounds, Allocator.TempJob, maxDepth: 4, maxLeafElements: 20);
        quadTree.ClearAndBulkInsert(elements);

        NativeReference<NativeQuadTree<int>> treeRef = new NativeReference<NativeQuadTree<int>>(quadTree, Allocator.TempJob);
        ValidationHelpers.ValidateNativeTreeContent(treeRef, elements);
        ValidationHelpers.BruteForceLocationHitCheck(treeRef, elements);

        treeRef.Dispose();
        quadTree.Dispose();
        elements.Dispose();
    }
}
