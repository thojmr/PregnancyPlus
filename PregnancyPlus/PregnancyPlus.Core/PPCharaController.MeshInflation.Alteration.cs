using KKAPI;
using KKAPI.Chara;
using UnityEngine;
using System;
using System.Collections.Generic;
using System.Collections;
using System.Threading;
using BepInEx;

namespace KK_PregnancyPlus
{

    //This partial class contains all logic that actually interfaces with the characters mesh to apply changes made
    public partial class PregnancyPlusCharaController: CharaCustomFunctionController
    {   

        /// <summary>
        /// This will update a mesh blendshape, calculated from the current slider selection
        /// </summary>
        /// <param name="mesh">Target mesh to update</param>
        /// <param name="renderKey">The Shared Mesh render name, used in dictionary keys to get the current verticie values</param>
        /// <param name="needsOverwrite">When false we don't have to overwrite the blendshape, and only have to set it's weight</param>
        /// <param name="blendShapeTag">string to append to the end of the blendshape name, for identification</param>
        /// <param name="bypassWhen0">When true, continue through when inflation size is 0</param>
        /// <returns>Will return True if any verticies are changed</returns>
        internal bool ApplyInflation(SkinnedMeshRenderer smr, string renderKey, bool needsOverwrite, string blendShapeTag = null, bool bypassWhen0 = false) 
        {
            if (smr == null) 
            {
                if (PregnancyPlusPlugin.DebugLog.Value)  PregnancyPlusPlugin.Logger.LogWarning(
                     $"ApplyInflation > smr is null");
                return false;
            }

            var infSize = infConfig.inflationSize;

            //When during inflation scene, use the inflation scene size value (ususally triggered by clothing change)
            if (isDuringInflationScene && !bypassWhen0)
            {
                if (PregnancyPlusPlugin.DebugLog.Value)  PregnancyPlusPlugin.Logger.LogInfo($"ApplyInflation > using TargetPregPlusSize instead {TargetPregPlusSize}");
                infSize = TargetPregPlusSize;
            }

            //Only inflate if the value is above 0  
            if (!bypassWhen0 && (infSize.Equals(null) || infSize == 0)) return false;      

            //Some meshes are not readable and cant be touched...  Nothing I can do about this right now
            if (!smr.sharedMesh.isReadable) 
            {
                PregnancyPlusPlugin.errorCodeCtrl.LogErrorCode(ChaControl.chaID, ErrorCode.PregPlus_MeshNotReadable, 
                    $"ApplyInflation > smr '{renderKey}' is not readable, skipping");
                return false;
            } 

            //Check key exists in dict, remove it if it does not
            var exists = originalVertices.TryGetValue(renderKey, out var val);
            if (!exists) 
            {
                // if (PregnancyPlusPlugin.DebugLog.Value)  PregnancyPlusPlugin.Logger.LogInfo(
                //      $"ApplyInflation > smr '{renderKey}' does not exists, skipping");
                RemoveRenderKey(renderKey);
                return false;
            }

            //Check that the mesh did not change behind the scenes.  It will have a different vert count if it did (possible to be the same though...)
            if (inflatedVertices[renderKey].Length != smr.sharedMesh.vertexCount) 
            {
                PregnancyPlusPlugin.errorCodeCtrl.LogErrorCode(ChaControl.chaID, ErrorCode.PregPlus_IncorrectVertCount, 
                    $"ApplyInflation > smr.sharedMesh '{renderKey}' has incorrect vert count {inflatedVertices[renderKey].Length}|{smr.sharedMesh.vertexCount}");
                return false;
            }

            //Create or update the smr blendshape
            ApplyBlendShapeWeight(smr, renderKey, needsOverwrite, blendShapeTempTagName);

            if (PregnancyPlusPlugin.DebugLog.Value)  PregnancyPlusPlugin.Logger.LogInfo($" mesh did ApplyInflation to {smr.name}");
            return true;
        }    


        /// <summary>   
        /// Will reset all meshes stored in the mesh dictionaries to default positons
        /// </summary>   
        internal void ResetInflation() 
        {   
            //Resets all mesh inflations
            var keyList = new List<string>(originalVertices.Keys);

            //For every active meshRenderer key we have created
            foreach(var renderKey in keyList) 
            {
                var smr = PregnancyPlusHelper.GetMeshRenderer(ChaControl, renderKey);
                //Normally triggered when user changes clothes, the old clothes render wont be found
                if (smr == null) {
                    if (PregnancyPlusPlugin.DebugLog.Value)  PregnancyPlusPlugin.Logger.LogInfo($" ResetInflation > smr was not found {renderKey}");
                    continue;                
                }
                
                ResetBlendShapeWeight(smr, renderKey, blendShapeTempTagName);
            }
        }


        /// <summary>   
        /// Will smooth and jagged edges around the characters belly caused by some slider combinations
        /// </summary>   
        /// <param name="includeClothMesh">Optionally include all cloth meshes as well</param>
        internal void ApplySmoothing(bool includeClothMesh = false)
        {
            //Run in coroutine to "reduce" locking main thread, sine this is a heavy task
            StartCoroutine(ApplySmoothingCoroutine(includeClothMesh));
        }


