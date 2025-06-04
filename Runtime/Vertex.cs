using System.Collections;
using System.Collections.Generic;
using System.Linq;
using JetBrains.Annotations;
using UnityEngine;

namespace Jomo.HalfEdgeMesh
{
    public class Vertex
    {
        public Vector3 Position;
        public HalfEdge IncidentEdge;
        public int id;

        public Vertex(Vector3 position)
        {
            Position = position;
        }
        
        public List<Vertex> GetNeighbourVertices()
        {
            List<Vertex> verts = new List<Vertex>();

            HalfEdge startEdge = IncidentEdge;
            HalfEdge currentEdge = startEdge;

            do
            {
                verts.Add(currentEdge.Twin.Origin);
                currentEdge = currentEdge.Previous.Twin;
            } while (startEdge != currentEdge);
            
            return verts;
        }

        public HalfEdge GetIncomingOuterEdge()
        {
            if (IncidentEdge == null) return null;
            
            HalfEdge startEdge = IncidentEdge;
            HalfEdge currentEdge = startEdge;

            do
            {
                if (currentEdge.Twin.IncidentFace == null) return currentEdge.Twin;

                currentEdge = currentEdge.Twin.Next;
            } while (startEdge != currentEdge);

            return null;
        }

        public HalfEdge GetOutgoingOuterEdge()
        {
            if (IncidentEdge == null) return null;
            
            HalfEdge startEdge = IncidentEdge;
            HalfEdge currentEdge = startEdge;

            do
            {
                if (currentEdge.IncidentFace == null) return currentEdge;

                currentEdge = currentEdge.Previous.Twin;
            } while (startEdge != currentEdge);

            return null;
        }

        public List<Face> GetFaces()
        {
            HashSet<Face> faces = new HashSet<Face>();

            HalfEdge startEdge = IncidentEdge;
            HalfEdge currentEdge = startEdge;

            int loopSafety = 0;

            do
            {
                if(loopSafety > 1000) Debug.Log("Loop safety GetFaces");
                if(currentEdge.IncidentFace != null) faces.Add(currentEdge.IncidentFace);
                currentEdge = currentEdge.Previous.Twin;
            } while (startEdge != currentEdge);

            return faces.ToList();
        }

        public bool IsOuter()
        {
            HalfEdge startEdge = IncidentEdge;
            HalfEdge currentEdge = startEdge;
            
            int loopSafety = 0;

            do
            {
                if(loopSafety > 1000) Debug.Log("Loop safety IsOuter");
                
                if (currentEdge.IncidentFace == null || currentEdge.Twin.IncidentFace == null) return true;

                loopSafety++;
                currentEdge = currentEdge.Previous.Twin;
            } while (startEdge != currentEdge);
            
            return false;
        }

        public bool IsStraight()
        {
            if (GetNeighbourVertices().Count != 2) return false;
            
            Vector3 v1 = IncidentEdge.Twin.Origin.Position;
            Vector3 v2 = IncidentEdge.Previous.Origin.Position;


            Vector3 direction1 = (v1 - Position).normalized;
            Vector3 direction2 = (v2 - Position).normalized;

            float dotProduct = Vector3.Dot(direction1, direction2);
            
            
            return Mathf.Abs(dotProduct + 1) < 0.001f;
        }

        public void Relax()
        {
            List<Face> faces = GetFaces();
            
            Vector3 averageFacePos = Vector3.zero;
                
            foreach (var face in faces)
            {
                averageFacePos += face.GetCenter();
            }

            Position = averageFacePos / faces.Count;
        }
    }
}