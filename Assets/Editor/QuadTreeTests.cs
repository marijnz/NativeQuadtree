using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Text;
using NUnit.Framework;
using QuadTree;
using Unity.Burst;
using Unity.Jobs;
using Unity.Mathematics;
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

    [Test]
    public void InsertTriggerDivide()
    {
        var s = Stopwatch.StartNew();


        List<(float2, ulong, string)> values = new List<(float2, ulong, string)>();

        List<ulong> mortonValues = new List<ulong>();

        for (int x = 0; x < 5; x++)
        {
            for (int i = 0; i < 5; i++)
            {
                var val = new float2(x, i);
                var mortonVal = xy2d_morton((ulong) (val.x), (ulong) (val.y));

                var someString = Convert.ToString((int) mortonVal, 2).PadLeft(32, '0');

                // string someString = Decimal.GetBits(mortonVal).Select(t => t.ToString()).Aggregate((t, b) => t + "" + b);


                values.Add((val, mortonVal, someString));
            }
        }

        var results = values.OrderBy(t => t.Item2).ToList();

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


    /*
    long mortonIndex(float2 pos) {
        // Pun the x and y coordinates as integers: Just re-interpret the bits.
        //
        var ix = (uint) pos.x;
        var iy = (uint) pos.y;

        // Since we're assuming 2s complement arithmetic (99.99% of hardware today),
        // we'll need to convert these raw integer-punned floats into
        // their corresponding integer "indices".

        // Smear their sign bits into these for twiddling below.
        //
        var ixs = (int) (ix) >> 31;
        var iys = (int) (iy) >> 31;

        // This is a combination of a fast absolute value and a bias.
        //
        // We need to adjust the values so -FLT_MAX is close to 0.
        //
        ix = (((ix & 0x7FFFFFFFL) ^ ixs) - ixs) + 0x7FFFFFFFL;
        iy = (((iy & 0x7FFFFFFFL) ^ iys) - iys) + 0x7FFFFFFFL;

        // Now we have -FLT_MAX close to 0, and FLT_MAX close to UINT_MAX,
        // with everything else in-between.
        //
        // To make this easy, we'll work with x and y as 64-bit integers.
        //
        long long xx = ix;
        long long yy = iy;

        // Dilate and combine as usual...

        xx = (xx | (xx << 16)) & 0x0000ffff0000ffffLL;
        yy = (yy | (yy << 16)) & 0x0000ffff0000ffffLL;

        xx = (xx | (xx <<  8)) & 0x00ff00ff00ff00ffLL;
        yy = (yy | (yy <<  8)) & 0x00ff00ff00ff00ffLL;

        xx = (xx | (xx <<  4)) & 0x0f0f0f0f0f0f0f0fLL;
        yy = (yy | (yy <<  4)) & 0x0f0f0f0f0f0f0f0fLL;

        xx = (xx | (xx <<  2)) & 0x3333333333333333LL;
        yy = (yy | (yy <<  2)) & 0x3333333333333333LL;

        xx = (xx | (xx <<  1)) & 0x5555555555555555LL;
        yy = (yy | (yy <<  1)) & 0x5555555555555555LL;

        return xx | (yy << 1);
    }
    */


    uint MortonCode2(uint x, uint y) {
        return SeparateBy1(x) | (SeparateBy1(y) << 1);
    }

    uint SeparateBy1(uint x) {
        x &= 0x0000ffff;                  // x = ---- ---- ---- ---- fedc ba98 7654 3210
        x = (x ^ (x <<  8)) & 0x00ff00ff; // x = ---- ---- fedc ba98 ---- ---- 7654 3210
        x = (x ^ (x <<  4)) & 0x0f0f0f0f; // x = ---- fedc ---- ba98 ---- 7654 ---- 3210
        x = (x ^ (x <<  2)) & 0x33333333; // x = --fe --dc --ba --98 --76 --54 --32 --10
        x = (x ^ (x <<  1)) & 0x55555555; // x = -f-e -d-c -b-a -9-8 -7-6 -5-4 -3-2 -1-0
        return x;
    }

    ulong xy2d_morton(ulong x, ulong y)
    {
        x = (x | (x << 16)) & 0x0000FFFF0000FFFF;
        x = (x | (x << 8)) & 0x00FF00FF00FF00FF;
        x = (x | (x << 4)) & 0x0F0F0F0F0F0F0F0F;
        x = (x | (x << 2)) & 0x3333333333333333;
        x = (x | (x << 1)) & 0x5555555555555555;

        y = (y | (y << 16)) & 0x0000FFFF0000FFFF;
        y = (y | (y << 8)) & 0x00FF00FF00FF00FF;
        y = (y | (y << 4)) & 0x0F0F0F0F0F0F0F0F;
        y = (y | (y << 2)) & 0x3333333333333333;
        y = (y | (y << 1)) & 0x5555555555555555;

        return x | (y << 1);
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
