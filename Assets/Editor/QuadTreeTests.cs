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

    AABB2D[] GetValues()
    {
        Random.InitState(0);
        var values = new AABB2D[20000];

        for (int x = 0; x < values.Length; x++)
        {
            var val = new int2((int) Random.Range(-900, 900), (int) Random.Range(-900, 900));
            values[x] = new AABB2D(new float2(val.x, val.y), new float2(5, 5));
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
                bounds = values[i],
                element = i
            };
        }

        var job = new QuadTreeJobs.AddBulkJob<int>
        {
            Elements = elements,
            QuadTree = new NativeQuadTree<int>(Bounds)
        };

        var s = Stopwatch.StartNew();

        job.Run();

        s.Stop();
        Debug.Log(s.Elapsed.TotalMilliseconds);

        QuadTreeDrawer.Draw(job.QuadTree);
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
                bounds = values[i],
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
    public void RayQueryAfterBulk()
    {
        var values = GetValues();

        NativeArray<QuadElement<int>> elements = new NativeArray<QuadElement<int>>(values.Length, Allocator.TempJob);

        for (int i = 0; i < values.Length; i++)
        {
            elements[i] = new QuadElement<int>
            {
                bounds = values[i],
                element = i
            };
        }

        var quadTree = new NativeQuadTree<int>(Bounds);
        quadTree.ClearAndBulkInsert(elements);

        var queryJob = new QuadTreeJobs.RayQueryJob<int>
        {
            QuadTree = quadTree,
            Ray = new NativeQuadTree<int>.Ray { from = new float2(-300, -200), to = new float2(800, 300)},
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

        var positions = new NativeArray<AABB2D>(values.Length, Allocator.TempJob);
        var quadTree = new NativeQuadTree<int>(Bounds);

        positions.CopyFrom(values);


        NativeArray<QuadElement<int>> elements = new NativeArray<QuadElement<int>>(positions.Length, Allocator.Temp);

        for (int i = 0; i < positions.Length; i++)
        {
            elements[i] = new QuadElement<int>
            {
                bounds = positions[i],
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
