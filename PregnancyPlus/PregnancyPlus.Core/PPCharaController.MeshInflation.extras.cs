﻿using KKAPI;
using KKAPI.Chara;
using UnityEngine;
using System;
using System.Collections.Generic;
using System.Linq;
using KKAPI.Studio;
using KKAPI.Maker;

using UniRx;
#if HS2 || AI
    using AIChara;
#endif

namespace KK_PregnancyPlus
{

    //This partial class contains all the less critical mesh inflation methods
    public partial class PregnancyPlusCharaController: CharaCustomFunctionController
    {   

        public float BellyButtonOffset = 0.155f;
        

        /// <summary>
        /// An overload for MeshInflate() that allows you to pass an initial inflationSize param
        /// For quickly setting the size, without worrying about the other config params
        /// </summary>
        /// <param name="inflationSize">Sets inflation size from 0 to 40, clamped</param>
        /// <param name="meshInflateFlags">Contains any flags needed for mesh computation</param>
        public void MeshInflate(float inflationSize, string callee, MeshInflateFlags meshInflateFlags = null)
        {                  
            //Allow an initial size to be passed in, and sets it to the config           
            infConfig.inflationSize = Mathf.Clamp(inflationSize, 0, 40);         
            if (meshInflateFlags == null) meshInflateFlags = new MeshInflateFlags(this);
            meshInflateFlags.infConfig = infConfig;//Update the new config value here too

            MeshInflate(meshInflateFlags, callee);
        }

        /// <summary>
        /// An overload for MeshInflate() that allows you to pass existing card data as the first param
        /// </summary>
        /// <param name="cardData">Some prexisting PregnancyPlusData that we want to activate</param>
        /// <param name="meshInflateFlags">Contains any flags needed for mesh computation</param>
        public void MeshInflate(PregnancyPlusData cardData, string callee, MeshInflateFlags meshInflateFlags = null)
        {                  
            //Allow an initial size to be passed in, and sets it to the config           
            infConfig = cardData;          
            if (meshInflateFlags == null) meshInflateFlags = new MeshInflateFlags(this);
            meshInflateFlags.infConfig = infConfig;//Update the new config value here too

            MeshInflate(meshInflateFlags, callee);
        }


        /// <summary>
        /// Limit where you can and cannot trigger inflation.  Always in Studio and Maker. Conditionally in Story mode
        /// </summary>
        public bool AllowedToInflate() 
        {
            var storyModeEnabled = PregnancyPlusPlugin.StoryMode != null ? PregnancyPlusPlugin.StoryMode.Value : false;
            return StudioAPI.InsideStudio || MakerAPI.InsideMaker || (storyModeEnabled && infConfig.GameplayEnabled);
        }


        /// <summary>
        /// Get the distance from the characters feet to the belly button collapsed into a straight Y line.null  (Ignores animation and scale, gives true measurement)
        /// </summary>
        internal float GetBellyButtonLocalHeight() 
        {            
            //Calculate the belly button height by getting each bone distance from foot to belly button (even during animation the height is correct!)
            #if KKS
                var bbHeight = BoneChainYDistance("cf_j_foot_L", "cf_j_waist01");
            #elif HS2 || AI            
                var bbHeight = BoneChainYDistance("cf_J_Toes01_L", "cf_J_Kosi01");                       
            #endif                      
            
            return bbHeight;
        }


        /// <summary>
        /// Calculate the initial sphere radius by taking the smaller of the wasit width or waist to rib height.static  InflationMultiplier will augment this
        /// </summary>
        internal float GetSphereRadius(float wasitToRibDist, float wasitWidth) 
        {
            //In 6.0+ we have a static radius size
            if (infConfig.IsPluginVersionBelow(6.0))
            {
                //The float numbers are just arbitrary numbers that ended up looking porportional
                var radius = Math.Min(wasitToRibDist/1.25f, wasitWidth/1.3f);

                //Scale older card radiuses to make them about the same size they used to be
                var legacyScale = infConfig.UseOldCalcLogic() ? 0.9f : 1f;

                //Older cards had slightly smaller radiuses because of less accuraate belly bone measurements, adjust these old cards to look similar in size with new bone logic
                radius = radius * legacyScale;

                return radius;
            }
            else
            {
                #if KKS
                    return 0.13f;
                #elif HS2 || AI
                    return 1.3f;
                #endif
            }

        }


        /// <summary>   
        /// Move the sphereCenter this much up or down to place it better visually (This used to be way more complucated,  too lazy to reduce it)
        /// </summary>
        internal Vector3 GetBellyButtonOffsetVector(float currentHeight) 
        {
            //Makes slight vertical adjustments to put the sphere at the correct point                  
            return Vector3.up * GetBellyButtonOffset(currentHeight);     
        }


        /// <summary>   
        /// The belly center offset, thats needed to line it up with the belly button
        /// </summary>
        internal float GetBellyButtonOffset(float currentHeight) 
        {
            //Makes slight vertical adjustments to put the sphere at the correct point                  
            return BellyButtonOffset * currentHeight;     
        }



