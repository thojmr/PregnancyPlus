using KKAPI;
using KKAPI.Chara;
using KKABMX.Core;
using UnityEngine;
using System;
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

        internal const float gameSizeToCentimetersRatio = 10.3092781f;    


        internal static SkinnedMeshRenderer GetMeshRenderer(ChaControl chaControl, string renderKey, bool findAll = false) 
        {
            var renderers = chaControl.GetComponentsInChildren<SkinnedMeshRenderer>(findAll);
            var renderer = renderers.FirstOrDefault(x => (KeyFromNameAndVerts(x.name, x.sharedMesh.vertexCount)) == renderKey);
            return renderer;
        }


        /// <summary>
        /// Craft smr render key from the name and vert count, used to identify a stored mesh inflation
        /// </summary>
        internal static string KeyFromNameAndVerts(string name, int vertexCount) => $"{name}_{vertexCount.ToString()}";


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
                if (skinnedItems != null && skinnedItems.Count > 0) {
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


        internal static Renderer GetRenderer(ChaControl chaControl, string renderKey) 
        {
            var renderers = chaControl.GetComponentsInChildren<Renderer>(true);
            var renderer = renderers.FirstOrDefault(x => x.name == renderKey);
            return renderer;
        }


        internal static List<Renderer> GetRenderers(GameObject[] chaControlObjs) 
        {            
            var renderers = new List<Renderer>();
            if (chaControlObjs == null) return renderers;

            foreach(var chaControlObj in chaControlObjs) 
            {
                if (chaControlObj == null) continue;

                var skinnedItems = GetRenderers(chaControlObj);
                if (skinnedItems != null && skinnedItems.Count > 0) {
                    renderers.AddRange(skinnedItems);
                }
            }

            // PregnancyPlusPlugin.Logger.LogInfo($"GetMeshRenderers > {renderers.Count}");
            return renderers;
        }


        internal static List<Renderer> GetRenderers(GameObject characterObj) 
        {            
            var renderers = new List<Renderer>();
            if (characterObj == null) return renderers;

            var skinnedItem = characterObj.GetComponentsInChildren<Renderer>(true);            
            if (skinnedItem.Length > 0) 
            {
                renderers.AddRange(skinnedItem);
            }

            return renderers;
        }


        /// <summary>   
        /// Will fetch number of weeks from KK_Pregnancy data for this character
        /// </summary>  
        internal static int GetWeeksFromPregnancyPluginData(ChaControl chaControl, string targetBehaviorId)
        {
            var kkPregCtrlInst = PregnancyPlusHelper.GetCharacterBehaviorController<CharaCustomFunctionController>(chaControl, targetBehaviorId);
            if (kkPregCtrlInst == null) return -1;

            //Get the pregnancy data object
            var data = kkPregCtrlInst.GetType().GetProperty("Data").GetValue(kkPregCtrlInst, null);
            if (data == null) return -1;

            var week = Traverse.Create(data).Field("Week").GetValue<int>();
            if (week.Equals(null) || week < -1) return -1;

            return week;
        }


        /// <summary>   
        /// If the current characters mesh is set by the Uncensor plugin we need to know this to correctly shift the mesh's localspace vertex positions
        /// The LS positions for uncensor match that of HS2 and AI, but not the defulat KK body mesh (This took forever to track down!)
        /// </summary>  
        internal static bool IsUncensorBody(ChaControl chaControl, string UncensorCOMName) 
        {
            //grab the active uncensor controller of it exists
            var uncensorController = PregnancyPlusHelper.GetCharacterBehaviorController<CharaCustomFunctionController>(chaControl, UncensorCOMName);
            if (uncensorController == null) return false;

            //Get the body type name, and see if it is the default mesh name
            var bodyData = uncensorController.GetType().GetProperty("BodyData").GetValue(uncensorController, null);
            if (bodyData == null) return false;

            var bodyGUID = Traverse.Create(bodyData).Field("BodyGUID").GetValue<string>();
            if (bodyGUID == null) return false;

            return bodyGUID != PregnancyPlusCharaController.DefaultBodyFemaleGUID && bodyGUID != PregnancyPlusCharaController.DefaultBodyMaleGUID;
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
            
            return chaControl.GetComponentsInChildren<Transform>().FirstOrDefault(x => x.name == boneName);
        }


        internal static GameObject GetBoneGO(ChaControl chaControl, string boneName) 
        {
            if (chaControl == null) return null;
            
            var bone = GetBone(chaControl, boneName);
            if (bone == null) return null;
            return bone.gameObject;
        }


        /// <summary>
        /// Calculates the length of a set of chained bones from bottom up.  It will only caluculate the true Y distance, so it effectively ignores any animations (behaves like a TPose measurement).false  Should include bones scales as well
        /// </summary>
        /// <param name="chaControl">The character to fetch bones from</param>
        /// <param name="boneStart">The starting (bottom of tree) bone name</param>
        /// <param name="boneEnd">The optional (top level) end bone name.  If null, the entire bone tree from bottom to top will be calculated.</param>
        internal static float BoneChainStraigntenedDistance(ChaControl chaControl, string boneStart, string boneEnd = null) 
        {
            //loops through each bone starting bottom going up through parent to destination (or root)
            var currentBone = GetBoneGO(chaControl, boneStart);
            GameObject lastBone = currentBone;

            if (currentBone == null) return 0;  
            float distance = 0;        

            //Keep going while a parent transform exists
            while (currentBone != null && currentBone.transform.parent) 
            {            
                //If the bone name matches boneEnd return the total distance to this bone so far
                if (boneEnd != null && currentBone.name.ToLower() == boneEnd.ToLower()) 
                {
                    break;
                }

                //calculate the diatance by measuring y local distances only (we want to exclude angular distances)
                var newDifference = (lastBone != null ? currentBone.transform.InverseTransformPoint(currentBone.transform.position).y - currentBone.transform.InverseTransformPoint(lastBone.transform.position).y : 0);
                // if (PregnancyPlusPlugin.debugLog) PregnancyPlusPlugin.Logger.LogInfo($" newDifference {newDifference}  currentBone.name {currentBone.name}  scale {currentBone.transform.localScale} corrected {((newDifference * currentBone.transform.localScale.y) - newDifference)}");
                
                //Ignore any negative bone differences (like char root bone which is at 0,0,0)
                if (newDifference > 0) {                    
                    distance = distance + newDifference;
                    lastBone = currentBone;
                }                

                currentBone = currentBone.transform.parent.gameObject;
            }

            //Check for BodyTop scale to apply it to distance (cf_n_height scale doesnt matter here for some reason)
            var BodyTopScale = GetBodyTopScale(chaControl);
            if (BodyTopScale.y != 1) 
            {                
                if (PregnancyPlusPlugin.debugLog) PregnancyPlusPlugin.Logger.LogInfo($" applying BodyTop scale to distance: {distance} scale: {BodyTopScale.y}");
                distance = distance * BodyTopScale.y;
            }

            if (PregnancyPlusPlugin.debugLog) PregnancyPlusPlugin.Logger.LogInfo($" total bone chain dist {distance}  cm:{ConvertToCm(distance)}");
            return distance;
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
            Mesh newmesh = new Mesh();
            newmesh.vertices = mesh.vertices;
            newmesh.triangles = mesh.triangles;
            newmesh.uv = mesh.uv;
            newmesh.normals = mesh.normals;
            newmesh.colors = mesh.colors;
            newmesh.tangents = mesh.tangents;

            return newmesh;
        }


        public static string ConvertToCm(float unitySize)
        {
            return (unitySize * gameSizeToCentimetersRatio).ToString("F1") + "cm";
        }
    
    }
}
