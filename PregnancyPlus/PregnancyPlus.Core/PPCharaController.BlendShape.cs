using KKAPI;
using KKAPI.Chara;
using UnityEngine;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UniRx;
using MessagePack;

#if HS2 || AI
    using AIChara;
#endif

namespace KK_PregnancyPlus
{

    //This partial class contains the blendshape logic for KK Timelines (and VNGE in future)
    public partial class PregnancyPlusCharaController: CharaCustomFunctionController
    {           

        //Keep track of which meshes are given blendshapes for the GUI to make the slider list
        internal List<SkinnedMeshRenderer> meshWithBlendShapes = new List<SkinnedMeshRenderer>();


        //Allows us to identify which mesh a blendshape belongs to when loading character cards
        [MessagePackObject(keyAsPropertyName: true)]
        public class MeshBlendShape
        {
            public string MeshName;//like smr.name
            public int VertCount;//To differentiate 2 meshes with the same names use vertex count comparison
            public BlendShapeController.BlendShape BlendShape;//Just a single Frame for now, though its possible to have multiple frames

            public MeshBlendShape(string meshName, BlendShapeController.BlendShape blendShape, int vertCount) 
            {
                MeshName = meshName;
                BlendShape = blendShape;
                VertCount = vertCount;
            }
        }


        /// <summary>
        /// On user button click. Create blendshape from current belly state.  Add it to infConfig so it will be saved to char card if the user chooses save scene
        /// </summary>
        /// <returns>boolean true if any blendshapes were created</returns>
        internal bool OnCreateBlendShapeSelected() 
        {
            if (PregnancyPlusPlugin.debugLog)  PregnancyPlusPlugin.Logger.LogInfo($" ");
            if (PregnancyPlusPlugin.debugLog)  PregnancyPlusPlugin.Logger.LogInfo($" OnCreateBlendShapeSelected ");

            var meshBlendShapes = new List<MeshBlendShape>();
            meshWithBlendShapes = new List<SkinnedMeshRenderer>();

            //Get all cloth renderes and attemp to create blendshapes from preset inflatedVerticies
            var clothRenderers = PregnancyPlusHelper.GetMeshRenderers(ChaControl.objClothes);
            meshBlendShapes = LoopAndCreateBlendShape(clothRenderers, meshBlendShapes, true);

            //do the same for body meshs
            var bodyRenderers = PregnancyPlusHelper.GetMeshRenderers(ChaControl.objBody);
            meshBlendShapes = LoopAndCreateBlendShape(bodyRenderers, meshBlendShapes);

            //Save any meshBlendShapes to card
            AddBlendShapesToData(meshBlendShapes);

            //Reset belly size to 0 so the blendshape can be used with out interference
            MeshInflate(0);

            PregnancyPlusPlugin.OpenBlendShapeGui(meshWithBlendShapes);

            return meshBlendShapes.Count > 0;
        }


        /// <summary>
        /// Loop through each skinned mesh rendere and if it has inflated verts, create a blendshape from them
        /// </summary>
        /// <param name="smrs">List of skinnedMeshRenderes</param>
        /// <param name="meshBlendShapes">the current list of MeshBlendShapes collected so far</param>
        /// <returns>Returns final list of MeshBlendShapes we want to store in char card</returns>
        internal List<MeshBlendShape> LoopAndCreateBlendShape(List<SkinnedMeshRenderer> smrs, List<MeshBlendShape> meshBlendShapes, bool isClothingMesh = false) 
        {
            foreach(var smr in smrs) 
            {                
                var renderKey = GetMeshKey(smr);
                var exists = inflatedVertices.ContainsKey(renderKey);

                //Dont create blend shape if no inflated verts exists
                if (!exists || inflatedVertices[renderKey].Length < 0) continue;

                var meshBlendShape = CreateBlendShape(smr, renderKey);
                if (meshBlendShape != null) meshBlendShapes.Add(meshBlendShape);
                meshWithBlendShapes.Add(smr);

                // LogMeshBlendShapes(smr);
            }  

            return meshBlendShapes;
        }
     

        /// <summary>
        /// Convert a BlendShape to MeshBlendShape, used for storing to character card data
        /// </summary>
        internal MeshBlendShape ConvertToMeshBlendShape(string smrName, BlendShapeController.BlendShape blendShape) 
        {            
            if (blendShape == null) return null;
            var meshBlendShape = new MeshBlendShape(smrName, blendShape, blendShape.vertexCount);
            return meshBlendShape;
        }


        /// <summary>
        /// Sets a custom meshBlendShape object to character data
        /// </summary>
        /// <param name="meshBlendShapes">the list of MeshBlendShapes we want to save</param>
        internal void AddBlendShapesToData(List<MeshBlendShape> meshBlendShapes) 
        {            
            infConfig.meshBlendShape = MessagePack.LZ4MessagePackSerializer.Serialize(meshBlendShapes);
        }


