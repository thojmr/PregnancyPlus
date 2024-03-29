using UnityEngine;
using System;
using System.Linq;
using System.Collections;
using System.Collections.Generic;

namespace KK_PregnancyPlus
{
    //This is used to control individual blendshapes on a skinned mesh renderer.  Adding, Updating, and Overwriting them as needed
    public class BlendShapeController
    {
        public BlendShape blendShape = new BlendShape();
        //In certain cases we want to store the smr reference on this class for later
        public SkinnedMeshRenderer smr = null;

#region Constructors

        /// <summary>
        /// Constructor that takes the pre computed blendshape deltas, and applies them to a target SMR
        /// </summary>
        /// <param name="md">The meshData obhect that has our computed blendshape deltas</param>
        /// <param name="blendShapeName">Desired name of the blend shape, should be unique</param>        
        /// <param name="smr">The target SMR</param>        
        public BlendShapeController(MeshData md, string blendShapeName, SkinnedMeshRenderer smr) 
        {
            if (!blendShape.isInitilized) 
            {
                //Create blend shape deltas from both meshes
                blendShape = new BlendShape();
                blendShape.name = blendShapeName;

                //Get delta diffs of the two meshes for the blend shape
                blendShape.verticies = md.deltaVerticies;
                blendShape.normals = md.deltaNormals;
                blendShape.tangents = md.deltaTangents;
            }

            AddBlendShapeToMesh(smr);
        }


        /// <summary>
        /// Constructor overload that takes an existing Preg+ blendshape and sets it to an SMR
        ///     Typically used when loading BlendShape from card
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
        /// Constructor overload that finds a blendshape by name on a specific SMR
        /// </summary>
        /// <param name="smr">The skinned mesh renderer to search on</param>
        /// <param name="blendShapeName">The blendshape name to search for</param>
        public BlendShapeController(SkinnedMeshRenderer _smr, string blendShapeName)         
        {
            //Once found you can use this controller to call any of its blendshape methods
            blendShape = GetBlendShapeByName(_smr, blendShapeName);
            smr = _smr;
        }
        

        /// <summary>
        /// Use this constructor just to access any methods inside this class
        /// </summary>
        public BlendShapeController() { }


#endregion Constructors


        /// <summary>
        /// This will apply the current BlendShape object to an existing skinned mesh renderer
        /// </summary>
        /// <param name="smr">The skinned mesh renderer to attach the blend shape</param>
        public bool AddBlendShapeToMesh(SkinnedMeshRenderer smr) 
        {
            if (!blendShape.isInitilized) return false;
            if (blendShape.vertexCount != smr.sharedMesh.vertexCount 
                || blendShape.verticies.Length != smr.sharedMesh.vertexCount
                || blendShape.normals.Length != smr.sharedMesh.vertexCount) 
            {
                if (PregnancyPlusPlugin.DebugBlendShapeLog.Value) PregnancyPlusPlugin.Logger.LogWarning($" AddBlendShape > missmatch vertex count on {smr.name}: smr {smr.sharedMesh.vertexCount} -> blendshape {blendShape.vertexCount} skipping"); 
                if (PregnancyPlusPlugin.DebugBlendShapeLog.Value) PregnancyPlusPlugin.Logger.LogWarning($" verticies {blendShape.verticies.Length}, normals {blendShape.normals.Length}, tangents {blendShape.tangents.Length}"); 
                return false;
            }

            if (PregnancyPlusPlugin.DebugBlendShapeLog.Value) PregnancyPlusPlugin.Logger.LogInfo($" smr.sharedMesh.tangents {smr.sharedMesh.tangents.Length}, blendShape.tangents {blendShape.tangents.Length}"); 

            //When tangents are empty, pad them to the same length as the vert count to prevent errors
            //  Not sure why they would be empty in the first place?
            if (blendShape.tangents.Length == 0)
            {                
                blendShape.tangents = new Vector3[smr.sharedMesh.vertexCount ];
                if (PregnancyPlusPlugin.DebugBlendShapeLog.Value) PregnancyPlusPlugin.Logger.LogWarning($" smr.sharedMesh.tangents {smr.sharedMesh.tangents.Length}, blendShape.tangents {blendShape.tangents.Length}"); 
            }

            //Create mesh instance on character to prevent changes leaking to other characters
            smr.sharedMesh = (Mesh)UnityEngine.Object.Instantiate(smr.sharedMesh); 

            //Not going to try to debug this unity problem with blendshapes not being found by name, just always overwright the existing blendshape...
            if (smr.sharedMesh.blendShapeCount > 0) 
            {
                //Blend shape already exists overwright it the hard way
                if (PregnancyPlusPlugin.DebugBlendShapeLog.Value) PregnancyPlusPlugin.Logger.LogInfo($" AddBlendShape > overwriting {blendShape.name}");                       
                OverwriteBlendShape(smr, blendShape);

                //Fix for some shared mesh properties not updating after AddBlendShapeFrame (Thanks Unity!)
                smr.sharedMesh = smr.sharedMesh;
                return true;
            }

            if (PregnancyPlusPlugin.DebugBlendShapeLog.Value) PregnancyPlusPlugin.Logger.LogInfo($" AddBlendShape > {blendShape.log}");
            //Actually attach the blendspape
            smr.sharedMesh.AddBlendShapeFrame(blendShape.name, blendShape.frameWeight, blendShape.verticies, blendShape.normals, blendShape.tangents);    
            //Fix for some shared mesh properties not updating after AddBlendShapeFrame
            smr.sharedMesh = smr.sharedMesh; //I hate this line of code        

            return true;      
        }


