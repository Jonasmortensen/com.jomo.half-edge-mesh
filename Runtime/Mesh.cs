using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Random = UnityEngine.Random;


namespace Jomo.HalfEdgeMesh
{
    public class Mesh
    {
        //TODO: These should probably be linked list to get O(1) removal
        public List<HalfEdge> HalfEdges;
        public List<Vertex> Vertices;
        public List<Face> Faces;

        private int HalfEdgeId;
        private int FaceId;
        private int VertexId;

        private Mesh()
        {
            HalfEdgeId = 0;
            FaceId = 0;
            Faces = new List<Face>();
            HalfEdges = new List<HalfEdge>();
            Vertices = new List<Vertex>();
        }
        
        private HalfEdge AddHalfEdge(Vertex origin)
        {
            HalfEdge e = new HalfEdge(origin, HalfEdgeId);
            origin.IncidentEdge = e;
            HalfEdges.Add(e);
            HalfEdgeId++;
            return e;
        }

        private Face AddFace(HalfEdge incidentEdge)
        {
            Face f = new Face(incidentEdge);
            //f.id = FaceId++;
            Faces.Add(f);
            incidentEdge.IncidentFace = f;
            return f;
        }

        private Vertex AddVertex(Vector3 position)
        {
            Vertex v = new Vertex(position);
            v.id = VertexId;
            Vertices.Add(v);
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

        public HalfEdge FindEdge(Vertex a, Vertex b)
        {
            if (a == b) throw new Exception("Same vertex bro!");
            
            HalfEdge start = a.IncidentEdge;
            HalfEdge current = start;
            
            do
            {
                if (current.Origin == b) return current;

                if (current.Previous == null) return null;
                
                current = current.Previous.Twin;
            } while (current != start);
            return null;
        }

        public Face DissolveEdge(HalfEdge e)
        {
            if (e.IncidentFace == null || e.Twin.IncidentFace == null)
                throw new Exception("Can't dissolve edge with less than two incident faces");
            
            Face newFace = e.IncidentFace;
            newFace.Edge = e.Next;
            Face oldFace = e.Twin.IncidentFace;

            //redirect links to e and e.twin
            e.Previous.SetNext(e.Twin.Next);
            e.Next.SetPrevious(e.Twin.Previous);

            //Set new incident edges of vertices
            e.Origin.IncidentEdge = e.Previous.Twin;
            e.Twin.Origin.IncidentEdge = e.Next;

            //Iterate edges in new face
            var start = e.Next;
            var curent = e.Next;
            do
            {
                curent.IncidentFace = newFace;
                curent = curent.Next;
            } while (start != curent);
            
            //Remove disconnected elements
            Faces.Remove(oldFace); //TODO: This is O(n). Could be constant with linked list?
            HalfEdges.Remove(e);
            HalfEdges.Remove(e.Twin);
            return newFace;
        }

        public void ValidateEdges()
        {
            Debug.Log("Edge count: " + HalfEdges.Count);
            for (int i = 0; i < HalfEdges.Count; i++)
            {
                HalfEdge e = HalfEdges[i];
                if (e.Twin == null || e.Next == null || e.Previous == null)
                {
                    e.Validate();
                }
            }
        }
        

        public void ValidateVertices()
        {
            for (int i = 0; i < Vertices.Count; i++)
            {
                var v = Vertices[i];
                if(v.IncidentEdge == null) Debug.LogError("Vertex " + v.id + " doesn't have an incident edge");
            }
        }
        
        public Vertex SplitEdge(HalfEdge e)
        {
            Vertex a = e.Origin;
            Vertex b = e.Twin.Origin;
            
            Vector3 newPos = Vector3.Lerp(a.Position, b.Position, 0.5f);

            Vertex v = AddVertex(newPos);

            var (e1, e2) = AddEdge(v, b);
            e1.IncidentFace = e.IncidentFace;
            e2.IncidentFace = e.Twin.IncidentFace;
            
            e.Twin.Origin = v;
            
            e1.SetNext(e.Next);
            e1.SetPrevious(e);
            e2.SetPrevious(e.Twin.Previous);
            e2.SetNext(e.Twin);

            return v;
        }

        public void SplitAllEdges()
        {
            int edgeCountAtSubdivide = HalfEdges.Count;
            
            for (int i = 0; i < edgeCountAtSubdivide; i += 2)
            {
                SplitEdge(HalfEdges[i]);
            }
        }

        public void RelaxVertices()
        {
            foreach (var v in Vertices)
            {
                if (!v.IsOuter()) v.Relax();
            }
        }
        
        public (HalfEdge, HalfEdge) SplitFace(HalfEdge aOut, HalfEdge bOut)
        {
            if (aOut.IncidentFace != bOut.IncidentFace)
                throw new Exception("Can't split if edges do not have common face");
            
            if (aOut.Next == bOut || aOut.Previous == bOut) 
                throw new Exception("Can't split neighboring vertices");
            
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

            HalfEdge current = e1, start = e1;

            do
            {
                current.IncidentFace = newFace;
                current = current.Next;
            } while (start != current);

            return (e1, e2);
        }

        public void QuadSubdivide()
        {
            SplitAllEdges();
            
            List<List<HalfEdge>> newFaces = new List<List<HalfEdge>>();
            for (int i = 0; i < Faces.Count; i++)
            {
                Face face = Faces[i];
                List<HalfEdge> edgesToRedirect = new List<HalfEdge>();
                var startEdge = face.Edge;
                var currentEdge = face.Edge;
                
                do
                {
                    if(currentEdge.Origin.IsStraight()) edgesToRedirect.Add(currentEdge);
                    currentEdge = currentEdge.Next;
                } while (startEdge != currentEdge);
                if (edgesToRedirect.Count != 3 && edgesToRedirect.Count != 4)
                {
                    Debug.LogError("I found " + edgesToRedirect.Count + " to redirect");
                }

                newFaces.Add(edgesToRedirect);
            }

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
                
                
                //Split remaining
                var (_, lastSplit) = SplitFace(e4, edgesToRedirect[2]);
                if (edgesToRedirect.Count() == 4)
                {
                    SplitFace(edgesToRedirect[3], lastSplit);
                }
            }
        }

