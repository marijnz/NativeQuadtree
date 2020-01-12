using QuadTree;
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
		window.DoDraw(quadTree);
	}

	[SerializeField]
	Color[][] pixels;

	void DoDraw<T>(NativeQuadTree<T> quadTree) where T : unmanaged
	{
		pixels = new Color[512][];
		for (var i = 0; i < pixels.Length; i++)
		{
			pixels[i] = new Color[512];
		}
		NativeQuadTree<T>.Draw(quadTree, pixels);
	}

	void OnGUI()
	{
		if(pixels != null)
		{
			var texture = new Texture2D(512, 512);
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