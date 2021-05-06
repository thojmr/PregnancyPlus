using UnityEngine;
using System;
using System.Collections;
using System.Collections.Generic;
using MessagePack;

namespace KK_PregnancyPlus
{
    //This is used to control individual blendshapes on a skinned mesh renderer.  Adding, Updating, and Overwriting them as needed
    public class BlendShapeController
    {
        public BlendShape blendShape = new BlendShape();
        public SkinnedMeshRenderer smr = null;

        //This format contains all the info a blendshape needs to be made.  It also server as the format we will save to a character card later
        [MessagePackObject(keyAsPropertyName: true)]
        public class BlendShape 
        {
            public string name;
            private float _weight = 100;
            public float weight 
            {
                set { _weight = Mathf.Clamp(value, 0, 100); }
                get { return _weight; }
            }
            public Vector3[] verticies;
            public Vector3[] normals;
            public Vector3[] tangents;

            [IgnoreMember]
            public bool isInitilized
            {
                get { return name != null; }
            }

            [IgnoreMember]
            public int vertexCount 
            {
                get { return verticies.Length; }
            }

            [IgnoreMember]
            public string log 
            {
                get { return $"name {name} weight {_weight} vertexCount {vertexCount} isInitilized {isInitilized}"; }
            }
        }


        /// <summary>
        /// Constructor that takes in a target skinned mesh renderer with verts and creates a blend shape object from it.  This blend shape will then be assigned to the mesh
        /// </summary>
        /// <param name="originalSmr">Original untouched mesh</param>
        /// <param name="blendShapeName">Desired name of the blend shape, should be unique</param>
        /// <param name="newSmr">The smr containing the target mesh shape</param>
        public BlendShapeController(Mesh originalSmrMesh, Mesh targetSmrMesh, string blendShapeName, SkinnedMeshRenderer smr) 
        {
            if (!blendShape.isInitilized) 
            {
                var maxShapeSize = 100f;

                //Create blend shape deltas from both meshes
                blendShape = new BlendShape();
                blendShape.name = blendShapeName;
                blendShape.weight = maxShapeSize;

                //Get delta diffs of the two meshes for the blend shape
                blendShape.verticies = GetV3Deltas(originalSmrMesh.vertices, targetSmrMesh.vertices);
                blendShape.normals = GetV3Deltas(originalSmrMesh.normals, targetSmrMesh.normals);
                blendShape.tangents = GetV3Deltas(ConvertV4ToV3(originalSmrMesh.tangents), ConvertV4ToV3(targetSmrMesh.tangents));                            
            }

            AddBlendShapeToMesh(smr);
        }


        /// <summary>
        /// Constructor overload that takes a saved blendshape and sets it to the correct mesh
        /// </summary>
        /// <param name="smr">The current active mesh</param>
        /// <param name="_blendShape">The blendshape we loaded from character card</param>
        public BlendShapeController(BlendShape _blendShape, SkinnedMeshRenderer _smr)         
        {
            blendShape = _blendShape;
            smr = _smr;
            AddBlendShapeToMesh(_smr);
        }


        /// <summary>
        /// Constructor overload that finds a blendshape by name
        /// </summary>
        /// <param name="smr">The mesh to search on</param>
        /// <param name="blendShapeName">The blendshape name to search for</param>
        public BlendShapeController(SkinnedMeshRenderer _smr, string blendShapeName)         
        {
            //Once found you can use this controller to call any of its blendshape methods
            blendShape = GetBlendShapeByName(_smr, blendShapeName);
            smr = _smr;
        }


        /// <summary>
        /// This will apply the previously created BlendShape object to an existing skinned mesh renderer
        /// </summary>
        /// <param name="smr">The skinned mesh renderer to attach the blend shape</param>
        public void AddBlendShapeToMesh(SkinnedMeshRenderer smr) 
        {
            if (!blendShape.isInitilized) return;

            //Not going to try to debug this unity problem with blendshapes not being found by name, just always overwright the existing blendshape...
            if (smr.sharedMesh.blendShapeCount > 0) 
            {
                //Blend shape already exists overwright it the hard way
                if (PregnancyPlusPlugin.DebugLog.Value) PregnancyPlusPlugin.Logger.LogInfo($" AddBlendShape > overwriting {blendShape.name}");                       
                OverwriteBlendShape(smr, blendShape);

                //Fix for some shared mesh properties not updating after AddBlendShapeFrame
                smr.sharedMesh = smr.sharedMesh; 
                return;
            }

            if (PregnancyPlusPlugin.DebugLog.Value) PregnancyPlusPlugin.Logger.LogInfo($" AddBlendShape > {blendShape.log}");
            smr.sharedMesh.AddBlendShapeFrame(blendShape.name, blendShape.weight, blendShape.verticies, blendShape.normals, blendShape.tangents);    
            //Fix for some shared mesh properties not updating after AddBlendShapeFrame
            smr.sharedMesh = smr.sharedMesh; //I hate this line of code              
        }