        //TODO: Simplify this by using edge split
        public void TriangleSubdivide()
        {
            SplitAllEdges();
            
            List<List<HalfEdge>> newFaces = new List<List<HalfEdge>>();
            for (int i = 0; i < Faces.Count; i++)
            {
                Face face = Faces[i];
                List<HalfEdge> edgesToRedirect = new List<HalfEdge>();
                var startEdge = face.Edge;
                var currentEdge = face.Edge;
                do
                {
                    if(currentEdge.Origin.IsStraight()) edgesToRedirect.Add(currentEdge);
                    
                    currentEdge = currentEdge.Next;
                } while (startEdge != currentEdge);
                if (edgesToRedirect.Count != 3)
                {
                    Debug.LogError("I found " + edgesToRedirect.Count + " to redirect");
                }

                newFaces.Add(edgesToRedirect);
            }

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

        public static Mesh CreateTriangle()
        {
            Mesh m = new Mesh();

            Vertex v1 = m.AddVertex(Quaternion.Euler(0, 0, 0) * Vector3.right);
            Vertex v2 = m.AddVertex(Quaternion.Euler(0, 120, 0) * Vector3.right);
            Vertex v3 = m.AddVertex(Quaternion.Euler(0, 240, 0) * Vector3.right);
            
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

        public static Mesh CreateQuad(Vector3 origin, float width, float height)
        {
            float halfWidth = width * 0.5f;
            float halfHeight = height * 0.5f;
            
            Vector3 v0 = new Vector3(-halfWidth, 0, halfHeight) + origin;
            Vector3 v1 = new Vector3(halfWidth, 0, halfHeight) + origin;
            Vector3 v2 = new Vector3(halfWidth, 0, -halfHeight) + origin;
            Vector3 v3 = new Vector3(-halfWidth, 0, -halfHeight) + origin;
            
            return CreateQuad(v0, v1, v2, v3);
        }

        public static Mesh CreateQuad(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3)
        {
            Mesh m = new Mesh();

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

            e2.SetNext(e4);
            e4.SetNext(e6);
            e6.SetNext(e8);
            e8.SetNext(e2);

            Face f = m.AddFace(e1);
            e1.IncidentFace = f;
            e3.IncidentFace = f;
            e5.IncidentFace = f;
            e7.IncidentFace = f;

            return m;
        }

        public static Mesh CreatePolygon(int sides, float radius)
        {
            Mesh m = new Mesh();
            Vertex center = m.AddVertex(Vector3.zero);

            //Create vertex "star"
            for (int i = 0; i < sides; i++)
            {
                Vector3 newPos = Vector3.right * radius;
                newPos = Quaternion.Euler(0, 360f/sides * i, 0) * newPos;
                Vertex v = m.AddVertex(newPos);
                
                //Connect to center
                var (e0, e1) = m.AddEdge(center, v); 
            }

            HalfEdge firstOuter = null;
            HalfEdge previousOuter = null;
            //Connect outer edge
            for (int i = 0; i < sides; i++)
            {
                var (inner, outer) = m.AddEdge(m.Vertices[i + 1], m.Vertices[(i + 1) % sides + 1]);
                
                HalfEdge outEdge = m.HalfEdges[i * 2]; //edge going out from the center
                HalfEdge inEdge = m.HalfEdges[(i * 2 + 3)%(sides*2)]; //Edge going in to the center

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
        
        //O(n)
        public HalfEdge GetEdge(Vertex a, Vertex b)
        {
            foreach (var halfEdge in HalfEdges)
            {
                if (halfEdge.Origin == a && halfEdge.Twin.Origin == b) return halfEdge;
            }

            return null;
        }

        public (HalfEdge, HalfEdge) GetOrAddEdge(Vertex a, Vertex b)
        {
            
            var e1 = GetEdge(a, b);

            if (e1 != null)
            {
                return (e1, e1.Twin);
            }
            
            return AddEdge(a, b);
        }
         
        public static Mesh FromUnityMesh(UnityEngine.Mesh unityMesh)
        {
            if (unityMesh.GetSubMesh(0).topology != MeshTopology.Quads)
            {
                Debug.Log("Mesh is not Quads!");
                return null;
            }
            
            Mesh m = new Mesh();
            
            int[] quads = unityMesh.GetIndices(0);
            Vector3[] vertices = unityMesh.vertices;

            for (int i = 0; i < vertices.Length; i++)
            {
                m.AddVertex(vertices[i]);
            }
            
            for (int i = 0; i < quads.Length; i += 4)
            {
                
                Vertex v0 = m.Vertices[quads[i + 0]];
                Vertex v1 = m.Vertices[quads[i + 1]];
                Vertex v2 = m.Vertices[quads[i + 2]];
                Vertex v3 = m.Vertices[quads[i + 3]];
                
                
                var (e1,e2) =  m.GetOrAddEdge(v0, v1);
                var (e3,e4) =  m.GetOrAddEdge(v1, v2);
                var (e5,e6) =  m.GetOrAddEdge(v2, v3);
                var (e7,e8) =  m.GetOrAddEdge(v3, v0);
                
                //Set up inner halfedges
                e1.SetNext(e3);
                e3.SetNext(e5);
                e5.SetNext(e7);
                e7.SetNext(e1);
                
                Face f = m.AddFace(e1);
                e1.IncidentFace = f;
                e3.IncidentFace = f;
                e5.IncidentFace = f;
                e7.IncidentFace = f;

                List<HalfEdge> edges = new List<HalfEdge> { e1, e2, e3, e4, e5, e6, e7, e8 };

                for (int j = 1; j < edges.Count; j += 2)
                {
                    HalfEdge currentEdge = edges[j];
                    
                    if (currentEdge.IncidentFace == null) //outer edge needs to be rerouted
                    {
                        var nextOuterEdge = currentEdge.Twin.Origin.GetOutgoingOuterEdge();
                        currentEdge.SetNext(nextOuterEdge);

                        var previousOuterEdge = currentEdge.Origin.GetIncomingOuterEdge();
                        currentEdge.SetPrevious(previousOuterEdge);
                    }
                }
            }

            return m;
        }
        
        public static Mesh CreateNonUniformRandomGrid(float radius, int subdivisions, int relaxCount, int seed)
        {
            Mesh mesh = CreatePolygon(6, radius);
            for(int i = 0; i < subdivisions; i++) mesh.TriangleSubdivide();
            
            mesh.DissolveToQuads(seed);
            mesh.QuadSubdivide();
            for(int i = 0; i < relaxCount; i++) mesh.RelaxVertices();

            return mesh;
        }
        
        //Assumes mesh of tris. Dissolves random edges to create quads while it can
        public void DissolveToQuads(int seed = 0)
        {
            Random.InitState(seed);
        
            var halfEdges = new HashSet<HalfEdge>(HalfEdges);
            while (halfEdges.Count > 0)
            {
                //O(n)
                HalfEdge current = halfEdges.ElementAt(Random.Range(0, halfEdges.Count));
                halfEdges.Remove(current);
                halfEdges.Remove(current.Twin);
            
                if (current.IncidentFace == null || current.Twin.IncidentFace == null)
                {
                    continue;
                }
                Face mergedFace = DissolveEdge(current);
                var toRemoveCurrent = mergedFace.Edge;
                var toRemoveStart = mergedFace.Edge;
                do
                {
                    halfEdges.Remove(toRemoveCurrent);
                    halfEdges.Remove(toRemoveCurrent.Twin);
                    toRemoveCurrent = toRemoveCurrent.Next;
                } while (toRemoveCurrent != toRemoveStart);
            }
        }
    }
}
