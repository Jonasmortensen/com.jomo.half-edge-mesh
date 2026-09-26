using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;

namespace Jomo.HalfEdgeMesh
{
    public class HalfEdgeMesh
    {
        // Elements know their own list index, so removal is an O(1) swap with the last element.
        // This means removing elements changes the order of the lists.
        private readonly List<HalfEdge> m_HalfEdges = new List<HalfEdge>();
        private readonly List<Vertex> m_Vertices = new List<Vertex>();
        private readonly List<Face> m_Faces = new List<Face>();

        public IReadOnlyList<HalfEdge> HalfEdges => m_HalfEdges;
        public IReadOnlyList<Vertex> Vertices => m_Vertices;
        public IReadOnlyList<Face> Faces => m_Faces;

        private int HalfEdgeId;
        private int FaceId;
        private int VertexId;

        private HalfEdgeMesh()
        {
        }

        private HalfEdge AddHalfEdge(Vertex origin)
        {
            HalfEdge e = new HalfEdge(origin, HalfEdgeId++);
            if (origin.IncidentEdge == null) origin.IncidentEdge = e;
            e.Index = m_HalfEdges.Count;
            m_HalfEdges.Add(e);
            return e;
        }

        private Face AddFace(HalfEdge incidentEdge)
        {
            Face f = new Face(incidentEdge);
            f.ID = FaceId++;
            f.Index = m_Faces.Count;
            m_Faces.Add(f);
            incidentEdge.IncidentFace = f;
            return f;
        }

        private Vertex AddVertex(Vector3 position)
        {
            Vertex v = new Vertex(position);
            v.ID = VertexId++;
            v.Index = m_Vertices.Count;
            m_Vertices.Add(v);
            return v;
        }

        private (HalfEdge, HalfEdge) AddEdge(Vertex a, Vertex b)
        {
            HalfEdge e1 = AddHalfEdge(a);
            HalfEdge e2 = AddHalfEdge(b);
            e1.Twin = e2;
            e2.Twin = e1;
            return (e1, e2);
        }

        private void RemoveHalfEdge(HalfEdge e)
        {
            int last = m_HalfEdges.Count - 1;
            m_HalfEdges[e.Index] = m_HalfEdges[last];
            m_HalfEdges[e.Index].Index = e.Index;
            m_HalfEdges.RemoveAt(last);
            e.Index = -1;
        }

        private void RemoveFace(Face f)
        {
            int last = m_Faces.Count - 1;
            m_Faces[f.Index] = m_Faces[last];
            m_Faces[f.Index].Index = f.Index;
            m_Faces.RemoveAt(last);
            f.Index = -1;
        }

        // The half-edge going from a to b, or null if they are not connected. O(degree of a).
        public HalfEdge FindEdge(Vertex a, Vertex b)
        {
            if (a == b) throw new ArgumentException("Can't find an edge from a vertex to itself");

            foreach (var e in a.OutgoingEdges())
            {
                if (e.Destination == b) return e;
            }

            return null;
        }

        // Removes the edge and merges its two faces into e.IncidentFace, which is returned
        public Face DissolveEdge(HalfEdge e)
        {
            if (e.IsBoundary || e.Twin.IsBoundary)
                throw new InvalidOperationException("Can't dissolve edge with less than two incident faces");

            if (e.IncidentFace == e.Twin.IncidentFace)
                throw new InvalidOperationException("Can't dissolve edge with the same face on both sides");

            Face newFace = e.IncidentFace;
            newFace.Edge = e.Next;
            Face oldFace = e.Twin.IncidentFace;

            //redirect links to e and e.twin
            e.Previous.SetNext(e.Twin.Next);
            e.Next.SetPrevious(e.Twin.Previous);

            //Set new incident edges of vertices
            e.Origin.IncidentEdge = e.Previous.Twin;
            e.Twin.Origin.IncidentEdge = e.Next;

            foreach (var edge in newFace.Edges())
            {
                edge.IncidentFace = newFace;
            }

            //Remove disconnected elements
            RemoveFace(oldFace);
            RemoveHalfEdge(e.Twin);
            RemoveHalfEdge(e);
            return newFace;
        }

