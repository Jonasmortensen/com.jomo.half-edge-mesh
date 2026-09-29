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

        // source is the face the new one is derived from, if any. The new face gets a clone of its data.
        private Face AddFace(HalfEdge incidentEdge, Face source = null)
        {
            Face f = new Face(incidentEdge);
            f.Data = source?.Data?.Clone();
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

        // Removes the edge and merges its two faces into e.IncidentFace, which is returned. Its Data becomes its own
        // data merged with the other face's (see IMeshData.Merge), or the other face's if it had none.
        public Face DissolveEdge(HalfEdge e)
        {
            if (e.IsBoundary || e.Twin.IsBoundary)
                throw new InvalidOperationException("Can't dissolve edge with less than two incident faces");

            if (e.IncidentFace == e.Twin.IncidentFace)
                throw new InvalidOperationException("Can't dissolve edge with the same face on both sides");

            Face newFace = e.IncidentFace;
            newFace.Edge = e.Next;
            Face oldFace = e.Twin.IncidentFace;

            if (oldFace.Data != null) newFace.Data = newFace.Data != null ? newFace.Data.Merge(oldFace.Data) : oldFace.Data;

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

        // Moves every vertex along the line from origin through it, so it lies on the sphere with the given
        // radius around origin. A vertex exactly at origin has no direction and is left where it is.
        public void SphereProject(float radius, Vector3 origin)
        {
            foreach (var v in m_Vertices)
            {
                Vector3 offset = v.Position - origin;
                if (offset == Vector3.zero) continue;

                v.Position = origin + offset.normalized * radius;
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

            Face newFace = AddFace(e1, commonFace);
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

        // Adds a vertex at the center of the face and connects it to every corner, turning an n-sided face into
        // n triangles. The face's own edges are kept, so neighbouring faces are not affected. Returns the new vertex.
        public Vertex PokeFace(Face face)
        {
            List<HalfEdge> sides = face.Edges().ToList();
            int n = sides.Count;

            Vertex center = AddVertex(face.GetCenter());

            var toCorner = new HalfEdge[n];
            var toCenter = new HalfEdge[n];
            for (int i = 0; i < n; i++)
            {
                (toCorner[i], toCenter[i]) = AddEdge(center, sides[i].Origin);
            }

            // Triangle i: along side i, up its end corner's spoke to the center, down its start corner's spoke
            for (int i = 0; i < n; i++)
            {
                HalfEdge side = sides[i];
                HalfEdge up = toCenter[(i + 1) % n];
                HalfEdge down = toCorner[i];

                side.SetNext(up);
                up.SetNext(down);
                down.SetNext(side);

                // The first triangle keeps the original face
                Face triangle = face;
                if (i == 0) face.Edge = side;
                else triangle = AddFace(side, face);

                side.IncidentFace = triangle;
                up.IncidentFace = triangle;
                down.IncidentFace = triangle;
            }

            return center;
        }

        // Pokes every face, see PokeFace. Quads become four triangles.
        public void PokeFaces()
        {
            foreach (var face in m_Faces.ToList())
            {
                PokeFace(face);
            }
        }

        // Splits every quad into two triangles. Triangles are left as they are; only quads are supported so far.
        // Each quad is split along the diagonal whose triangles have the biggest minimum angle, and on a tie
        // the smallest maximum angle.
        public void Triangulate()
        {
            var quads = new List<Face>();
            foreach (var face in m_Faces)
            {
                int sides = face.GetSideCount();
                if (sides > 4)
                    throw new NotSupportedException("Face " + face.ID + " has " + sides + " sides. Only triangles and quads can be triangulated.");
                if (sides == 4) quads.Add(face);
            }

            foreach (var quad in quads)
            {
                HalfEdge e0 = quad.Edge;
                HalfEdge e1 = e0.Next;
                HalfEdge e2 = e1.Next;
                HalfEdge e3 = e2.Next;

                if (PrefersSecondDiagonal(e0.Origin.Position, e1.Origin.Position, e2.Origin.Position, e3.Origin.Position))
                    SplitFace(e1, e3);
                else
                    SplitFace(e0, e2);
            }
        }

        // For the quad a, b, c, d: whether the diagonal b-d gives better triangles than a-c
        private static bool PrefersSecondDiagonal(Vector3 a, Vector3 b, Vector3 c, Vector3 d)
        {
            var (minAC, maxAC) = AngleRange(a, b, c, d);
            var (minBD, maxBD) = AngleRange(b, c, d, a);

            const float tolerance = 1e-3f; // degrees
            if (Mathf.Abs(minAC - minBD) > tolerance) return minBD > minAC;
            return maxBD < maxAC - tolerance;
        }

        // Smallest and largest corner angle, in degrees, of the triangles a, b, c and a, c, d
        private static (float, float) AngleRange(Vector3 a, Vector3 b, Vector3 c, Vector3 d)
        {
            float[] angles =
            {
                Vector3.Angle(b - a, c - a), Vector3.Angle(a - b, c - b), Vector3.Angle(a - c, b - c),
                Vector3.Angle(c - a, d - a), Vector3.Angle(a - c, d - c), Vector3.Angle(a - d, c - d),
            };

            return (angles.Min(), angles.Max());
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
                Face newFace = AddFace(e1, face);
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
                    Face newFace = AddFace(e1, currentEdge.IncidentFace);
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

                    m.AddPolygon(faceVertices, directedEdges);
                }
            }

            m.LinkBoundary();
            return m;
        }

        // Adds a face through the given vertices in winding order, reusing half-edges already created as
        // twins of neighbouring faces. directedEdges maps (origin index, destination index) to half-edges
        // and must be shared by all faces of the mesh. Call LinkBoundary once all faces are added.
        private Face AddPolygon(List<Vertex> faceVertices, Dictionary<(int, int), HalfEdge> directedEdges)
        {
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
                    var (ab, ba) = AddEdge(a, b);
                    directedEdges.Add((a.Index, b.Index), ab);
                    directedEdges.Add((b.Index, a.Index), ba);
                    e = ab;
                }

                faceEdges.Add(e);
            }

            Face f = AddFace(faceEdges[0]);
            for (int j = 0; j < faceEdges.Count; j++)
            {
                faceEdges[j].IncidentFace = f;
                faceEdges[j].SetNext(faceEdges[(j + 1) % faceEdges.Count]);
            }

            return f;
        }

        // A new mesh containing copies of the given faces of this mesh, which is left unchanged.
        // Faces that only touch at a corner get separate copies of that vertex, since a half-edge mesh
        // can't represent a vertex joining two otherwise unconnected fans.
        public HalfEdgeMesh CopyFaces(IEnumerable<Face> faces)
        {
            return CopyFaces(faces, out _);
        }

        // CopyFaces that also gives, for every vertex of the copy, the vertex of this mesh it copies. Several copies can
        // share an original, where faces only touch at a corner. Later operations on the copy keep its vertices, so the
        // map still tells which of its vertices came from this mesh after it has been subdivided.
        public HalfEdgeMesh CopyFaces(IEnumerable<Face> faces, out Dictionary<Vertex, Vertex> originals)
        {
            var selected = new HashSet<Face>();
            foreach (var face in faces)
            {
                if (face == null || face.Index < 0 || m_Faces[face.Index] != face)
                    throw new ArgumentException("Face does not belong to this mesh");
                selected.Add(face);
            }

            HalfEdgeMesh copy = new HalfEdgeMesh();
            var corners = new Dictionary<(Vertex, Face), Vertex>();
            var directedEdges = new Dictionary<(int, int), HalfEdge>();
            var faceVertices = new List<Vertex>();

            // Walk m_Faces rather than the set, so the copy's order doesn't depend on hashing
            foreach (var face in m_Faces)
            {
                if (!selected.Contains(face)) continue;

                faceVertices.Clear();
                foreach (var v in face.Vertices())
                {
                    if (!corners.ContainsKey((v, face))) copy.AddVertexCopies(v, selected, corners);
                    faceVertices.Add(corners[(v, face)]);
                }

                Face faceCopy = copy.AddPolygon(faceVertices, directedEdges);
                faceCopy.Data = face.Data?.Clone();
            }

            copy.LinkBoundary();

            originals = new Dictionary<Vertex, Vertex>();
            foreach (var pair in corners) originals[pair.Value] = pair.Key.Item1;

            return copy;
        }

        // Adds copies of v (a vertex of another mesh) to this mesh: one for each run of selected faces that are
        // connected by edges around v. Records in corners which copy each selected face around v uses.
        private void AddVertexCopies(Vertex v, HashSet<Face> selected, Dictionary<(Vertex, Face), Vertex> corners)
        {
            // Consecutive faces in the fan share an edge through v. Boundary gaps give null faces.
            List<Face> fan = v.OutgoingEdges().Select(e => e.IncidentFace).ToList();

            int start = fan.FindIndex(f => !selected.Contains(f));
            if (start < 0)
            {
                // Every face around v is selected, so they all share one copy
                Vertex shared = AddVertex(v.Position);
                foreach (var f in fan) corners[(v, f)] = shared;
                return;
            }

            // Starting right after an unselected face means no run wraps around the end of the list
            Vertex current = null;
            for (int i = 1; i <= fan.Count; i++)
            {
                Face f = fan[(start + i) % fan.Count];
                if (!selected.Contains(f))
                {
                    current = null;
                    continue;
                }

                if (current == null) current = AddVertex(v.Position);
                corners[(v, f)] = current;
            }
        }

        // Links the boundary: each boundary half-edge continues with the boundary half-edge leaving its end.
        // Closed meshes have no boundary, so this does nothing for them.
        private void LinkBoundary()
        {
            var boundaryFrom = new Dictionary<Vertex, HalfEdge>();
            foreach (var e in m_HalfEdges)
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
        }

        // A closed icosahedron centered on the origin, with all 12 vertices at the given radius.
        // Faces are wound clockwise seen from outside, so their normals point outwards.
        public static HalfEdgeMesh CreateIcosahedron(float radius)
        {
            HalfEdgeMesh m = new HalfEdgeMesh();

            // Three orthogonal golden rectangles
            float t = (1f + Mathf.Sqrt(5f)) / 2f;
            Vector3[] corners =
            {
                new Vector3(-1, t, 0), new Vector3(1, t, 0), new Vector3(-1, -t, 0), new Vector3(1, -t, 0),
                new Vector3(0, -1, t), new Vector3(0, 1, t), new Vector3(0, -1, -t), new Vector3(0, 1, -t),
                new Vector3(t, 0, -1), new Vector3(t, 0, 1), new Vector3(-t, 0, -1), new Vector3(-t, 0, 1),
            };

            int[] triangles =
            {
                0, 11, 5,   0, 5, 1,    0, 1, 7,    0, 7, 10,   0, 10, 11,
                1, 5, 9,    5, 11, 4,   11, 10, 2,  10, 7, 6,   7, 1, 8,
                3, 9, 4,    3, 4, 2,    3, 2, 6,    3, 6, 8,    3, 8, 9,
                4, 9, 5,    2, 4, 11,   6, 2, 10,   8, 6, 7,    9, 8, 1,
            };

            foreach (var corner in corners)
            {
                m.AddVertex(corner.normalized * radius);
            }

            var directedEdges = new Dictionary<(int, int), HalfEdge>();
            for (int i = 0; i < triangles.Length; i += 3)
            {
                var faceVertices = new List<Vertex>
                {
                    m.m_Vertices[triangles[i]], m.m_Vertices[triangles[i + 1]], m.m_Vertices[triangles[i + 2]]
                };
                m.AddPolygon(faceVertices, directedEdges);
            }

            m.LinkBoundary();
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
