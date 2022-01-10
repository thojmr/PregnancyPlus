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

    //This partial class contains the blendshape logic for Timeline and VNGE
    public partial class PregnancyPlusCharaController: CharaCustomFunctionController
    {           

        //Keep track of which meshes are given blendshapes for the GUI to make the slider list
        internal List<MeshIdentifier> meshWithBlendShapes = new List<MeshIdentifier>();

        internal string blendShapeTempTagName = "[temp]";//Used to identify blendshapes created by the p+ sliders, and not the blendshape gui sliders (which are more permanent).


        //Allows us to identify which mesh a blendshape belongs to when loading character cards
        [MessagePackObject(keyAsPropertyName: true)]
        public class MeshBlendShape
        {
            public string MeshName;//like SkinnedMeshRenderer.name
            public int VertCount;//To differentiate 2 meshes with the same names use vertex count comparison
            public string UncensorGUID;//Stores the uncensorGUID used with this blendshape
            public BlendShapeController.BlendShape BlendShape;//Store just a single Frame for now, though its possible to have multiple frames.  Preg+ only uses 1

            public MeshBlendShape(string meshName, BlendShapeController.BlendShape blendShape, int vertCount, string uncensorGUID) 
            {
                MeshName = meshName;
                BlendShape = blendShape;
                VertCount = vertCount;
                UncensorGUID = uncensorGUID;
            }
        }


        /// <summary>
        /// On user button click. Create blendshape from current belly state.  Add it to infConfig so it will be saved to char card if the user chooses save scene
        /// </summary>
        /// <param name="temporary">If Temporary, the blendshape will not be saved to char card</param>
        /// <returns>true if any blendshapes were created</returns>
        internal bool OnCreateBlendShapeSelected(bool temporary = false) 
        {
            if (PregnancyPlusPlugin.DebugLog.Value)  PregnancyPlusPlugin.Logger.LogInfo($" ");
            if (PregnancyPlusPlugin.DebugLog.Value)  PregnancyPlusPlugin.Logger.LogInfo($" OnCreateBlendShapeSelected ");

            var meshBlendShapes = new List<MeshBlendShape>();
            meshWithBlendShapes = new List<MeshIdentifier>();

            var uncensorGUID = GetUncensorGuid();

            //Get all cloth renderes and attempt to create blendshapes from current inflatedVerticies
            var clothRenderers = PregnancyPlusHelper.GetMeshRenderers(ChaControl.objClothes);
            meshBlendShapes = LoopAndCreateBlendShape(clothRenderers, meshBlendShapes, uncensorGUID);

            //do the same for body mesh
            var bodyRenderers = PregnancyPlusHelper.GetMeshRenderers(ChaControl.objBody);
            meshBlendShapes = LoopAndCreateBlendShape(bodyRenderers, meshBlendShapes, uncensorGUID);

            //Save meshBlendShapes to card
            if (!temporary) AddBlendShapesToData(infConfig, meshBlendShapes);

            //Reset Preg+ slider 0 so the blendshape can be used with out interference
            UnityUiTools.ResetSlider(PregnancyPlusGui.cat, PregnancyPlusGui.inflationSize, 0);

            //Append the smrs that have new blendspahes to the blendshape GUI
            blendShapeGui.OnSkinnedMeshRendererBlendShapesCreated(meshWithBlendShapes);

            return meshBlendShapes.Count > 0;
        }


        /// <summary>
        /// Before saving card, get current GUI blendshape weights, since some may have changed
        /// </summary>
        internal void CaptureNewBlendshapeWeights() 
        {
            var meshBlendShapes = LoadBlendShapesFromCardData(infConfig.meshBlendShape);
            if (meshBlendShapes.Count <= 0) return;
            var uncensorGUID = GetUncensorGuid();

            //For each active GUI blendshape
            foreach(var meshBlendShape in meshBlendShapes)
            {
                if (meshBlendShape == null || meshBlendShape.BlendShape == null)
                {
                    if (PregnancyPlusPlugin.DebugLog.Value)  
                        PregnancyPlusPlugin.Logger.LogInfo($" Found a null saved BlendShape.  Is it corrupt?"); 
                    continue;
                }

                //Get the smr, and the blendshape if any still exist
                var smr = PregnancyPlusHelper.GetMeshRendererByName(ChaControl, meshBlendShape.MeshName, meshBlendShape.VertCount);
                if (smr == null) continue;            

                var blendShapeName = MakeBlendShapeName(GetMeshKey(smr));
                //Get existing blend shape from mesh            
                var bsc = new BlendShapeController(smr, blendShapeName);
                if (bsc.blendShape == null) continue;

                if (PregnancyPlusPlugin.DebugLog.Value && meshBlendShape.BlendShape.weight != bsc.blendShape.weight)  
                    PregnancyPlusPlugin.Logger.LogInfo($" CaptureNewBlendshapeWeights > {meshBlendShape.MeshName} weight:{bsc.blendShape.weight}");  

                //Update with new or existing weight
                meshBlendShape.BlendShape.weight = bsc.blendShape.weight;        

                Legacy_CheckNullUncensorGuid(meshBlendShapes, meshBlendShape, smr, uncensorGUID);
            }
            
            AddBlendShapesToData(infConfig, meshBlendShapes);
        }


        /// <summary>
        /// Append an UncensorGUID to all saved blendshapes
        /// </summary>
        internal void AddUncensorGUID(List<MeshBlendShape> meshBlendShapes, string uncensorGUID) 
        {
            foreach(var meshBlendShape in meshBlendShapes)
            {
                meshBlendShape.UncensorGUID = uncensorGUID;
            }
        }


        /// <summary>
        /// Get current uncensor GUID
        /// </summary>
        internal string GetUncensorGuid() 
        {
            return PregnancyPlusPlugin.Hooks_Uncensor.GetUncensorBodyGuid(ChaControl, UncensorCOMName);
        }


        internal void OnOpenBlendShapeSelected()
        {
            //GUI blendshape popup, with existing blendshapes if any exists
            blendShapeGui.OpenBlendShapeGui(meshWithBlendShapes, this);
        }


        /// <summary>
        /// When the user wants to remove all existing Preg+ GUI blendshapes 
        /// </summary>
        internal void OnRemoveAllGUIBlendShapes()
        {
            //Set all GUI blendshapes to 0 weight
            foreach (var smrIdentifier in meshWithBlendShapes)
            {
                var smr = PregnancyPlusHelper.GetMeshRendererByName(ChaControl, smrIdentifier.name, smrIdentifier.vertexCount);
                if (smr == null) continue;

                for (var i = 0; i < smr.sharedMesh.blendShapeCount; i++)
                {
                    //Search for GUI blendshape
                    var name = smr.sharedMesh.GetBlendShapeName(i);
                    if (name.EndsWith(PregnancyPlusPlugin.GUID))
                    {
                        //Set weight to 0.  Maybe in the future we'll actually remove them
                        var bsc = new BlendShapeController(smr, name);
                        bsc.ApplyBlendShapeWeight(smr, 0);
                    }
                }
            }
            meshWithBlendShapes = new List<MeshIdentifier>();
            ClearBlendShapesFromCharData();
        }


        /// <summary>
        /// When you want to start fresh and remove all (non GUI) Preg+ [temp] blendshapes completely.
        /// </summary>
        internal void ScrubTempBlendShapes()
        {
            var renderers = PregnancyPlusHelper.GetMeshRenderers(ChaControl.objClothes);            
            var bodyRenderers = PregnancyPlusHelper.GetMeshRenderers(ChaControl.objBody, findAll: true);
            renderers.AddRange(bodyRenderers);

            //Remove any Preg+ [temp] blendshapes
            foreach (var smr in renderers)
            {
                for (var i = 0; i < smr.sharedMesh.blendShapeCount; i++)
                {
                    //Search for all blendshapes on a mesh
                    var name = smr.sharedMesh.GetBlendShapeName(i);
                    var blendShapePartialName = MakeBlendShapeName(GetMeshKey(smr), blendShapeTempTagName);

                    //If it is a [temp] blendshape
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
        /// Loop through each skinned mesh renderer and create a blendshape from its current preg+ state
        /// </summary>
        /// <param name="smrs">List of skinnedMeshRenderes</param>
        /// <param name="meshBlendShapes">the current list of MeshBlendShapes collected so far</param>
        /// <param name="uncensorGUID">the current body uncensorGUID</param>
        /// <returns>Returns final list of MeshBlendShapes we want to store in char card</returns>
        internal List<MeshBlendShape> LoopAndCreateBlendShape(List<SkinnedMeshRenderer> smrs, List<MeshBlendShape> meshBlendShapes, string uncensorGUID) 
        {
            foreach(var smr in smrs) 
            {                
                var renderKey = GetMeshKey(smr);
                var exists = md.ContainsKey(renderKey);

                //Dont create blend shape if no inflated verts exists
                if (!exists || !md[renderKey].HasInflatedVerts) continue;

                var blendShapeCtrl = CreateBlendShape(smr, renderKey);

                //Get the blendshape format that can be saved to character card
                var meshBlendShape = ConvertToMeshBlendShape(smr.name, blendShapeCtrl.blendShape, uncensorGUID);
                if (meshBlendShape != null) 
                {
                    meshBlendShapes.Add(meshBlendShape);                
                    meshWithBlendShapes.Add(new MeshIdentifier(smr.name, smr.sharedMesh.vertexCount));
                }
            }  

            return meshBlendShapes;
        }
     

        /// <summary>
        /// Convert a BlendShape to MeshBlendShape, used for storing as character card data
        /// </summary>
        internal MeshBlendShape ConvertToMeshBlendShape(string smrMeshName, BlendShapeController.BlendShape blendShape, string uncensorGUID) 
        {            
            if (blendShape == null) return null;
            return new MeshBlendShape(smrMeshName, blendShape, blendShape.vertexCount, uncensorGUID);
        }


        /// <summary>
        /// Serialize a custom meshBlendShape object to characters card data (still requires a save to save it)
        /// </summary>
        /// <param name="_infConfig">The character data instance to save it to</param>
        /// <param name="meshBlendShapes">the list of MeshBlendShapes we want to save</param>
        internal void AddBlendShapesToData(PregnancyPlusData _infConfig, List<MeshBlendShape> meshBlendShapes) 
        {            
            _infConfig.meshBlendShape = MessagePack.LZ4MessagePackSerializer.Serialize(meshBlendShapes);
        }


        /// <summary>
        /// Clears any card data blendshapes (needs user save to apply though)
        /// </summary>
        internal void ClearBlendShapesFromCharData() 
        {            
            infConfig.meshBlendShape = null;
        }


        /// <summary>
        /// Loads any blendshapes from character card and sets them to the correct mesh
        /// </summary>
        /// <param name="data">The characters card data for this plugin</param>
        /// <param name="checkUncensor">When true will check the uncensor, and swap to the correct one to match saved blendshapes</param>
        internal void LoadBlendShapes(PregnancyPlusData data, bool checkUncensor = false) 
        {
            if (data.meshBlendShape == null) return;
            if (PregnancyPlusPlugin.DebugLog.Value)  PregnancyPlusPlugin.Logger.LogInfo($" MeshBlendShape size > {data.meshBlendShape.Length/1024}KB ");

            meshWithBlendShapes = new List<MeshIdentifier>();

            //Unserialize the blendshape from characters card
            var meshBlendShapes = LoadBlendShapesFromCardData(data.meshBlendShape);
            if (meshBlendShapes.Count <= 0) return;
            if (PregnancyPlusPlugin.DebugLog.Value)  PregnancyPlusPlugin.Logger.LogInfo($" MeshBlendShape count > {meshBlendShapes.Count} ");            
            
            var uncensorGUID = GetUncensorGuid();
            //When the saved body blendshape does not match the uncensor, change to that uncensor
            NeedsUncensorChanged(meshBlendShapes, uncensorGUID, checkUncensor);

            //For each meshBlendShape loaded from card
            foreach(var meshBlendShape in meshBlendShapes)
            {
                //Loop through all meshes and append any that need to be
                var clothRenderers = PregnancyPlusHelper.GetMeshRenderers(ChaControl.objClothes, findAll: true);
                LoopMeshAndAddSavedBlendShape(clothRenderers, meshBlendShape, uncensorGUID, isClothingMesh: true);

                var bodyRenderers = PregnancyPlusHelper.GetMeshRenderers(ChaControl.objBody, findAll: true);
                LoopMeshAndAddSavedBlendShape(bodyRenderers, meshBlendShape, uncensorGUID);
            }       

            //When we do load a saved blendshape add it to the history so we can track it has been successfully loaded
            infConfigHistory = (PregnancyPlusData) infConfig.Clone();    
        }


        /// <summary>
        /// Use messagepack to Deserialize the saved blendshapes from the characters card
        /// </summary>
        internal List<MeshBlendShape> LoadBlendShapesFromCardData(byte[] meshBlendShapesByte) 
        {
            if (meshBlendShapesByte == null) return new List<MeshBlendShape>();

            var meshBlendShapes = MessagePack.LZ4MessagePackSerializer.Deserialize<List<MeshBlendShape>>(meshBlendShapesByte);
            if (meshBlendShapes == null) return new List<MeshBlendShape>();

            return meshBlendShapes;
        }


        /// <summary>
        /// For any stored blendshape, make sure the current uncensor matches, when the uncensor does not match but we know what it should be, swap it
        /// </summary>
        /// <param name="updateUncensor">When true the uncensor will be changed to match the stored body blendshape when weight is present</param>
        /// <param name="uncensorGUID">The GUID of the character's current uncensor</param>
        internal bool NeedsUncensorChanged(List<MeshBlendShape> meshBlendShapes, string uncensorGUID, bool updateUncensor = false) 
        {
            //The uncensorGUID that this blendshape actually belongs to
            string validUncensorGUID = null;

            //Search for a valid uncensor ID
            foreach(var bs in meshBlendShapes) 
            {
                if (bs.UncensorGUID != null) validUncensorGUID = bs.UncensorGUID;
            }

            //Skip when old card data doesnt have the uncensor GUID (pre v3.6), or if the uncensors already match
            if (validUncensorGUID == null || uncensorGUID == validUncensorGUID) 
            {                  
                Legacy_CheckInitialUncensorGuid(meshBlendShapes, uncensorGUID);
                return false;
            }

            //If we dont want to allow uncensor changes automatically, just log the warning
            if (!updateUncensor)
            {
                PregnancyPlusPlugin.errorCodeCtrl.LogErrorCode(charaFileName, ErrorCode.PregPlus_BodyUncensorChanged, 
                        $" !BlendShape uncensorGUID '{validUncensorGUID}' does not match the current uncensor '{uncensorGUID}' for the body BlendShape.  Skipping"); 
                return false;
            }

            return TriggerUncensorChange(validUncensorGUID, uncensorGUID);
        }


        /// <summary>
        /// Triggers an uncensor change so we can successfully add a saved blendshape
        /// </summary>
        internal bool TriggerUncensorChange(string validUncensorGUID, string uncensorGUID)
        {
            if (PregnancyPlusPlugin.DebugLog.Value) PregnancyPlusPlugin.Logger.LogInfo(
                    $" Stored uncensorGUID '{validUncensorGUID}' does not match the current uncensor '{uncensorGUID}'. Attempting uncensor swap");

            ignoreNextUncensorHook = true;
            //Change body to the stored uncensorGUID
            PregnancyPlusPlugin.Hooks_Uncensor.ChangeUncensorTo(ChaControl, UncensorCOMName, validUncensorGUID);
            return true;
        }


        /// <summary>
        /// Loop through each mesh, and if the name/vertexcount matches, append the blendshape
        /// </summary>
        /// <param name="smrs">List of skinnedMeshRenderers to check for matching mesh name</param>
        /// <param name="meshBlendShape">The MeshBlendShape loaded from character card</param>
        /// <param name="uncensorGUID">The body uncensor GUID</param>
        internal void LoopMeshAndAddSavedBlendShape(List<SkinnedMeshRenderer> smrs, MeshBlendShape meshBlendShape, string uncensorGUID, bool isClothingMesh = false) 
        {
            var meshName = meshBlendShape.MeshName;
            var vertexCount = meshBlendShape.VertCount;
            
            foreach (var smr in smrs) 
            {   
                //Fixes any stale data on mesh blendshapes.  Thanks Unity...
                smr.sharedMesh = smr.sharedMesh;

                //If mesh matches, append the blend shape
                if (smr.name == meshName && smr.sharedMesh.vertexCount == vertexCount) 
                {
                    meshWithBlendShapes.Add(new MeshIdentifier(smr.name, smr.sharedMesh.vertexCount));
                    AddSavedBlendShape(smr, meshBlendShape);                    
                } 
                //When the vertex count is different but the uncensor matches (maybe mesh owner updated the uncensor?)
                else if (smr.name == meshName && smr.sharedMesh.vertexCount != vertexCount)
                {                  
                    //When the mesh vertex count is different now, warn the user that their blendshape is not going to load
                    PregnancyPlusPlugin.errorCodeCtrl.LogErrorCode(charaFileName, ErrorCode.PregPlus_BodyMeshVertexChanged, 
                        $" Mesh '{smr.name}' has a different vertex count, and no longer fits the BlendShape saved to this card {smr.sharedMesh.vertexCount} => {vertexCount}.  Blendshape {meshBlendShape.BlendShape.name} skipped."); 
                }                               
            }              
        }


        /// <summary>
        /// Add a saved blendshape (from card) back to its mesh
        /// </summary>
        /// <param name="meshBlendShape">The MeshBlendShape loaded from character card</param>
        internal bool AddSavedBlendShape(SkinnedMeshRenderer smr, MeshBlendShape meshBlendShape) 
        {
            //Make sure the blendshape does not already exists
            if (BlendShapeAlreadyExists(smr, meshBlendShape.BlendShape.name)) 
            {
                //If it does exists, make sure the weights are correct incase char just reloaded
                BlendShapeController _bsc = new BlendShapeController(smr, meshBlendShape.BlendShape.name);

                if (_bsc.blendShape == null) 
                {
                    if (PregnancyPlusPlugin.DebugLog.Value)  PregnancyPlusPlugin.Logger.LogWarning(
                        $"LoopMeshAndAddExistingBlendShape > There was a problem finding the blendshape ${meshBlendShape.BlendShape.name}");
                    return false;
                }
                
                //Update the weight to match the weight from the character card   
                _bsc.ApplyBlendShapeWeight(smr, meshBlendShape.BlendShape.weight, floatLerp: false);
                return true;
            }

            //Othwewise add the blendshape to the mesh, and set the weight
            var bsc = new BlendShapeController(meshBlendShape.BlendShape, smr);
            bsc.ApplyBlendShapeWeight(smr, meshBlendShape.BlendShape.weight, floatLerp: false);

            return true;
        }
                

        /// <summary>
        /// Check whether the blendshape already exists
        /// </summary>
        internal bool BlendShapeAlreadyExists(SkinnedMeshRenderer smr, string blendShapeName) 
        {
            var shapeIndex = new BlendShapeController().GetBlendShapeIndex(smr, blendShapeName);
            //If the shape exists then true
            return (shapeIndex >= 0);
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
            //When the mesh is not readable, temporarily make it readble
            if (!meshCopyTarget.isReadable) nativeDetour.Apply();

            //Make sure we have an existing belly shape to work with (can be null if user hasnt used sliders yet)
            var exists = md.TryGetValue(renderKey, out MeshData _md);
            if (!exists || !_md.HasInflatedVerts) 
            {
                if (PregnancyPlusPlugin.DebugLog.Value)  PregnancyPlusPlugin.Logger.LogInfo(
                     $"CreateBlendShape > smr '{renderKey}' meshData do not exists, skipping");

                nativeDetour.Undo();
                return null;
            }

            //Make sure the vertex count matches what the blendshape has (differs when swapping meshes)
            if (md[renderKey].VertexCount != meshCopyTarget.vertexCount) 
            {
                PregnancyPlusPlugin.errorCodeCtrl.LogErrorCode(charaFileName, ErrorCode.PregPlus_IncorrectVertCount, 
                    $"CreateBlendShape > smr.sharedMesh '{renderKey}' has incorrect vert count {md[renderKey].VertexCount}|{meshCopyTarget.vertexCount}");   

                nativeDetour.Undo();
                return null;
            }

            //Calculate the new normals, but don't show them.  We just want it for the blendshape shape target
            meshCopyTarget.vertices = md[renderKey].HasSmoothedVerts ? md[renderKey].smoothedVertices : md[renderKey].inflatedVertices;
            meshCopyTarget.RecalculateBounds();
            NormalSolver.RecalculateNormals(meshCopyTarget, 40f, md[renderKey].alteredVerticieIndexes);
            //Since we are hacking this readable state, prevent hard crash when calculating tangents on originally unreadable meshes
            if (meshCopyTarget.isReadable) meshCopyTarget.RecalculateTangents();

            var blendShapeName = MakeBlendShapeName(renderKey, blendShapeTag);

            //Create a blend shape object on the mesh, and return the controller object
            var bsc = new BlendShapeController(smr.sharedMesh, meshCopyTarget, blendShapeName, smr);            

            nativeDetour.Undo();
            return bsc;
        }  


        /// <summary>
        /// This is how we name preg+ blendshapes
        /// </summary>
        internal string MakeBlendShapeName(string renderKey, string blendShapeTag = null) 
        {
            return blendShapeTag == null ? $"{renderKey}_{PregnancyPlusPlugin.GUID}" : $"{renderKey}_{PregnancyPlusPlugin.GUID}_{blendShapeTag}";
        }


        /// <summary>
        /// Find a blendshape by name on a smr, and change its weight.  If it does not exists, create it.
        /// </summary>
        /// <param name="smr">Target mesh renderer to update (original shape)</param>
        /// <param name="renderKey">The Shared Mesh render name, used in dictionary keys to get the current verticie values</param>
        /// <param name="needsOverwrite">Whether the blendshape needs to be remade because the mesh shape was altered</param>
        /// <param name="blendShapeTag">Optional blend shape tag to append to the blend shape name, used for identification if needed</param>
        internal bool ApplyBlendShapeWeight(SkinnedMeshRenderer smr, string renderKey, bool needsOverwrite, string blendShapeTag = null) 
        {
            var blendShapeName = MakeBlendShapeName(renderKey, blendShapeTag);
            //Try to find an existing blendshape by name
            BlendShapeController bsc = new BlendShapeController(smr, blendShapeName);
            
            //If not found then create it
            if (bsc.blendShape == null || needsOverwrite) bsc = CreateBlendShape(smr, renderKey, blendShapeTag);

            if (bsc.blendShape == null) 
            {
                if (PregnancyPlusPlugin.DebugLog.Value)  PregnancyPlusPlugin.Logger.LogWarning(
                     $"ApplyBlendShapeWeight > There was a problem creating the blendshape ${blendShapeName}");
                return false;
            }
            
            //Determine the current infaltion size
            var size = isDuringInflationScene ? CurrentInflationChange : infConfig.inflationSize;

            //Update the weight to be the same as inflationSize value   
            return bsc.ApplyBlendShapeWeight(smr, size);
        }


        /// <summary>
        /// Reset a blendshape weight back to 0
        /// </summary>
        /// <param name="smr">Target mesh renderer to update (original shape)</param>
        /// <param name="renderKey">The Shared Mesh renderkey name, used to calculate the blendshape name</param>
        /// <param name="blendShapeTag">Optional blend shape tag to append to the blend shape name, used for identification if needed</param>
        internal bool ResetBlendShapeWeight(SkinnedMeshRenderer smr, string renderKey, string blendShapeTag = null) 
        {
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
        internal List<BlendShapeController> ComputeInflationBlendShapes() 
        {
            var blendshapes = new List<BlendShapeController>();

            //Trigger inflation at 0 size to create the blendshapes            
            MeshInflate(0, "ComputeInflationBlendShapes", new MeshInflateFlags(this, _bypassWhen0: true));

            var clothRenderers = PregnancyPlusHelper.GetMeshRenderers(ChaControl.objClothes);            
            //For each mesh, grab any existing Preg+ blendshapes and add to list
            foreach(var smr in clothRenderers)
            {
                var blendShapeName = MakeBlendShapeName(GetMeshKey(smr), blendShapeTempTagName);
                var blendshapeCtrl = new BlendShapeController(smr, blendShapeName);
                if (blendshapeCtrl.blendShape != null) blendshapes.Add(blendshapeCtrl);
            }

            var bodyRenderers = PregnancyPlusHelper.GetMeshRenderers(ChaControl.objBody, findAll: true);
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


