using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Jomo.HalfEdgeMesh
{
    public class HalfEdgeMeshDebugView : MonoBehaviour
    {
        public Mesh m_Mesh;
    
        public bool m_IterateHalfEdges;
        
        private int m_CurrentFace;
        private HalfEdge m_CurrentHalfEdge;
        private HalfEdge m_StartHalfEdge;
        private float m_Timer;
        
        [Range(0,0.2f)]
        public float DrawWidth = 0.04f;
        
        // Start is called before the first frame update
        void Start()
        {
            m_CurrentFace = -1;
            m_Timer = 0;
        }
        
        void OnGUI()
        {
            GUI.Label(new Rect(10, 10, 100, 20), "Hello World!");
        }
    
        // Update is called once per frame
        void Update()
        {
            if (m_Mesh == null) return;
            
            if (m_IterateHalfEdges)
            {
                m_Timer += Time.deltaTime;
    
                if (m_CurrentFace < 0 || (m_StartHalfEdge == m_CurrentHalfEdge && m_Timer > 0.4f))
                {
                    m_CurrentFace = (m_CurrentFace + 1) % m_Mesh.Faces.Count; 
                    
                    Face face = m_Mesh.Faces[m_CurrentFace];
                    m_StartHalfEdge = face.Edge;
                    m_CurrentHalfEdge = m_StartHalfEdge.Next;
    
                    m_Timer = 0;
                }
                
                DisplayEdge(m_CurrentHalfEdge, Color.white);
                
                
    
                if (m_Timer > 0.4f)
                {
                    m_Timer = 0;
                    m_CurrentHalfEdge = m_CurrentHalfEdge.Next;
                    
                    
                }
            }
            
            
            
            int iterationCount = 0;
            
            
            foreach (var greyFace in m_Mesh.Faces)
            {
                iterationCount = 0;
                var greystart = greyFace.Edge;
                var greycurrent = greyFace.Edge;
                do
                {
                    if(!(m_IterateHalfEdges && greycurrent == m_CurrentHalfEdge)) DisplayEdge(greycurrent, Color.black);
                    
                    iterationCount++;
                    if (iterationCount > 100)
                    {
                        Debug.Log("Infinite loop. Can't render");  break;
                    }
                    
                    if(greycurrent.Previous == null) Debug.Log("This is an issue");
                    greycurrent = greycurrent.Previous;
                } while (greystart != greycurrent);
            }
        }
        
        void DisplayEdge(HalfEdgeMesh.HalfEdge edge, Color color)
        {
            Vector3 from = edge.Origin.Position;
            Vector3 to = edge.Twin.Origin.Position;
    
            Vector3 direction = (to - from).normalized;
            Vector3 biTangent = Vector3.Cross(direction, Vector3.up);
    
            Vector3 offsetFrom = (from - biTangent * DrawWidth) + direction * DrawWidth*3; 
            Vector3 offsetTo = (to - biTangent * DrawWidth) - direction*DrawWidth*3;
    
            offsetFrom = transform.TransformPoint(offsetFrom);
            offsetTo = transform.TransformPoint(offsetTo);
            direction = transform.TransformDirection(direction);
            biTangent = transform.TransformDirection(biTangent);
    
            Vector3 offset = new Vector3(0, 0.01f, 0);
            
            Debug.DrawLine(offsetFrom + offset, offsetTo + offset, color);
            Debug.DrawLine(offsetTo + offset, offsetTo + offset - biTangent*DrawWidth*3 - direction*DrawWidth*3, color);
        }
    }
}