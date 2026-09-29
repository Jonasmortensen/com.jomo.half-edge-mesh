using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Jomo.HalfEdgeMesh
{
    public class Vertex
    {
        public Vector3 Position;
        public HalfEdge IncidentEdge;
        public int ID;

        // Position in HalfEdgeMesh.Vertices
        internal int Index = -1;

        public Vertex(Vector3 position)
        {
            Position = position;
        }

        // Every half-edge that starts at this vertex, including boundary half-edges
        public IEnumerable<HalfEdge> OutgoingEdges() => Traversal.Fan(IncidentEdge);

        public List<Vertex> GetNeighbourVertices() => OutgoingEdges().Select(e => e.Destination).ToList();

        public HalfEdge GetIncomingOuterEdge()
        {
            foreach (var e in OutgoingEdges())
            {
                if (e.Twin.IsBoundary) return e.Twin;
            }

            return null;
        }

        public HalfEdge GetOutgoingOuterEdge()
        {
            foreach (var e in OutgoingEdges())
            {
                if (e.IsBoundary) return e;
            }

            return null;
        }

        public List<Face> GetFaces()
        {
            return OutgoingEdges().Where(e => !e.IsBoundary).Select(e => e.IncidentFace).ToList();
        }

        public bool IsOuter()
        {
            return OutgoingEdges().Any(e => e.IsBoundary || e.Twin.IsBoundary);
        }

        // True when the vertex has exactly two neighbours and lies on the straight line between them
        public bool IsStraight()
        {
            var neighbours = GetNeighbourVertices();
            if (neighbours.Count != 2) return false;

            Vector3 direction1 = (neighbours[0].Position - Position).normalized;
            Vector3 direction2 = (neighbours[1].Position - Position).normalized;

            float dotProduct = Vector3.Dot(direction1, direction2);

            return Mathf.Abs(dotProduct + 1) < 0.001f;
        }

        // Moves the vertex to the average of its surrounding face centers
        public void Relax()
        {
            List<Face> faces = GetFaces();
            if (faces.Count == 0) return;

            Vector3 averageFacePos = Vector3.zero;

            foreach (var face in faces)
            {
                averageFacePos += face.GetCenter();
            }

            Position = averageFacePos / faces.Count;
        }
    }
}