        // Checks every structural invariant and returns a description of each problem found.
        // An empty list means the mesh is consistent.
        public List<string> Validate()
        {
            var errors = new List<string>();
            var outgoingCount = new Dictionary<Vertex, int>();

            for (int i = 0; i < m_HalfEdges.Count; i++)
            {
                HalfEdge e = m_HalfEdges[i];
                string name = "Edge " + e.ID;

                if (e.Index != i) errors.Add(name + " has index " + e.Index + " but is stored at " + i);

                if (e.Origin == null) errors.Add(name + " has no origin");
                else
                {
                    if (e.Origin.Index < 0) errors.Add(name + " has an origin that is not in the mesh");
                    outgoingCount.TryGetValue(e.Origin, out int count);
                    outgoingCount[e.Origin] = count + 1;
                }

                if (e.Twin == null) errors.Add(name + " is missing a twin");
                else
                {
                    if (e.Twin == e) errors.Add(name + " is its own twin");
                    if (e.Twin.Twin != e) errors.Add(name + " has an incorrect twin link");
                    if (e.Twin.Index < 0) errors.Add(name + " has a twin that was removed");
                }

                if (e.Next == null) errors.Add(name + " is missing Next");
                else
                {
                    if (e.Next.Previous != e) errors.Add(name + ".Next.Previous does not point back");
                    if (e.Next.Index < 0) errors.Add(name + " has a Next that was removed");
                    if (e.Next.IncidentFace != e.IncidentFace) errors.Add(name + " and its Next have different faces");
                    if (e.Twin != null && e.Next.Origin != e.Twin.Origin) errors.Add(name + ".Next does not start where it ends");
                }

                if (e.Previous == null) errors.Add(name + " is missing Previous");
                else
                {
                    if (e.Previous.Next != e) errors.Add(name + ".Previous.Next does not point back");
                    if (e.Previous.Index < 0) errors.Add(name + " has a Previous that was removed");
                }

                if (e.IncidentFace != null && e.IncidentFace.Index < 0) errors.Add(name + " has a face that was removed");
            }

            for (int i = 0; i < m_Faces.Count; i++)
            {
                Face f = m_Faces[i];
                string name = "Face " + f.ID;

                if (f.Index != i) errors.Add(name + " has index " + f.Index + " but is stored at " + i);

                if (f.Edge == null)
                {
                    errors.Add(name + " has no edge");
                    continue;
                }

                if (f.Edge.IncidentFace != f) errors.Add(name + ".Edge belongs to another face");
                if (f.Edge.Index < 0) errors.Add(name + ".Edge was removed");

                try
                {
                    int sides = f.GetSideCount();
                    if (sides < 3) errors.Add(name + " has only " + sides + " sides");
                }
                catch (Exception ex)
                {
                    errors.Add(name + ": " + ex.Message);
                }
            }

            for (int i = 0; i < m_Vertices.Count; i++)
            {
                Vertex v = m_Vertices[i];
                string name = "Vertex " + v.ID;

                if (v.Index != i) errors.Add(name + " has index " + v.Index + " but is stored at " + i);

                if (v.IncidentEdge == null)
                {
                    errors.Add(name + " doesn't have an incident edge");
                    continue;
                }

                if (v.IncidentEdge.Origin != v) errors.Add(name + ".IncidentEdge starts at another vertex");
                if (v.IncidentEdge.Index < 0) errors.Add(name + ".IncidentEdge was removed");

                // A fan that misses some outgoing edges means the vertex is non-manifold or badly linked
                try
                {
                    int fanCount = v.OutgoingEdges().Count();
                    outgoingCount.TryGetValue(v, out int expected);
                    if (fanCount != expected)
                        errors.Add(name + " reaches " + fanCount + " of its " + expected + " outgoing edges by walking around it");
                }
                catch (Exception ex)
                {
                    errors.Add(name + ": " + ex.Message);
                }
            }

            return errors;
        }

        // Inserts a vertex at the middle of the edge and returns it
        public Vertex SplitEdge(HalfEdge e)
        {
            Vertex a = e.Origin;
            Vertex b = e.Twin.Origin;

            Vector3 newPos = Vector3.Lerp(a.Position, b.Position, 0.5f);

            Vertex v = AddVertex(newPos);

            var (e1, e2) = AddEdge(v, b);
            e1.IncidentFace = e.IncidentFace;
            e2.IncidentFace = e.Twin.IncidentFace;

            // e.Twin now starts at v, so b needs a different incident edge
            e.Twin.Origin = v;
            b.IncidentEdge = e2;

            e1.SetNext(e.Next);
            e1.SetPrevious(e);
            e2.SetPrevious(e.Twin.Previous);
            e2.SetNext(e.Twin);

            return v;
        }

