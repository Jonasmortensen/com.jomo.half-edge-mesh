using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Jomo.HalfEdgeMesh
{
    public class Face
    {
        public HalfEdge Edge;
        public int ID;

        // Optional user data. Mesh operations pass it on to faces derived from this one, see IMeshData.
        public IMeshData Data;

        // Position in HalfEdgeMesh.Faces, or -1 once removed from the mesh
        internal int Index = -1;

        public Face(HalfEdge edge)
        {
            Edge = edge;
        }

        // Data as T, or null if there is no data or it is of another type
        public T GetData<T>() where T : class, IMeshData => Data as T;

        // Half-edges around the face in winding (clockwise) order, starting at Edge
        public IEnumerable<HalfEdge> Edges() => Traversal.Loop(Edge);

        public IEnumerable<Vertex> Vertices() => Edges().Select(e => e.Origin);

        public int GetSideCount() => Edges().Count();

        public Vector3 GetCenter()
        {
            Vector3 center = Vector3.zero;
            int sideCount = 0;

            foreach (var e in Edges())
            {
                center += e.Origin.Position;
                sideCount++;
            }

            return center / sideCount;
        }

        // Unit normal on the side the face is visible from (clockwise winding, like Unity)
        public Vector3 GetNormal()
        {
            // Newell's method, which also handles non-planar polygons
            Vector3 normal = Vector3.zero;
            foreach (var e in Edges())
            {
                Vector3 a = e.Origin.Position;
                Vector3 b = e.Destination.Position;
                normal.x += (a.y - b.y) * (a.z + b.z);
                normal.y += (a.z - b.z) * (a.x + b.x);
                normal.z += (a.x - b.x) * (a.y + b.y);
            }

            return normal.normalized;
        }

        public List<Vertex> GetVertices() => Vertices().ToList();

        // One entry per side. Sides on the mesh boundary have no neighbour and give null.
        public List<Face> GetNeighbours() => Edges().Select(e => e.Twin.IncidentFace).ToList();
    }
}
