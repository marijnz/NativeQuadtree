using NativeQuadTree;
using Unity.Collections;
using UnityEditor;
using UnityEngine;

public class QuadTreeDrawer : EditorWindow
{
	[MenuItem("Window/QuadTreeDrawer")]
	static void Init()
	{
		GetWindow(typeof(QuadTreeDrawer)).Show();
	}

	public static void Draw<T>(NativeQuadTree<T> quadTree) where T : unmanaged
	{
		QuadTreeDrawer window = (QuadTreeDrawer)GetWindow(typeof(QuadTreeDrawer));
		window.DoDraw(quadTree, default, default);
	}

	public static void DrawWithResults<T>(QuadTreeJobs.RangeQueryJob<T> queryJob) where T : unmanaged
	{
		QuadTreeDrawer window = (QuadTreeDrawer)GetWindow(typeof(QuadTreeDrawer));
		window.DoDraw(queryJob);
	}

	[SerializeField]
	Color[][] pixels;

	void DoDraw<T>(NativeQuadTree<T> quadTree, NativeList<QuadElement<T>> results, AABB2D bounds) where T : unmanaged
	{
		pixels = new Color[256][];
		for (var i = 0; i < pixels.Length; i++)
		{
			pixels[i] = new Color[256];
		}
		NativeQuadTree<T>.Draw(quadTree, results, bounds, pixels);
	}

	void DoDraw<T>(QuadTreeJobs.RangeQueryJob<T> queryJob) where T : unmanaged
	{
		DoDraw(queryJob.QuadTree, queryJob.Results, queryJob.Bounds);
	}

	void OnGUI()
	{
		if(pixels != null)
		{
			var texture = new Texture2D(256, 256);
			for (var x = 0; x < pixels.Length; x++)
			{
				for (int y = 0; y < pixels[x].Length; y++)
				{
					texture.SetPixel(x, y, pixels[x][y]);
				}
			}
			texture.Apply();

			GUI.DrawTexture(new Rect(0, 0, position.width, position.height), texture);
		}
	}
}