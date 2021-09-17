using System;
using System.Collections;
using System.Collections.Generic;
using NativeQuadTree;
using NativeQuadTree.Helpers;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

[RequireComponent(typeof(RectTransform))]
public class NativeQueryRect : MonoBehaviour
{
    public NativeQuadTreeCreator Tree;

    private RectTransform trans;
    private QuadElement<int>[] Results;
    
    void Start()
    {
        trans = GetComponent<RectTransform>();
    }

    void Update()
    {
        AABB2D box = new AABB2D(new float2(trans.position.x, trans.position.y), (trans.rect.max - trans.rect.min) / 2f);
        NativeReference<NativeQuadTree<int>> treeRef = new NativeReference<NativeQuadTree<int>>(Tree.tree, Allocator.TempJob);
        NativeList<QuadElement<int>> results = new NativeList<QuadElement<int>>(Tree.tree.EstimateResultSize(box), Allocator.TempJob);
        
        RectQueryJob<int> query = new RectQueryJob<int>(box, treeRef, results);
        query.Schedule().Complete();

        Results = results.ToArray();
        results.Dispose();
        treeRef.Dispose();
    }

    private void OnDrawGizmos()
    {
        if(trans == null) trans = GetComponent<RectTransform>();
        
        Gizmos.color = new Color(1f, 0.38f, 0.12f);

        Rect transRect = trans.rect;
        float transRectXMin = trans.position.x + transRect.xMin;
        float transRectXMax = trans.position.x + transRect.xMax;
        float transRectYMin = trans.position.y + transRect.yMin;
        float transRectYMax = trans.position.y + transRect.yMax;
        Gizmos.DrawLine(
            new Vector3(transRectXMin, transRectYMin), 
            new Vector3(transRectXMax, transRectYMin));
        Gizmos.DrawLine(
            new Vector3(transRectXMin, transRectYMax), 
            new Vector3(transRectXMax, transRectYMax));
        Gizmos.DrawLine(
            new Vector3(transRectXMin, transRectYMin), 
            new Vector3(transRectXMin, transRectYMax));
        Gizmos.DrawLine(
            new Vector3(transRectXMax, transRectYMin), 
            new Vector3(transRectXMax, transRectYMax));

        const float size = 0.2f;
        foreach (QuadElement<int> result in Results ?? Array.Empty<QuadElement<int>>())
        {
            float xMin = result.Pos.x - size;
            float xMax = result.Pos.x + size;
            float yMin = result.Pos.y - size;
            float yMax = result.Pos.y + size;
            
            Gizmos.DrawLine(
                new Vector3(xMin, yMin), 
                new Vector3(xMax, yMin));
            Gizmos.DrawLine(
                new Vector3(xMin, yMax), 
                new Vector3(xMax, yMax));
            Gizmos.DrawLine(
                new Vector3(xMin, yMin), 
                new Vector3(xMin, yMax));
            Gizmos.DrawLine(
                new Vector3(xMax, yMin), 
                new Vector3(xMax, yMax));
        }
    }
}
