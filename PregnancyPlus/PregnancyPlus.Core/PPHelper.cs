using KKAPI;
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
    //Stores the values to uniqoely identify a skinned mesh renderer.  Much better than passing around the blendshapes themselves since they tend to be big
    public class MeshIdentifier {            
        public string name;
        public int vertexCount;

        public string RenderKey {
            get { return $"{name}_{vertexCount}"; }
        }

        public MeshIdentifier(string _name, int _vertexCount) 
        {
            name = _name;
            vertexCount = _vertexCount;
        }
    }

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
        /// Craft smr render key from the name and instance id, used to identify a stored mesh inflation
        /// </summary>
        internal static string KeyFromNameAndVerts(SkinnedMeshRenderer smr) 
        {        
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
        /// Just get the BodyTop bone local scale
        /// </summary>
        internal static Vector3 GetBodyTopScale(ChaControl chaControl)  
        {
            var bodyTopBone = GetBone(chaControl, "BodyTop");
            if (bodyTopBone == null) return Vector3.one;
            return bodyTopBone.localScale;
        }


        /// <summary>
        /// Just get the N_geight bone local scale (Needed in some local to world scale translations)
        /// </summary>
        internal static Vector3 GetN_HeightScale(ChaControl chaControl)  
        {
            #if KK
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
    
    }
}
