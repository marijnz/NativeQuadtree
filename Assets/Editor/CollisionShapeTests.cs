using NativeQuadTree;
using NUnit.Framework;
using Unity.Mathematics;
using Assert = UnityEngine.Assertions.Assert;

public class CollisionShapeTests
{
    [Test]
    public void SquareOverlap()
    {
        AABB2D largeSquare = new AABB2D(new float2(5f), new float2(5f));
        AABB2D smallSquare = new AABB2D(new float2(5f), new float2(1f));

        Assert.IsTrue(largeSquare.Contains(smallSquare), "small square is entirely contained inside the large square");
    }

    [Test]
    public void SquareNoOverlap()
    {
        AABB2D square1 = new AABB2D(new float2(5f), new float2(5f));
        AABB2D square2 = new AABB2D(new float2(-2f), new float2(1f));
        AABB2D square3 = new AABB2D(new float2(15f), new float2(6f));

        Assert.IsFalse(square1.Contains(square2), "No overlap between the two squares");
        Assert.IsFalse(square1.Contains(square3), "No overlap between the two squares");
        Assert.IsFalse(square2.Contains(square3), "No overlap between the two squares");
    }

    [Test]
    public void SquareEdgeIntersect()
    {
        AABB2D largeSquare = new AABB2D(new float2(5f), new float2(5f));
        AABB2D perfectOverlap = new AABB2D(new float2(1f), new float2(1f));

        Assert.IsTrue(largeSquare.Intersects(perfectOverlap), "perfect overlap is entirely contained inside the large square");
        Assert.IsTrue(perfectOverlap.Intersects(largeSquare), "perfect overlap is entirely contained inside the large square");
        Assert.IsTrue(largeSquare.Intersects(largeSquare), "perfect overlap is entirely contained inside the large square");
    }

    [Test]
    public void SquareEdgePartialIntersect()
    {
        AABB2D largeSquare = new AABB2D(new float2(5f), new float2(5f));
        AABB2D partialOverlap = new AABB2D(new float2(0.8f), new float2(1f));

        Assert.IsTrue(largeSquare.Intersects(partialOverlap), "partial overlap is contained inside the large square");
    }

    [Test]
    public void SquareEdgePartialIntersect2()
    {
        AABB2D horizontal = new AABB2D(new float2(5f), new float2(5f, 1f));
        AABB2D vertical = new AABB2D(new float2(5), new float2(1f, 5f));

        Assert.IsTrue(horizontal.Intersects(vertical), "partial overlap without either square containing a corner");
        Assert.IsTrue(vertical.Intersects(horizontal), "partial overlap without either square containing a corner");
    }

    [Test]
    public void CircleContains()
    {
        AABB2D box = new AABB2D(new float2(5f), new float2(5f));
        Circle2D testCircle = new Circle2D(5f, 1f);

        Assert.IsTrue(box.Contains(testCircle), "partial overlap without either square containing a corner");
    }

    [Test]
    public void CircleContains2()
    {
        AABB2D box = new AABB2D(new float2(5f), new float2(5f));
        Circle2D testCircle = new Circle2D(5f, 5f);

        Assert.IsTrue(box.Contains(testCircle), "partial overlap without either square containing a corner");
    }

    [Test]
    public void CircleContains3()
    {
        AABB2D box = new AABB2D(new float2(5f), new float2(5f));
        Circle2D testCircle = new Circle2D(5f, 8f);

        Assert.IsTrue(box.Contains(testCircle), "partial overlap without either square containing a corner");
    }

    [Test]
    public void CircleContains4()
    {
        AABB2D box = new AABB2D(new float2(50f), new float2(50f));
        Circle2D testCircle = new Circle2D(5f, 3f);

        Assert.IsTrue(box.Contains(testCircle), "partial overlap without either square containing a corner");
    }
}