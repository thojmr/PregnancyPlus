using KKAPI;
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
        public bool MeshInflate(float inflationSize, MeshInflateFlags meshInflateFlags = null)
        {                  
            //Allow an initial size to be passed in, and sets it to the config           
            infConfig.inflationSize = Mathf.Clamp(inflationSize, 0, 40);         
            if (meshInflateFlags == null) meshInflateFlags = new MeshInflateFlags(this);
            meshInflateFlags.infConfig = infConfig;//Update the new config value here too

            return MeshInflate(meshInflateFlags);
        }

        /// <summary>
        /// An overload for MeshInflate() that allows you to pass existing card data as the first param
        /// </summary>
        /// <param name="cardData">Some prexisting PregnancyPlusData that we want to activate</param>
        /// <param name="meshInflateFlags">Contains any flags needed for mesh computation</param>
        public bool MeshInflate(PregnancyPlusData cardData, MeshInflateFlags meshInflateFlags = null)
        {                  
            //Allow an initial size to be passed in, and sets it to the config           
            infConfig = cardData;          
            if (meshInflateFlags == null) meshInflateFlags = new MeshInflateFlags(this);
            meshInflateFlags.infConfig = infConfig;//Update the new config value here too

            return MeshInflate(meshInflateFlags);
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
            #if KK
                var bbHeight = BoneChainYDistance(ChaControl, bellyInfo.TotalCharScale, "cf_j_foot_L", "cf_j_waist01");
            #elif HS2 || AI            
                var bbHeight = BoneChainYDistance(ChaControl, bellyInfo.TotalCharScale, "cf_J_Toes01_L", "cf_J_Kosi01");                       
            #endif                      
            
            return bbHeight;
        }


        /// <summary>
        /// Calculate the initial sphere radius by taking the smaller of the wasit width or waist to rib height. This is pre InflationMultiplier
        /// </summary>
        internal float GetSphereRadius(float wasitToRibDist, float wasitWidth, Vector3 charScale) {
            //The float numbers are just arbitrary numbers that ended up looking porportional
            return Math.Min(wasitToRibDist/1.25f, wasitWidth/1.3f) * charScale.y;
        }


        /// <summary>   
        /// Move the sphereCenter this much up or down to place it better visually
        /// </summary>
        internal Vector3 GetBellyButtonOffsetVector(Transform fromPosition, float currentHeight) 
        {
            //Makes slight vertical adjustments to put the sphere at the correct point                  
            return fromPosition.up * GetBellyButtonOffset(currentHeight);     
        }


        /// <summary>   
        /// The belly center offset, thats needed to line it up with the belly button
        /// </summary>
        internal float GetBellyButtonOffset(float currentHeight) 
        {
            //Makes slight vertical adjustments to put the sphere at the correct point                  
            return BellyButtonOffset * currentHeight;     
        }



        internal float FastDistance(Vector3 firstPosition, Vector3 secondPosition) 
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


        /// <summary>   
        /// Clear all inflations and remove the known mesh verts
        /// </summary>   
        public void CleanSlate() {
            ResetInflation();
            var keyList = new List<string>(originalVertices.Keys);
            RemoveRenderKeys(keyList);
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
            if (originalVertices.ContainsKey(keyToRemove)) originalVertices.Remove(keyToRemove);
            if (inflatedVertices.ContainsKey(keyToRemove)) inflatedVertices.Remove(keyToRemove);
            if (currentVertices.ContainsKey(keyToRemove)) currentVertices.Remove(keyToRemove);
            if (bellyVerticieIndexes.ContainsKey(keyToRemove)) bellyVerticieIndexes.Remove(keyToRemove);        
            if (alteredVerticieIndexes.ContainsKey(keyToRemove)) alteredVerticieIndexes.Remove(keyToRemove);  
            if (clothingOffsets.ContainsKey(keyToRemove)) clothingOffsets.Remove(keyToRemove);  
        }

        
        /// <summary>   
        /// Creates a mesh dictionary key based on mesh name and vert count. (because mesh names can be the same, vertex count makes it almost always unique)
        /// </summary>    
        internal string GetMeshKey(SkinnedMeshRenderer smr) 
        {
            return PregnancyPlusHelper.KeyFromNameAndVerts(smr);
        }


        /// <summary>
        /// If the vert is within the calculated normals radius, then consider it as an altered vert that needs normal recalculation when applying inflation
        ///  Hopefully this will reduce breast shadows for smaller bellies
        /// </summary>
        public void CalculateNormalsBoundary(float vertDistance, float vertNormalCaluRadius, int i, string renderKey)
        {
            if (vertDistance < vertNormalCaluRadius)
            {
                alteredVerticieIndexes[renderKey][i] = true;
            }
            else 
            {
                alteredVerticieIndexes[renderKey][i] = false;
            }
        }

    }
}


