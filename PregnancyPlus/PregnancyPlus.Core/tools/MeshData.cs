using System.Collections.Generic;
using UnityEngine;

namespace KK_PregnancyPlus
{
    //Contains the mesh vert data for each character mesh.  Used to compute the belly shape
    public class MeshData
    {        
        public Vector3[] originalVertices;//The original untouched verts from the mesh
        public Vector3[] inflatedVertices;//The verts from the mesh after being inflated        
        public float[] clothingOffsets;//The distance we want to offset each vert from the body mesh when inflated
        public bool[] bellyVerticieIndexes;//When an index is True, that vertex is near the belly area
        public bool[] alteredVerticieIndexes;//When an index is True that vertex's position has been altered by GetInflatedVerticies()
        public Vector3 meshOffset = Vector3.zero;//The offset for this mesh in order to move the unskinned verts to the characters root position
        public bool isFirstPass = true;

        public bool HasInflatedVerts
        {
            get {return inflatedVertices != null && inflatedVertices.Length > 0;}
        }

        public bool HasOriginalVerts
        {
            get {return originalVertices != null && originalVertices.Length > 0;}
        }

        public bool HasClothingOffsets
        {
            get {return clothingOffsets != null && clothingOffsets.Length > 0;}
        }

        public int VertexCount
        {
            get {return inflatedVertices == null ? 0 : inflatedVertices.Length;}
        }


        //Initialize some fields, we will popupate others as needed
        public MeshData(int vertCount)
        {           
            bellyVerticieIndexes = new bool[vertCount];
            alteredVerticieIndexes = new bool[vertCount];
            isFirstPass = true;
        }
    }
}