        /// <summary>   
        /// Will smooth and jagged edges around the characters belly caused by some slider combinations
        /// </summary>   
        /// <param name="includeClothMesh">Optionally include all cloth meshes as well</param>
        internal IEnumerator ApplySmoothingCoroutine(bool includeClothMesh = false)
        {
            //Check that inflationConfig has a value
            if (!infConfig.HasAnyValue()) yield return null;
            if (infConfig.inflationSize == 0) yield return null;
            if (PregnancyPlusPlugin.DebugLog.Value) PregnancyPlusPlugin.Logger.LogInfo($" ApplySmoothing({includeClothMesh})");

            //Trigger mesh recalculation to overwrite last smoothing pass changes if any existed
            MeshInflate(new MeshInflateFlags(this, _checkForNewMesh: true, _freshStart: true));

            //Smooth all mesh around the belly area including clothing
            if (includeClothMesh)
            {
                //Get all existing mesh keys
                var keyList = new List<string>(originalVertices.Keys);

                //Ill add this back laater once I do some more research
                // //For each mesh key, calculate the new smoothed mesh in parallel
                // var meshResults = ThreadingExtensions.RunParallel<string, Dictionary<String, Vector3[]>>(keyList, (_renderKey) => {                    
                //     // if (PregnancyPlusPlugin.DebugLog.Value) PregnancyPlusPlugin.Logger.LogInfo($" QueueUserWorkItem : {_renderKey}");

                //     var smr = PregnancyPlusHelper.GetMeshRenderer(ChaControl, _renderKey, false);
                //     if (smr == null) return null;

                //     //Do the smoothing here, and get the new smoothed verts
                //     var newVerts = SmoothSingleMesh(smr, _renderKey);

                //     //Create returned data format
                //     var keyAndVertDict = new Dictionary<String, Vector3[]>();
                //     keyAndVertDict[_renderKey] = newVerts;

                //     return keyAndVertDict;
                // });
                
                // //For each mesh result
                // foreach(var meshResult in meshResults) 
                // {
                //     if (meshResult == null) continue;
                //     var renderKeys = meshResult.Keys;//Only 1 key per result in this case                    

                //     //Get get the render key and apply the changes
                //     foreach(var renderKey in renderKeys) 
                //     {
                //         ApplySmoothResults(meshResult[renderKey], renderKey);
                //     }
                //     yield return null;//Allow UI updates after each mesh has finished updating, instead of locking ui until the very end
                // }

                //For every `active` meshRenderer key we have created, smooth the mesh
                foreach(var renderKey in keyList) 
                {
                    var smr = PregnancyPlusHelper.GetMeshRenderer(ChaControl, renderKey, false);
                    var newVerts = SmoothSingleMesh(smr, renderKey);
                    //Re-trigger ApplyInflation to set the new smoothed mesh
                    ApplySmoothResults(newVerts, renderKey, smr);
                }
            } 
            //Only smooth the body mesh around the belly area
            else
            {
                #if KK
                    var meshName = "o_body_a";
                #elif HS2 || AI
                    var meshName = "o_body_cf";
                #endif

                var bodySmr = PregnancyPlusHelper.GetMeshRendererByName(ChaControl, meshName);
                var renderKey = GetMeshKey(bodySmr);

                var newVerts = SmoothSingleMesh(bodySmr, renderKey);     
                ApplySmoothResults(newVerts, renderKey, bodySmr);
            }

            yield return null;
        }        


        /// <summary>   
        /// Update characters mesh with the new smoothed results
        /// </summary>
        internal void ApplySmoothResults(Vector3[] newMesh, string renderKey, SkinnedMeshRenderer smr = null) {
            //Set the new smoothed mesh verts
            if (newMesh != null) inflatedVertices[renderKey] = newMesh;

            if (smr == null) smr = PregnancyPlusHelper.GetMeshRenderer(ChaControl, renderKey, false);
            //Re-trigger ApplyInflation to set the new smoothed mesh           
            ApplyInflation(smr, renderKey, true, blendShapeTempTagName);            
        }


        /// <summary>   
        /// Smooths a single mesh with lapacian smoothing
        /// </summary>   
        internal Vector3[] SmoothSingleMesh(SkinnedMeshRenderer smr, string renderKey, Func<bool> callback = null)
        {
            if (smr == null || renderKey == null) return null;
            
            //Check that this mesh has computed inflated verticies            
            if (!inflatedVertices.ContainsKey(renderKey)) 
            {
                if (PregnancyPlusPlugin.DebugLog.Value) PregnancyPlusPlugin.Logger.LogWarning($" No inflated verts found for SmoothSingleMesh");
                return null;
            }

            //Make a copy of the mesh. We dont want to affect the existing for this
            var meshCopyTarget = PregnancyPlusHelper.CopyMesh(smr.sharedMesh);   
            if (!meshCopyTarget.isReadable) 
            {
                PregnancyPlusPlugin.errorCodeCtrl.LogErrorCode(ChaControl.chaID, ErrorCode.PregPlus_MeshNotReadable, 
                    $"SmoothSingleMesh > smr '{renderKey}' is not readable, skipping");                     
                return null;
            } 

            //Calculate the original normals, but don't show them.  We just want it for the blendshape target
            meshCopyTarget.vertices = inflatedVertices[renderKey];
            meshCopyTarget.RecalculateBounds();
            NormalSolver.RecalculateNormals(meshCopyTarget, 40f, alteredVerticieIndexes[renderKey]);
            meshCopyTarget.RecalculateTangents();

            //Get the new smoothed mesh verticies (compute only the belly verts)
            return SmoothMesh.Start(meshCopyTarget, alteredVerticieIndexes[renderKey]);
        }

    }
}


