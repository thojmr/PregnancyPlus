using KKAPI;
using KKAPI.Chara;
using UnityEngine;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UniRx;
#if HS2 || AI
    using AIChara;
#endif

namespace KK_PregnancyPlus
{

    //This partial class contains the blendshape logic
    public partial class PregnancyPlusCharaController: CharaCustomFunctionController
    {           

        //Allows us to identify which mesh a blendshape belongs to when loading character cards
        public class MeshBlendShape
        {
            public string MeshName;//like smr.name
            public int VertexCount;//To differentiate 2 meshes with the same names use vertex count comparison
            public BlendShapeController.BlendShape BlendShape;

            public MeshBlendShape(string meshName, BlendShapeController.BlendShape blendShape, int vertexCount) 
            {
                MeshName = meshName;
                BlendShape = blendShape;
                VertexCount = vertexCount;
            }
        }


        /// <summary>
        /// Will loop through each mesh and as long as it has some inflation value, it will append a blendshape to the smr
        ///  Only needed in KK for now, triggered by studio button
        /// </summary>
        /// <returns>boolean true if any blendshapes were created</returns>
        internal bool OnCreateBlendShapeSelected() 
        {
            if (PregnancyPlusPlugin.debugLog)  PregnancyPlusPlugin.Logger.LogInfo($" ");
            if (PregnancyPlusPlugin.debugLog)  PregnancyPlusPlugin.Logger.LogInfo($" OnCreateBlendShapeSelected ");
            var anyBlendShapesCreated = false;

            //Get all cloth renderes and attemp to create blendshapes from preset inflatedVerticies
            var clothRenderers = PregnancyPlusHelper.GetMeshRenderers(ChaControl.objClothes);
            anyBlendShapesCreated = LoopAndCreateBlendShape(clothRenderers, anyBlendShapesCreated, true);

            //do the same for body meshs
            var bodyRenderers = PregnancyPlusHelper.GetMeshRenderers(ChaControl.objBody);
            anyBlendShapesCreated = LoopAndCreateBlendShape(bodyRenderers, anyBlendShapesCreated);

            return anyBlendShapesCreated;
        }


        /// <summary>
        /// Loop through each skinned mesh rendere and if it has inflated verts, create a blendshape from them
        /// </summary>
        /// <param name="smrs">List of skinnedMeshRenderes</param>
        /// <param name="anyBlendShapesCreated">If any mesh changes have happened so far</param>
        /// <param name="isClothingMesh">If this smr is a cloth mesh</param>
        /// <returns>boolean true if any meshes were changed</returns>
        internal bool LoopAndCreateBlendShape(List<SkinnedMeshRenderer> smrs, bool anyBlendShapesCreated, bool isClothingMesh = false) 
        {
            foreach(var smr in smrs) 
            {                
                var renderKey = GetMeshKey(smr);
                var exists = inflatedVertices.ContainsKey(renderKey);

                //Dont create blend shape if no inflated verts exists
                if (!exists || inflatedVertices[renderKey].Length < 0) continue;

                var appliedClothBSChanges = CreateBlendShape(smr, renderKey);
                if (appliedClothBSChanges) anyBlendShapesCreated = true;
            }  

            return anyBlendShapesCreated;
        }
    

