using UnityEngine;

namespace Jomo.HalfEdgeMesh
{
    // Draws every half-edge as an arrow on the inside of its face (boundary edges in red) using gizmos,
    // optionally skipping faces that point away from the camera.
    // With m_IterateHalfEdges on, play mode steps through the edges of each face in turn.
    public class HalfEdgeMeshDebugView : MonoBehaviour
    {
        public HalfEdgeMesh m_Mesh;

        public bool m_IterateHalfEdges;

        private int m_CurrentFace = -1;
        private HalfEdge m_CurrentHalfEdge;
        private HalfEdge m_StartHalfEdge;
        private float m_Timer;

        // Skip faces pointing away from the camera, so closed meshes don't draw their far side
        public bool m_BackfaceCulling = true;

        [Range(0,0.2f)]
        public float DrawWidth = 0.04f;

        void Update()
        {
            if (m_Mesh == null || !m_IterateHalfEdges || m_Mesh.Faces.Count == 0) return;

            m_Timer += Time.deltaTime;

            // Restart if the mesh changed under us and removed the edge we were on
            bool edgeRemoved = m_CurrentHalfEdge == null || m_CurrentHalfEdge.Index < 0;

            if (edgeRemoved || (m_StartHalfEdge == m_CurrentHalfEdge && m_Timer > 0.4f))
            {
                m_CurrentFace = (m_CurrentFace + 1) % m_Mesh.Faces.Count;

                Face face = m_Mesh.Faces[m_CurrentFace];
                m_StartHalfEdge = face.Edge;
                m_CurrentHalfEdge = m_StartHalfEdge.Next;

                m_Timer = 0;
            }

            if (m_Timer > 0.4f)
            {
                m_Timer = 0;
                m_CurrentHalfEdge = m_CurrentHalfEdge.Next;
            }
        }

        void OnDrawGizmos()
        {
            if (m_Mesh == null) return;

            Gizmos.matrix = transform.localToWorldMatrix;

            // Camera.current is the camera being drawn, usually the Scene view camera
            Camera cam = m_BackfaceCulling ? Camera.current : null;

            foreach (var face in m_Mesh.Faces)
            {
                Vector3 normal = face.GetNormal();
                if (cam != null && !IsFacingCamera(face, normal, cam)) continue;

                foreach (var halfEdge in face.Edges())
                {
                    DisplayEdge(halfEdge, normal, EdgeColor(halfEdge));

                    // Boundary edges have no face, so they are drawn and culled with the face on the other side
                    if (halfEdge.Twin.IsBoundary) DisplayEdge(halfEdge.Twin, normal, EdgeColor(halfEdge.Twin));
                }
            }

            Gizmos.matrix = Matrix4x4.identity;
        }

        Color EdgeColor(HalfEdge halfEdge)
        {
            if (m_IterateHalfEdges && halfEdge == m_CurrentHalfEdge) return new Color(1f, 0.5f, 0f);
            return halfEdge.IsBoundary ? Color.red : Color.blue;
        }

        bool IsFacingCamera(Face face, Vector3 normal, Camera cam)
        {
            // Compare in the mesh's local space, where the vertex positions and normals live
            Vector3 toCamera = cam.orthographic
                ? transform.InverseTransformDirection(-cam.transform.forward)
                : transform.InverseTransformPoint(cam.transform.position) - face.GetCenter();

            return Vector3.Dot(normal, toCamera) > 0;
        }

        void DisplayEdge(HalfEdge edge, Vector3 normal, Color color)
        {
            Vector3 from = edge.Origin.Position;
            Vector3 to = edge.Destination.Position;

            Vector3 direction = (to - from).normalized;
            Vector3 biTangent = Vector3.Cross(direction, normal);

            Vector3 offset = normal * 0.01f;
            Vector3 offsetFrom = (from - biTangent * DrawWidth) + direction * DrawWidth * 3 + offset;
            Vector3 offsetTo = (to - biTangent * DrawWidth) - direction * DrawWidth * 3 + offset;

            Gizmos.color = color;
            Gizmos.DrawLine(offsetFrom, offsetTo);
            Gizmos.DrawLine(offsetTo, offsetTo - biTangent * DrawWidth * 3 - direction * DrawWidth * 3);
        }
    }
}
