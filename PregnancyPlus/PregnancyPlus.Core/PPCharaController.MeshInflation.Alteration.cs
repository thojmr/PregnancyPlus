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
        /// This will update all verticies with a lerp from originalVertices to inflatedVertices depending on the inflationSize config
        /// Only modifies belly verticies, and if none are found, no action taken.
        /// </summary>
        /// <param name="mesh">Target mesh to update</param>
        /// <param name="renderKey">The Shared Mesh render name, used in dictionary keys to get the current verticie values</param>
        /// <returns>Will return True if any verticies are changed</returns>
        internal bool ApplyInflation(SkinnedMeshRenderer smr, string renderKey) 
        {
            if (smr == null) 
            {
                if (PregnancyPlusPlugin.DebugLog.Value)  PregnancyPlusPlugin.Logger.LogWarning(
                     $"ApplyInflation > smr is null");
                return false;
            }

            var infSize = infConfig.inflationSize;
            //Only inflate if the value is above 0  
            if (infSize.Equals(null) || infSize == 0) return false;      

            //Create an instance of sharedMesh so we don't modify the mesh shared between characters
            var meshCopy = (Mesh)UnityEngine.Object.Instantiate(smr.sharedMesh);    
            smr.sharedMesh = meshCopy;

            var sharedMesh = smr.sharedMesh;

            //Some meshes are not readable and cant be touched...  Nothing I can do about this right now
            if (!sharedMesh.isReadable) 
            {
                PregnancyPlusPlugin.errorCodeCtrl.LogErrorCode(ChaControl.chaID, ErrorCode.PregPlus_MeshNotReadable, 
                    $"ApplyInflation > smr '{renderKey}' is not readable, skipping");
                return false;
            } 

            //Check key exists in dict, remove it if it does not
            var exists = originalVertices.TryGetValue(renderKey, out var val);
            if (!exists) 
            {
                if (PregnancyPlusPlugin.DebugLog.Value)  PregnancyPlusPlugin.Logger.LogInfo(
                     $"ApplyInflation > smr '{renderKey}' does not exists, skipping");
                RemoveRenderKey(renderKey);
                return false;
            }

            //Get computed mesh values
            var origVert = originalVertices[renderKey];
            var currentVert = currentVertices[renderKey];
            var bellyVertIndex = bellyVerticieIndexes[renderKey];

            if (bellyVertIndex.Length == 0) return false;
            infConfigHistory.inflationSize = infSize;

            var currentVertLength = currentVert.Length;
            //Apply lerp morph for each changed verticie
            for (int i = 0; i < currentVertLength; i++)
            {
                //If not a belly index verticie then skip the morph
                if (!PregnancyPlusPlugin.DebugVerts.Value && !bellyVertIndex[i]) continue;

                //Set the lerp size of the belly based on the users slider value (if clothing, it will include clothing offset)
                currentVert[i] = Vector3.Lerp(origVert[i], inflatedVertices[renderKey][i], (infSize/40));
            }

            //Check that the mesh did not change behind the scenes.  It will have a different vert count if it did (possible to be the same though...)
            if (currentVert.Length != sharedMesh.vertexCount) 
            {
                PregnancyPlusPlugin.errorCodeCtrl.LogErrorCode(ChaControl.chaID, ErrorCode.PregPlus_IncorrectVertCount, 
                    $"ApplyInflation > smr.sharedMesh '{renderKey}' has incorrect vert count {currentVert.Length}|{sharedMesh.vertexCount}");
                return false;
            }

            if (PregnancyPlusPlugin.DebugLog.Value)  PregnancyPlusPlugin.Logger.LogInfo($" mesh did ApplyInflation > {smr.name}");

            sharedMesh.vertices = currentVert;
            sharedMesh.RecalculateBounds();
            NormalSolver.RecalculateNormals(sharedMesh, 40f, alteredVerticieIndexes[renderKey]);
            //sharedMesh.RecalculateNormals();  //old way that leaves skin seams at UV boundaries
            sharedMesh.RecalculateTangents();

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
                    if (PregnancyPlusPlugin.DebugLog.Value)  PregnancyPlusPlugin.Logger.LogWarning($" ResetInflation > smr was not found {renderKey}");
                    continue;                
                }

                //Create an instance of sharedMesh so we don't modify the mesh shared between characters, that was a fun issue
                Mesh meshCopy = (Mesh)UnityEngine.Object.Instantiate(smr.sharedMesh);
                smr.sharedMesh = meshCopy;

                var sharedMesh = smr.sharedMesh;
                var hasValue = originalVertices.TryGetValue(renderKey, out Vector3[] origVerts); 

                //On change clothes original verts become useless, so skip this
                if (!hasValue) return;   

                //Some meshes are not readable and cant be touched...
                if (!sharedMesh.isReadable) {
                    PregnancyPlusPlugin.errorCodeCtrl.LogErrorCode(ChaControl.chaID, ErrorCode.PregPlus_MeshNotReadable, 
                        $"ResetInflation > smr '{renderKey}' is not readable, skipping");
                    continue;
                } 

                if (!sharedMesh || origVerts.Equals(null) || origVerts.Length == 0) continue;
                
                if (origVerts.Length != sharedMesh.vertexCount) 
                {
                    PregnancyPlusPlugin.errorCodeCtrl.LogErrorCode(ChaControl.chaID, ErrorCode.PregPlus_IncorrectVertCount, 
                        $"ResetInflation > smr '{renderKey}' has incorrect vert count {origVerts.Length}|{sharedMesh.vertexCount}");
                    continue;
                }

                sharedMesh.vertices = origVerts;
                sharedMesh.RecalculateBounds();
                NormalSolver.RecalculateNormals(sharedMesh, 40f, alteredVerticieIndexes[renderKey]);
                //sharedMesh.RecalculateNormals(); //old way that leaves skin seams
                sharedMesh.RecalculateTangents();
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
            MeshInflate(true, true);

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
                    if (newVerts != null) inflatedVertices[renderKey] = newVerts;

                    //Re-trigger ApplyInflation to set the new smoothed mesh
                    ApplyInflation(smr, renderKey);
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
                if (newVerts != null) inflatedVertices[renderKey] = newVerts;

                //Re-trigger ApplyInflation to set the new smoothed mesh
                ApplyInflation(bodySmr, renderKey);
            }

            yield return null;
        }        


        /// <summary>   
        /// Update characters mesh with the new smoothed results
        /// </summary>
        internal void ApplySmoothResults(Vector3[] newMesh, string renderKey) {
            //Set the new smoothed mesh verts
            if (newMesh != null) inflatedVertices[renderKey] = newMesh;

            var smr = PregnancyPlusHelper.GetMeshRenderer(ChaControl, renderKey, false);
            //Re-trigger ApplyInflation to set the new smoothed mesh           
            ApplyInflation(smr, renderKey);
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

            //Get the new smoothed mesh verticies (compute only the belly verts)
            return SmoothMesh.Start(smr.sharedMesh, alteredVerticieIndexes[renderKey]);
        }

    }
}


