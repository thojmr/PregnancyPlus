using KKAPI;
using KKAPI.Chara;
using UnityEngine;
using System;
using System.Collections.Generic;

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
                if (infConfig.clothingOffsetVersion == 1) currentVert[i] = Vector3.Lerp(origVert[i], inflatedVerticesOffsets[renderKey][i], (infSize/40));
                if (infConfig.clothingOffsetVersion != 1) currentVert[i] = Vector3.Lerp(origVert[i], inflatedVertices[renderKey][i], (infSize/40));
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

    }
}


