using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace Marchio.Tests
{
    public class PolygonMathTests
    {
        static List<Vector2> Square(float size) => new List<Vector2>
        {
            new Vector2(0, 0), new Vector2(size, 0), new Vector2(size, size), new Vector2(0, size)
        };

        [Test]
        public void AreaOfSquare()
        {
            Assert.AreEqual(100f, PolygonMath.Area(Square(10f)), 1e-4f);
        }

        [Test]
        public void PointInPolygon()
        {
            var sq = Square(10f);
            Assert.IsTrue(PolygonMath.PointInPolygon(new Vector2(5f, 5f), sq));
            Assert.IsFalse(PolygonMath.PointInPolygon(new Vector2(15f, 5f), sq));
        }

        [Test]
        public void SegmentsIntersectReturnsHitPoint()
        {
            Assert.IsTrue(PolygonMath.SegmentsIntersect(new Vector2(0, 0), new Vector2(10, 10), new Vector2(0, 10), new Vector2(10, 0), out var hit));
            Assert.AreEqual(5f, hit.x, 1e-4f);
            Assert.AreEqual(5f, hit.y, 1e-4f);
            Assert.IsFalse(PolygonMath.SegmentsIntersect(new Vector2(0, 0), new Vector2(1, 1), new Vector2(5, 5), new Vector2(6, 6), out _));
        }

        [Test]
        public void DistToBoundaryFromOutside()
        {
            Assert.AreEqual(5f, PolygonMath.DistToBoundary(new Vector2(15f, 5f), Square(10f)), 1e-4f);
        }
    }
}
