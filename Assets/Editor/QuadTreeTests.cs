using System.Diagnostics;
using NUnit.Framework;
using marijnz.NativeQuadTree;
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

        var elements = new NativeArray<QuadElement>(values.Length, Allocator.TempJob);

        for (int i = 0; i < values.Length; i++)
        {
            elements[i] = new QuadElement
            {
                pos = values[i],
                id = i
            };
        }

        var job = new QuadTreeJobs.AddBulkJob
        {
            Elements = elements,
            QuadTree = new NativeQuadTree(Bounds)
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

        NativeArray<QuadElement> elements = new NativeArray<QuadElement>(values.Length, Allocator.TempJob);

        for (int i = 0; i < values.Length; i++)
        {
            elements[i] = new QuadElement
            {
                pos = values[i],
                id = i
            };
        }

        var quadTree = new NativeQuadTree(Bounds);
        quadTree.BulkInsert(elements);

        var queryJob = new QuadTreeJobs.RangeQueryJob
        {
            QuadTree = quadTree,
            Bounds = new AABB2D(100, 140),
            Results = new NativeList<QuadElement>(100000, Allocator.TempJob)
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
        var quadTree = new NativeQuadTree(Bounds);

        positions.CopyFrom(values);


        NativeArray<QuadElement> elements = new NativeArray<QuadElement>(positions.Length, Allocator.Temp);

        for (int i = 0; i < positions.Length; i++)
        {
            elements[i] = new QuadElement
            {
                pos = positions[i],
                id = i
            };
        }

        var s = Stopwatch.StartNew();

        quadTree.BulkInsert(elements);

        s.Stop();
        Debug.Log(s.Elapsed.TotalMilliseconds);

        QuadTreeDrawer.Draw(quadTree);
        quadTree.Dispose();
        positions.Dispose();
    }
}