        /// <summary>
        /// This will replace an existing blendshape of the same name.  Will only use a single frame of the newly added blendshape
        /// </summary>
        /// <param name="smrMesh">The skinned mesh renderer to overwrite the blend shape</param>
        /// <param name="newBs">The new blend shape to overwrite the existing one (must have same name)</param>
        private void OverwriteBlendShape(SkinnedMeshRenderer smr, BlendShape newBs) 
        {
            var smrMesh = smr.sharedMesh;
            var bsCount = smrMesh.blendShapeCount;            
            var existingBlendShapes = CopyAllBlendShapeFrames(smrMesh);

            //Clear all blend shapes (because we cant just delete one.  Thanks unity!)
            smrMesh.ClearBlendShapes();
            var found = false;

            //Add all of the copies back (excluding the one we are overriding)
            for (var i = 0; i < bsCount; i++)
            {
                //For each frame add it back in
                for (var f = 0; f < existingBlendShapes[i].Length; f++) 
                {
                    //If this is the BS we want to replace, add it, but keep the current weight
                    if (existingBlendShapes[i][f].name == newBs.name) 
                    {
                        found = true;
                        smrMesh.AddBlendShapeFrame(newBs.name, existingBlendShapes[i][f].weight, newBs.verticies, newBs.normals, newBs.tangents);    
                        continue;
                    }
                    //Otherwise just add back the old blend shapes, and weights in the same order
                    smrMesh.AddBlendShapeFrame(existingBlendShapes[i][f].name, existingBlendShapes[i][f].weight, existingBlendShapes[i][f].verticies, 
                        existingBlendShapes[i][f].normals, existingBlendShapes[i][f].tangents);
                }
            }

            //If not found then just add it as per normal
            if (!found) 
            {
                smrMesh.AddBlendShapeFrame(newBs.name, newBs.weight, newBs.verticies, newBs.normals, newBs.tangents);    
            }
        }


        /// <summary>
        /// This will change the weight (apperance) of an existing BlendShape attached to a skinned mesh renderer. Weight 0 will reset to the default shape (Not used here)
        /// </summary>
        /// <param name="smr">The skinned mesh renderer to update the blend shape weight</param>
        /// <param name="weight">Float value from 0-40 that will increase the blend to the target shape as the number grows</param>
        /// <returns>boolean true if the blend shape exists</returns>
        public bool ApplyBlendShapeWeight(SkinnedMeshRenderer smr, float weight) 
        {
            if (!blendShape.isInitilized || weight < 0) return false;
            if (smr == null) return false;

            //Once again if you don't force the mesh to update here, the blendshape below could have stale data
            smr.sharedMesh = smr.sharedMesh;

            //Belly size goes from 0-40, but blendShapes have to be 0-100
            //Technically unity 2018x + can go above 100 when unclamped, but not any illusion games yet
            var lerpWeight = Mathf.Lerp(0, 100, weight/40);
            var shapeIndex = smr.sharedMesh.GetBlendShapeIndex(blendShape.name);
            //If the blendshape is not found, return
            if (shapeIndex < 0) return false;

            var shapeWeight = smr.GetBlendShapeWeight(shapeIndex);            
            var shapeFrameCount = smr.sharedMesh.GetBlendShapeFrameCount(shapeIndex);
            var shapeName = smr.sharedMesh.GetBlendShapeName(shapeIndex);
            var shapeCount = smr.sharedMesh.blendShapeCount;                 

            // if (PregnancyPlusPlugin.DebugLog.Value) PregnancyPlusPlugin.Logger.LogInfo($" ApplyBlendShapeWeight > shapeIndex {shapeIndex} shapeWeight {shapeWeight} shapeCount {shapeCount} shapeFrameCount {shapeFrameCount} lerpWeight {lerpWeight}");            
            smr.SetBlendShapeWeight(shapeIndex, lerpWeight);

            return true;
        }


