﻿using KKAPI;
using KKAPI.Chara;
using UnityEngine;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
#if HS2 || AI
    using AIChara;
#endif

namespace KK_PregnancyPlus
{
    internal static class PregnancyPlusHelper
    {        

        //Convert unity unit to a unity cemtimeter (probably need to divide by 10 for KK)
        internal const float gameSizeToCentimetersRatio = 10.3092781f;


        /// <summary>
        /// Search all SMR's for a matchtching key
        /// </summary>
        internal static SkinnedMeshRenderer GetMeshRenderer(ChaControl chaControl, string renderKey, bool searchInactive = true) 
        {
            var renderers = chaControl.GetComponentsInChildren<SkinnedMeshRenderer>(searchInactive);//Even search inactive renderers
            var renderer = renderers.FirstOrDefault(x => (KeyFromNameAndVerts(x)) == renderKey);
            return renderer;
        }


        /// <summary>
        /// Search all SMR's for a matchtching name
        /// </summary>
        /// <param name="vertexCount">When vertex count is included, the result must match on name AND count</param>
        internal static SkinnedMeshRenderer GetMeshRendererByName(ChaControl chaControl, string smrName, int vertexCount = -1) 
        {
            var renderers = chaControl.GetComponentsInChildren<SkinnedMeshRenderer>(true);//Even search inactive renderers

            SkinnedMeshRenderer renderer;
            if (vertexCount > 0) renderer = renderers.FirstOrDefault(x => x.name == smrName && x.sharedMesh.vertexCount == vertexCount);
            else renderer = renderers.FirstOrDefault(x => x.name == smrName);

            return renderer;
        }


        /// <summary>
        /// Search all SMR's that have existing MeshData values
        /// </summary>
        internal static List<SkinnedMeshRenderer> GetMeshRenderersWithMeshData(ChaControl chaControl, Dictionary<string, MeshData> md) 
        {
            var renderers = chaControl.GetComponentsInChildren<SkinnedMeshRenderer>(true);//Even search inactive renderers
            var meshDataRenderers = new List<SkinnedMeshRenderer>();

            foreach(var smr in renderers) 
            {
                var meshKey = GetMeshKey(smr);
                //Only add the smr if we have computed its mesh data
                var hasMeshData = md.TryGetValue(meshKey, out var _md);
                if (!hasMeshData) 
                    continue;

                meshDataRenderers.Add(smr);
            }

            return meshDataRenderers;
        }


        /// <summary>
        /// Craft smr render key from the name and instance id, used to identify a stored mesh inflation
        /// </summary>
        internal static string KeyFromNameAndVerts(SkinnedMeshRenderer smr) 
        {        
            if (smr == null || smr.sharedMesh == null) return null;
            var meshIdentifier = new MeshIdentifier(smr.name, smr.sharedMesh.vertexCount);
            return meshIdentifier.RenderKey;
        }


        /// <summary>
        /// Will get any Mesh Renderers for the given ChaControl.objxxx passed in
        /// </summary>
        /// <param name="chaControlObjs">The ChaControl.objxxx to fetch mesh renderers from  Might work for other GameObjects as well</param>
        internal static List<SkinnedMeshRenderer> GetMeshRenderers(GameObject[] chaControlObjs, bool findAll = false) 
        {            
            var renderers = new List<SkinnedMeshRenderer>();
            if (chaControlObjs == null) return renderers;

            foreach(var chaControlObj in chaControlObjs) 
            {
                if (chaControlObj == null) continue;

                var skinnedItems = GetMeshRenderers(chaControlObj, findAll);
                if (skinnedItems != null && skinnedItems.Count > 0) 
                {
                    renderers.AddRange(skinnedItems);
                }
            }

            // PregnancyPlusPlugin.Logger.LogInfo($"GetMeshRenderers > {renderers.Count}");
            return renderers;
        }
        

        internal static List<SkinnedMeshRenderer> GetMeshRenderers(GameObject characterObj, bool findAll = false) 
        {            
            var renderers = new List<SkinnedMeshRenderer>();
            if (characterObj == null) return renderers;

            var skinnedItem = characterObj.GetComponentsInChildren<SkinnedMeshRenderer>(findAll);            
            if (skinnedItem.Length > 0) 
            {
                renderers.AddRange(skinnedItem);
            }

            return renderers;
        }


        /// <summary>   
        /// Will fetch an active CharaCustomFunctionController for the given character and plugin GUID
        /// </summary>  
        internal static T GetCharacterBehaviorController<T>(ChaControl chaControl, string targetBehaviorId)  where T : CharaCustomFunctionController
        {
            if (chaControl == null) return null;

            //Get all registered behaviors for this character
            var behaviors = CharacterApi.GetBehaviours(chaControl);
            if (behaviors == null) return null;

            foreach(var behavior in behaviors) 
            {
                //Find the behavior with matching id (COM name)
                if (behavior.ExtendedDataId == targetBehaviorId) 
                {
                    //If we know the type cast it, otherwise use CharaCustomFunctionController
                    return (T)behavior;
                }                
            }

            return null;
        }


        /// <summary>   
        /// Needed a standard way to pull bones from ChaControl obj
        /// </summary>  
        internal static Transform GetBone(ChaControl chaControl, string boneName) 
        {
            if (chaControl == null) return null;
            if (boneName.Contains(".")) return null;
            
            return chaControl.GetComponentsInChildren<Transform>().FirstOrDefault(x => x.name == boneName);
        }


