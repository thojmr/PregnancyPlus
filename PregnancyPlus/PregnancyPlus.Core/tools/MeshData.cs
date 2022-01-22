using System.Collections.Generic;
using UnityEngine;

namespace KK_PregnancyPlus
{
    //Contains the mesh vert data for each character mesh.  Used to compute the belly shape
    public class MeshData
    {        
        public Vector3[] originalVertices;//The original untouched verts from the BindPose mesh
        internal Vector3[] _inflatedVertices;//The verts from the BindPose mesh after being inflated        
        public Vector3[] smoothedVertices;//The inflated verts with lapacian smoothing applied (Use selected smooth belly mesh button)    
        public float[] clothingOffsets;//The distance we want to offset each vert from the body mesh when inflated
        public bool[] bellyVerticieIndexes;//When an index is True, that vertex is near the belly area
        public bool[] alteredVerticieIndexes;//When an index is True that vertex's position has been altered by GetInflatedVerticies()
        public bool isFirstPass = true;

        //The verticie deltas we want to apply to a blendshape
        public Vector3[] deltaVerticies;
        public Vector3[] deltaNormals;
        public Vector3[] deltaTangents;


        
        public Vector3[] inflatedVertices
        {
            //When we do have smoothed verts use those.
            get { return HasSmoothedVerts ? smoothedVertices : _inflatedVertices; }            
            set 
            {
                //Need to clear out smoothed verts when inflated are ever set
                smoothedVertices = null; 
                _inflatedVertices = value;
            }
        }
        

        
        public bool HasInflatedVerts
        {
            get { return HasNonZeroVerts(_inflatedVertices); }
        }

        public bool HasSmoothedVerts
        {
            get {return smoothedVertices != null && smoothedVertices.Length > 0;}
        }

        public bool HasOriginalVerts
        {
            //At least a single vert must have non 0 value
            get { return HasNonZeroVerts(originalVertices); }
        }

        public bool HasClothingOffsets
        {
            get {return clothingOffsets != null && clothingOffsets.Length > 0;}
        }
        
        public bool HasDeltas
        {
            get {return deltaVerticies != null && deltaVerticies.Length > 0;}
        }

        public int VertexCount
        {
            get {return _inflatedVertices == null ? 0 : _inflatedVertices.Length;}
        }


        //Initialize some fields, we will popupate others as needed
        public MeshData(int vertCount, MeshData md = null)
        {           
            bellyVerticieIndexes = new bool[vertCount];
            alteredVerticieIndexes = new bool[vertCount];
            isFirstPass = true;

            //If a mesh is detected that already has original verts, use them
            if (md != null)
            {
                originalVertices = md.originalVertices;
            }
        }


        /// <summary>
        /// Check whether a vertex list has at least one non 0 value, which means its been populated
        /// </summary>
        internal bool HasNonZeroVerts(Vector3[] verts)
        {
            //If empty list
            if (verts == null || verts.Length <= 0) return false;

            var numVertsToCheck = verts.Length > 20 ? 20 : verts.Length;

            //Otherwise check the first few values
            for (int i = 0; i < verts.Length; i++)
            {
                //If the vert has a non zero value
                if (verts[i] != Vector3.zero) return true;

                //Assume by this point all are vector3.zero
                if (i > numVertsToCheck) break;
            }

            return false;
        }
    }
}