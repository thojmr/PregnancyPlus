﻿using KKAPI;
using KKAPI.Chara;
using UnityEngine;
using System;
using System.Linq;
using System.Threading.Tasks;
using System.Collections.Generic;
#if HS2 || AI
    using AIChara;
#endif

namespace KK_PregnancyPlus
{

    //This partial class contains the logic for filtering the list of verts we want to manipulate
    public partial class PregnancyPlusCharaController: CharaCustomFunctionController
    {           

        //This limits the area around the belly where we want to affect any verts
        //This value used to be 0.02f, but in order to make it compatible with BP5 I reduced it to 0f
        const float minBoneWeight = 0f; 


        /// <summary>
        /// This will get all of the indexes of verticies that have a weight attached to a belly bone (bone filter).
        /// This lets us filter out all other verticies since we only care about the belly anyway. Saves on compute time.
        /// </summary>
        /// <param name="smr">The target mesh renderer</param>
        /// <param name="boneFilters">The bones that must have weights, if none are passed it will get all bone indexes</param>
        /// <param name="boneExclusions">Any mesh vertex with weights to these bones should be exlcuded and left alone, the float part is the minimum weight it must have to be ignored</param>
        /// <returns>Returns True if any verticies are found with matching boneFilter that needs processing</returns>
        internal async Task<bool> GetFilteredVerticieIndexes(SkinnedMeshRenderer smr, string[] boneFilters, string[] boneExclusions) 
        {
            var sharedMesh = smr.sharedMesh;
            var renderKey = GetMeshKey(smr);
            var bones = smr.bones;
            var hasBellyVerticies = false;            

            if (!sharedMesh.isReadable) nativeDetour.Apply();
            
            //return early if no bone weights found
            if (sharedMesh.boneWeights.Length == 0) 
            {
                if (!ignoreMeshList.Contains(renderKey)) ignoreMeshList.Add(renderKey);//Ignore this mesh/key from now on
                if (PregnancyPlusPlugin.DebugCalcs.Value) PregnancyPlusPlugin.Logger.LogInfo($" No Bone Weights found for {smr.name} skipping");

                nativeDetour.Undo();
                return false; 
            }

            //Get bone indexes belonging to belly verts
            var indexesFound = GetFilteredBoneIndexes(bones, boneFilters, boneExclusions, out List<int> bellyBoneIndexes, out List<int> bellyBoneExclusionIndexes);
            if (!indexesFound) 
            {
                if (!ignoreMeshList.Contains(renderKey)) ignoreMeshList.Add(renderKey);
                if (PregnancyPlusPlugin.DebugCalcs.Value) PregnancyPlusPlugin.Logger.LogInfo($" No FilteredBoneIndexes found for {smr.name} skipping");

                nativeDetour.Undo();
                return false;             
            }

            if (PregnancyPlusPlugin.DebugCalcs.Value) PregnancyPlusPlugin.Logger.LogInfo($" GetFilteredVerticieIndexes for {smr.name}");

            //Create new mesh dictionary key from scratch (Note: This will overwrite existing)
            md[renderKey] = new MeshData(sharedMesh.vertexCount);           
            var verticies = sharedMesh.vertices;

            var debugAnyVerts = PregnancyPlusPlugin.MakeBalloon.Value || PregnancyPlusPlugin.DebugVerts.Value;
            
            //Put threadpool work inside task and await the results
            await Task.Run(() => 
            {
                //In KKS the mesh data key is sometime reset after maker load, so set it again
                var hasValue = md.TryGetValue(renderKey, out var _meshData);
                if (!hasValue) md[renderKey] = new MeshData(verticies.Length);

                //Spread work across multiple threads
                md[renderKey].bellyVerticieIndexes = Threading.RunParallel(sharedMesh.boneWeights, (bw, i) => 
                {
                    if (debugAnyVerts) 
                    {
                        hasBellyVerticies = true;
                        return true;
                    }
                    
                    int[] boneIndicies = new int[] { bw.boneIndex0, bw.boneIndex1, bw.boneIndex2, bw.boneIndex3 };
                    float[] boneWeights = new float[] { bw.weight0, bw.weight1, bw.weight2, bw.weight3 };                    

                    //For each bone weight
                    for (int j = 0; j < 4; j++)
                    {            
                        var isExcludedWeight = bellyBoneExclusionIndexes != null && bellyBoneExclusionIndexes.Contains(boneIndicies[j]);
                        //When an excluded bone has any weight ignore this vert
                        if (isExcludedWeight)                     
                            return false;                                                

                        //If it has a weight, and the bone is a belly bone. Weight goes (0-1f)
                        //Include all if debug = true
                        var hasValidWeight = boneWeights[j] > minBoneWeight && bellyBoneIndexes.Contains(boneIndicies[j]);
                        if (hasValidWeight)
                        {
                            hasBellyVerticies = true;
                            return true;                                                     
                        }                                                                
                    }

                    return false;
                });

            });

            nativeDetour.Undo();

            //Dont need to keep this meshData if there are no belly verts in it
            if (!hasBellyVerticies) 
            {
                if (!ignoreMeshList.Contains(renderKey)) ignoreMeshList.Add(renderKey);
                if (PregnancyPlusPlugin.DebugCalcs.Value) PregnancyPlusPlugin.Logger.LogInfo($" hasBellyVerticies none found for {smr.name} skipping");
                RemoveRenderKey(renderKey);
            } 
            else
            {
                //just in case a new mesh is added that is valid but also on ignore list, remove it from the list
                if (ignoreMeshList.Contains(renderKey))
                    ignoreMeshList.Remove(renderKey);                
            }
            
            return hasBellyVerticies;
        }

