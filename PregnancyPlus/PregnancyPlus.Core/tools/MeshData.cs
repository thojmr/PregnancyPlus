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
        public bool[] bellyVerticieIndexes;//List of verticie indexes that belong to the belly area
        public bool[] alteredVerticieIndexes;//List of verticie indexes inside the current belly radius from sphere center

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


        //Initialize some props with mesh vert count length, we will popupate others as needed
        public MeshData(int vertCount)
        {           
            bellyVerticieIndexes = new bool[vertCount];
            alteredVerticieIndexes = new bool[vertCount];
        }
    }
}