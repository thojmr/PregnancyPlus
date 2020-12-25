using KKAPI;
using KKAPI.Chara;
using UnityEngine;
using System;
using System.Collections.Generic;
using System.Linq;
using ExtensibleSaveFormat;

namespace KK_PregnancyPlus
{
    internal static class PregnancyPlusHelper
    {

        internal static float FastDistance(Vector3 firstPosition, Vector3 secondPosition) 
        {
            //Calculates distance faster than vector3.distance.
            Vector3 heading;
            float distanceSquared;
    
            heading.x = firstPosition.x - secondPosition.x;
            heading.y = firstPosition.y - secondPosition.y;
            heading.z = firstPosition.z - secondPosition.z;
    
            distanceSquared = heading.x * heading.x + heading.y * heading.y + heading.z * heading.z;
            return Mathf.Sqrt(distanceSquared);
        }

        internal static SkinnedMeshRenderer GetMeshRenderer(ChaControl chaControl, string renderKey) 
        {
            var renderers = chaControl.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            var renderer = renderers.FirstOrDefault(x => (x.name + x.sharedMesh.vertexCount.ToString()) == renderKey);
            return renderer;
        }

        /// <summary>
        /// Will get any Mesh Renderers for the given ChaControl.objxxx passed in
        /// </summary>
        /// <param name="chaControlObjs">The ChaControl.objxxx to fetch mesh renderers from  Might work for other GameObjects as well</param>
        internal static List<SkinnedMeshRenderer> GetMeshRenderers(GameObject[] chaControlObjs) {            
            var renderers = new List<SkinnedMeshRenderer>();
            if (chaControlObjs == null) return renderers;

            foreach(var chaControlObj in chaControlObjs) 
            {
                if (chaControlObj == null) continue;

                var skinnedItems = GetMeshRenderers(chaControlObj);
                if (skinnedItems != null && skinnedItems.Count > 0) {
                    renderers.AddRange(skinnedItems);
                }
            }

            // PregnancyPlusPlugin.Logger.LogInfo($"GetMeshRenderers > {renderers.Count}");
            return renderers;
        }
        
        internal static List<SkinnedMeshRenderer> GetMeshRenderers(GameObject characterObj) {            
            var renderers = new List<SkinnedMeshRenderer>();
            if (characterObj == null) return renderers;

            var skinnedItem = characterObj.GetComponentsInChildren<SkinnedMeshRenderer>(true);            
            if (skinnedItem.Length > 0) {
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

        internal static List<Renderer> GetRenderers(GameObject[] chaControlObjs) {            
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

        internal static List<Renderer> GetRenderers(GameObject characterObj) {            
            var renderers = new List<Renderer>();
            if (characterObj == null) return renderers;

            var skinnedItem = characterObj.GetComponentsInChildren<Renderer>(true);            
            if (skinnedItem.Length > 0) {
                renderers.AddRange(skinnedItem);
            }

            return renderers;
        }

        internal static int GetWeeksFromData(PluginData data)
        {
            if (data?.data == null) return 0;

            if (data.data.TryGetValue("Week", out var weekVal))
            {
                try
                {
                    if (weekVal == null) return 0;
                    var week = (int)weekVal;
                    return week;
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex);
                }

                return 0;
            }

            return 0;
        }
    
    }
}
