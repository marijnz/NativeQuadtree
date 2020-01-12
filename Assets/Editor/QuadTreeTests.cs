using System.Diagnostics;
using NUnit.Framework;
using QuadTree;
using Unity.Burst;
using Unity.Jobs;
using Debug = UnityEngine.Debug;

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

    [Test]
    public void InsertTriggerDivide()
    {
        var s = Stopwatch.StartNew();
        var job = new AddJob()
        {
            QuadTree = new NativeQuadTree<int>(new AABB2D(0, 50))
        };
        job.Run();
        s.Stop();
        Debug.Log(s.Elapsed.TotalMilliseconds);

        QuadTreeDrawer.Draw(job.QuadTree);
        job.QuadTree.Dispose();
    }

    [BurstCompile]
    struct AddJob : IJob
    {
        public NativeQuadTree<int> QuadTree;

        public void Execute()
        {
            var rnd = new Unity.Mathematics.Random(1);

            for (int i = 0; i < 2000; i++)
            {
                QuadTree.InsertElement(new QuadElement<int>
                {
                    pos = rnd.NextFloat2(-40, 40),
                    element = 53
                });
            }
        }
    }
}
