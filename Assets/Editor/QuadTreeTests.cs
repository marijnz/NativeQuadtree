using System.Diagnostics;
using NUnit.Framework;
using QuadTree;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using Debug = UnityEngine.Debug;
using Random = UnityEngine.Random;

public class QuadTreeTests
{
    [Test]
    public void Insert()
    {
        var quadTree = new NativeQuadTree<int>(new AABB2D(0, 50));
        quadTree.InsertElement(new QuadElement<int>
        {
            pos = 5,
            element = 53
        });
    }

    AABB2D Bounds => new AABB2D(0, 1000);

    float2[] GetValues()
    {
        Random.InitState(0);
        var values = new float2[5000];

        for (int x = 0; x < values.Length; x++)
        {
            var val = new int2((int) Random.Range(-900, 900), (int) Random.Range(-900, 900));
            values[x] = val;
        }

        return values;
    }

    float2 RandomPoint(float2 around, float scale, float density)
    {
        var angle = Random.Range(0, 1f) *2*Mathf.PI;

        var x = Random.Range(0f,1f);
        if (x == 0)
        {
            x = 0.0000001f;
        }

        var distance = scale * (Mathf.Pow(x, -1.0f/density) - 1);
        return new float2(around.x + distance * math.sin(angle),
            around.y + distance * math.cos(angle));

    }

    [Test]
    public void InsertTriggerDivide()
    {
        var values = GetValues();

        var job = new AddJob()
        {
            Positions = new NativeArray<float2>(values.Length, Allocator.TempJob),
            QuadTree = new NativeQuadTree<int>(Bounds)
        };

        job.Positions.CopyFrom(values);

        var s = Stopwatch.StartNew();


        job.Run();

        s.Stop();
        Debug.Log(s.Elapsed.TotalMilliseconds);

        QuadTreeDrawer.Draw(job.QuadTree);
        job.QuadTree.Dispose();
        job.Positions.Dispose();
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
                pos = values[i],
                element = i
            };
        }

        var quadTree = new NativeQuadTree<int>(Bounds);
        quadTree.BulkInsert(elements);

        var queryJob = new QuadTreeJobs.RangeQueryJob<int>
        {
            QuadTree = quadTree,
            Bounds = new AABB2D(100, 140),
            Results = new NativeList<QuadElement<int>>(100000, Allocator.TempJob)
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
    public void InsertTriggerDivideNonBurst()
    {
        var quadTree = new NativeQuadTree<int>(Bounds);

        var values = GetValues();

        var s = Stopwatch.StartNew();
        for (int i = 0; i < values.Length; i++)
        {
            quadTree.InsertElement(new QuadElement<int>
            {
                pos = values[i],
                element = i
            });
        }
        s.Stop();
        Debug.Log(s.Elapsed.TotalMilliseconds);

        QuadTreeDrawer.Draw(quadTree);
        quadTree.Dispose();

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

        quadTree.BulkInsert(elements);

        s.Stop();
        Debug.Log(s.Elapsed.TotalMilliseconds);

        QuadTreeDrawer.Draw(quadTree);
        quadTree.Dispose();
        positions.Dispose();

    }

    [BurstCompile]
    struct AddJob : IJob
    {
        [Unity.Collections.ReadOnly]
        public NativeArray<float2> Positions;

        public NativeQuadTree<int> QuadTree;

        public void Execute()
        {
              for (int i = 0; i < Positions.Length; i++)
              {
                  QuadTree.InsertElement(new QuadElement<int>
                  {
                      pos = Positions[i],
                      element = i
                  });
              }
        }
    }
}
