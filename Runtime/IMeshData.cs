namespace Jomo.HalfEdgeMesh
{
    // User data attached to mesh elements (currently faces, see Face.Data).
    //
    // Mesh operations carry data along: a face created from another face, such as a split off piece, a
    // subdivision child or a copy made by CopyFaces, gets Clone() of that face's data. When two faces merge,
    // the face that is kept keeps its data and the other face's data is dropped.
    public interface IMeshData
    {
        // Data for an element derived from the one holding this data. Return a new instance for mutable data,
        // so derived elements don't share state. Immutable data can return this.
        IMeshData Clone();
    }
}