        //Override for when the smr is already included in this controller
        public bool ApplyBlendShapeWeight(float weight) 
        {
            return ApplyBlendShapeWeight(smr, weight);
        }


        /// <summary>
        /// Get an existing blendshape by name.null  Only returns the first frame since thats all Preg+ uses
        /// </summary>
        /// <param name="smr">The skinned mesh renderer to search for the blend shape</param>
        /// <param name="blendShapeName">The blendshape name to search for</param>
        internal BlendShape GetBlendShapeByName(SkinnedMeshRenderer smr, string blendShapeName) {
            //Check whether the blendshape exists
            var shapeIndex = smr.sharedMesh.GetBlendShapeIndex(blendShapeName);

            //If the blendshape is not found return
            if (shapeIndex < 0) {
                // if (PregnancyPlusPlugin.DebugLog.Value) PregnancyPlusPlugin.Logger.LogInfo($" GetBlendShapeByName > not found: {blendShapeName}");
                return null;
            }

            var shapeFrameCount = smr.sharedMesh.GetBlendShapeFrameCount(shapeIndex);
            if (shapeFrameCount <= 0) {
                if (PregnancyPlusPlugin.DebugLog.Value) PregnancyPlusPlugin.Logger.LogInfo($" GetBlendShapeByName > frame count <= 0: {blendShapeName}");
                return null;
            }

            Vector3[] deltaVertices = new Vector3 [smr.sharedMesh.vertexCount];
            Vector3[] deltaNormals = new Vector3 [smr.sharedMesh.vertexCount];
            Vector3[] deltaTangents = new Vector3 [smr.sharedMesh.tangents.Length];

            //Get the blendshape details
            smr.sharedMesh.GetBlendShapeFrameVertices(shapeIndex, 0, deltaVertices, deltaNormals, deltaTangents);
            var name = smr.sharedMesh.GetBlendShapeName(shapeIndex);
            var weight = smr.sharedMesh.GetBlendShapeFrameWeight(shapeIndex, 0);

            //Copy the blendshape data for the first frame
            var bsFrame = new BlendShape();
            bsFrame.verticies = deltaVertices;
            bsFrame.normals = deltaNormals;
            bsFrame.tangents = deltaTangents;
            bsFrame.weight = weight;
            bsFrame.name = name;

            return bsFrame;
        }


        /// <summary>
        /// Remove a single blendshape from a mesh
        /// </summary>
        public bool RemoveBlendShape(SkinnedMeshRenderer smr) {

            if (blendShape == null) return false;

            var smrMesh = smr.sharedMesh;            
            var bsCount = smrMesh.blendShapeCount;      
            if (bsCount == 0) return true;
                  
            var existingBlendShapes = CopyAllBlendShapeFrames(smrMesh);

            //Clear all blend shapes (because we cant just delete one.  Thanks unity!)
            smrMesh.ClearBlendShapes();

            //Add all of the copies back (excluding the one we are removing)
            for (var i = 0; i < bsCount; i++)
            {
                //For each frame add it back in
                for (var f = 0; f < existingBlendShapes[i].Length; f++) 
                {
                    //If this is the BS we want to remove, skip it
                    if (existingBlendShapes[i][f].name == blendShape.name) continue;

                    //Otherwise add back the old blend shapes, and weights in the same order (shifted around the removed one)
                    smrMesh.AddBlendShapeFrame(existingBlendShapes[i][f].name, existingBlendShapes[i][f].weight, existingBlendShapes[i][f].verticies, 
                        existingBlendShapes[i][f].normals, existingBlendShapes[i][f].tangents);
                }
            }

            return true;
        }


        /// <summary>
        /// Copy all existing blendshape frames on a mesh
        /// </summary>
        /// <param name="smrMesh">The mesh contining the blendshapes</param>
        internal Dictionary<int, BlendShape[]> CopyAllBlendShapeFrames(Mesh smrMesh) 
        {
            var existingBlendShapes = new Dictionary<int, BlendShape[]>();
            var bsCount = smrMesh.blendShapeCount;  

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
                    var bsFrame = new BlendShape();
                    bsFrame.verticies = deltaVertices;
                    bsFrame.normals = deltaNormals;
                    bsFrame.tangents = deltaTangents;
                    bsFrame.weight = weight;
                    bsFrame.name = name;
                
                    existingBlendShapes[i][f] = bsFrame;
                }
            }

            return existingBlendShapes;
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
