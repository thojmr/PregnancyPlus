using UnityEngine;
using System;
using System.Collections;
using System.Collections.Generic;

namespace KK_PregnancyPlus
{
    public class BlendShapeController
    {
        public BlendShape blendShape = new BlendShape();

        public class BlendShape 
        {
            public string name;
            private float _weight;
            public float weight 
            {
                set { _weight = Mathf.Clamp(value, 0, 100); }
                get { return _weight; }
            }
            public Vector3[] verticies;
            public Vector3[] normals;
            public Vector3[] tangents;
            public bool isInitilized {
                get { return name != null; }
            }
        }

        /// <summary>
        /// Constructor that takes in a target skinned mesh renderer with verts and creates a blend shape object from it.  This blend shape will then be assigned to the mesh
        /// </summary>
        /// <param name="originalSmr">Original vert mesh</param>
        /// <param name="blendShapeName">Desired name of the blend shape, should be unique</param>
        /// <param name="newSmr">The smr containing the new mesh vert positions</param>
        /// <param name="targetMesh">Optional target mesh that is calculated outside of the newSmr, but should be used like its mesh</param>
        public BlendShapeController(Mesh originalSmrMesh, SkinnedMeshRenderer newSmr, string blendShapeName) 
        {
            if (!blendShape.isInitilized) 
            {
                var maxShapeSize = 100f;

                //Create blend shape object to be reused on other meshes (maybe)
                blendShape = new BlendShape();
                blendShape.name = blendShapeName;
                blendShape.weight = maxShapeSize;

                //Get delta diffs of the two meshes for the blend shape
                blendShape.verticies = GetV3Deltas(originalSmrMesh.vertices, newSmr.sharedMesh.vertices);
                blendShape.normals = GetV3Deltas(originalSmrMesh.normals, newSmr.sharedMesh.normals);
                blendShape.tangents = GetV3Deltas(ConvertV4ToV3(originalSmrMesh.tangents), ConvertV4ToV3(newSmr.sharedMesh.tangents));                            
            }

            AddBlendShapeToMesh(newSmr);
        }


        /// <summary>
        /// This will apply the previously created BlendShape object to an existing skinned mesh renderer
        /// </summary>
        /// <param name="smr">Target skinned mesh renderer to attche the blend shape</param>
        public void AddBlendShapeToMesh(SkinnedMeshRenderer smr) 
        {
            if (!blendShape.isInitilized) return;
            var shapeIndex = smr.sharedMesh.GetBlendShapeIndex(blendShape.name);
            //If the shape exists then overwrite it
            if (shapeIndex >= 0) 
            {
                //Blend shape already exists overwright it the hard way
                if (PregnancyPlusPlugin.debugLog) PregnancyPlusPlugin.Logger.LogInfo($" AddBlendShape > overwriting {blendShape.name}");

                var newBs = new BlendShape();
                newBs.name = blendShape.name;
                newBs.verticies = blendShape.verticies;
                newBs.normals = blendShape.normals;            
                newBs.weight = blendShape.weight;            
                newBs.tangents = blendShape.tangents;         
                
                OverwriteBlendShape(smr.sharedMesh, newBs);

                //Fix for some shared mesh properties not updating after AddBlendShapeFrame
                smr.sharedMesh = smr.sharedMesh; 
                return;
            }

            smr.sharedMesh.AddBlendShapeFrame(blendShape.name, blendShape.weight, blendShape.verticies, blendShape.normals, blendShape.tangents);    
            //Fix for some shared mesh properties not updating after AddBlendShapeFrame
            smr.sharedMesh = smr.sharedMesh;    

            if (PregnancyPlusPlugin.debugLog) PregnancyPlusPlugin.Logger.LogInfo($" AddBlendShape > {blendShape.name}");
        }

