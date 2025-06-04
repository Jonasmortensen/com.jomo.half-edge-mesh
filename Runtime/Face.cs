using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Jomo.HalfEdgeMesh
{
    public class Face
    {
        public HalfEdge Edge;
        public int id;

        public Face(HalfEdge edge)
        {
            Edge = edge;
        }
        
        public int GetSideCount()
        {
            int count = 0;
            var start = Edge;
            var current = Edge;
            
            do
            {
                count++;
                current = current.Next;
                if (count > 1000) break;
            } while (current != start);

            return count;
        }

        public Vector3 GetCenter()
        {
            Vector3 center = Vector3.zero;

            HalfEdge start = Edge, current = Edge;
            int sideCount = 0;
            
            do
            {
                center += current.Origin.Position;
                sideCount++;
                current = current.Next;
            } while (current != start);

            return center / sideCount;
        }

        public List<Vertex> GetVertices()
        {
            List<Vertex> vertices = new List<Vertex>();

            HalfEdge start = Edge, current = Edge;
            do
            {
                vertices.Add(current.Origin);
                current = current.Next;
            } while (start != current);

            return vertices;
        }

        public List<Face> GetNeighbours()
        {
            List<Face> neighbours = new List<Face>();
            
            HalfEdge start = Edge, current = Edge;
            do
            {
                neighbours.Add(current.Twin.IncidentFace);
                current = current.Next;
            } while (start != current);

            return neighbours;
        }
    }
}

