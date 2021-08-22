using Unity.Mathematics;

namespace NativeQuadTree
{
    /// <summary>
    /// Represents an element node in the quadtree
    /// </summary>
    public struct QuadElement<T> where T : unmanaged
    {
        public float2 Pos;
        public T Element;
    }
}