        /// <summary>
        /// This will replace an existing blendshape of the same name.  Only works when the new blend shape is single frame
        /// </summary>
        /// <param name="smrMesh">Target skinned mesh renderer to attche the blend shape</param>
        /// <param name="newBs">The new blend shape</param>
        private void OverwriteBlendShape(Mesh smrMesh, BlendShape newBs) 
        {
            var existingBlendShapes = new Dictionary<int, BlendShape[]>();
            var bsCount = smrMesh.blendShapeCount;

            // if (PregnancyPlusPlugin.debugLog) PregnancyPlusPlugin.Logger.LogInfo($" OverwriteBlendShape > bsCount {bsCount} newBs.name {newBs.name}");

            //For each shape index that exists
            for (var i = 0; i < bsCount; i++) 
            {
                int frameCount = smrMesh.GetBlendShapeFrameCount(i);

                Vector3[] deltaVertices = new Vector3 [smrMesh.vertexCount];
                Vector3[] deltaNormals = new Vector3 [smrMesh.vertexCount];
                Vector3[] deltaTangents = new Vector3 [smrMesh.tangents.Length];

                existingBlendShapes[i] = new BlendShape[frameCount];

                //For each frame of the shape index
                for (var f = 0; f < frameCount; f++) 
                {
                    //Get the blendshape details
                    smrMesh.GetBlendShapeFrameVertices(i, f, deltaVertices, deltaNormals, deltaTangents);
                    var name = smrMesh.GetBlendShapeName(i);
                    var weight = smrMesh.GetBlendShapeFrameWeight(i, f);

                    //Copy the blendshape data
                    var bsMesh = new BlendShape();
                    bsMesh.verticies = deltaVertices;
                    bsMesh.normals = deltaNormals;
                    bsMesh.tangents = deltaTangents;
                    bsMesh.weight = weight;
                    bsMesh.name = name;
                
                    existingBlendShapes[i][f] = bsMesh;
                }
            }

            //Clear all blend shapes (because we cant just delete one.  Thanks unity!)
            smrMesh.ClearBlendShapes();

            //Add all of the copies back (excluding the one we are overriding)
            for (var i = 0; i < bsCount; i++)
            {
                //For each frame add it back in
                for (var f = 0; f < existingBlendShapes[i].Length; f++) 
                {
                    //If this is the BS we want to replace, add it, but keep the current weight
                    if (existingBlendShapes[i][f].name == newBs.name) 
                    {
                        smrMesh.AddBlendShapeFrame(newBs.name, existingBlendShapes[i][f].weight, newBs.verticies, newBs.normals, newBs.tangents);    
                        continue;
                    }
                    //Otherwise just add back the old blend shapes, and weights
                    smrMesh.AddBlendShapeFrame(existingBlendShapes[i][f].name, existingBlendShapes[i][f].weight, existingBlendShapes[i][f].verticies, existingBlendShapes[i][f].normals, existingBlendShapes[i][f].tangents);
                }
            }

        }


        /// <summary>
        /// This will change the weight (apperance) of an existing BlendShape attached to a skinned mesh renderer. Weight 0 will reset to the default shape
        /// </summary>
        /// <param name="smr">Target skinned mesh renderer to attche the blend shape</param>
        /// <param name="weight">Float value from 0-100 that will increase the blend to the target shape as the number grows</param>
        /// <returns>boolean true if the blend shape exists</returns>
        public bool ApplyBlendShapeWeight(SkinnedMeshRenderer smr, float weight) 
        {
            if (!blendShape.isInitilized || weight < 0) return false;

            //Belly size goes from 0-40, but blendShapes have to be 0-100
            var lerpWeight = Mathf.Lerp(0, 100, weight/40);
            var shapeIndex = smr.sharedMesh.GetBlendShapeIndex(blendShape.name);
            //If the blendshape is not found, return
            if (shapeIndex < 0) return false;

            var shapeWeight = smr.GetBlendShapeWeight(shapeIndex);            
            var shapeFrameCount = smr.sharedMesh.GetBlendShapeFrameCount(shapeIndex);
            var shapeName = smr.sharedMesh.GetBlendShapeName(shapeIndex);
            var shapeCount = smr.sharedMesh.blendShapeCount;

            if (PregnancyPlusPlugin.debugLog) PregnancyPlusPlugin.Logger.LogInfo($" ApplyBlendShapeWeight > shapeIndex {shapeIndex} shapeWeight {shapeWeight} shapeCount {shapeCount} shapeFrameCount {shapeFrameCount} lerpWeight {lerpWeight}");            
            smr.SetBlendShapeWeight(shapeIndex, lerpWeight);

            return true;
        }

        public void ClearBlendShapes(SkinnedMeshRenderer smr) 
        {
            smr.sharedMesh.ClearBlendShapes();
            blendShape = new BlendShape();
        }

        //Just subtract the vectors to get deltas
        internal Vector3[] GetV3Deltas(Vector3[] origins, Vector3[] targets) 
        {
            var deltas = new Vector3[origins.Length];

            for (var i = 0; i < origins.Length; i++) 
            {
                deltas[i] = targets[i] - origins[i];
            }

            return deltas;
        }

        internal Mesh CopyMesh(Mesh mesh)
        {
            Mesh newmesh = new Mesh();
            newmesh.vertices = mesh.vertices;
            newmesh.triangles = mesh.triangles;
            newmesh.uv = mesh.uv;
            newmesh.normals = mesh.normals;
            newmesh.colors = mesh.colors;
            newmesh.tangents = mesh.tangents;

            return newmesh;
        }

        internal Vector3[] ConvertV4ToV3(Vector4[] v4s) 
        {
            var v3s = new Vector3[v4s.Length];
            var i = 0;//I know, I know....

            foreach(var v4 in v4s) 
            {
                v3s[i] = new Vector3(v4.x, v4.y, v4.z);
                i++;
            }

            return v3s;
        }
    }
}
