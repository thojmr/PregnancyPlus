using KKAPI;
using KKAPI.Chara;
using UnityEngine;
using System;
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

        const float minBoneWeight = 0.02f;


        /// <summary>
        /// This will get all of the indexes of verticies that have a weight attached to a belly bone (bone filter).
        /// This lets us filter out all other verticies since we only care about the belly anyway. Saves on compute time.
        /// </summary>
        /// <param name="smr">The target mesh renderer</param>
        /// <param name="boneFilters">The bones that must have weights, if none are passed it will get all bone indexes</param>
        /// <returns>Returns True if any verticies are found with matching boneFilter that needs processing</returns>
        internal async Task<bool> GetFilteredVerticieIndexes(SkinnedMeshRenderer smr, string[] boneFilters) 
        {
            var sharedMesh = smr.sharedMesh;
            var renderKey = GetMeshKey(smr);
            var bones = smr.bones;
            var bellyBoneIndexes = new List<int>();
            var hasBellyVerticies = false;            

            if (!sharedMesh.isReadable) nativeDetour.Apply();
            
            //return early if no bone weights found
            if (sharedMesh.boneWeights.Length == 0) 
            {
                if (!ignoreMeshList.Contains(renderKey)) ignoreMeshList.Add(renderKey);//Ignore this mesh/key from now on

                nativeDetour.Undo();
                return false; 
            }

            var indexesFound = GetFilteredBoneIndexes(bones, boneFilters, bellyBoneIndexes);
            if (!indexesFound) 
            {
                if (!ignoreMeshList.Contains(renderKey)) ignoreMeshList.Add(renderKey);

                nativeDetour.Undo();
                return false;             
            }

            if (PregnancyPlusPlugin.DebugCalcs.Value) PregnancyPlusPlugin.Logger.LogInfo($" GetFilteredVerticieIndexes for {smr.name}");

            //Create new mesh dictionary key from scratch (Note: This will overwrite existing)
            md[renderKey] = new MeshData(sharedMesh.vertexCount);           
            var bellyVertIndex = md[renderKey].bellyVerticieIndexes;
            var verticies = sharedMesh.vertices;

            //Since the z limit check is done on the unskinned verts, we need to apply any bindpose scale to the limit to make it match the real unskinned vert positions
            //  Note: I bet rotated meshes are similarily affected, but that's a lot of math to correct
            var bindPoseScaleZ = Matrix.GetScale(MeshSkinning.GetBindPoseScale(smr).inverse).z;
            //The distance backwards from characters center that verts are allowed to be modified
            var backExtent = bindPoseScaleZ * -bellyInfo.ZLimit;
            
            //Put threadpool work inside task and await the results
            await Task.Run(() => 
            {
                //Spread work across multiple threads
                md[renderKey].bellyVerticieIndexes = Threading.RunParallel(sharedMesh.boneWeights, (bw, i) => 
                {
                    int[] boneIndicies = new int[] { bw.boneIndex0, bw.boneIndex1, bw.boneIndex2, bw.boneIndex3 };
                    float[] boneWeights = new float[] { bw.weight0, bw.weight1, bw.weight2, bw.weight3 };

                    //For each bone weight
                    for (int j = 0; j < 4; j++)
                    {                    
                        //If it has a weight, and the bone is a belly bone. Weight goes (0-1f) Ignore 0 and maybe filter below 0.1 as well
                        //Include all if debug = true
                        if ((boneWeights[j] > minBoneWeight && bellyBoneIndexes.Contains(boneIndicies[j]) || PregnancyPlusPlugin.MakeBalloon.Value))
                        {
                            //Make sure to exclude verticies on characters back, we only want to modify the front.  No back bellies!
                            //add all vertexes in debug mode
                            if (verticies[i].z >= backExtent || PregnancyPlusPlugin.MakeBalloon.Value) 
                            {
                                hasBellyVerticies = true;
                                return true;                                
                            }                        
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
                // PregnancyPlusPlugin.Logger.LogInfo($"bellyVerticieIndexes > removing {renderKey}"); 
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
        /// <param name="bellyBoneIndexes">Where we store the matching index values</param>
        /// <returns>Returns false if no bones found, or no indexes found</returns>
        internal bool GetFilteredBoneIndexes(Transform[] bones, string[] boneFilters, List<int> bellyBoneIndexes) 
        {
            if (bones.Length <= 0) return false;
            var hasBoneFilters = boneFilters != null && boneFilters.Length > 0;

            var bonesLength = bones.Length;

            //For each bone, see if it matches a belly boneFilter
            for (int i = 0; i < bonesLength; i++)
            {   
                if (!bones[i]) continue;  

                //Get all the bone indexes if no filters are used              
                if (!hasBoneFilters) 
                {
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


