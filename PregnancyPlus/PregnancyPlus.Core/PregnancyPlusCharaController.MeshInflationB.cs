using KKAPI;
using KKAPI.Chara;
using UnityEngine;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
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
        
        /// <summary>
        /// Limit where you can and cannot trigger inflation.  Always in Studio and Maker. Conditionally in Story mode
        /// </summary>
        public bool AllowedToInflate() {
            var storyModeEnabled = PregnancyPlusPlugin.StoryMode != null ? PregnancyPlusPlugin.StoryMode.Value : false;
            return StudioAPI.InsideStudio || MakerAPI.InsideMaker || (storyModeEnabled && infConfig.GameplayEnabled);
        }


        /// <summary>
        /// An overload for MeshInflate() that allows you to pass an initial inflationSize param
        /// For quickly setting the size, without worrying about the other config params
        /// </summary>
        /// <param name="inflationSize">Sets inflation size from 0 to 40, clamped</param>
        public bool MeshInflate(float inflationSize, bool forceInflate = false)
        {                  
            //Allow an initial size to be passed in, and sets it to the config           
            infConfig.inflationSize = Mathf.Clamp(inflationSize, 0, 40);            

            return MeshInflate(forceInflate);
        }

        /// <summary>
        /// An overload for MeshInflate() that allows you to pass existing card data as the first param
        /// </summary>
        /// <param name="cardData">Some prexisting PregnancyPlusData that we want to activate</param>
        public bool MeshInflate(PregnancyPlusData cardData, bool forceInflate = false)
        {                  
            //Allow an initial size to be passed in, and sets it to the config           
            infConfig = cardData;           

            return MeshInflate(forceInflate);
        }


        /// <summary>
        /// Just a helper function to combine searching for verts in a mesh, and then applying the transforms
        /// </summary>
        internal bool ComputeMeshVerts(SkinnedMeshRenderer smr, float sphereRadius, float waistWidth, bool isClothingMesh = false) 
        {
            //The list of bones to get verticies for
            #if KK            
                var boneFilters = new string[] { "cf_s_spine02", "cf_s_waist01", "cf_s_waist02" };//"cs_s_spine01" optionally for wider affected area
            #elif HS2 || AI
                var boneFilters = new string[] { "cf_J_Spine02_s", "cf_J_Kosi01_s", "cf_J_Kosi02_s" };
            #endif

            var hasVerticies = GetFilteredVerticieIndexes(smr, PregnancyPlusPlugin.MakeBalloon.Value ? null : boneFilters);        

            //If no belly verts found, or existing verts already exists, then we can skip this mesh
            if (!hasVerticies) return false; 

            if (PregnancyPlusPlugin.debugLog) PregnancyPlusPlugin.Logger.LogInfo($" ");
            if (PregnancyPlusPlugin.debugLog) PregnancyPlusPlugin.Logger.LogInfo($"  SkinnedMeshRenderer > {smr.name}"); 
            return GetInflatedVerticies(smr, sphereRadius, waistWidth, isClothingMesh);
        }


        /// <summary>
        /// See if we already have this mesh's indexes stored, if the slider values haven't changed then we dont need to recompute, just apply existing cumputed verts
        /// </summary>
        internal bool NeedsComputeVerts(SkinnedMeshRenderer smr, bool sliderHaveChanged) 
        {
            var renderKey = GetMeshKey(smr);
            //Do a quick check to see if we need to fetch the bone indexes again.  ex: on second call we should allready have them
            //This saves a lot on compute apparently!            
            var isInitialized = bellyVerticieIndexes.TryGetValue(renderKey, out bool[] existingValues);
            if (isInitialized)
            {
                //If the vertex count has not changed then we can skip this
                if (existingValues.Length == smr.sharedMesh.vertexCount) return sliderHaveChanged;
            }

            return true;
        }


        /// <summary>
        /// Tried to correct cloth flattening when inflation is at max, by offsetting each vert based on the distance it is from the sphere center to the max sphere radius
        /// </summary>
        /// <param name="sphereCenter">The center position of the inflation sphere</param>
        /// <param name="sphereRadius">The desired sphere radius</param>
        /// <param name="waistWidth">The average width of the characters waist</param>
        /// <param name="origVertWS">The original verticie's worldspace position</param>
        internal float GetClothesFixOffset(Vector3 sphereCenter, float sphereRadius, float waistWidth, Vector3 origVertWS, string meshName) 
        {  
            //The size of the area to spread the flattened offsets over like shrinking center dist -> inflated dist into a small area shifted outside the radius.  So hard to explin with words...
            float baseFlattenExtent = bellyInfo.WaistWidth/40 + (bellyInfo.WaistWidth/40 * GetInflationClothOffset());

            var inflatedVerWS = (origVertWS - sphereCenter).normalized * sphereRadius + sphereCenter;//Get the line we want to do measurements on            
            //We dont care about empty space at sphere center, move outwards a bit before determining vector location on the line
            float awayFromCenter = (waistWidth/3);

            var totatDist = (sphereRadius - awayFromCenter);
            var originToEndDist = FastDistance(origVertWS, inflatedVerWS);
            //Get the positon on a line that this vector exists between flattenExtensStartAt -> to sphereRadius.  Shrink it to scale
            var offset = Math.Abs((totatDist - originToEndDist)) * baseFlattenExtent;

            //This is the total additional distance we want to move this vert away from sphere center
            return offset + GetClothLayerOffset(meshName);
        }


        /// <summary>
        /// There are two cloth layers, inner and outer. I've assigned each cloth layer a static offset. layers: 1 = skin tight, 2 = above skin tight.  This way each layer will have less chance of cliping through to the next
        /// </summary>
        internal float GetClothLayerOffset(string meshName) {            
            #if KK      
                string[] innerLayers = {"o_bra_a", "o_bra_b", "o_shorts_a", "o_shorts_b", "o_panst_garter1", "o_panst_a", "o_panst_b"};
            #elif HS2 || AI                
                string[] innerLayers = {"o_bra_a", "o_bra_b", "o_shorts_a", "o_shorts_b", "o_panst_garter1", "o_panst_a", "o_panst_b"};
            #endif            

            //If inner layer then it doesnt need an additional offset
            if (innerLayers.Contains(meshName)) {
                return 0;
            }

            //The mininum distance offset for each cloth layer, adjusted by user
            float additonalOffset = (bellyInfo.WaistWidth/50) + ((bellyInfo.WaistWidth/50) * GetInflationClothOffset());

            //If outer layer then add the offset
            return additonalOffset;
        } 


        /// <summary>
        /// Get the distance from the characters feet to the belly button unfolded into a straight Y line, even when not standing straight
        /// </summary>
        internal float GetBellyButtonLocalHeight(Transform boneOrMeshTf) 
        {            
            //Calculate the belly button height by getting each bone distance from foot to belly button (even during animation the height is correct!)
            #if KK
                var bbHeight = PregnancyPlusHelper.BoneChainStraigntenedDistance(ChaControl, "cf_j_foot_L", "cf_j_waist01");
            #elif HS2 || AI            
                var bbHeight = PregnancyPlusHelper.BoneChainStraigntenedDistance(ChaControl, "cf_J_Toes01_L", "cf_J_Kosi01");                       
            #endif                      
            
            return bbHeight;
        }


        /// <summary>
        /// Calculate the user input move distance
        /// </summary>
        internal Vector3 GetUserMoveTransform(Transform fromPosition) 
        {
            return fromPosition.up * GetInflationMoveY() + fromPosition.forward * GetInflationMoveZ();
        }


        /// <summary>   
        /// Move the sphereCenter this much up or down to place it better visually
        /// </summary>
        internal Vector3 GetBellyButtonOffset(Transform fromPosition, float currentHeight) 
        {
            //Makes slight vertical adjustments to put the sphere at the correct point                  
            return fromPosition.up * (0.155f * currentHeight);     
        }


        /// <summary>   
        /// This sill taper the belly shape based on user input slider. shrinking the top width, and expanding the bottom width along the YX adis
        /// </summary>
        internal Vector3 GetUserTaperYTransform(Transform meshRootTf, Vector3 smoothedVector, Vector3 sphereCenterPos, float sphereRadius) 
        {
            //Get local space equivalents
            var smoothedVectorLs = meshRootTf.InverseTransformPoint(smoothedVector);
            var sphereCenterLs = meshRootTf.InverseTransformPoint(sphereCenterPos);

            //local Distance up or down from sphere center
            var distFromYCenterLs = smoothedVectorLs.y - sphereCenterLs.y; 
            var distFromXCenterLs = smoothedVectorLs.x - sphereCenterLs.x; 
            //Left side tilts one way, right side the opposite
            var isTop = distFromYCenterLs > 0; 
            var isRight = distFromXCenterLs > 0; 

            //Increase taper amount for vecters further above or below center.  No shift along center
            var taperY = Mathf.Lerp(0, GetInflationTaperY(), Math.Abs(distFromYCenterLs)/sphereRadius);
            //Second lerp to limit how much it shifts l/r when near x=0 line, no shift along center
            taperY = Mathf.Lerp(0, taperY, Math.Abs(distFromXCenterLs)/sphereRadius);
            //Reverse the direction based on which side the vert is on
            taperY = (isRight ? taperY : -taperY);
            taperY = (isTop ? taperY : -taperY);

            smoothedVectorLs.x = (smoothedVectorLs + Vector3.right * taperY).x;

            return meshRootTf.TransformPoint(smoothedVectorLs);
        }

        /// <summary>   
        /// This sill taper the belly shape based on user input slider. pulling out the bottom and pushing in the top along the XZ axis
        /// </summary>
        internal Vector3 GetUserTaperZTransform(Transform meshRootTf, Vector3 smoothedVector, Vector3 sphereCenterPos, float sphereRadius) 
        {
            //Get local space equivalents
            var smoothedVectorLs = meshRootTf.InverseTransformPoint(smoothedVector);
            var sphereCenterLs = meshRootTf.InverseTransformPoint(sphereCenterPos);

            //local Distance up or down from sphere center
            var distFromYCenterLs = smoothedVectorLs.y - sphereCenterLs.y; 
            var distFromZCenterLs = smoothedVectorLs.z - sphereCenterLs.z; 
            //top of belly shifts one way, bottom shifts the opposite
            var isTop = distFromYCenterLs > 0; 

            //Increase taper amount for vecters further above or below center.  No shifting at center
            var taperZ = Mathf.Lerp(0, GetInflationTaperZ(), Math.Abs(distFromYCenterLs)/sphereRadius);
            //Reverse the direction based on which side the vert is on
            taperZ = (isTop ? taperZ : -taperZ);
            var taperedZVert = smoothedVectorLs + Vector3.forward * taperZ;

            //Only lerp z when pulling out, pushing in looks fine as is
            if (smoothedVectorLs.z < taperedZVert.z) {
                //Move verts closest to z=0 more slowly than those out front to reduce skin stretching
                smoothedVectorLs = Vector3.Lerp(smoothedVectorLs, taperedZVert, Math.Abs(distFromZCenterLs)/sphereRadius);
            } else {
                smoothedVectorLs = taperedZVert;
            }

            return meshRootTf.TransformPoint(smoothedVectorLs);
        }

        /// <summary>   
        /// This will shift the sphereCenter position *After sphereifying* on Y or Z axis (This stretches the mesh, where pre sphereifying, it would move the sphere within the mesh like Move Y)
        /// </summary>
        internal Vector3 GetUserShiftTransform(Transform meshRootTf, Vector3 smoothedVector, Vector3 sphereCenterPos, float sphereRadius) 
        {            
            //Get local space equivalents            
            var sphereCenterLs = meshRootTf.InverseTransformPoint(sphereCenterPos);

            //IF the user has selected a y value, lerp the top and bottom slower and lerp any verts closer to z = 0 slower
            if (GetInflationShiftY() != 0) {
                var smoothedVectorLs = meshRootTf.InverseTransformPoint(smoothedVector);
                var distFromYCenterLs = smoothedVectorLs.y - sphereCenterLs.y;
                //Lerp up and down positon more when the belly is near the center Y, and less for top and bottom
                var lerpY = Mathf.Lerp(GetInflationShiftY(), GetInflationShiftY()/4, Math.Abs(distFromYCenterLs/sphereRadius));
                var yLerpedsmoothedVector = smoothedVectorLs + Vector3.up * lerpY;//Since its all local space here, we dont have to use meshRootTf.up

                //Then lerp the previous result based on the distance forward.  More forward is able to move more
                var distanceForward = smoothedVectorLs.z - sphereCenterLs.z; 
                var forwardLerpPos = Vector3.Lerp(smoothedVectorLs, yLerpedsmoothedVector, Math.Abs(distanceForward/sphereRadius + sphereRadius/2));

                //Finally lerp sides slightly slower than center
                var distanceSide = Math.Abs(smoothedVectorLs.x - sphereCenterLs.x); 
                var finalLerpPos = Vector3.Lerp(forwardLerpPos, smoothedVectorLs, Math.Abs(distanceSide/(sphereRadius*2)));

                //return the shift up/down 
                smoothedVector = meshRootTf.TransformPoint(finalLerpPos);
            }
            //If the user has selected a z value
            if (GetInflationShiftZ() != 0) {
                var smoothedVectorLs = meshRootTf.InverseTransformPoint(smoothedVector);//In case it was changed above in Y

                //Move the verts closest to sphere center Z more slowly than verts at the belly button.  Otherwise you stretch the ones near the body too much
                var lerpZ = Mathf.Lerp(0, GetInflationShiftZ(), (smoothedVectorLs.z - sphereCenterLs.z)/(sphereRadius *2));
                var finalLerpPos = smoothedVectorLs + Vector3.forward * lerpZ;

                smoothedVector = meshRootTf.TransformPoint(finalLerpPos);
            }
            return smoothedVector;
        }


        /// <summary>   
        /// This will stretch the mesh wider
        /// </summary>        
        internal Vector3 GetUserStretchXTransform(Transform meshRootTf, Vector3 smoothedVector, Vector3 sphereCenterPos) 
        {
            //Allow user adjustment of the width of the belly
            //Get local space position to eliminate rotation in world space
            var smoothedVectorLs = meshRootTf.InverseTransformPoint(smoothedVector);
            var sphereCenterLs = meshRootTf.InverseTransformPoint(sphereCenterPos);
            //local Distance left or right from sphere center
            var distFromXCenterLs = smoothedVectorLs.x - sphereCenterLs.x;                

            var changeInDist = distFromXCenterLs * (GetInflationStretchX() + 1);  
            //Get new local space X position
            smoothedVectorLs.x = (sphereCenterLs + Vector3.right * changeInDist).x;

            //Convert back to world space
            return meshRootTf.TransformPoint(smoothedVectorLs);  
        }


        internal Vector3 GetUserStretchYTransform(Transform meshRootTf, Vector3 smoothedVector, Vector3 sphereCenterPos) 
        {
            //Allow user adjustment of the height of the belly
            //Get local space position to eliminate rotation in world space
            var smoothedVectorLs = meshRootTf.InverseTransformPoint(smoothedVector);
            var sphereCenterLs = meshRootTf.InverseTransformPoint(sphereCenterPos);

            //local Distance up or down from sphere center
            var distFromYCenterLs = smoothedVectorLs.y - sphereCenterLs.y; 
            
            //have to change growth direction above and below center line
            var changeInDist = distFromYCenterLs * (GetInflationStretchY() + 1);  
            //Get new local space X position
            smoothedVectorLs.y = (sphereCenterLs + Vector3.up * changeInDist).y;
            
            //Convert back to world space
            return meshRootTf.TransformPoint(smoothedVectorLs); 
        }

        /// <summary>
        /// This will help pvent too much XY direction change, keeping the belly more round than disk like at large sizes
        /// </summary>
        internal Vector3 SculptBaseShape(Transform meshRootTf, Vector3 originalVertice, Vector3 smoothedVector, Vector3 sphereCenter) {
            var originalVerticeLs = meshRootTf.InverseTransformPoint(originalVertice);
            var smoothedVectorLs = meshRootTf.InverseTransformPoint(smoothedVector);
            var sphereCenterLs = meshRootTf.InverseTransformPoint(sphereCenter);

            //We only want to limit expansion n XY plane for this lerp
            var sphereCenterXY = new Vector2(sphereCenterLs.x, sphereCenterLs.y);
            var origVertXY = new Vector2(originalVerticeLs.x, originalVerticeLs.y);
            var smoothedVertXY = new Vector2(smoothedVectorLs.x, smoothedVectorLs.y);

            //As the inflatied vert moves further than the original sphere radius lerp its movement slower
            var radiusLerpScale = Vector2.Distance(sphereCenterXY, smoothedVertXY)/(bellyInfo.OriginalSphereRadius * 7);
            var lerpXY = Vector3.Lerp(smoothedVertXY, origVertXY, radiusLerpScale);

            //set limited XY, but keep the new z postion
            smoothedVectorLs = new Vector3(lerpXY.x, lerpXY.y, smoothedVectorLs.z);

            return meshRootTf.TransformPoint(smoothedVectorLs);
        }

        /// <summary>
        /// Dampen any mesh changed near edged of the belly (sides, top, and bottom) to prevent too much vertex stretching.false  The more forward the vertex is from Z the more it's allowd to be altered by sliders
        /// </summary>        
        internal Vector3 RoundToSides(Transform meshRootTf, Vector3 originalVertice, Vector3 smoothedVector, Vector3 sphereCenter, float inflatedToCenterDist) {        
            var zSmoothDist = inflatedToCenterDist/3f;//Just pick a float that looks good as a z limiter
            //Get local space vectors to eliminate rotation in world space
            var smoothedVectorLs = meshRootTf.InverseTransformPoint(smoothedVector);

            // To calculate vectors z difference, we need to do it from local space to eliminate any character rotation in world space
            var forwardFromCenter = smoothedVectorLs.z - meshRootTf.InverseTransformPoint(sphereCenter).z;            
            if (forwardFromCenter <= zSmoothDist) {                                
                var originalVerticeLs = meshRootTf.InverseTransformPoint(originalVertice);
                var lerpScale = Mathf.Abs(forwardFromCenter/zSmoothDist);//As vert.z approaches our z limit, allow it to move more
                //Back to world space
                smoothedVector = meshRootTf.TransformPoint(Vector3.Lerp(originalVerticeLs, smoothedVectorLs, lerpScale));
            }

            return smoothedVector;
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
        /// Compares current to last slider values, if they havent changed it returns false
        /// </summary>        
        internal bool NeedsMeshUpdate() 
        {
            bool hasChanges = false;
            //TODO change to loop over all objects in preg data, so when we add a new one we dont have to add it here
            if (infConfig.inflationSize != infConfigHistory.inflationSize) hasChanges = true;              
            if (infConfig.inflationMoveY != infConfigHistory.inflationMoveY) hasChanges = true;
            if (infConfig.inflationMoveZ != infConfigHistory.inflationMoveZ) hasChanges = true;
            if (infConfig.inflationStretchX != infConfigHistory.inflationStretchX) hasChanges = true;
            if (infConfig.inflationStretchY != infConfigHistory.inflationStretchY) hasChanges = true;
            if (infConfig.inflationShiftY != infConfigHistory.inflationShiftY) hasChanges = true;
            if (infConfig.inflationShiftZ != infConfigHistory.inflationShiftZ) hasChanges = true;
            if (infConfig.inflationTaperY != infConfigHistory.inflationTaperY) hasChanges = true;
            if (infConfig.inflationTaperZ != infConfigHistory.inflationTaperZ) hasChanges = true;
            if (infConfig.inflationMultiplier != infConfigHistory.inflationMultiplier) hasChanges = true;
            if (infConfig.inflationClothOffset != infConfigHistory.inflationClothOffset) hasChanges = true;

            return hasChanges;
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
        }

        
        /// <summary>   
        /// Creates a mesh dictionary key based on mesh name and vert count. (because mesh names can be the same, vertex count makes it almost always unique)
        /// </summary>    
        internal string GetMeshKey(SkinnedMeshRenderer smr) 
        {
            return smr.name + smr.sharedMesh.vertexCount.ToString();
        }


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
            //Only inflate if the value changed        
            if (infSize.Equals(null) || infSize == 0) return false;      

            //Create an instance of sharedMesh so we don't modify the mesh shared between characters
            Mesh meshCopy = (Mesh)UnityEngine.Object.Instantiate(smr.sharedMesh);    
            smr.sharedMesh = meshCopy;

            var sharedMesh = smr.sharedMesh;

            if (!sharedMesh.isReadable) {
                if (PregnancyPlusPlugin.debugLog)  PregnancyPlusPlugin.Logger.LogInfo(
                     $"ApplyInflation > smr '{renderKey}' is not readable, skipping");
                    return false;
            } 

            var origVert = originalVertices[renderKey];
            var currentVert = currentVertices[renderKey];
            var bellyVertIndex = bellyVerticieIndexes[renderKey];

            if (bellyVertIndex.Length == 0) return false;
            infConfigHistory.inflationSize = infSize;

            var currentVertLength = currentVert.Length;
            for (int i = 0; i < currentVertLength; i++)
            {
                //If not a belly index verticie then skip the morph
                if (!bellyVertIndex[i]) continue;

                //Set the lerp size of the belly based on the users slider value
                currentVert[i] = Vector3.Lerp(origVert[i], inflatedVertices[renderKey][i], (infSize/40));
            }

            if (currentVert.Length != sharedMesh.vertexCount) 
            {
                PregnancyPlusPlugin.Logger.LogInfo(
                            $"ApplyInflation > smr.sharedMesh '{renderKey}' has incorrect vert count {currentVert.Length}|{sharedMesh.vertexCount}");
                return false;
            }

            sharedMesh.vertices = currentVert;
            sharedMesh.RecalculateBounds();
            NormalSolver.RecalculateNormals(sharedMesh, 35f, bellyVerticieIndexes[renderKey]);
            //sharedMesh.RecalculateNormals();  old way that leaves skin seams
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
                if (smr == null) continue;                

                //Create an instance of sharedMesh so we don't modify the mesh shared between characters, that was a fun issue
                Mesh meshCopy = (Mesh)UnityEngine.Object.Instantiate(smr.sharedMesh);
                smr.sharedMesh = meshCopy;

                var sharedMesh = smr.sharedMesh;
                var hasValue = originalVertices.TryGetValue(renderKey, out Vector3[] origVerts); 

                //On change clothes original verts become useless, so skip this
                if (!hasValue) return;   

                if (!sharedMesh.isReadable) {
                    if (PregnancyPlusPlugin.debugLog)  PregnancyPlusPlugin.Logger.LogInfo(
                         $"ResetInflation > smr '{renderKey}' is not readable, skipping");
                        continue;
                } 

                if (!sharedMesh || origVerts.Equals(null) || origVerts.Length == 0) continue;
                if (origVerts.Length != sharedMesh.vertexCount) 
                {
                    PregnancyPlusPlugin.Logger.LogInfo(
                        $"ResetInflation > smr '{renderKey}' has incorrect vert count {origVerts.Length}|{sharedMesh.vertexCount}");
                    continue;
                }

                sharedMesh.vertices = origVerts;
                sharedMesh.RecalculateBounds();
                NormalSolver.RecalculateNormals(sharedMesh, 35f, bellyVerticieIndexes[renderKey]);
                //sharedMesh.RecalculateNormals(); old way that leaves skin seams
                sharedMesh.RecalculateTangents();
            }
        }
        


        //Allow user config values to be added in during story mode
        internal float GetInflationMultiplier() {
            if (StudioAPI.InsideStudio || MakerAPI.InsideMaker) return infConfig.inflationMultiplier;
            var globalOverrideVal = PregnancyPlusPlugin.StoryModeInflationMultiplier != null ? PregnancyPlusPlugin.StoryModeInflationMultiplier.Value : 0;
            return (infConfig.inflationMultiplier + globalOverrideVal);
        }
        internal float GetInflationMoveY() {
            if (StudioAPI.InsideStudio || MakerAPI.InsideMaker) return infConfig.inflationMoveY;
            var globalOverrideVal = PregnancyPlusPlugin.StoryModeInflationMoveY != null ? PregnancyPlusPlugin.StoryModeInflationMoveY.Value : 0;
            return (infConfig.inflationMoveY + globalOverrideVal);
        }

        internal float GetInflationMoveZ() {
            if (StudioAPI.InsideStudio || MakerAPI.InsideMaker) return infConfig.inflationMoveZ;
            var globalOverrideVal = PregnancyPlusPlugin.StoryModeInflationMoveZ != null ? PregnancyPlusPlugin.StoryModeInflationMoveZ.Value : 0;
            return (infConfig.inflationMoveZ + globalOverrideVal);
        }

        internal float GetInflationStretchX() {
            if (StudioAPI.InsideStudio || MakerAPI.InsideMaker) return infConfig.inflationStretchX;
            var globalOverrideVal = PregnancyPlusPlugin.StoryModeInflationStretchX != null ? PregnancyPlusPlugin.StoryModeInflationStretchX.Value : 0;
            return (infConfig.inflationStretchX + globalOverrideVal);
        }

        internal float GetInflationStretchY() {
            if (StudioAPI.InsideStudio || MakerAPI.InsideMaker) return infConfig.inflationStretchY;
            var globalOverrideVal = PregnancyPlusPlugin.StoryModeInflationStretchY != null ? PregnancyPlusPlugin.StoryModeInflationStretchY.Value : 0;
            return (infConfig.inflationStretchY + globalOverrideVal);
        }

        internal float GetInflationShiftY() {
            if (StudioAPI.InsideStudio || MakerAPI.InsideMaker) return infConfig.inflationShiftY;
            var globalOverrideVal = PregnancyPlusPlugin.StoryModeInflationShiftY != null ? PregnancyPlusPlugin.StoryModeInflationShiftY.Value : 0;
            return (infConfig.inflationShiftY + globalOverrideVal);
        }

        internal float GetInflationShiftZ() {
            if (StudioAPI.InsideStudio || MakerAPI.InsideMaker) return infConfig.inflationShiftZ;
            var globalOverrideVal = PregnancyPlusPlugin.StoryModeInflationShiftZ != null ? PregnancyPlusPlugin.StoryModeInflationShiftZ.Value : 0;
            return (infConfig.inflationShiftZ + globalOverrideVal);
        }

        internal float GetInflationTaperY() {
            if (StudioAPI.InsideStudio || MakerAPI.InsideMaker) return infConfig.inflationTaperY;
            var globalOverrideVal = PregnancyPlusPlugin.StoryModeInflationTaperY != null ? PregnancyPlusPlugin.StoryModeInflationTaperY.Value : 0;
            return (infConfig.inflationTaperY + globalOverrideVal);
        }

        internal float GetInflationTaperZ() {
            if (StudioAPI.InsideStudio || MakerAPI.InsideMaker) return infConfig.inflationTaperZ;
            var globalOverrideVal = PregnancyPlusPlugin.StoryModeInflationTaperZ != null ? PregnancyPlusPlugin.StoryModeInflationTaperZ.Value : 0;
            return (infConfig.inflationTaperZ + globalOverrideVal);
        }

        internal float GetInflationClothOffset() {
            if (StudioAPI.InsideStudio || MakerAPI.InsideMaker) return infConfig.inflationClothOffset;
            var globalOverrideVal = PregnancyPlusPlugin.StoryModeInflationClothOffset != null ? PregnancyPlusPlugin.StoryModeInflationClothOffset.Value : 0;
            return (infConfig.inflationClothOffset + globalOverrideVal);
        }

        
    }
}


