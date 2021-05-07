using KKAPI;
using KKAPI.Chara;
using UnityEngine;
using System;
using System.Collections;
using System.Collections.Generic;
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

        internal string blendShapeTempTagName = "[temp]";//Used to identify blendshapes created by the p+ sliders, and not the blendshape gui sliders (which are more permanent).


        //Allows us to identify which mesh a blendshape belongs to when loading character cards
        [MessagePackObject(keyAsPropertyName: true)]
        public class MeshBlendShape
        {
            public string MeshName;//like SkinnedMeshRenderer.name
            public int VertCount;//To differentiate 2 meshes with the same names use vertex count comparison
            public BlendShapeController.BlendShape BlendShape;//Store just a single Frame for now, though its possible to have multiple frames.  Preg+ only uses 1

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
        /// <param name="temporary">If Temporary, the blendshape will not be saved to char card</param>
        /// <returns>boolean true if any blendshapes were created</returns>
        internal bool OnCreateBlendShapeSelected(bool temporary = false) 
        {
            if (PregnancyPlusPlugin.DebugLog.Value)  PregnancyPlusPlugin.Logger.LogInfo($" ");
            if (PregnancyPlusPlugin.DebugLog.Value)  PregnancyPlusPlugin.Logger.LogInfo($" OnCreateBlendShapeSelected ");

            var meshBlendShapes = new List<MeshBlendShape>();
            meshWithBlendShapes = new List<SkinnedMeshRenderer>();

            //Get all cloth renderes and attempt to create blendshapes from preset inflatedVerticies
            var clothRenderers = PregnancyPlusHelper.GetMeshRenderers(ChaControl.objClothes);
            meshBlendShapes = LoopAndCreateBlendShape(clothRenderers, meshBlendShapes);

            //do the same for body meshs
            var bodyRenderers = PregnancyPlusHelper.GetMeshRenderers(ChaControl.objBody);
            meshBlendShapes = LoopAndCreateBlendShape(bodyRenderers, meshBlendShapes);

            //Save any meshBlendShapes to card
            if (!temporary) AddBlendShapesToData(meshBlendShapes);

            //Reset belly size to 0 so the blendshape can be used with out interference
            PregnancyPlusGui.ResetSlider(PregnancyPlusGui.inflationSize, 0);

            //Append the smrs that have new blendspahes to the GUI to be seen
            blendShapeGui.OnSkinnedMeshRendererBlendShapesCreated(meshWithBlendShapes);

            return meshBlendShapes.Count > 0;
        }


        internal void OnOpenBlendShapeSelected()
        {
            //GUI blendshape popup, with existing blendshapes if any exists
            blendShapeGui.OpenBlendShapeGui(meshWithBlendShapes, this);
        }


        /// <summary>
        /// When the user wants to remove all existing PregnancyPlus GUI blendshapes
        /// </summary>
        internal void OnRemoveAllGUIBlendShapes()
        {
            //Set all GUI blendshapes to 0 weight
            foreach (var smr in meshWithBlendShapes)
            {
                if (smr == null) continue;

                for (var i = 0; i < smr.sharedMesh.blendShapeCount; i++)
                {
                    //Search for GUI blendshape
                    var name = smr.sharedMesh.GetBlendShapeName(i);
                    if (name.EndsWith(PregnancyPlusPlugin.GUID))
                    {
                        //Set weight to 0
                        var bsc = new BlendShapeController(smr, name);
                        bsc.ApplyBlendShapeWeight(smr, 0);
                    }
                }
            }
            meshWithBlendShapes = new List<SkinnedMeshRenderer>();
            ClearBlendShapesFromCharData();
        }


        /// <summary>
        /// When you want to start fresh and remove all (non GUI) Preg+ blendshapes completely.
        /// </summary>
        internal void ScrubBlendShapes()
        {
            var renderers = PregnancyPlusHelper.GetMeshRenderers(ChaControl.objClothes);            
            var bodyRenderers = PregnancyPlusHelper.GetMeshRenderers(ChaControl.objBody, true);
            renderers.AddRange(bodyRenderers);

            //Remove any Preg+ blendshapes
            foreach (var smr in renderers)
            {
                for (var i = 0; i < smr.sharedMesh.blendShapeCount; i++)
                {
                    //Search for GUI blendshape
                    var name = smr.sharedMesh.GetBlendShapeName(i);
                    var blendShapePartialName = MakeBlendShapeName(GetMeshKey(smr), blendShapeTempTagName);

                    if (name.EndsWith(blendShapePartialName))
                    {
                        //Remove the blendshape
                        var bsc = new BlendShapeController(smr, name);
                        bsc.RemoveBlendShape(smr);
                    }
                }
            }

        }


        /// <summary>
        /// Loop through each skinned mesh rendere and if the char card has a blendshape for it, add it
        /// </summary>
        /// <param name="smrs">List of skinnedMeshRenderes</param>
        /// <param name="meshBlendShapes">the current list of MeshBlendShapes collected so far</param>
        /// <returns>Returns final list of MeshBlendShapes we want to store in char card</returns>
        internal List<MeshBlendShape> LoopAndCreateBlendShape(List<SkinnedMeshRenderer> smrs, List<MeshBlendShape> meshBlendShapes) 
        {
            foreach(var smr in smrs) 
            {                
                var renderKey = GetMeshKey(smr);
                var exists = inflatedVertices.ContainsKey(renderKey);

                //Dont create blend shape if no inflated verts exists
                if (!exists || inflatedVertices[renderKey].Length < 0) continue;

                var blendShapeCtrl = CreateBlendShape(smr, renderKey);
                //Return the blendshape format that can be saved to character card
                var meshBlendShape = ConvertToMeshBlendShape(smr.name, blendShapeCtrl.blendShape);
                if (meshBlendShape != null) 
                {
                    meshBlendShapes.Add(meshBlendShape);                
                    meshWithBlendShapes.Add(smr);
                }

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
        /// Clears any card data blendshapes (needs user save to apply though)
        /// </summary>
        internal void ClearBlendShapesFromCharData() 
        {            
            infConfig.meshBlendShape = null;
        }


        /// <summary>
        /// Loads a blendshape from character card and sets it to the correct mesh
        /// </summary>
        /// <param name="data">The characters card data for this plugin</param>
        internal void LoadBlendShapes(PregnancyPlusData data) 
        {
            if (data.meshBlendShape == null) return;
            if (PregnancyPlusPlugin.DebugLog.Value)  PregnancyPlusPlugin.Logger.LogInfo($" MeshBlendShape size > {data.meshBlendShape.Length/1024}KB ");

            meshWithBlendShapes = new List<SkinnedMeshRenderer>();

            //Unserialize the blendshape from characters card
            var meshBlendShapes = MessagePack.LZ4MessagePackSerializer.Deserialize<List<MeshBlendShape>>(data.meshBlendShape);
            if (meshBlendShapes == null || meshBlendShapes.Count <= 0) return;
            if (PregnancyPlusPlugin.DebugLog.Value)  PregnancyPlusPlugin.Logger.LogInfo($" MeshBlendShape count > {meshBlendShapes.Count} ");

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
                //Fixes any stale data on mesh blendshapes           
                smr.sharedMesh = smr.sharedMesh;

                //If mesh matches, append the blend shape
                if (smr.name == meshName && smr.sharedMesh.vertexCount == vertexCount) 
                {
                    meshWithBlendShapes.Add(smr);

                    //Make sure the blendshape does not already exists
                    if (BlendShapeAlreadyExists(smr, meshBlendShape.BlendShape.name)) {
                        //If it does, make sure the weights are correct incase char just reloaded
                        //Try to find an existing blendshape by name
                        BlendShapeController bsc = new BlendShapeController(smr, meshBlendShape.BlendShape.name);

                        if (bsc.blendShape == null) {
                            if (PregnancyPlusPlugin.DebugLog.Value)  PregnancyPlusPlugin.Logger.LogWarning(
                                $"LoopMeshAndAddExistingBlendShape > There was a problem finding the blendshape ${meshBlendShape.BlendShape.name}");
                            continue;
                        }
                        
                        //Update the weight to match the weight from the character card   
                        bsc.ApplyBlendShapeWeight(smr, meshBlendShape.BlendShape.weight);
                        continue;
                    }

                    //Add the blendshape to the mesh
                    new BlendShapeController(blendShape, smr);
                } 
                else if (smr.name == meshName && smr.sharedMesh.vertexCount != vertexCount)
                {
                    //When the mesh vertex count is different now, warn the user that their blendshape is not going to load
                    PregnancyPlusPlugin.errorCodeCtrl.LogErrorCode(ChaControl.chaID, ErrorCode.PregPlus_BodyMeshVertexChanged, 
                        $" Mesh '{smr.name}' has a different vertex count, and no longer fits the BlendShape saved to this card.  Blendshape {meshBlendShape.BlendShape.name} skipped."); 
                }               
            }              
        }
                

        /// <summary>
        /// Check whether the blendshape already exists
        /// </summary>
        internal bool BlendShapeAlreadyExists(SkinnedMeshRenderer smr, string blendShapeName) 
        {
            var shapeIndex = smr.sharedMesh.GetBlendShapeIndex(blendShapeName);
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

                if (PregnancyPlusPlugin.DebugLog.Value) PregnancyPlusPlugin.Logger.LogInfo($" LogMeshBlendShapes > {name} shapeIndex {i} weight {weight} frameCount {frameCount} deltaVertices {deltaVertices.Length}");            
            }
        }


        
        /// <summary>
        /// This will create a blendshape frame for a mesh, there must be a matching inflatedVertices[renderKey] for the target mesh shape
        /// </summary>
        /// <param name="smr">Target mesh renderer to update (original shape)</param>
        /// <param name="renderKey">The Shared Mesh render name, used in dictionary keys to get the current verticie values</param>
        /// <param name="blendShapeTag">Optional blend shape tag to append to the blend shape name, used for identification if needed</param>
        /// <returns>Returns the MeshBlendShape that is created. Can be null</returns>
        internal BlendShapeController CreateBlendShape(SkinnedMeshRenderer smr, string renderKey, string blendShapeTag = null) 
        {     
            //Make a copy of the mesh. We dont want to affect the existing for this
            var meshCopyTarget = PregnancyPlusHelper.CopyMesh(smr.sharedMesh);   
            if (!meshCopyTarget.isReadable) 
            {
                PregnancyPlusPlugin.errorCodeCtrl.LogErrorCode(ChaControl.chaID, ErrorCode.PregPlus_MeshNotReadable, 
                    $"CreateBlendShape > smr '{renderKey}' is not readable, skipping");                     
                return null;
            } 

            //Make sure we have an existing belly shape to work with (can be null if user hasnt used sliders yet)
            var exists = inflatedVertices.TryGetValue(renderKey, out var val);
            if (!exists) 
            {
                if (PregnancyPlusPlugin.DebugLog.Value)  PregnancyPlusPlugin.Logger.LogInfo(
                     $"CreateBlendShape > smr '{renderKey}' inflatedVertices do not exists, skipping");
                return null;
            }

            //Make sure the vertex count matches what the blendshape has (differs when swapping meshes)
            if (inflatedVertices[renderKey].Length != meshCopyTarget.vertexCount) 
            {
                PregnancyPlusPlugin.errorCodeCtrl.LogErrorCode(ChaControl.chaID, ErrorCode.PregPlus_IncorrectVertCount, 
                    $"CreateBlendShape > smr.sharedMesh '{renderKey}' has incorrect vert count {inflatedVertices[renderKey].Length}|{meshCopyTarget.vertexCount}");  
                return null;
            }

            //Calculate the new normals, but don't show them.  We just want it for the blendshape shape target
            meshCopyTarget.vertices = inflatedVertices[renderKey];
            meshCopyTarget.RecalculateBounds();
            NormalSolver.RecalculateNormals(meshCopyTarget, 40f, alteredVerticieIndexes[renderKey]);
            meshCopyTarget.RecalculateTangents();

            // LogMeshBlendShapes(smr);

            var blendShapeName = MakeBlendShapeName(renderKey, blendShapeTag);

            //Create a blend shape object on the mesh, and return the controller object
            return new BlendShapeController(smr.sharedMesh, meshCopyTarget, blendShapeName, smr);            
        }  

        internal string MakeBlendShapeName(string renderKey, string blendShapeTag = null) {
            return blendShapeTag == null ? $"{renderKey}_{PregnancyPlusPlugin.GUID}" : $"{renderKey}_{PregnancyPlusPlugin.GUID}_{blendShapeTag}";
        }


        /// <summary>
        /// Find a blendshape by name on a smr, and change its weight.  If it does not exists, create it.
        /// </summary>
        /// <param name="smr">Target mesh renderer to update (original shape)</param>
        /// <param name="renderKey">The Shared Mesh render name, used in dictionary keys to get the current verticie values</param>
        /// <param name="needsOverwrite">Whether the blendshape needs to be remade because the mesh shape was altered</param>
        /// <param name="blendShapeTag">Optional blend shape tag to append to the blend shape name, used for identification if needed</param>
        internal bool ApplyBlendShapeWeight(SkinnedMeshRenderer smr, string renderKey, bool needsOverwrite, string blendShapeTag = null) {

            var blendShapeName = MakeBlendShapeName(renderKey, blendShapeTag);
            //Try to find an existing blendshape by name
            BlendShapeController bsc = new BlendShapeController(smr, blendShapeName);
            
            //If not found then create it
            if (bsc.blendShape == null || needsOverwrite) bsc = CreateBlendShape(smr, renderKey, blendShapeTag);

            if (bsc.blendShape == null) {
                if (PregnancyPlusPlugin.DebugLog.Value)  PregnancyPlusPlugin.Logger.LogWarning(
                     $"ApplyBlendShapeWeight > There was a problem creating the blendshape ${blendShapeName}");
                return false;
            }
            
            var size = isDuringInflationScene ? TargetPregPlusSize : infConfig.inflationSize;

            //Update the weight to be the same as inflationSize value   
            return bsc.ApplyBlendShapeWeight(smr, size);
        }


        /// <summary>
        /// Reset a blendshape weight back to 0
        /// </summary>
        /// <param name="smr">Target mesh renderer to update (original shape)</param>
        /// <param name="renderKey">The Shared Mesh renderkey name, used to calculate the blendshape name</param>
        /// <param name="blendShapeTag">Optional blend shape tag to append to the blend shape name, used for identification if needed</param>
        internal bool ResetBlendShapeWeight(SkinnedMeshRenderer smr, string renderKey, string blendShapeTag = null) {
            var blendShapeName = MakeBlendShapeName(renderKey, blendShapeTag);

            //Try to find an existing blendshape by name
            BlendShapeController bsc = new BlendShapeController(smr, blendShapeName);
            if (bsc.blendShape == null) return false;

            return bsc.ApplyBlendShapeWeight(smr, 0);
        }


        /// <summary>
        /// Compute the blendshapes for a character to be used during inflation HScene (Does not apply the shape)
        /// </summary>
        /// <param name="smr">Target mesh renderer to update (original shape)</param>
        internal List<BlendShapeController> ComputeInflationBlendShapes() {
            var blendshapes = new List<BlendShapeController>();

            //Trigger inflation at 0 size to create the blendshapes
            MeshInflate(0, false, false, true);

            var clothRenderers = PregnancyPlusHelper.GetMeshRenderers(ChaControl.objClothes);            
            foreach(var smr in clothRenderers)
            {
                var blendShapeName = MakeBlendShapeName(GetMeshKey(smr), blendShapeTempTagName);
                var blendshapeCtrl = new BlendShapeController(smr, blendShapeName);
                if (blendshapeCtrl.blendShape != null) blendshapes.Add(blendshapeCtrl);
            }

            var bodyRenderers = PregnancyPlusHelper.GetMeshRenderers(ChaControl.objBody, true);
            foreach(var smr in bodyRenderers)
            {
                var blendShapeName = MakeBlendShapeName(GetMeshKey(smr), blendShapeTempTagName);
                var blendshapeCtrl = new BlendShapeController(smr, blendShapeName);
                if (blendshapeCtrl.blendShape != null) blendshapes.Add(blendshapeCtrl);
            }

            if (PregnancyPlusPlugin.DebugLog.Value)  PregnancyPlusPlugin.Logger.LogInfo(
                     $"ComputeInflationBlendShapes > Found {blendshapes.Count} blendshapes ");

            return blendshapes;
        }
    }
}


