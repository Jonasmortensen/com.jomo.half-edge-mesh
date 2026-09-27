using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace Jomo.HalfEdgeMesh.Tests
{
    public class HalfEdgeMeshTests
    {
        static void AssertValid(HalfEdgeMesh mesh)
        {
            List<string> errors = mesh.Validate();
            Assert.IsEmpty(errors, string.Join("\n", errors));
        }

        // V - E + F, which is 1 for any mesh shaped like a disk and 2 for a closed sphere-like mesh
        static int EulerCharacteristic(HalfEdgeMesh mesh)
        {
            return mesh.Vertices.Count - mesh.HalfEdges.Count / 2 + mesh.Faces.Count;
        }

        static HalfEdgeMesh CreateGrid(int seed)
        {
            return HalfEdgeMesh.CreateNonUniformRandomGrid(5, 2, 5, seed);
        }

        [Test]
        public void CreateTriangle_IsValid()
        {
            var mesh = HalfEdgeMesh.CreateTriangle();

            AssertValid(mesh);
            Assert.AreEqual(3, mesh.Vertices.Count);
            Assert.AreEqual(1, mesh.Faces.Count);
            Assert.AreEqual(1, EulerCharacteristic(mesh));
        }

        [Test]
        public void CreateQuad_IsValidAndFacesUp()
        {
            var mesh = HalfEdgeMesh.CreateQuad(Vector3.zero, 2, 2);

            AssertValid(mesh);
            Assert.AreEqual(4, mesh.Vertices.Count);
            Assert.AreEqual(1, mesh.Faces.Count);
            Assert.AreEqual(4, mesh.Faces[0].GetSideCount());
            Assert.Greater(Vector3.Dot(mesh.Faces[0].GetNormal(), Vector3.up), 0.99f);
        }

        [TestCase(3)]
        [TestCase(6)]
        public void CreatePolygon_IsValid(int sides)
        {
            var mesh = HalfEdgeMesh.CreatePolygon(sides, 1);

            AssertValid(mesh);
            Assert.AreEqual(sides + 1, mesh.Vertices.Count);
            Assert.AreEqual(sides, mesh.Faces.Count);
            Assert.AreEqual(1, EulerCharacteristic(mesh));
        }

        [Test]
        public void CreateIcosahedron_IsClosedAndFacesOutwards()
        {
            var mesh = HalfEdgeMesh.CreateIcosahedron(2);

            AssertValid(mesh);
            Assert.AreEqual(12, mesh.Vertices.Count);
            Assert.AreEqual(30, mesh.HalfEdges.Count / 2);
            Assert.AreEqual(20, mesh.Faces.Count);
            Assert.AreEqual(2, EulerCharacteristic(mesh));
            Assert.IsTrue(mesh.HalfEdges.All(e => !e.IsBoundary));

            foreach (var v in mesh.Vertices)
            {
                Assert.AreEqual(2f, v.Position.magnitude, 1e-5f);
                Assert.AreEqual(5, v.GetNeighbourVertices().Count);
            }

            foreach (var f in mesh.Faces)
            {
                Assert.AreEqual(3, f.GetSideCount());
                Assert.Greater(Vector3.Dot(f.GetNormal(), f.GetCenter().normalized), 0.9f);
            }
        }

        [Test]
        public void CreateIcosahedron_CanBeSubdivided()
        {
            var mesh = HalfEdgeMesh.CreateIcosahedron(1);

            mesh.TriangleSubdivide();

            AssertValid(mesh);
            Assert.AreEqual(80, mesh.Faces.Count);
            Assert.AreEqual(2, EulerCharacteristic(mesh));
        }

        [Test]
        public void SphereProject_PutsVerticesOnSphereAroundOrigin()
        {
            var mesh = HalfEdgeMesh.CreateIcosahedron(1);
            mesh.TriangleSubdivide();
            var origin = new Vector3(1, 2, 3);
            var directions = mesh.Vertices.ToDictionary(v => v, v => (v.Position - origin).normalized);

            mesh.SphereProject(4, origin);

            AssertValid(mesh);
            foreach (var v in mesh.Vertices)
            {
                Vector3 offset = v.Position - origin;
                Assert.AreEqual(4f, offset.magnitude, 1e-4f);
                Assert.Greater(Vector3.Dot(offset.normalized, directions[v]), 0.9999f, "Vertex " + v.ID + " changed direction");
            }
        }

        [Test]
        public void CopyFaces_AdjacentFacesShareVertices()
        {
            // Consecutive hexagon triangles share a spoke
            var mesh = HalfEdgeMesh.CreatePolygon(6, 1);

            var copy = mesh.CopyFaces(new[] { mesh.Faces[0], mesh.Faces[1] });

            AssertValid(copy);
            Assert.AreEqual(2, copy.Faces.Count);
            Assert.AreEqual(4, copy.Vertices.Count);
            AssertValid(mesh);
            Assert.AreEqual(6, mesh.Faces.Count);
        }

        [Test]
        public void CopyFaces_FacesTouchingAtCornerGetSeparateVertices()
        {
            // These two only share the center vertex
            var mesh = HalfEdgeMesh.CreatePolygon(6, 1);

            var copy = mesh.CopyFaces(new[] { mesh.Faces[0], mesh.Faces[3] });

            AssertValid(copy);
            Assert.AreEqual(2, copy.Faces.Count);
            Assert.AreEqual(6, copy.Vertices.Count);
        }

        [Test]
        public void CopyFaces_SelectionAndRestTogetherCoverMesh()
        {
            var mesh = HalfEdgeMesh.CreateIcosahedron(1);
            mesh.TriangleSubdivide();
            var selected = new HashSet<Face>(mesh.Faces.Where((f, i) => i % 3 == 0));

            var part = mesh.CopyFaces(selected);
            var rest = mesh.CopyFaces(mesh.Faces.Where(f => !selected.Contains(f)));

            AssertValid(part);
            AssertValid(rest);
            Assert.AreEqual(mesh.Faces.Count, part.Faces.Count + rest.Faces.Count);
        }

        [Test]
        public void CopyFaces_AllFaces_CopiesClosedMesh()
        {
            var mesh = HalfEdgeMesh.CreateIcosahedron(1);

            var copy = mesh.CopyFaces(mesh.Faces);

            AssertValid(copy);
            Assert.AreEqual(12, copy.Vertices.Count);
            Assert.AreEqual(20, copy.Faces.Count);
            Assert.AreEqual(2, EulerCharacteristic(copy));
        }

        [Test]
        public void CopyFaces_FaceFromOtherMesh_Throws()
        {
            var mesh = HalfEdgeMesh.CreateQuad(Vector3.zero, 1, 1);
            var other = HalfEdgeMesh.CreateQuad(Vector3.zero, 1, 1);

            Assert.Throws<ArgumentException>(() => mesh.CopyFaces(other.Faces));
        }

        [Test]
        public void PokeFace_TurnsQuadIntoFourTrianglesAroundCenter()
        {
            var mesh = HalfEdgeMesh.CreateQuad(Vector3.zero, 2, 2);

            Vertex center = mesh.PokeFace(mesh.Faces[0]);

            AssertValid(mesh);
            Assert.AreEqual(Vector3.zero, center.Position);
            Assert.AreEqual(5, mesh.Vertices.Count);
            Assert.AreEqual(4, mesh.Faces.Count);
            Assert.IsTrue(mesh.Faces.All(f => f.GetSideCount() == 3));
            Assert.AreEqual(4, center.GetNeighbourVertices().Count);
            Assert.IsTrue(mesh.Faces.All(f => Vector3.Dot(f.GetNormal(), Vector3.up) > 0.99f));
        }

        [Test]
        public void PokeFace_LeavesNeighboursAlone()
        {
            var mesh = HalfEdgeMesh.CreatePolygon(6, 1);
            Face poked = mesh.Faces[0];
            var neighbours = mesh.Faces.Where(f => f != poked).ToList();

            mesh.PokeFace(poked);

            AssertValid(mesh);
            Assert.AreEqual(5 + 3, mesh.Faces.Count);
            Assert.IsTrue(neighbours.All(f => f.GetSideCount() == 3));
        }

        [Test]
        public void PokeFaces_MakesOneTrianglePerSide()
        {
            var mesh = CreateGrid(4);
            int sideCount = mesh.Faces.Sum(f => f.GetSideCount());
            int vertexCount = mesh.Vertices.Count + mesh.Faces.Count;

            mesh.PokeFaces();

            AssertValid(mesh);
            Assert.AreEqual(sideCount, mesh.Faces.Count);
            Assert.AreEqual(vertexCount, mesh.Vertices.Count);
            Assert.IsTrue(mesh.Faces.All(f => f.GetSideCount() == 3));
            Assert.AreEqual(1, EulerCharacteristic(mesh));
        }

        [Test]
        public void PokeFaces_OnClosedMesh()
        {
            var mesh = HalfEdgeMesh.CreateIcosahedron(1);

            mesh.PokeFaces();

            AssertValid(mesh);
            Assert.AreEqual(60, mesh.Faces.Count);
            Assert.AreEqual(2, EulerCharacteristic(mesh));
        }

        [Test]
        public void Triangulate_SplitsEveryQuadInTwo()
        {
            var mesh = CreateGrid(3);
            int quadCount = mesh.Faces.Count;

            mesh.Triangulate();

            AssertValid(mesh);
            Assert.AreEqual(quadCount * 2, mesh.Faces.Count);
            Assert.IsTrue(mesh.Faces.All(f => f.GetSideCount() == 3));
            Assert.AreEqual(1, EulerCharacteristic(mesh));
        }

        [Test]
        public void Triangulate_PicksDiagonalWithBiggestMinimumAngle()
        {
            // A flat rhombus. The short diagonal gives a smallest angle of about 37 degrees, the long one about 18.
            var mesh = HalfEdgeMesh.CreateQuad(new Vector3(0, 0, 1), new Vector3(3, 0, 0), new Vector3(0, 0, -1), new Vector3(-3, 0, 0));
            var v = mesh.Vertices;

            mesh.Triangulate();

            AssertValid(mesh);
            Assert.IsNotNull(mesh.FindEdge(v[0], v[2]));
            Assert.IsNull(mesh.FindEdge(v[1], v[3]));

            // The same shape rotated so the short diagonal is the other one
            mesh = HalfEdgeMesh.CreateQuad(new Vector3(-3, 0, 0), new Vector3(0, 0, 1), new Vector3(3, 0, 0), new Vector3(0, 0, -1));
            v = mesh.Vertices;

            mesh.Triangulate();

            AssertValid(mesh);
            Assert.IsNull(mesh.FindEdge(v[0], v[2]));
            Assert.IsNotNull(mesh.FindEdge(v[1], v[3]));
        }

        [Test]
        public void Triangulate_LeavesTrianglesAlone()
        {
            var mesh = HalfEdgeMesh.CreateIcosahedron(1);

            mesh.Triangulate();

            AssertValid(mesh);
            Assert.AreEqual(20, mesh.Faces.Count);
        }

        [Test]
        public void Triangulate_FaceWithMoreThanFourSides_ThrowsWithoutChangingMesh()
        {
            var mesh = HalfEdgeMesh.CreatePolygon(6, 1);
            mesh.QuadSubdivide();
            mesh.SplitEdge(mesh.Faces[0].Edge);
            int faceCount = mesh.Faces.Count;

            Assert.Throws<NotSupportedException>(() => mesh.Triangulate());

            AssertValid(mesh);
            Assert.AreEqual(faceCount, mesh.Faces.Count);
        }

        class TestData : IMeshData
        {
            public int Value;
            public IMeshData Clone() => new TestData { Value = Value };
        }

        class OtherData : IMeshData
        {
            public IMeshData Clone() => this;
        }

        // Gives every face data holding its index, so faces derived from it can be traced back
        static void TagFaces(HalfEdgeMesh mesh)
        {
            for (int i = 0; i < mesh.Faces.Count; i++) mesh.Faces[i].Data = new TestData { Value = i };
        }

        // Asserts every face has TestData of its own and that each original value occurs expectedPerValue times
        static void AssertDataCarried(HalfEdgeMesh mesh, int originalFaceCount, int expectedPerValue)
        {
            var data = mesh.Faces.Select(f => f.GetData<TestData>()).ToList();
            Assert.IsTrue(data.All(d => d != null), "Some faces lost their data");
            Assert.AreEqual(data.Count, data.Distinct().Count(), "Some faces share a data instance");

            var counts = data.GroupBy(d => d.Value).ToDictionary(g => g.Key, g => g.Count());
            Assert.AreEqual(originalFaceCount, counts.Count);
            Assert.IsTrue(counts.Values.All(c => c == expectedPerValue));
        }

        [Test]
        public void FaceData_IsNullByDefaultAndStaysNull()
        {
            var mesh = HalfEdgeMesh.CreateQuad(Vector3.zero, 1, 1);

            mesh.PokeFaces();
            var copy = mesh.CopyFaces(mesh.Faces);

            Assert.IsTrue(mesh.Faces.All(f => f.Data == null));
            Assert.IsTrue(copy.Faces.All(f => f.Data == null));
        }

        [Test]
        public void GetData_ReturnsNullForOtherType()
        {
            var mesh = HalfEdgeMesh.CreateQuad(Vector3.zero, 1, 1);
            mesh.Faces[0].Data = new OtherData();

            Assert.IsNull(mesh.Faces[0].GetData<TestData>());
            Assert.IsNotNull(mesh.Faces[0].GetData<OtherData>());
        }

        [Test]
        public void FaceData_CarriedBySplitFace()
        {
            var mesh = HalfEdgeMesh.CreateQuad(Vector3.zero, 1, 1);
            TagFaces(mesh);
            HalfEdge e = mesh.Faces[0].Edge;

            mesh.SplitFace(e, e.Next.Next);

            AssertDataCarried(mesh, 1, 2);
        }

        [Test]
        public void FaceData_CarriedByPokeFaces()
        {
            var mesh = HalfEdgeMesh.CreatePolygon(6, 1);
            mesh.QuadSubdivide();
            TagFaces(mesh);

            mesh.PokeFaces();

            AssertDataCarried(mesh, 18, 4);
        }

        [Test]
        public void FaceData_CarriedByTriangulate()
        {
            var mesh = HalfEdgeMesh.CreatePolygon(6, 1);
            mesh.QuadSubdivide();
            TagFaces(mesh);

            mesh.Triangulate();

            AssertDataCarried(mesh, 18, 2);
        }

        [Test]
        public void FaceData_CarriedByTriangleSubdivide()
        {
            var mesh = HalfEdgeMesh.CreateIcosahedron(1);
            TagFaces(mesh);

            mesh.TriangleSubdivide();

            AssertDataCarried(mesh, 20, 4);
        }

        [Test]
        public void FaceData_CarriedByQuadSubdivide()
        {
            var mesh = HalfEdgeMesh.CreatePolygon(6, 1);
            TagFaces(mesh);

            mesh.QuadSubdivide();

            AssertDataCarried(mesh, 6, 3);
        }

        [Test]
        public void FaceData_ClonedByCopyFaces()
        {
            var mesh = HalfEdgeMesh.CreatePolygon(6, 1);
            TagFaces(mesh);
            var originals = mesh.Faces.Select(f => f.Data).ToList();

            var copy = mesh.CopyFaces(mesh.Faces.Take(3));

            AssertDataCarried(copy, 3, 1);
            Assert.IsTrue(copy.Faces.All(f => !originals.Contains(f.Data)), "The copy shares data with the original");
            CollectionAssert.AreEqual(originals, mesh.Faces.Select(f => f.Data).ToList());
        }

        [Test]
        public void FaceData_DissolveEdgeKeepsDataOfReturnedFace()
        {
            var mesh = HalfEdgeMesh.CreateQuad(Vector3.zero, 1, 1);
            HalfEdge e = mesh.Faces[0].Edge;
            var (split, _) = mesh.SplitFace(e, e.Next.Next);
            var kept = new TestData { Value = 1 };
            split.IncidentFace.Data = kept;
            split.Twin.IncidentFace.Data = new TestData { Value = 2 };

            Face merged = mesh.DissolveEdge(split);

            Assert.AreSame(kept, merged.Data);
        }

        [Test]
        public void VertexIdsAreUnique()
        {
            var mesh = CreateGrid(1);

            Assert.AreEqual(mesh.Vertices.Count, mesh.Vertices.Select(v => v.ID).Distinct().Count());
            Assert.AreEqual(mesh.Faces.Count, mesh.Faces.Select(f => f.ID).Distinct().Count());
        }

        [Test]
        public void FindEdge_ReturnsEdgeBetweenVertices()
        {
            var mesh = HalfEdgeMesh.CreateQuad(Vector3.zero, 1, 1);

            foreach (var e in mesh.HalfEdges)
            {
                Assert.AreSame(e, mesh.FindEdge(e.Origin, e.Destination));
            }

            List<Vertex> corners = mesh.Faces[0].GetVertices();
            Assert.IsNull(mesh.FindEdge(corners[0], corners[2]));
        }

        [Test]
        public void SplitEdge_AddsVertexToFace()
        {
            var mesh = HalfEdgeMesh.CreateQuad(Vector3.zero, 1, 1);
            HalfEdge e = mesh.Faces[0].Edge;
            Vector3 expected = Vector3.Lerp(e.Origin.Position, e.Destination.Position, 0.5f);

            Vertex v = mesh.SplitEdge(e);

            AssertValid(mesh);
            Assert.AreEqual(expected, v.Position);
            Assert.AreEqual(5, mesh.Faces[0].GetSideCount());
        }

        [Test]
        public void SplitFace_ThenDissolveEdge_RestoresQuad()
        {
            var mesh = HalfEdgeMesh.CreateQuad(Vector3.zero, 1, 1);
            HalfEdge e = mesh.Faces[0].Edge;

            var (split, _) = mesh.SplitFace(e, e.Next.Next);

            AssertValid(mesh);
            Assert.AreEqual(2, mesh.Faces.Count);
            Assert.IsTrue(mesh.Faces.All(f => f.GetSideCount() == 3));

            Face merged = mesh.DissolveEdge(split);

            AssertValid(mesh);
            Assert.AreEqual(1, mesh.Faces.Count);
            Assert.AreSame(merged, mesh.Faces[0]);
            Assert.AreEqual(4, merged.GetSideCount());
        }

        [Test]
        public void DissolveEdge_OnBoundary_Throws()
        {
            var mesh = HalfEdgeMesh.CreateQuad(Vector3.zero, 1, 1);

            Assert.Throws<InvalidOperationException>(() => mesh.DissolveEdge(mesh.Faces[0].Edge));
        }

        [Test]
        public void TriangleSubdivide_MakesFourTrianglesPerTriangle()
        {
            var mesh = HalfEdgeMesh.CreatePolygon(6, 1);

            mesh.TriangleSubdivide();

            AssertValid(mesh);
            Assert.AreEqual(24, mesh.Faces.Count);
            Assert.IsTrue(mesh.Faces.All(f => f.GetSideCount() == 3));
            Assert.AreEqual(1, EulerCharacteristic(mesh));
        }

        [Test]
        public void QuadSubdivide_MakesOneQuadPerCorner()
        {
            var quad = HalfEdgeMesh.CreateQuad(Vector3.zero, 1, 1);
            quad.QuadSubdivide();
            AssertValid(quad);
            Assert.AreEqual(4, quad.Faces.Count);
            Assert.IsTrue(quad.Faces.All(f => f.GetSideCount() == 4));

            var hexagon = HalfEdgeMesh.CreatePolygon(6, 1);
            hexagon.QuadSubdivide();
            AssertValid(hexagon);
            Assert.AreEqual(18, hexagon.Faces.Count);
            Assert.IsTrue(hexagon.Faces.All(f => f.GetSideCount() == 4));
            Assert.AreEqual(1, EulerCharacteristic(hexagon));
        }

        [Test]
        public void DissolveToQuads_LeavesNoTwoTrianglesAdjacent()
        {
            var mesh = HalfEdgeMesh.CreatePolygon(6, 1);
            mesh.TriangleSubdivide();
            mesh.TriangleSubdivide();

            mesh.DissolveToQuads(7);

            AssertValid(mesh);
            Assert.IsTrue(mesh.Faces.All(f => f.GetSideCount() == 3 || f.GetSideCount() == 4));
            foreach (var e in mesh.HalfEdges)
            {
                if (e.IsBoundary || e.Twin.IsBoundary) continue;
                bool bothTriangles = e.IncidentFace.GetSideCount() == 3 && e.Twin.IncidentFace.GetSideCount() == 3;
                Assert.IsFalse(bothTriangles, "Edge " + e.ID + " could still have been dissolved");
            }
        }

        [Test]
        public void NonUniformRandomGrid_IsValidQuadMesh()
        {
            var mesh = CreateGrid(42);

            AssertValid(mesh);
            Assert.IsTrue(mesh.Faces.All(f => f.GetSideCount() == 4));
            Assert.AreEqual(1, EulerCharacteristic(mesh));
        }

        [Test]
        public void NonUniformRandomGrid_IsDeterministic()
        {
            Vector3[] first = CreateGrid(42).Vertices.Select(v => v.Position).ToArray();
            Vector3[] second = CreateGrid(42).Vertices.Select(v => v.Position).ToArray();
            Vector3[] other = CreateGrid(43).Vertices.Select(v => v.Position).ToArray();

            CollectionAssert.AreEqual(first, second);
            CollectionAssert.AreNotEqual(first, other);
        }

        [Test]
        public void RelaxVertices_KeepsOuterVerticesInPlace()
        {
            var mesh = HalfEdgeMesh.CreatePolygon(6, 1);
            mesh.TriangleSubdivide();
            mesh.DissolveToQuads(3);
            mesh.QuadSubdivide();
            var outer = mesh.Vertices.Where(v => v.IsOuter()).ToDictionary(v => v, v => v.Position);

            mesh.RelaxVertices();

            AssertValid(mesh);
            foreach (var pair in outer)
            {
                Assert.AreEqual(pair.Value, pair.Key.Position);
            }
        }

        // The tests below create UnityEngine.Mesh objects, which needs the engine
        [Test, Category("UnityMesh")]
        public void FromUnityMesh_WeldsSplitQuadGrid()
        {
            // 2x2 quads, each with its own four vertices
            var positions = new List<Vector3>();
            var indices = new List<int>();
            for (int x = 0; x < 2; x++)
            {
                for (int z = 0; z < 2; z++)
                {
                    indices.AddRange(Enumerable.Range(positions.Count, 4));
                    positions.Add(new Vector3(x, 0, z + 1));
                    positions.Add(new Vector3(x + 1, 0, z + 1));
                    positions.Add(new Vector3(x + 1, 0, z));
                    positions.Add(new Vector3(x, 0, z));
                }
            }

            var unityMesh = new UnityEngine.Mesh();
            unityMesh.SetVertices(positions);
            unityMesh.SetIndices(indices, MeshTopology.Quads, 0);

            var mesh = HalfEdgeMesh.FromUnityMesh(unityMesh);

            AssertValid(mesh);
            Assert.AreEqual(9, mesh.Vertices.Count);
            Assert.AreEqual(4, mesh.Faces.Count);
            Assert.AreEqual(1, mesh.Vertices.Count(v => !v.IsOuter()));

            UnityEngine.Object.DestroyImmediate(unityMesh);
        }

        [Test, Category("UnityMesh")]
        public void ToUnityMesh_RoundTrips()
        {
            var original = CreateGrid(5);

            var unityMesh = original.ToUnityMesh();
            var roundTrip = HalfEdgeMesh.FromUnityMesh(unityMesh);

            Assert.AreEqual(original.Faces.Count * 2, unityMesh.triangles.Length / 3);
            Assert.Greater(Vector3.Dot(unityMesh.normals[0], Vector3.up), 0.99f);
            AssertValid(roundTrip);
            Assert.AreEqual(original.Vertices.Count, roundTrip.Vertices.Count);
            Assert.AreEqual(original.Faces.Count * 2, roundTrip.Faces.Count);

            UnityEngine.Object.DestroyImmediate(unityMesh);
        }

        [Test, Category("UnityMesh")]
        public void FromUnityMesh_UnsupportedTopology_Throws()
        {
            var unityMesh = new UnityEngine.Mesh();
            unityMesh.SetVertices(new List<Vector3> { Vector3.zero, Vector3.right });
            unityMesh.SetIndices(new[] { 0, 1 }, MeshTopology.Lines, 0);

            Assert.Throws<ArgumentException>(() => HalfEdgeMesh.FromUnityMesh(unityMesh));

            UnityEngine.Object.DestroyImmediate(unityMesh);
        }
    }
}