        // Splits every edge in the middle and returns the vertices that were created
        public HashSet<Vertex> SplitAllEdges()
        {
            // Twins are created together, so the lower ID picks exactly one half-edge per edge
            var toSplit = m_HalfEdges.Where(e => e.ID < e.Twin.ID).ToList();

            var midpoints = new HashSet<Vertex>();
            foreach (var e in toSplit)
            {
                midpoints.Add(SplitEdge(e));
            }

            return midpoints;
        }

        // Relaxes all inner vertices once. Positions are updated in place, so vertices later in the list
        // see the already moved positions of earlier ones.
        public void RelaxVertices()
        {
            foreach (var v in m_Vertices)
            {
                if (!v.IsOuter()) v.Relax();
            }
        }

        // Connects aOut.Origin and bOut.Origin with a new edge, splitting their common face in two.
        // Returns (edge from b to a, edge from a to b). The first one is in the new face.
        public (HalfEdge, HalfEdge) SplitFace(HalfEdge aOut, HalfEdge bOut)
        {
            if (aOut.IncidentFace != bOut.IncidentFace)
                throw new InvalidOperationException("Can't split if edges do not have common face");

            if (aOut.IsBoundary)
                throw new InvalidOperationException("Can't split the outside of the mesh");

            if (aOut.Next == bOut || aOut.Previous == bOut)
                throw new InvalidOperationException("Can't split neighboring vertices");

            Vertex a = aOut.Origin;
            Vertex b = bOut.Origin;

            Face commonFace = aOut.IncidentFace;

            var (e1, e2) = AddEdge(b, a);

            Face newFace = AddFace(e1);
            commonFace.Edge = e2;
            e2.IncidentFace = commonFace;

            aOut.Previous.SetNext(e2);
            bOut.Previous.SetNext(e1);
            e1.SetNext(aOut);
            e2.SetNext(bOut);

            foreach (var edge in newFace.Edges())
            {
                edge.IncidentFace = newFace;
            }

            return (e1, e2);
        }

        // For each face, the edges starting at a midpoint created by SplitAllEdges, in winding order
        private List<List<HalfEdge>> CollectMidpointEdges(HashSet<Vertex> midpoints)
        {
            var result = new List<List<HalfEdge>>();
            foreach (var face in m_Faces)
            {
                List<HalfEdge> faceEdges = face.Edges().ToList();
                List<HalfEdge> midpointEdges = faceEdges.Where(e => midpoints.Contains(e.Origin)).ToList();

                if (midpointEdges.Count * 2 != faceEdges.Count)
                {
                    throw new InvalidOperationException(
                        "Face " + face.ID + " has " + faceEdges.Count + " sides but " + midpointEdges.Count + " midpoints");
                }

                result.Add(midpointEdges);
            }

            return result;
        }

        // Splits every n-sided face into n quads around a vertex at its center
        public void QuadSubdivide()
        {
            var newFaces = CollectMidpointEdges(SplitAllEdges());

            for (int i = 0; i < newFaces.Count; i++)
            {
                var edgesToRedirect = newFaces[i];

                Face face = edgesToRedirect[0].IncidentFace;

                Vector3 centerPos = face.GetCenter();

                Vertex centerVert = AddVertex(centerPos);


                //Create first face
                HalfEdge currentEdge = edgesToRedirect[0];
                Vertex firstVert = currentEdge.Origin;
                Vertex thirdVert = edgesToRedirect[1].Origin;

                var (e1, e2) = AddEdge(centerVert, firstVert);
                var (e3, e4) = AddEdge(thirdVert, centerVert);

                //Set face incidence
                Face newFace = AddFace(e1);
                e3.IncidentFace = newFace;
                e2.IncidentFace = face;
                face.Edge = e4;
                e4.IncidentFace = face;
                currentEdge.IncidentFace = newFace;
                currentEdge.Next.IncidentFace = newFace;
                e2.SetNext(e4);


                e1.SetPrevious(e3);
                currentEdge.Previous.SetNext(e2);
                currentEdge.Next.Next.SetPrevious(e4);
                currentEdge.SetPrevious(e1);
                currentEdge.Next.SetNext(e3);


                //Split remaining. centerOut is always an edge from the center in the part not yet split.
                HalfEdge centerOut = e4;
                for (int j = 2; j < edgesToRedirect.Count; j++)
                {
                    (_, centerOut) = SplitFace(centerOut, edgesToRedirect[j]);
                }
            }
        }