        /// <summary>
        /// This will create a blendshape frame for a mesh, that can be used in timeline, required there be a renderKey for inflatedVertices for this smr
        ///  Only needed in KK for now
        /// </summary>
        /// <param name="mesh">Target mesh to update</param>
        /// <param name="renderKey">The Shared Mesh render name, used in dictionary keys to get the current verticie values</param>
        /// <returns>Will return True if any the blendshape was created</returns>
        internal bool CreateBlendShape(SkinnedMeshRenderer smr, string renderKey) 
        {     
            var meshCopyOrig = PregnancyPlusHelper.CopyMesh(smr.sharedMesh);   
            if (!meshCopyOrig.isReadable) 
            {
                if (PregnancyPlusPlugin.debugLog)  PregnancyPlusPlugin.Logger.LogInfo(
                     $"CreateBlendShape > smr '{renderKey}' is not readable, skipping");
                return false;
            } 

            //Check key exists in dict, remnove it if it does not
            var exists = originalVertices.TryGetValue(renderKey, out var val);
            if (!exists) 
            {
                if (PregnancyPlusPlugin.debugLog)  PregnancyPlusPlugin.Logger.LogInfo(
                     $"CreateBlendShape > smr '{renderKey}' does not exists, skipping");
                return false;
            }

            var bellyVertIndex = bellyVerticieIndexes[renderKey];
            if (bellyVertIndex.Length == 0) return false;

            if (originalVertices[renderKey].Length != meshCopyOrig.vertexCount) 
            {
                PregnancyPlusPlugin.Logger.LogInfo(
                            $"CreateBlendShape > smr.sharedMesh '{renderKey}' has incorrect vert count {originalVertices[renderKey].Length}|{meshCopyOrig.vertexCount}");
                return false;
            }

            //Calculate new normals
            meshCopyOrig.vertices = originalVertices[renderKey];
            meshCopyOrig.RecalculateBounds();
            NormalSolver.RecalculateNormals(meshCopyOrig, 40f, bellyVerticieIndexes[renderKey]);
            meshCopyOrig.RecalculateTangents();

            //Create blend shape for Timeline
            var blendShapeController = new BlendShapeController(meshCopyOrig, smr, $"{renderKey}_{PregnancyPlusPlugin.GUID}");

            //Add it to character data so it can be saved if needed
            if (blendShapeController != null && blendShapeController.blendShape.isInitilized) 
            {
                AddBlendShapeToData(smr.name, blendShapeController.blendShape);
            } 

            return true;
        }   


        /// <summary>
        /// Allows a new  blendshape to be added to character data so it can be saved with the character card
        /// </summary>
        /// <param name="smrName">The mesh name</param>
        /// <param name="blendShape">The blendshape we just created</param>
        internal void AddBlendShapeToData(string smrName, BlendShapeController.BlendShape blendShape) 
        {
            var meshBlendShape = new MeshBlendShape(smrName, blendShape, blendShape.vertexCount);
            infConfig.meshBlendShape.Add(meshBlendShape);
        }


        /// <summary>
        /// Loads a blendshape from character card and sets it to the correct mesh
        /// </summary>
        /// <param name="smrName">The mesh name</param>
        /// <param name="blendShape">The blendshape we just created</param>
        internal void LoadBlendShapes(PregnancyPlusData data) 
        {
            if (data.meshBlendShape == null || data.meshBlendShape.Count <= 0) return;
            if (PregnancyPlusPlugin.debugLog)  PregnancyPlusPlugin.Logger.LogInfo($" LoadBlendShapes ");

            //For each stores meshBlendShape
            foreach(var meshBlendShape in data.meshBlendShape)
            {
                //Loop through all meshes and find matching name
                var clothRenderers = PregnancyPlusHelper.GetMeshRenderers(ChaControl.objClothes, true);
                LoopMeshForBlendShape(clothRenderers, meshBlendShape);

                //do the same for body meshs
                var bodyRenderers = PregnancyPlusHelper.GetMeshRenderers(ChaControl.objBody, true);
                LoopMeshForBlendShape(bodyRenderers, meshBlendShape);
            }
        }


        /// <summary>
        /// Loop through each mesh, and if the name/vertexcount matches, append the blendshape
        /// </summary>
        internal void LoopMeshForBlendShape(List<SkinnedMeshRenderer> smrs, MeshBlendShape meshBlendShape) 
        {
            var meshName = meshBlendShape.MeshName;
            var vertexCount = meshBlendShape.VertexCount;
            var blendShape = meshBlendShape.BlendShape; 
            
            foreach(var smr in smrs) 
            {              
                //If mesh matches, append the blend shape
                if (smr.name == meshName && smr.sharedMesh.vertexCount == vertexCount) 
                {
                    //Make sure the blendshape does not already exists
                    if (BlendShapeAlreadyExists(smr, meshBlendShape.BlendShape)) continue;
                    if (PregnancyPlusPlugin.debugLog)  PregnancyPlusPlugin.Logger.LogInfo($" LoopMeshForBlendShape > match {smr.name} ");

                    //Add the blendshape to the mesh
                    new BlendShapeController(smr, blendShape);
                }
            }  
        }
                

        /// <summary>
        /// Check whether the blendshape already exists
        /// </summary>
        internal bool BlendShapeAlreadyExists(SkinnedMeshRenderer smr, BlendShapeController.BlendShape blendShape) 
        {
            var shapeIndex = smr.sharedMesh.GetBlendShapeIndex(blendShape.name);
            //If the shape exists then true
            return (shapeIndex >= 0);
        }
    }
}


