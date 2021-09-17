using System.Diagnostics;

namespace NativeQuadTree
{
    [DebuggerDisplay("QuadNode Count: {" + nameof(count) + "}{" + nameof(isLeaf) + " ? \", Leaf\" : \"\", nq}")]
    internal struct QuadNode
    {
        // Points to this node's first child index in elements
        public int firstChildIndex;

        // Number of elements in the leaf
        public ushort count;
        public bool isLeaf;
    }
}