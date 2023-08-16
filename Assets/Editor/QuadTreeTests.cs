using System.Diagnostics;
using NUnit.Framework;
using NativeQuadTree;
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

        var elements = new NativeArray<QuadElement<int>>(values.Length, Allocator.TempJob);

        for (int i = 0; i < values.Length; i++)
        {
            elements[i] = new QuadElement<int>
            {
                pos = values[i],
                element = i
            };
        }

        using var quadtree = new NativeQuadTree<int>(Bounds, Allocator.TempJob);
        var job = new QuadTreeJobs.AddBulkJob<int>
        {
            Elements = elements,
            QuadTree = quadtree,
        };

        var s = Stopwatch.StartNew();

        job.Run();

        s.Stop();
        Debug.Log(s.Elapsed.TotalMilliseconds);

        QuadTreeDrawer.Draw(quadtree);
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
                pos = values[i],
                element = i
            };
        }

        var quadTree = new NativeQuadTree<int>(Bounds);
        quadTree.ClearAndBulkInsert(elements);

        var queryJob = new QuadTreeJobs.RangeQueryJob<int>
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

        var positions = new NativeArray<float2>(values.Length, Allocator.TempJob);
        var quadTree = new NativeQuadTree<int>(Bounds);

        positions.CopyFrom(values);


        NativeArray<QuadElement<int>> elements = new NativeArray<QuadElement<int>>(positions.Length, Allocator.Temp);

        for (int i = 0; i < positions.Length; i++)
        {
            elements[i] = new QuadElement<int>
            {
                pos = positions[i],
                element = i
            };
        }

        var s = Stopwatch.StartNew();

        quadTree.ClearAndBulkInsert(elements);

        s.Stop();
        Debug.Log(s.Elapsed.TotalMilliseconds);

        QuadTreeDrawer.Draw(quadTree);
        quadTree.Dispose();
        positions.Dispose();
    }
}