        /// <summary>
        /// Loads a blendshape from character card and sets it to the correct mesh
        /// </summary>
        /// <param name="data">The characters card data for this plugin</param>
        internal void LoadBlendShapes(PregnancyPlusData data) 
        {
            if (data.meshBlendShape == null) return;
            if (PregnancyPlusPlugin.debugLog)  PregnancyPlusPlugin.Logger.LogInfo($" MeshBlendShape size > {data.meshBlendShape.Length/1024}KB ");
            //Unserialize the blendshape from characters card
            var meshBlendShapes = MessagePack.LZ4MessagePackSerializer.Deserialize<List<MeshBlendShape>>(data.meshBlendShape);
            if (meshBlendShapes == null || meshBlendShapes.Count <= 0) return;

            //For each stores meshBlendShape
            foreach(var meshBlendShape in meshBlendShapes)
            {
                //Loop through all meshes and find matching name
                var clothRenderers = PregnancyPlusHelper.GetMeshRenderers(ChaControl.objClothes, true);
                LoopMeshAndAddExistingBlendShape(clothRenderers, meshBlendShape, true);

                //do the same for body meshs
                var bodyRenderers = PregnancyPlusHelper.GetMeshRenderers(ChaControl.objBody, true);
                LoopMeshAndAddExistingBlendShape(bodyRenderers, meshBlendShape);
            }            
        }


        /// <summary>
        /// Loop through each mesh, and if the name/vertexcount matches, append the blendshape
        /// </summary>
        /// <param name="smrs">List of skinnedMeshRenderers to check for matching mesh name</param>
        /// <param name="meshBlendShape">The MeshBlendShape loaded from character card</param>
        internal void LoopMeshAndAddExistingBlendShape(List<SkinnedMeshRenderer> smrs, MeshBlendShape meshBlendShape, bool isClothingMesh = false) 
        {
            var meshName = meshBlendShape.MeshName;
            var vertexCount = meshBlendShape.VertCount;
            var blendShape = meshBlendShape.BlendShape; 
            
            foreach (var smr in smrs) 
            {              
                //If mesh matches, append the blend shape
                if (smr.name == meshName && smr.sharedMesh.vertexCount == vertexCount) 
                {
                    //Make sure the blendshape does not already exists
                    if (BlendShapeAlreadyExists(smr, meshBlendShape.BlendShape)) continue;

                    //Add the blendshape to the mesh
                    new BlendShapeController(smr, blendShape);

                    // LogMeshBlendShapes(smr);
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


        //just for debugging
        public void LogMeshBlendShapes(SkinnedMeshRenderer smr) 
        {
            var bsCount = smr.sharedMesh.blendShapeCount;

            //For each existing blend shape
            for (var i = 0; i < bsCount; i++)
            {
                Vector3[] deltaVertices = new Vector3 [smr.sharedMesh.vertexCount];
                Vector3[] deltaNormals = new Vector3 [smr.sharedMesh.vertexCount];
                Vector3[] deltaTangents = new Vector3 [smr.sharedMesh.tangents.Length];

                var name = smr.sharedMesh.GetBlendShapeName(i);
                var weight = smr.sharedMesh.GetBlendShapeFrameWeight(i, 0);
                var frameCount = smr.sharedMesh.GetBlendShapeFrameCount(i);
                smr.sharedMesh.GetBlendShapeFrameVertices(i, 0, deltaVertices, deltaNormals, deltaTangents);

                if (PregnancyPlusPlugin.debugLog) PregnancyPlusPlugin.Logger.LogInfo($" LogMeshBlendShapes > {name} shapeIndex {i} weight {weight} frameCount {frameCount} deltaVertices {deltaVertices.Length}");            
            }
        }


        
        /// <summary>
        /// This will create a blendshape frame for a mesh, that can be used in timeline, required there be a renderKey for inflatedVertices for this smr
        /// </summary>
        /// <param name="smr">Target mesh renderer to update (original shape)</param>
        /// <param name="renderKey">The Shared Mesh render name, used in dictionary keys to get the current verticie values</param>
        /// <returns>Returns the MeshBlendShape that is created. Can be null</returns>
        internal MeshBlendShape CreateBlendShape(SkinnedMeshRenderer smr, string renderKey) 
        {     
            //Make a copy of the mesh. We dont want to affect the existing for this
            var meshCopyOrig = PregnancyPlusHelper.CopyMesh(smr.sharedMesh);   
            if (!meshCopyOrig.isReadable) 
            {
                if (PregnancyPlusPlugin.debugLog)  PregnancyPlusPlugin.Logger.LogInfo(
                     $"CreateBlendShape > smr '{renderKey}' is not readable, skipping");
                return null;
            } 

            //Make sure we have an existing belly shape to work with (can be null if user hasnt used sliders yet)
            var exists = originalVertices.TryGetValue(renderKey, out var val);
            if (!exists) 
            {
                if (PregnancyPlusPlugin.debugLog)  PregnancyPlusPlugin.Logger.LogInfo(
                     $"CreateBlendShape > smr '{renderKey}' does not exists, skipping");
                return null;
            }

            if (originalVertices[renderKey].Length != meshCopyOrig.vertexCount) 
            {
                PregnancyPlusPlugin.Logger.LogInfo(
                            $"CreateBlendShape > smr.sharedMesh '{renderKey}' has incorrect vert count {originalVertices[renderKey].Length}|{meshCopyOrig.vertexCount}");
                return null;
            }

            //Calculate the original normals, but don't show them.  We just want it for the blendshape shape origin
            meshCopyOrig.vertices = originalVertices[renderKey];
            meshCopyOrig.RecalculateBounds();
            NormalSolver.RecalculateNormals(meshCopyOrig, 40f, bellyVerticieIndexes[renderKey]);
            meshCopyOrig.RecalculateTangents();

            //Create blend shape object on the mesh
            var bsc = new BlendShapeController(meshCopyOrig, smr, $"{renderKey}_{PregnancyPlusPlugin.GUID}");

            //Return the blendshape format that can be saved to character card
            return ConvertToMeshBlendShape(smr.name, bsc.blendShape);
        }  
    }
}


