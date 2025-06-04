using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Jomo.HalfEdgeMesh
{
    //TODO: These structures should probably just have indices rather than references
    public class HalfEdge
    {
        public HalfEdge Twin, Next, Previous;
        public Vertex Origin;
        public Face IncidentFace;
        public int ID;

        public HalfEdge(Vertex origin, int id)
        {
            Origin = origin;
            ID = id;
        }
        
        public void Validate()
        {
            if(Twin == null) Debug.Log("Edge " + ID + " is missing a twin");
            if(Next == null) Debug.Log("Edge " + ID + " is missing Next");
            if(Previous == null) Debug.Log("Edge " + ID + " is missing Previous");
            if(Twin == this) Debug.Log("Edge " + ID + " has identical twin");
            if(this != Twin.Twin) Debug.Log("Edge " + ID + " incorrect twin link");
        }

        public void SetPrevious(HalfEdge e)
        {
            Previous = e;
            e.Next = this;
        }

        public void SetNext(HalfEdge e)
        {
            Next = e;
            e.Previous = this;
        }
    }
}
