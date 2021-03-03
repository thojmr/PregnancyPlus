using KKAPI;
using KKAPI.Chara;
using UnityEngine;
using System;
using System.Collections.Generic;
#if HS2 || AI
    using AIChara;
#endif

namespace KK_PregnancyPlus
{

    //This partial class contains the logic for filtering the list of verts we want to manipulate
    public partial class PregnancyPlusCharaController: CharaCustomFunctionController
    {           

        /// <summary>
        /// This will get all of the indexes of verticies that have a weight attached to a belly bone (bone filter).
        /// This lets us filter out all other verticies since we only care about the belly anyway. Saves on compute time.
        /// </summary>
        /// <param name="skinnedMeshRenderer">The target mesh renderer</param>
        /// <param name="boneFilters">The bones that must have weights, if none are passed it will get all bone indexes</param>
        /// <returns>Returns True if any verticies are found with matching boneFilter</returns>
        internal bool GetFilteredVerticieIndexes(SkinnedMeshRenderer skinnedMeshRenderer, string[] boneFilters) 
        {
            var sharedMesh = skinnedMeshRenderer.sharedMesh;
            var renderKey = GetMeshKey(skinnedMeshRenderer);
            var bones = skinnedMeshRenderer.bones;
            var bellyBoneIndexes = new List<int>();
            var hasBellyVerticies = false;            

            if (!sharedMesh.isReadable) 
            {
                PregnancyPlusPlugin.errorCodeCtrl.LogErrorCode(ChaControl.chaID, ErrorCode.PregPlus_MeshNotReadable, 
                    $"GetFilteredVerticieIndexes > smr '{renderKey}' is not readable, skipping");
                return false;
            }

            //return early if no bone weights found
            if (sharedMesh.boneWeights.Length == 0) return false; 

            var indexesFound = GetFilteredBoneIndexes(bones, boneFilters, bellyBoneIndexes);
            if (!indexesFound) return false;             

            //Create new mesh dictionary key for bone indexes
            bellyVerticieIndexes[renderKey] = new bool[sharedMesh.vertexCount];
            alteredVerticieIndexes[renderKey] = new bool[sharedMesh.vertexCount];            
            var bellyVertIndex = bellyVerticieIndexes[renderKey];

            var verticies = sharedMesh.vertices;

            //The distance backwards from characters center that verts are allowed to be modified
            var backExtent = bellyInfo.ZLimit;
            
            var c = 0;
            var meshBoneWeights = sharedMesh.boneWeights;
            foreach (BoneWeight bw in meshBoneWeights) 
            {
                int[] boneIndicies = new int[] { bw.boneIndex0, bw.boneIndex1, bw.boneIndex2, bw.boneIndex3 };
                float[] boneWeights = new float[] { bw.weight0, bw.weight1, bw.weight2, bw.weight3 };

                //For each bone weight
                for (int i = 0; i < 4; i++)
                {                    
                    //If it has a weight, and the bone is a belly bone. Weight goes (0-1f) Ignore 0 and maybe filter below 0.1 as well
                    //Include all if debug = true
                    if ((boneWeights[i] > 0.02f && bellyBoneIndexes.Contains(boneIndicies[i]) || PregnancyPlusPlugin.MakeBalloon.Value))
                    {
                        //Make sure to exclude verticies on characters back, we only want to modify the front.  No back bellies!
                        //add all vertexes in debug mode
                        if (verticies[c].z >= 0 - backExtent || PregnancyPlusPlugin.MakeBalloon.Value) 
                        {
                            bellyVertIndex[c] = true;
                            hasBellyVerticies = true;
                            break;
                        }                        
                    }                
                }
                c++;//lol                                          
            }

            //Dont need to remember this mesh if there are no belly verts in it
            if (!hasBellyVerticies) 
            {
                // PregnancyPlusPlugin.Logger.LogInfo($"bellyVerticieIndexes > removing {renderKey}"); 
                RemoveRenderKey(renderKey);
            }

            return hasBellyVerticies;
        }

        /// <summary>
        /// From a list of bone filters, get all the bone indexes that have matching bone names
        /// </summary>
        /// <param name="bones">the mesh's bones list</param>
        /// <param name="boneFilters">The bones that must have weights, if none are passed it will get all bone indexes</param>
        /// <param name="bellyBoneIndexes">Where we store the matching index values</param>
        /// <returns>Returns false if no bones found, or no indexes found</returns>
        internal bool GetFilteredBoneIndexes(Transform[] bones, string[] boneFilters, List<int> bellyBoneIndexes) {
            //Don't even know if this is possible, so why not
            if (bones.Length <= 0) return false;
            var hasBoneFilters = boneFilters != null && boneFilters.Length > 0;

            var bonesLength = bones.Length;

            //For each bone, see if it matches a belly boneFilter
            for (int i = 0; i < bonesLength; i++)
            {   
                if (!bones[i]) continue;  

                //Get all the bone indexes if no filters are used              
                if (!hasBoneFilters) {
                    bellyBoneIndexes.Add(i);
                    continue;
                }

                var boneName = bones[i].name;

                //If the current bone matches the current boneFilter, add it's index
                foreach(var boneFilter in boneFilters)
                {
                    if (boneFilter == boneName) 
                    {
                        bellyBoneIndexes.Add(i);
                        break;
                    }  
                }
            }
            
            return bellyBoneIndexes.Count > 0;
        }
                
    }
}