        /// <summary>   
        /// returns a bone by name.  If a period is included, it will get the child bone in the chain like "boneParentName.boneChildName"
        /// </summary>  
        internal static GameObject GetBoneGO(ChaControl chaControl, string boneName) 
        {
            if (chaControl == null) return null;
            if (boneName == null) return null;

            //When bone name is chanined with a period get the correct nested child bone (usefull when multiple matching bone names)
            if (boneName.Contains("."))
            {                
                var boneNames = boneName.Split('.');
                Transform _bone = null;
                var i = 0;

                //With each bone name, navigate to the last child and return it
                foreach(var name in boneNames)
                {
                    if (name == null || name.Equals("")) return null;

                    //On first iteration fetch bone like normal
                    if (i == 0) _bone = GetBone(chaControl, name);

                    //On nth iteration fetch bone by transform child name
                    if (i > 0) _bone = _bone.Find(name);                    

                    if (_bone == null) return null;
                    i++;
                }

                return _bone.gameObject;
            }
            
            //Otherwise Get a bone by name
            var bone = GetBone(chaControl, boneName);
            if (bone == null) return null;
            return bone.gameObject;
        }

        /// <summary>   
        /// Find a parent game object given its name
        /// </summary> 
        public static GameObject GetParentGoByName(GameObject childGo, string parentName)
        {
            if (childGo == null) return null;
            
            var currentGo = childGo;
            while (currentGo.transform.parent != null)
            {
                //Get the parent game object, and check the name for a match
                currentGo = currentGo.transform.parent.gameObject;                
                if (currentGo.name == parentName) return currentGo;                
            }

            return null;
        }
        

        /// <summary>
        /// Get the BodyTop bone local scale
        /// </summary>
        internal static Vector3 GetBodyTopScale(ChaControl chaControl)  
        {
            var bodyTopBone = GetBone(chaControl, "BodyTop");
            if (bodyTopBone == null) return Vector3.one;
            return bodyTopBone.localScale;
        }


        /// <summary>
        /// Get the N_geight bone local scale (I dont think we use this anywhere anymore)
        /// </summary>
        internal static Vector3 GetN_HeightScale(ChaControl chaControl)  
        {
            #if KKS
                var boneName = "cf_n_height";
            #elif HS2 || AI
                var boneName = "cf_N_height";
            #endif

            var bodyTopBone = GetBone(chaControl, boneName);
            if (bodyTopBone == null) return Vector3.one;
            return bodyTopBone.localScale;
        }


        internal static Mesh CopyMesh(Mesh mesh)
        {
            return (Mesh)UnityEngine.Object.Instantiate(mesh);
        }


        public static string ConvertToCm(float unitySize)
        {
            return (unitySize * gameSizeToCentimetersRatio).ToString("F1") + "cm";
        }

        /// <summary>   
        /// Creates a mesh dictionary key based on mesh name and vert count. (because mesh names can be the same, vertex count makes it almost always unique)
        /// </summary>    
        internal static string GetMeshKey(SkinnedMeshRenderer smr) 
        {
            if (!smr || !smr.sharedMesh) return null;
            return PregnancyPlusHelper.KeyFromNameAndVerts(smr);
        }


        /// <summary>   
        /// Get the cloth coeffients from an active unity cloth component
        ///     Apparently adding a blendshape at runtime destroys any existing cloth coefficents on that mesh.  Yay more Unity problems...
        /// </summary>   
        /// <param name="clothCoefficientsDict">The coefficients we want to reapply later</param>
        /// <param name="coefficientCounter">Counts the number of times we have re applied coefficients to a Cloth component (lets us limit it)</param>
        internal static bool GetClothCoefficients(GameObject clothObject, string renderKey, Dictionary<string, ClothSkinningCoefficient[]> clothCoefficientsDict, 
                                                  Dictionary<string, int> coefficientCounter) 
        {
            if (clothObject == null) 
                return false;
                
            var cloth = clothObject.GetComponent<Cloth>();
            if (cloth == null || !cloth.enabled)
                return false;

            //Reset count
            coefficientCounter[renderKey] = 0;

            //If we did not already store this meshes's cloth coefficients, store them
            if (!clothCoefficientsDict.ContainsKey(renderKey))
                clothCoefficientsDict[renderKey] = cloth.coefficients;

            if (PregnancyPlusPlugin.DebugLog.Value) PregnancyPlusPlugin.Logger.LogWarning($" Saving Cloth coefficients for later on {renderKey}"); 
            return true;            
        }


        /// <summary>   
        /// Re-apply a saved cloth coefficient list to an existing unity cloth component
        /// </summary> 
        /// <param name="clothCoefficientsDict">The coefficients we want to reapply later</param>
        /// <param name="coefficientCounter">Counts the number of times we have re applied coefficients to a Cloth component (lets us limit it)</param>
        internal static void SetClothCoefficients(GameObject clothObject, string renderKey, Dictionary<string, ClothSkinningCoefficient[]> clothCoefficientsDict, 
                                                  Dictionary<string, int> coefficientCounter) 
        {
            if (clothObject == null) 
                return;
            
            //If the key does not exists, skip
            if (!clothCoefficientsDict.ContainsKey(renderKey))
                return;
    
            var cloth = clothObject.GetComponent<Cloth>();
            if (cloth == null)
                return;            

            if (PregnancyPlusPlugin.DebugLog.Value) PregnancyPlusPlugin.Logger.LogWarning($" Setting original cloth coefficients");
            cloth.coefficients = clothCoefficientsDict[renderKey];   

            //Because we don't want to continue altering the coefficent state after bypassing the unity issue, remove the renderKey after updating the cloth component twice
            if (coefficientCounter[renderKey] > 0)   
                clothCoefficientsDict.Remove(renderKey);

            coefficientCounter[renderKey] += 1;
        }
    
    }
}