        // Cuts off every corner of every face. Triangles become four triangles; an n-gon becomes
        // n corner triangles around a smaller n-gon.
        public void TriangleSubdivide()
        {
            var newFaces = CollectMidpointEdges(SplitAllEdges());

            for (int i = 0; i < newFaces.Count; i++)
            {
                var edgesToRedirect = newFaces[i];

                for (int j = 0; j < newFaces[i].Count; j++)
                {
                    HalfEdge currentEdge = edgesToRedirect[j];
                    Vertex firstVert = edgesToRedirect[(j + 1) % newFaces[i].Count].Origin;
                    Vertex secondVert = currentEdge.Origin;
                    var (e1, e2) = AddEdge(firstVert, secondVert);
                    Face newFace = AddFace(e1);
                    e2.IncidentFace = currentEdge.IncidentFace;

                    currentEdge.IncidentFace.Edge = e2;
                    currentEdge.IncidentFace = newFace;
                    currentEdge.Next.IncidentFace = newFace;
                    currentEdge.Next.Next.SetPrevious(e2);
                    currentEdge.Previous.SetNext(e2);
                    currentEdge.Next.SetNext(e1);
                    currentEdge.SetPrevious(e1);
                }
            }

        }

        // Point on the XZ plane at the given angle, matching Quaternion.Euler(0, degrees, 0) * Vector3.right
        private static Vector3 PointOnCircle(float degrees, float radius)
        {
            float radians = degrees * Mathf.Deg2Rad;
            return new Vector3(Mathf.Cos(radians), 0, -Mathf.Sin(radians)) * radius;
        }

        public static HalfEdgeMesh CreateTriangle()
        {
            HalfEdgeMesh m = new HalfEdgeMesh();

            Vertex v1 = m.AddVertex(PointOnCircle(0, 1));
            Vertex v2 = m.AddVertex(PointOnCircle(120, 1));
            Vertex v3 = m.AddVertex(PointOnCircle(240, 1));

            var (e1, e2) = m.AddEdge(v1, v2);
            var (e3,e4) = m.AddEdge(v2, v3);
            var (e5, e6) = m.AddEdge(v3, v1);

            e1.SetNext(e3);
            e3.SetNext(e5);
            e5.SetNext(e1);

            e2.SetNext(e6);
            e6.SetNext(e4);
            e4.SetNext(e2);

            Face f = m.AddFace(e1);
            e1.IncidentFace = f;
            e3.IncidentFace = f;
            e5.IncidentFace = f;

            return m;
        }

        public static HalfEdgeMesh CreateQuad(Vector3 origin, float width, float height)
        {
            float halfWidth = width * 0.5f;
            float halfHeight = height * 0.5f;

            Vector3 v0 = new Vector3(-halfWidth, 0, halfHeight) + origin;
            Vector3 v1 = new Vector3(halfWidth, 0, halfHeight) + origin;
            Vector3 v2 = new Vector3(halfWidth, 0, -halfHeight) + origin;
            Vector3 v3 = new Vector3(-halfWidth, 0, -halfHeight) + origin;

            return CreateQuad(v0, v1, v2, v3);
        }

        public static HalfEdgeMesh CreateQuad(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3)
        {
            HalfEdgeMesh m = new HalfEdgeMesh();

            Vertex v0 = m.AddVertex(p0);
            Vertex v1 = m.AddVertex(p1);
            Vertex v2 = m.AddVertex(p2);
            Vertex v3 = m.AddVertex(p3);


            var (e1,e2) =  m.AddEdge(v0, v1);
            var (e3,e4) = m.AddEdge(v1, v2);
            var (e5,e6) = m.AddEdge(v2, v3);
            var (e7,e8) = m.AddEdge(v3, v0);

            e1.SetNext(e3);
            e3.SetNext(e5);
            e5.SetNext(e7);
            e7.SetNext(e1);

            // The outer loop runs the opposite way around
            e2.SetNext(e8);
            e8.SetNext(e6);
            e6.SetNext(e4);
            e4.SetNext(e2);

            Face f = m.AddFace(e1);
            e1.IncidentFace = f;
            e3.IncidentFace = f;
            e5.IncidentFace = f;
            e7.IncidentFace = f;

            return m;
        }