        /// <summary>
        /// From a list of bone filters, get all the bone indexes that have matching bone names
        /// </summary>
        /// <param name="bones">the mesh's bones list</param>
        /// <param name="boneFilters">The bones that must have weights, if none are passed it will get all bone indexes</param>
        /// <param name="boneExclusionFilters">The bones that we want to ignore any verts with weights to them</param>
        /// <param name="bellyBoneIndexes">Where we store the matching bone index values</param>
        /// <param name="bellyBoneExclusionIndexes">List of bone indexes that we want to ignore any verts with weights to</param>
        /// <returns>Returns false if no bones found, or no bone indexes found</returns>
        internal bool GetFilteredBoneIndexes(Transform[] bones, string[] boneFilters, string[] boneExclusionFilters, 
                                             out List<int> bellyBoneIndexes, out List<int> bellyBoneExclusionIndexes) 
        {
            bellyBoneIndexes = new List<int>();
            bellyBoneExclusionIndexes = new List<int>();

            //Don't even know if this is possible, so why not
            if (bones.Length <= 0) 
            {
                bellyBoneExclusionIndexes = null;
                return false;
            }

            var hasBoneFilters = boneFilters != null && boneFilters.Length > 0;
            var hasBoneExclusionFilters = boneExclusionFilters != null && boneExclusionFilters.Length > 0;
            var bonesLength = bones.Length;

            //For each bone, see if it matches a the boneFilter list
            for (int i = 0; i < bonesLength; i++)
            {   
                if (!bones[i]) continue;  

                //Get all the bone indexes if no filters are used              
                if (!hasBoneFilters) 
                {
                    bellyBoneIndexes.Add(i);
                }

                var boneName = bones[i].name;

                //If the current bone matches the current boneFilter, add its index
                foreach(var boneFilter in boneFilters)
                {
                    if (boneFilter == boneName) 
                    {
                        bellyBoneIndexes.Add(i);
                        continue;
                    }  
                }

                //If bone exclusions list exists, add its index
                if (!hasBoneExclusionFilters) continue;
                foreach(var boneExcludeName in boneExclusionFilters)
                {        
                    if (boneExcludeName == boneName) 
                    {
                        //Only add new values
                        bellyBoneExclusionIndexes.Add(i);
                        continue;
                    }  
                }
            }

            //Only keep distinct indexes to reduce compute later
            bellyBoneExclusionIndexes = bellyBoneExclusionIndexes.Distinct().ToList();
            bellyBoneIndexes = bellyBoneIndexes.Distinct().ToList();

            //If no exclusions, then set back to null
            if (bellyBoneExclusionIndexes.Count <= 0)
            {
                bellyBoneExclusionIndexes = null;
            }
            
            return bellyBoneIndexes.Count > 0;
        }
                
    }
}