        /// <summary>
        /// This will replace an existing blendshape of the same name.  Will only use a single frame of the newly added blendshape
        //      Unnfortulately Unity does not let you remove a single blendshape, so we have to copy all, delete all, and add all back including the new one
        /// </summary>
        /// <param name="smr">The skinned mesh renderer that contains the blendshape</param>
        /// <param name="newBs">The new blend shape to overwrite the existing one (must have same name)</param>
        private void OverwriteBlendShape(SkinnedMeshRenderer smr, BlendShape newBs) 
        {
            var smrMesh = smr.sharedMesh;
            var bsCount = smrMesh.blendShapeCount;            
            var existingBlendShapes = CopyAllBlendShapeFrames(smr);

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
                        smrMesh.AddBlendShapeFrame(newBs.name, existingBlendShapes[i][f].frameWeight, newBs.verticies, newBs.normals, newBs.tangents);    
                        continue;
                    }
                    //Otherwise just add back the old blend shapes, and weights in the same order
                    smrMesh.AddBlendShapeFrame(existingBlendShapes[i][f].name, existingBlendShapes[i][f].frameWeight, existingBlendShapes[i][f].verticies, 
                        existingBlendShapes[i][f].normals, existingBlendShapes[i][f].tangents);
                }
            }

            //If not found then just add it as per normal (You'll end up with duplicates if they don't have the same name!)
            if (!found) 
            {
                smrMesh.AddBlendShapeFrame(newBs.name, newBs.frameWeight, newBs.verticies, newBs.normals, newBs.tangents);    
            }
        }


        /// <summary>
        /// This will merge new deltas with an existing blendshape
        /// </summary>
        /// <param name="md">The mesh data containing the new deltas</param>
        /// <param name="percentage">The percentage of the new deltas we want to apply to the existing ones (1 == 100%)</param>
        public void MergeMeshDeltas(SkinnedMeshRenderer smr, MeshData md, float percentage = 1f) 
        {
            if (smr == null) return;            
            //A blendshape should alredy exists when calling constructor of this class
            if (blendShape == null) return;
            if (!md.HasDeltas) return;

            if (PregnancyPlusPlugin.DebugLog.Value) PregnancyPlusPlugin.Logger.LogWarning($" MergeDeltas > {smr.name}");

            var mergedBlendShape = new BlendShape();
            mergedBlendShape.name = blendShape.name;

            //Add deltas from both blendshapes together
            mergedBlendShape.verticies = blendShape.verticies.Zip(md.deltaVerticies, (x, y) => x + (y * percentage)).ToArray();
            mergedBlendShape.normals = blendShape.normals.Zip(md.deltaNormals, (x, y) => x + (y * percentage)).ToArray();
            mergedBlendShape.tangents = blendShape.tangents.Zip(md.deltaTangents, (x, y) => x + (y * percentage)).ToArray();

            blendShape = mergedBlendShape;

            //Add the merged blendshape back to the mesh
            AddBlendShapeToMesh(smr);
        }


        /// <summary>
        /// This will change the weight (apperance) of an existing BlendShape. Weight 0 will reset to the default shape.
        /// </summary>
        /// <param name="smr">The skinned mesh renderer to update the blend shape weight</param>
        /// <param name="weight">Float value from 0-40 that will increase the blend to the target shape as the number grows</param>
        /// <param name="floatLerp">When true, will conver the weight from 0-40 weeks to 0-100 blendshape weight, otherwise it expects the value to be 0-100 scale</param>
        /// <returns>boolean true if the blend shape exists</returns>
        public bool ApplyBlendShapeWeight(SkinnedMeshRenderer smr, float weight, bool floatLerp = true) 
        {
            if (!blendShape.isInitilized || weight < 0) return false;
            if (smr == null) return false;

            //Once again if you don't force the mesh to update here, the blendshape below could have stale data
            smr.sharedMesh = smr.sharedMesh;

            //Belly size goes from 0-40, but blendShapes have to be 0-100
            //  Technically unity 2018x + can go above 100 when unclamped, but not any illusion games yet (Requires some game compile flag)
            var lerpWeight = floatLerp ? Mathf.Lerp(0, 100, weight/40) : weight;
            var shapeIndex = GetBlendShapeIndex(smr, blendShape.name);
            //If the blendshape is not found, return
            if (shapeIndex < 0) return false;

            var shapeWeight = smr.GetBlendShapeWeight(shapeIndex);            
            var shapeFrameCount = smr.sharedMesh.GetBlendShapeFrameCount(shapeIndex);
            var shapeName = smr.sharedMesh.GetBlendShapeName(shapeIndex);
            var shapeCount = smr.sharedMesh.blendShapeCount;                 

            smr.SetBlendShapeWeight(shapeIndex, lerpWeight);

            return true;
        }


        //Override for when the smr is already included in this controller.
        public bool ApplyBlendShapeWeight(float weight) 
        {
            return ApplyBlendShapeWeight(smr, weight);
        }


        /// <summary>
        /// Get an existing blendshape by name.  Only returns the first frame since thats all Preg+ uses
        /// </summary>
        /// <param name="smr">The skinned mesh renderer to search for the blend shape</param>
        /// <param name="blendShapeName">The blendshape name to search for</param>
        internal BlendShape GetBlendShapeByName(SkinnedMeshRenderer smr, string blendShapeName) 
        {
            if (smr == null) 
            {
                if (PregnancyPlusPlugin.DebugLog.Value) PregnancyPlusPlugin.Logger.LogWarning($" GetBlendShapeByName > smr should not be mull!");
                return null;
            }

            if (smr.sharedMesh.blendShapeCount <= 0) return null;

            //Check whether the blendshape exists
            var shapeIndex = GetBlendShapeIndex(smr, blendShapeName);

            //If the blendshape is not found return
            if (shapeIndex < 0) return null;

            var shapeFrameCount = smr.sharedMesh.GetBlendShapeFrameCount(shapeIndex);
            if (shapeFrameCount <= 0) 
            {
                if (PregnancyPlusPlugin.DebugLog.Value) PregnancyPlusPlugin.Logger.LogInfo($" GetBlendShapeByName > frame count <= 0: {blendShapeName}");
                return null;
            }

            Vector3[] deltaVertices = new Vector3 [smr.sharedMesh.vertexCount];
            Vector3[] deltaNormals = new Vector3 [smr.sharedMesh.vertexCount];
            Vector3[] deltaTangents = new Vector3 [smr.sharedMesh.vertexCount];

            //Get the blendshape details
            smr.sharedMesh.GetBlendShapeFrameVertices(shapeIndex, 0, deltaVertices, deltaNormals, deltaTangents);
            var name = smr.sharedMesh.GetBlendShapeName(shapeIndex);
            var frameWeight = smr.sharedMesh.GetBlendShapeFrameWeight(shapeIndex, 0);
            var weight = smr.GetBlendShapeWeight(shapeIndex);

            //Copy the blendshape data for the first frame
            var bsFrame = new BlendShape();
            bsFrame.verticies = deltaVertices;
            bsFrame.normals = deltaNormals;
            bsFrame.tangents = deltaTangents;
            bsFrame.weight = weight;
            bsFrame.frameWeight = frameWeight;
            bsFrame.name = name;

            return bsFrame;
        }


        /// <summary>
        /// Check whether a SMR has an existing blendshape with maching name
        /// </summary>
        /// <param name="smr">The skinned mesh renderer to search for the blend shape</param>
        /// <param name="blendShapeName">The blendshape name to search for</param>
        public bool HasBlendshape(SkinnedMeshRenderer smr, string blendShapeName) 
        {
            if (smr == null) 
            {
                if (PregnancyPlusPlugin.DebugLog.Value) PregnancyPlusPlugin.Logger.LogWarning($" HasBlendshape > smr should not be mull!");
                return false;
            }

            if (smr.sharedMesh.blendShapeCount <= 0) return false;

            //Check whether the blendshape exists
            var shapeIndex = GetBlendShapeIndex(smr, blendShapeName);

            //If the blendshape is not found return
            if (shapeIndex < 0) return false;

            return true;
        }


        /// <summary>
        /// Full proof method to fetch the blendshape Index
        ///     Unity API "sharedMesh.GetBlendShapeIndex(string)" sometimes fails to match the name, so do it manually...  (Thanks Unity!)
        /// </summary>
        public int GetBlendShapeIndex(SkinnedMeshRenderer smr, string blendShapeName) 
        {
            var noShapeIndex = -1;
            if (smr == null) 
            {
                if (PregnancyPlusPlugin.DebugLog.Value) PregnancyPlusPlugin.Logger.LogWarning($" GetBlendShapeIndex > smr should not be mull!");
                return noShapeIndex;
            }

            //For each existing blend shape
            for (var i = 0; i < smr.sharedMesh.blendShapeCount; i++)
            {
                //Get this mesh blendshapes name
                var bsName = smr.sharedMesh.GetBlendShapeName(i);                
                //See if the name matches
                if (bsName == blendShapeName) 
                {
                    return i;
                }                
            }

            return noShapeIndex;
        }


        /// <summary>
        /// Remove a single blendshape from a mesh
        ///     Unfortunately Unity makes this difficult, by not having an API to remove 1 at a time.  So we reomve all and add back all -1
        /// </summary>
        /// <param name="smr">The mesh containing the blendshapes</param>
        public bool RemoveBlendShape(SkinnedMeshRenderer smr) 
        {
            //Make sure a blendshape is referenced
            if (blendShape == null) return false;

            var smrMesh = smr.sharedMesh;            
            var bsCount = smrMesh.blendShapeCount;      
            if (bsCount == 0) return true;
                  
            var existingBlendShapes = CopyAllBlendShapeFrames(smr);

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

                    //Otherwise add back the old blend shapes, and weights in the same order (collapsed around the removed one)
                    smrMesh.AddBlendShapeFrame(existingBlendShapes[i][f].name, existingBlendShapes[i][f].frameWeight, existingBlendShapes[i][f].verticies, 
                        existingBlendShapes[i][f].normals, existingBlendShapes[i][f].tangents);
                }
            }

            return true;
        }


        /// <summary>
        /// Copy all blendshape frames from an SMR
        /// </summary>
        /// <param name="smr">The mesh containing the blendshapes</param>
        internal Dictionary<int, BlendShape[]> CopyAllBlendShapeFrames(SkinnedMeshRenderer smr) 
        {
            var existingBlendShapes = new Dictionary<int, BlendShape[]>();
            var smrMesh = smr.sharedMesh;            
            var bsCount = smrMesh.blendShapeCount;  

            //For each shape index that exists
            for (var i = 0; i < bsCount; i++) 
            {
                var weight = smr.GetBlendShapeWeight(i);
                int frameCount = smrMesh.GetBlendShapeFrameCount(i);

                Vector3[] deltaVertices = new Vector3 [smrMesh.vertexCount];
                Vector3[] deltaNormals = new Vector3 [smrMesh.vertexCount];
                Vector3[] deltaTangents = new Vector3 [smrMesh.vertexCount];

                existingBlendShapes[i] = new BlendShape[frameCount];

                //For each frame of the shape index
                for (var f = 0; f < frameCount; f++) 
                {
                    //Get the blendshape details
                    smrMesh.GetBlendShapeFrameVertices(i, f, deltaVertices, deltaNormals, deltaTangents);
                    var name = smrMesh.GetBlendShapeName(i);
                    var frameWeight = smrMesh.GetBlendShapeFrameWeight(i, f);

                    //Copy the blendshape data
                    var bsFrame = new BlendShape();
                    bsFrame.verticies = deltaVertices;
                    bsFrame.normals = deltaNormals;
                    bsFrame.tangents = deltaTangents;
                    bsFrame.weight = weight;
                    bsFrame.frameWeight = frameWeight;
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

    }
}