        // A regular polygon on the XZ plane made of triangles around a center vertex
        public static HalfEdgeMesh CreatePolygon(int sides, float radius)
        {
            if (sides < 3) throw new ArgumentOutOfRangeException(nameof(sides), "A polygon needs at least 3 sides");

            HalfEdgeMesh m = new HalfEdgeMesh();
            Vertex center = m.AddVertex(Vector3.zero);

            //Create vertex "star"
            for (int i = 0; i < sides; i++)
            {
                Vertex v = m.AddVertex(PointOnCircle(360f / sides * i, radius));

                //Connect to center
                m.AddEdge(center, v);
            }

            HalfEdge firstOuter = null;
            HalfEdge previousOuter = null;
            //Connect outer edge
            for (int i = 0; i < sides; i++)
            {
                var (inner, outer) = m.AddEdge(m.m_Vertices[i + 1], m.m_Vertices[(i + 1) % sides + 1]);

                HalfEdge outEdge = m.m_HalfEdges[i * 2]; //edge going out from the center
                HalfEdge inEdge = m.m_HalfEdges[(i * 2 + 3)%(sides*2)]; //Edge going in to the center

                outEdge.Next = inner;
                outEdge.Previous = inEdge;

                inner.Next = inEdge;
                inner.Previous = outEdge;

                inEdge.Next = outEdge;
                inEdge.Previous = inner;

                Face face = m.AddFace(outEdge);
                inner.IncidentFace = face;
                outEdge.IncidentFace = face;
                inEdge.IncidentFace = face;

                outer.Next = null;
                outer.Previous = null;

                if (previousOuter == null)
                {
                    firstOuter = outer;
                }
                else
                {
                    outer.Next = previousOuter;
                    previousOuter.Previous = outer;
                }

                previousOuter = outer;
            }

            firstOuter.Next = previousOuter;
            previousOuter.Previous = firstOuter;

            return m;
        }

        // Builds a half-edge mesh from the Triangles and Quads submeshes of a Unity mesh.
        // Vertices closer than weldDistance are merged, since Unity meshes duplicate vertices along
        // UV and normal seams. Pass 0 to disable welding. Faces that collapse after welding are skipped.
        // The mesh must be manifold with consistent winding.
        public static HalfEdgeMesh FromUnityMesh(UnityEngine.Mesh unityMesh, float weldDistance = 1e-5f)
        {
            HalfEdgeMesh m = new HalfEdgeMesh();

            Vector3[] positions = unityMesh.vertices;
            var unityToVertex = new Vertex[positions.Length];
            var welded = new Dictionary<Vector3Int, Vertex>();

            Vertex GetVertex(int unityIndex)
            {
                if (unityToVertex[unityIndex] != null) return unityToVertex[unityIndex];

                Vector3 p = positions[unityIndex];
                Vertex v;
                if (weldDistance > 0)
                {
                    Vector3Int cell = Vector3Int.RoundToInt(p / weldDistance);
                    if (!welded.TryGetValue(cell, out v))
                    {
                        v = m.AddVertex(p);
                        welded.Add(cell, v);
                    }
                }
                else
                {
                    v = m.AddVertex(p);
                }

                unityToVertex[unityIndex] = v;
                return v;
            }

            var directedEdges = new Dictionary<(int, int), HalfEdge>();

            for (int subMesh = 0; subMesh < unityMesh.subMeshCount; subMesh++)
            {
                int sides;
                switch (unityMesh.GetTopology(subMesh))
                {
                    case MeshTopology.Triangles: sides = 3; break;
                    case MeshTopology.Quads: sides = 4; break;
                    default:
                        throw new ArgumentException("Submesh " + subMesh + " has topology " + unityMesh.GetTopology(subMesh) + ". Only Triangles and Quads are supported.");
                }

                int[] indices = unityMesh.GetIndices(subMesh);
                var faceVertices = new List<Vertex>(sides);

                for (int i = 0; i < indices.Length; i += sides)
                {
                    faceVertices.Clear();
                    for (int j = 0; j < sides; j++)
                    {
                        Vertex v = GetVertex(indices[i + j]);
                        if (!faceVertices.Contains(v)) faceVertices.Add(v);
                    }

                    if (faceVertices.Count < 3) continue;

                    var faceEdges = new List<HalfEdge>(faceVertices.Count);
                    for (int j = 0; j < faceVertices.Count; j++)
                    {
                        Vertex a = faceVertices[j];
                        Vertex b = faceVertices[(j + 1) % faceVertices.Count];

                        if (directedEdges.TryGetValue((a.Index, b.Index), out HalfEdge e))
                        {
                            // Created earlier as the twin of a neighbouring face's edge
                            if (!e.IsBoundary)
                                throw new ArgumentException("Mesh is not manifold or has inconsistent winding at edge " + a.Index + "-" + b.Index);
                        }
                        else
                        {
                            var (ab, ba) = m.AddEdge(a, b);
                            directedEdges.Add((a.Index, b.Index), ab);
                            directedEdges.Add((b.Index, a.Index), ba);
                            e = ab;
                        }

                        faceEdges.Add(e);
                    }

                    Face f = m.AddFace(faceEdges[0]);
                    for (int j = 0; j < faceEdges.Count; j++)
                    {
                        faceEdges[j].IncidentFace = f;
                        faceEdges[j].SetNext(faceEdges[(j + 1) % faceEdges.Count]);
                    }
                }
            }

            // Link the boundary: each boundary half-edge continues with the boundary half-edge leaving its end
            var boundaryFrom = new Dictionary<Vertex, HalfEdge>();
            foreach (var e in m.m_HalfEdges)
            {
                if (!e.IsBoundary) continue;
                if (boundaryFrom.ContainsKey(e.Origin))
                    throw new ArgumentException("Mesh is not manifold at vertex " + e.Origin.Index + " (faces only touch at a corner)");
                boundaryFrom.Add(e.Origin, e);
            }

            foreach (var e in boundaryFrom.Values)
            {
                e.SetNext(boundaryFrom[e.Destination]);
            }

            return m;
        }

