namespace Jomo.HalfEdgeMesh
{
    // User data attached to mesh elements (currently faces, see Face.Data).
    //
    // Mesh operations carry data along: a face created from another face, such as a split off piece, a
    // subdivision child or a copy made by CopyFaces, gets Clone() of that face's data. When two faces merge,
    // the kept face's data is combined with the other face's using Merge.
    public interface IMeshData
    {
        // Data for an element derived from the one holding this data. Return a new instance for mutable data,
        // so derived elements don't share state. Immutable data can return this.
        IMeshData Clone();

        // Data for the element left when the one holding this data is merged with one holding other. other is
        // never null, and neither element is used afterwards. By default the kept element's data stays as it is.
        IMeshData Merge(IMeshData other) => this;
    }
}
