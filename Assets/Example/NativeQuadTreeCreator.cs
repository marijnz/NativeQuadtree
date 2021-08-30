using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using NativeQuadTree;
using NativeQuadTree.Jobs;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEditor.Graphs;
using UnityEngine;
using Debug = UnityEngine.Debug;
using Random = UnityEngine.Random;

[RequireComponent(typeof(RectTransform))]
public class NativeQuadTreeCreator : MonoBehaviour
{
    public NativeQuadTree<int> tree;
    [Range(1, 8)]
    public int Depth = 6;
    public ushort LeafUnits = 200;

    public int PositionCount = 2000;
    public List<QuadElement<int>> Positions = new List<QuadElement<int>>();

    private RectTransform trans;
    
    void Start()
    {
        trans = GetComponent<RectTransform>();

        AABB2D rect = new AABB2D(new float2(trans.position.x, trans.position.y), (trans.rect.max - trans.rect.min) / 2f);
        tree = new NativeQuadTree<int>(rect, Allocator.Persistent, Depth, LeafUnits);

        do
        { 
            float x = Random.Range(rect.Center.x - rect.Extents.x, rect.Center.x + rect.Extents.x);
            float y = Random.Range(rect.Center.y - rect.Extents.y, rect.Center.y + rect.Extents.y);
            float2 testPos = new float2(x, y);

            if(rect.Contains(testPos))
            {
                QuadElement<int> element = new QuadElement<int>()
                {
                    Pos = testPos
                };
                Positions.Add(element);
            }
        }
        while (Positions.Count < PositionCount);

        AddBulkJob<int> bulkJob = new AddBulkJob<int>();
        bulkJob.Elements = new NativeArray<QuadElement<int>>(Positions.ToArray(), Allocator.TempJob);
        bulkJob.QuadTree = new NativeReference<NativeQuadTree<int>>(tree, Allocator.TempJob);

        Stopwatch stopwatch = Stopwatch.StartNew();
        bulkJob.Schedule().Complete();
        stopwatch.Stop();
        Debug.Log("Bulk Add Duration: " + stopwatch.ElapsedMilliseconds + " ms");

        bulkJob.Elements.Dispose();
        bulkJob.QuadTree.Dispose();
    }

    private void OnDrawGizmos()
    {
        if(trans == null) trans = GetComponent<RectTransform>();
        Gizmos.color = Color.white;
        
        // draw box
        DrawRectSubdivide(new AABB2D(trans), Depth);

        Gizmos.color = Color.white;
        const float size = 0.2f;
        foreach (QuadElement<int> position in Positions)
        {
            Gizmos.DrawLine(
                new Vector3(position.Pos.x - size, position.Pos.y - size), 
                new Vector3(position.Pos.x + size, position.Pos.y + size));
            Gizmos.DrawLine(
                new Vector3(position.Pos.x - size, position.Pos.y + size), 
                new Vector3(position.Pos.x + size, position.Pos.y - size));
        }
    }

    private void DrawRect(AABB2D rect)
    {
        float rectXMin = rect.Center.x - rect.Extents.x;
        float rectXMax = rect.Center.x + rect.Extents.x;
        float rectYMin = rect.Center.y - rect.Extents.y;
        float rectYMax = rect.Center.y + rect.Extents.y;

        Gizmos.DrawLine(
            new Vector3(rectXMin, rectYMin), 
            new Vector3(rectXMax, rectYMin));
        Gizmos.DrawLine(
            new Vector3(rectXMin, rectYMax), 
            new Vector3(rectXMax, rectYMax));
        Gizmos.DrawLine(
            new Vector3(rectXMin, rectYMin), 
            new Vector3(rectXMin, rectYMax));
        Gizmos.DrawLine(
            new Vector3(rectXMax, rectYMin), 
            new Vector3(rectXMax, rectYMax));
    }

    private void DrawRectSubdivide(AABB2D rect, int division)
    {
        if(division > 0)
        {
            var half = rect.Extents.x * .5f;
            DrawRectSubdivide(new AABB2D(new float2(rect.Center.x - half, rect.Center.y + half), half), division - 1);
            DrawRectSubdivide(new AABB2D(new float2(rect.Center.x + half, rect.Center.y + half), half), division - 1);
            DrawRectSubdivide(new AABB2D(new float2(rect.Center.x - half, rect.Center.y - half), half), division - 1);
            DrawRectSubdivide(new AABB2D(new float2(rect.Center.x + half, rect.Center.y - half), half), division - 1);
        }

        Gizmos.color = Color.Lerp(new Color(0.34f, 0.34f, 0.34f), new Color(1f, 1f, 1f), (float)division / Depth);
        DrawRect(rect);
    }
}
