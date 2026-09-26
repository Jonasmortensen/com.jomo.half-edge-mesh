namespace Jomo.HalfEdgeMesh
{
    //TODO: These structures should probably just have indices rather than references
    public class HalfEdge
    {
        public HalfEdge Twin, Next, Previous;
        public Vertex Origin;
        public Face IncidentFace;
        public int ID;

        // Position in HalfEdgeMesh.HalfEdges, or -1 once removed from the mesh
        internal int Index = -1;

        public HalfEdge(Vertex origin, int id)
        {
            Origin = origin;
            ID = id;
        }

        public Vertex Destination => Twin.Origin;

        // True when this half-edge lies on the outside of the mesh (it has no face)
        public bool IsBoundary => IncidentFace == null;

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
