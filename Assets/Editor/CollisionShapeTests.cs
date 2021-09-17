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
    public void SquareCircleContains()
    {
        AABB2D box = new AABB2D(new float2(5f), new float2(5f));
        Circle2D testCircle = new Circle2D(5f, 1f);

        Assert.IsTrue(box.Contains(testCircle), "fully enclosed inside square");
    }

    [Test]
    public void SquareCircleContains2()
    {
        AABB2D box = new AABB2D(new float2(5f), new float2(5f));
        Circle2D testCircle = new Circle2D(5f, 5f);

        Assert.IsTrue(box.Contains(testCircle), "fully enclosed inside square");
    }

    [Test]
    public void SquareCircleContains3()
    {
        AABB2D box = new AABB2D(new float2(5f), new float2(5f));
        Circle2D testCircle = new Circle2D(5f, 8f);

        Assert.IsFalse(box.Contains(testCircle), "the circle is outside the bounds of the square so it isn't fully contained");
    }

    [Test]
    public void SquareCircleContains3V2()
    {
        AABB2D box = new AABB2D(new float2(5f), new float2(5f));
        Circle2D testCircle = new Circle2D(6f, 8f);

        Assert.IsFalse(box.Contains(testCircle), "the circle is outside the bounds of the square so it isn't fully contained");
    }

    [Test]
    public void SquareCircleContains4()
    {
        AABB2D box = new AABB2D(new float2(50f), new float2(50f));
        Circle2D testCircle = new Circle2D(5f, 3f);

        Assert.IsTrue(box.Contains(testCircle), "fully contained");
    }

    [Test]
    public void SquareCircleContains5()
    {
        AABB2D box = new AABB2D(new float2(50f), new float2(50f));
        Circle2D testCircle = new Circle2D(-5f, 3f);

        Assert.IsFalse(box.Contains(testCircle), "outside the square");
    }

    [Test]
    public void SquareCircleContains6()
    {
        AABB2D box = new AABB2D(new float2(50f), new float2(50f));
        Circle2D testCircle = new Circle2D(3.1f, 3f);

        Assert.IsTrue(box.Contains(testCircle), "fully contained at the very edge of the square");
    }

    [Test]
    public void SquareCircleIntersect()
    {
        AABB2D box = new AABB2D(new float2(50f), new float2(50f));
        Circle2D testCircle = new Circle2D(new float2(1f, 50f), 3f);

        Assert.IsTrue(box.Intersects(testCircle), "intersects the left side of square with the majority of it's area");
    }

    [Test]
    public void SquareCircleIntersect2()
    {
        AABB2D box = new AABB2D(new float2(50f), new float2(50f));
        Circle2D testCircle = new Circle2D(new float2(-1f, 50f), 3f);

        Assert.IsTrue(box.Intersects(testCircle), "intersects the left side of square with the minority of it's area");
    }

    [Test]
    public void SquareCircleIntersect3()
    {
        AABB2D box = new AABB2D(new float2(5f), new float2(5f));
        Circle2D testCircle = new Circle2D(5f, 8f);

        Assert.IsTrue(box.Intersects(testCircle), "starts inside the square but expands outside it's bounds");
    }

    [Test]
    public void SquareCircleIntersect4()
    {
        AABB2D box = new AABB2D(new float2(50f), new float2(50f));
        Circle2D testCircle = new Circle2D(new float2(50f, 1f), 3f);

        Assert.IsTrue(box.Intersects(testCircle), "intersects the bottom side of square with the majority of it's area");
    }

    [Test]
    public void SquareCircleIntersect5()
    {
        AABB2D box = new AABB2D(new float2(50f), new float2(50f));
        Circle2D testCircle = new Circle2D(new float2(50f, -1f), 3f);

        Assert.IsTrue(box.Intersects(testCircle), "intersects the bottom side of square with the minority of it's area");
    }

    [Test]
    public void CircleContains()
    {
        AABB2D box = new AABB2D(new float2(5f), new float2(5f));
        Circle2D testCircle = new Circle2D(5f, 1f);

        Assert.IsFalse(testCircle.Contains(box), "fully enclosed inside square, so circle doesn't contain the entire box");
    }

    [Test]
    public void CircleContains2()
    {
        AABB2D box = new AABB2D(new float2(5f), new float2(5f));
        Circle2D testCircle = new Circle2D(5f, 5f);

        Assert.IsFalse(testCircle.Contains(box), "circle fully enclosed inside square, so circle doesn't contain the entire box");
    }

    [Test]
    public void CircleContains3()
    {
        AABB2D box = new AABB2D(new float2(5f), new float2(5f));
        Circle2D testCircle = new Circle2D(5f, 8f);

        Assert.IsTrue(testCircle.Contains(box), "the circle fully contains the box");
    }

    [Test]
    public void CircleContains3V2()
    {
        AABB2D box = new AABB2D(new float2(5f), new float2(5f));
        Circle2D testCircle = new Circle2D(6f, 8f);

        Assert.IsFalse(testCircle.Contains(box), "the circle is outside the bounds of the square so it isn't fully contained");
    }

    [Test]
    public void CircleContains4()
    {
        AABB2D box = new AABB2D(new float2(50f), new float2(50f));
        Circle2D testCircle = new Circle2D(5f, 3f);

        Assert.IsFalse(testCircle.Contains(box), "box is fully around the circle, so circle can't contain the box");
    }

    [Test]
    public void CircleContains5()
    {
        AABB2D box = new AABB2D(new float2(50f), new float2(50f));
        Circle2D testCircle = new Circle2D(-5f, 3f);

        Assert.IsFalse(testCircle.Contains(box), "outside the square");
    }

    [Test]
    public void CircleContains6()
    {
        Circle2D testCircle = new Circle2D(5f, 10f);
        AABB2D box = new AABB2D(new float2(5), new float2(6f));

        Assert.IsTrue(testCircle.Contains(box), "box is fully contained");
    }

    [Test]
    public void CircleContains7()
    {
        Circle2D testCircle = new Circle2D(5f, 10f);
        AABB2D box = new AABB2D(new float2(3), new float2(1f));

        Assert.IsTrue(testCircle.Contains(box), "box is fully contained");
    }

    [Test]
    public void CircleIntersect()
    {
        AABB2D box = new AABB2D(new float2(50f), new float2(50f));
        Circle2D testCircle = new Circle2D(new float2(1f, 50f), 3f);

        Assert.IsTrue(testCircle.Intersects(box), "intersects the left side of square with the majority of it's area");
    }

    [Test]
    public void CircleIntersect2()
    {
        AABB2D box = new AABB2D(new float2(50f), new float2(50f));
        Circle2D testCircle = new Circle2D(new float2(-1f, 50f), 3f);

        Assert.IsTrue(testCircle.Intersects(box), "intersects the left side of square with the minority of it's area");
    }

    [Test]
    public void CircleIntersect3()
    {
        AABB2D box = new AABB2D(new float2(5f), new float2(5f));
        Circle2D testCircle = new Circle2D(5f, 8f);

        Assert.IsTrue(testCircle.Intersects(box), "starts inside the square but expands outside it's bounds");
    }

    [Test]
    public void CircleIntersect4()
    {
        AABB2D box = new AABB2D(new float2(50f), new float2(50f));
        Circle2D testCircle = new Circle2D(new float2(50f, 1f), 3f);

        Assert.IsTrue(testCircle.Intersects(box), "intersects the bottom side of square with the majority of it's area");
    }

    [Test]
    public void CircleIntersect5()
    {
        AABB2D box = new AABB2D(new float2(50f), new float2(50f));
        Circle2D testCircle = new Circle2D(new float2(50f, -1f), 3f);

        Assert.IsTrue(testCircle.Intersects(box), "intersects the bottom side of square with the minority of it's area");
    }

    [Test]
    public void CircleIntersect6()
    {
        AABB2D box = new AABB2D(new float2(50f), new float2(50f));
        Circle2D testCircle = new Circle2D(new float2(5f, 5f), 3f);

        Assert.IsTrue(testCircle.Intersects(box), "the circle is fully inside the box which means it intersects the box area... not the edge");
    }
}