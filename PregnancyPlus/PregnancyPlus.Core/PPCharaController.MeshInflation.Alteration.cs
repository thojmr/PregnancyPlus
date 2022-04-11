using KKAPI;
using KKAPI.Chara;
using UnityEngine;
using System;
using System.Collections.Generic;
using System.Collections;
using System.Threading.Tasks;
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
                if (PregnancyPlusPlugin.DebugLog.Value)  PregnancyPlusPlugin.Logger.LogInfo($"ApplyInflation > using CurrentInflationChange instead {CurrentInflationChange}");
                infSize = CurrentInflationChange;
            }

            //Only inflate if the value is above 0  
            if (!bypassWhen0 && (infSize.Equals(null) || infSize == 0)) return false;      

            //Check key exists in dict, remove it if it does not
            var exists = md.TryGetValue(renderKey, out MeshData _md);
            if (!exists || !_md.HasOriginalVerts) 
            {
                RemoveRenderKey(renderKey);
                return false;
            }

            //Some meshes are not readable and cant be touched, make them readable
            if (!smr.sharedMesh.isReadable) nativeDetour.Apply();

            //Check that the mesh did not change behind the scenes.  It will have a different vert count if it did (possible to be the same though...)
            if (md[renderKey].VertexCount != smr.sharedMesh.vertexCount) 
            {
                PregnancyPlusPlugin.errorCodeCtrl.LogErrorCode(charaFileName, ErrorCode.PregPlus_IncorrectVertCount, 
                    $"ApplyInflation > smr.sharedMesh '{renderKey}' has incorrect vert count {md[renderKey].VertexCount}|{smr.sharedMesh.vertexCount}");

                nativeDetour.Undo();
                return false;
            }        

            nativeDetour.Undo();

            //Create or update the smr blendshape
            var didApply = ApplyBlendShapeWeight(smr, renderKey, needsOverwrite, blendShapeTempTagName);

            if (PregnancyPlusPlugin.DebugLog.Value && didApply)  PregnancyPlusPlugin.Logger.LogInfo($" Did ApplyInflation to {smr.name}");            
            return didApply;
        }    


        /// <summary>   
        /// Will reset all meshes stored in the mesh dictionaries to default positons
        /// </summary>   
        internal void ResetInflation() 
        {   
            //Resets all mesh inflations
            var keyList = new List<string>(md.Keys);

            //For every active meshRenderer key we have created
            foreach(var renderKey in keyList) 
            {
                var smr = PregnancyPlusHelper.GetMeshRenderer(ChaControl, renderKey);
                //Normally triggered when user changes clothes, the old clothes render wont be found
                if (smr == null) 
                {
                    // if (PregnancyPlusPlugin.DebugLog.Value)  PregnancyPlusPlugin.Logger.LogInfo($" ResetInflation > smr was not found {renderKey}");
                    continue;                
                }
                
                ResetBlendShapeWeight(smr, renderKey, blendShapeTempTagName);
            }
        }


        /// <summary>   
        /// Will smooth and jagged edges around the characters belly caused by some slider combinations
        /// </summary>   
        /// <param name="includeClothMesh">Optionally include all cloth meshes as well</param>
        internal async Task ApplySmoothing(bool includeClothMesh = false)
        {
            //Check that inflationConfig has a value
            if (!infConfig.HasAnyValue()) return;
            if (infConfig.inflationSize == 0) return;
            if (PregnancyPlusPlugin.DebugLog.Value) PregnancyPlusPlugin.Logger.LogInfo($" ApplySmoothing({includeClothMesh})");

            PregnancyPlusGui.StartTextCountIncrement();

            //Smooth all mesh around the belly area including clothing
            if (includeClothMesh)
            {
                //Get all existing mesh keys
                var keyList = new List<string>(md.Keys);

                //For every `active` meshRenderer key we have created, smooth the mesh
                foreach(var renderKey in keyList) 
                {
                    var smr = PregnancyPlusHelper.GetMeshRenderer(ChaControl, renderKey, searchInactive: false);
                    var _started = await SmoothSingleMesh(smr, renderKey);
                }
            } 
            //Only smooth the body mesh around the belly area
            else
            {
                var bodySmr = PregnancyPlusHelper.GetMeshRendererByName(ChaControl, BodyMeshName);
                var renderKey = GetMeshKey(bodySmr);

                var started = await SmoothSingleMesh(bodySmr, renderKey);     
            }       

            //Stop button timer when done
            PregnancyPlusGui.StopTextCountIncrement();  
        }        


        /// <summary>   
        /// Smooths a single mesh with lapacian smoothing
        /// </summary>   
        internal async Task<bool> SmoothSingleMesh(SkinnedMeshRenderer smr, string renderKey)
        {
            if (smr == null || renderKey == null) return false;
                        
            //Check that this mesh has computed inflated verticies            
            if (!md.ContainsKey(renderKey) || ! md[renderKey].HasInflatedVerts) 
            {
                if (PregnancyPlusPlugin.DebugLog.Value) PregnancyPlusPlugin.Logger.LogWarning($" No inflated verts found for SmoothSingleMesh {smr.name}");
                return false;
            }

            //Make a copy of the mesh. We dont want to affect the existing for this
            var meshCopyTarget = PregnancyPlusHelper.CopyMesh(smr.sharedMesh);   
            if (!meshCopyTarget.isReadable) nativeDetour.Apply();

            //Calculate the original normals, but don't show them.  We just want it for the blendshape target
            meshCopyTarget.vertices = md[renderKey]._inflatedVertices;//Always use interla _inflatedVerts directly, not the smoothedVerts by accident
            nativeDetour.Undo();

            var newVerts = new Vector3[md[renderKey].VertexCount];

            //Put threadpool work inside task and await the results
            await Task.Run(() => 
            {
                if (!meshCopyTarget.isReadable) nativeDetour.Apply();
                newVerts = SmoothMesh.Start(meshCopyTarget, md[renderKey].alteredVerticieIndexes);
                nativeDetour.Undo();
            });

            //Re-trigger ApplyInflation to set the new smoothed mesh
            await ApplySmoothResults(newVerts, renderKey, smr); 
            return true;
        }


        /// <summary>   
        /// Update characters mesh with the new smoothed results
        /// </summary>
        internal async Task ApplySmoothResults(Vector3[] newMesh, string renderKey, SkinnedMeshRenderer smr = null) 
        {
            //Set the new smoothed mesh verts
            if (newMesh != null) md[renderKey].smoothedVertices = newMesh;

            if (smr == null) smr = PregnancyPlusHelper.GetMeshRenderer(ChaControl, renderKey, searchInactive: false);
            if (smr == null) return;

            var meshInflateFlags = new MeshInflateFlags(this, _freshStart: true);
            //Compute the deltas and apply the smoothed mesh
            await ComputeDeltas(smr, renderKey, meshInflateFlags);      
                        
            //When we have already pre-computed the deltas we can go ahead and finalize now
            FinalizeInflation(smr, meshInflateFlags, blendShapeTempTagName);     
        }


    }
}


