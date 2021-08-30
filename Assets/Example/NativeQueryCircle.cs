using System;
using System.Collections;
using System.Collections.Generic;
using NativeQuadTree;
using NativeQuadTree.Helpers;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

public class NativeQueryCircle : MonoBehaviour
{
    public NativeQuadTreeCreator Tree;
    public float Radious = 10;

    private Transform trans;
    private QuadElement<int>[] Results;
    
    void Start()
    {
        trans = transform;
    }

    void Update()
    {
        Circle2D circle = new Circle2D(new float2(trans.position.x, trans.position.y), Radious);
        NativeReference<NativeQuadTree<int>> treeRef = new NativeReference<NativeQuadTree<int>>(Tree.tree, Allocator.TempJob);
        NativeList<QuadElement<int>> results = new NativeList<QuadElement<int>>(Tree.tree.EstimateResultSize(circle), Allocator.TempJob);
        
        CircleQueryJob<int> query = new CircleQueryJob<int>(circle, treeRef, results);
        query.Schedule().Complete();

        Results = results.ToArray();
        results.Dispose();
        treeRef.Dispose();
    }

    private void OnDrawGizmos()
    {
        if(trans == null) trans = transform;
        
        Gizmos.color = Color.green;
        
        Vector2 previousPos = trans.position + new Vector3(0f, Radious);
        const int steps = 48;
        const float stepDegree = (360f / steps);
        for (int i = 1; i <= steps; i++)
        {
            Vector2 newPos = trans.position + new Vector3(
                Radious * math.sin(math.radians(stepDegree * i)),
                Radious * math.cos(math.radians(stepDegree * i)));
            
            Gizmos.DrawLine(previousPos, newPos);
            previousPos = newPos;
        }

        const float size = 0.2f;
        foreach (QuadElement<int> result in Results ?? Array.Empty<QuadElement<int>>())
        {
            Gizmos.DrawLine(
                new Vector3(result.Pos.x - size, result.Pos.y), 
                new Vector3(result.Pos.x + size, result.Pos.y));
            Gizmos.DrawLine(
                new Vector3(result.Pos.x, result.Pos.y - size), 
                new Vector3(result.Pos.x, result.Pos.y + size));
        }
    }
}