        // Builds a Unity mesh with every face fan-triangulated from its first vertex.
        // Keeps the clockwise winding, so faces are visible from the same side. Assumes convex faces.
        public UnityEngine.Mesh ToUnityMesh()
        {
            var positions = new Vector3[m_Vertices.Count];
            for (int i = 0; i < m_Vertices.Count; i++)
            {
                positions[i] = m_Vertices[i].Position;
            }

            var triangles = new List<int>();
            var faceVertices = new List<Vertex>();
            foreach (var face in m_Faces)
            {
                faceVertices.Clear();
                faceVertices.AddRange(face.Vertices());
                for (int i = 1; i < faceVertices.Count - 1; i++)
                {
                    triangles.Add(faceVertices[0].Index);
                    triangles.Add(faceVertices[i].Index);
                    triangles.Add(faceVertices[i + 1].Index);
                }
            }

            var mesh = new UnityEngine.Mesh();
            if (positions.Length > ushort.MaxValue) mesh.indexFormat = IndexFormat.UInt32;
            mesh.SetVertices(positions);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        public static HalfEdgeMesh CreateNonUniformRandomGrid(float radius, int subdivisions, int relaxCount, int seed)
        {
            HalfEdgeMesh mesh = CreatePolygon(6, radius);
            for(int i = 0; i < subdivisions; i++) mesh.TriangleSubdivide();

            mesh.DissolveToQuads(seed);
            mesh.QuadSubdivide();
            for(int i = 0; i < relaxCount; i++) mesh.RelaxVertices();

            return mesh;
        }

        // Assumes mesh of tris. Dissolves random edges to create quads while it can.
        // Deterministic for a given seed and mesh, and doesn't touch UnityEngine.Random.
        public void DissolveToQuads(int seed = 0)
        {
            var random = new System.Random(seed);

            // One half-edge per inner edge, in random order
            var candidates = m_HalfEdges.Where(e => e.ID < e.Twin.ID && !e.IsBoundary && !e.Twin.IsBoundary).ToList();
            for (int i = candidates.Count - 1; i > 0; i--)
            {
                int j = random.Next(i + 1);
                (candidates[i], candidates[j]) = (candidates[j], candidates[i]);
            }

            var merged = new HashSet<Face>();
            foreach (var e in candidates)
            {
                if (merged.Contains(e.IncidentFace) || merged.Contains(e.Twin.IncidentFace)) continue;

                merged.Add(DissolveEdge(e));
            }
        }
    }
}