        /// <summary>   
        /// Calculate the magnitude of a vector, which is faster than Vector3.distance, but does not return a true distance
        /// </summary> 
        internal float FastMagnitude(Vector3 firstPosition, Vector3 secondPosition) 
        {
            Vector3 heading;    
            heading.x = firstPosition.x - secondPosition.x;
            heading.y = firstPosition.y - secondPosition.y;
            heading.z = firstPosition.z - secondPosition.z;
    
            return heading.x * heading.x + heading.y * heading.y + heading.z * heading.z;
        }    


        /// <summary>   
        /// Clear all inflations and remove the known mesh verts
        /// </summary>   
        public void CleanSlate() 
        {
            ResetInflation();
            var keyList = new List<string>(md.Keys);
            RemoveRenderKeys(keyList);

            //Always clear debug objects from character in debug mode
            #if DEBUG
                DebugTools.ClearAllThingsFromCharacter();
            #endif
        }
        

        internal void RemoveRenderKeys(List<string> keysToRemove) 
        {
            //Chear out any tracked verticie dictionaries by render key
            foreach(var key in keysToRemove) 
            {
                RemoveRenderKey(key);
            }
        }


        internal void RemoveRenderKey(string keyToRemove) 
        {
            if (md.ContainsKey(keyToRemove)) md.Remove(keyToRemove);
        }

        
        /// <summary>   
        /// Creates a mesh dictionary key based on mesh name and vert count. (because mesh names can be the same, vertex count makes it almost always unique)
        /// </summary>    
        internal string GetMeshKey(SkinnedMeshRenderer smr) 
        {
            return PregnancyPlusHelper.GetMeshKey(smr);
        }


        /// <summary>
        /// Get the main body mesh renderer for a character
        /// </summary>
        public SkinnedMeshRenderer GetBodyMeshRenderer()
        {
            var bodyMeshRenderers = PregnancyPlusHelper.GetMeshRenderers(ChaControl.objBody, findAll: true);
            var body = bodyMeshRenderers.FindAll(x => x?.name == BodyMeshName);
            if (body == null || body.Count <= 0)
            {
                PregnancyPlusPlugin.errorCodeCtrl.LogErrorCode(charaFileName, ErrorCode.PregPlus_NoBodyMesh, 
                    $" The body skin could not be found for this character: {charaFileName}.  That can't be healthy..."); 
                return null;
            }

            if (body.Count > 1 && PregnancyPlusPlugin.DebugLog.Value) 
                PregnancyPlusPlugin.Logger.LogWarning($" More than one body mesh was found under .objBody, returning the first for {charaFileName}");

            return body[0];
        }


        /// <summary>
        /// Whether the body mesh render is currently active
        /// </summary>
        public bool IsBodySmrActive()
        {
            var bodySmr = GetBodyMeshRenderer();
            if (!bodySmr) return false;
            
            return bodySmr.enabled;
        }


        /// <summary>
        /// Detect when this mesh is a body mesh nested under a cloth tree (body replacement plugin probably)
        /// </summary>
        public bool BodyNestedUnderCloth(SkinnedMeshRenderer smr, SkinnedMeshRenderer bodySmr) 
        {
            if (bodySmr == null) return false;

            //Ignore instances when both are disabled, since neither is even visible
            //  If the real bodySmr is currently visible, then this is not a nested body
            var shouldEvenConsider = smr.enabled && !bodySmr.enabled;

            //Does the smr have the bodymesh name inside it?
            return shouldEvenConsider && smr.name.Contains(BodyMeshName);
        }


        public void logCharMeshInfo(MeshData md, SkinnedMeshRenderer smr, Vector3 sphereCenter, bool isClothingMesh = false) 
        {
            if (!PregnancyPlusPlugin.DebugCalcs.Value) return;

            PregnancyPlusPlugin.Logger.LogInfo($@" 
    ******CharMeshInfo****** {smr.name}       
    ChaControl.position   {ChaControl.transform.position}
    smr.position          {Round(smr.transform.position)}
    smr.lRotation         {smr.transform.localRotation}
    smr.lScale            {smr.transform.localScale}
    smr.rootBone.name     {smr.rootBone?.name}
    smr.localBounds       {Round(smr.localBounds.center)}   
    smr.bounds            {Round(smr.sharedMesh.bounds.center)}
    bbHeight              {bellyInfo.BellyButtonHeight}
    sphereCenter          {sphereCenter}
    isClothingMesh        {isClothingMesh}
    MeshOffsetType        {MeshOffSet.GetMeshOffsetType(smr).ToString()}
    VertexCount           {smr.sharedMesh.vertexCount}
    mesh.isReadable       {smr.sharedMesh.isReadable}
    ************************
             ");
        }         

        public string Round(Vector3 vector, int digits = 4) 
        {
            return $"({Math.Round(vector.x, digits)}, {Math.Round(vector.y, digits)}, {Math.Round(vector.z, digits)})";
        }

    }
}


