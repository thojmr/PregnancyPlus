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

    //This partial class contains all the transforms used to smooth the belly verticies
    public partial class PregnancyPlusCharaController: CharaCustomFunctionController
    {
        

        /// <summary>
        /// Calculate the user input move distance
        /// </summary>
        internal Vector3 GetUserMoveTransform(Transform fromPosition) 
        {
            return fromPosition.up * GetInflationMoveY() + fromPosition.forward * GetInflationMoveZ();
        }


        /// <summary>
        /// This will help pvent too much XY direction change, keeping the belly more round than disk like at large sizes
        /// </summary>
        internal Vector3 SculptBaseShape(Transform meshRootTf, Vector3 originalVerticeLs, Vector3 smoothedVectorLs, Vector3 sphereCenterLs) 
        {
            //We only want to limit expansion n XY plane for this lerp
            var sphereCenterXY = new Vector2(sphereCenterLs.x, sphereCenterLs.y);
            var origVertXY = new Vector2(originalVerticeLs.x, originalVerticeLs.y);
            var smoothedVertXY = new Vector2(smoothedVectorLs.x, smoothedVectorLs.y);

            //As the inflatied vert moves further than the original sphere radius lerp its movement slower
            var radiusLerpScale = Vector2.Distance(sphereCenterXY, smoothedVertXY)/(bellyInfo.OriginalSphereRadius * 10);
            var lerpXY = Vector3.Lerp(smoothedVertXY, origVertXY, radiusLerpScale);

            //set limited XY, but keep the new z postion
            smoothedVectorLs = new Vector3(lerpXY.x, lerpXY.y, smoothedVectorLs.z);

            return smoothedVectorLs;
        }


        /// <summary>   
        /// This will shift the sphereCenter position *After sphereifying* on Y or Z axis (This stretches the mesh, where pre sphereifying, it would move the sphere within the mesh like Move Y)
        /// </summary>
        internal Vector3 GetUserShiftTransform(Transform meshRootTf, Vector3 smoothedVectorLs, Vector3 sphereCenterLs, float sphereRadius) 
        {                     
            //IF the user has selected a y value, lerp the top and bottom slower and lerp any verts closer to z = 0 slower
            if (GetInflationShiftY() != 0) 
            {
                var distFromYCenterLs = smoothedVectorLs.y - sphereCenterLs.y;
                //Lerp up and down positon more when the belly is near the center Y, and less for top and bottom
                var lerpY = Mathf.Lerp(GetInflationShiftY(), GetInflationShiftY()/4, Math.Abs(distFromYCenterLs/(sphereRadius*1.8f)));
                var yLerpedsmoothedVector = smoothedVectorLs + Vector3.up * lerpY;//Since its all local space here, we dont have to use meshRootTf.up

                //Finally lerp sides slightly slower than center
                var distanceSide = Math.Abs(smoothedVectorLs.x - sphereCenterLs.x); 
                var finalLerpPos = Vector3.Slerp(yLerpedsmoothedVector, smoothedVectorLs, Math.Abs(distanceSide/(sphereRadius*3) + 0.1f));

                //return the shift up/down 
                smoothedVectorLs = finalLerpPos;
            }

            //If the user has selected a z value
            if (GetInflationShiftZ() != 0) 
            {
                //Move the verts closest to sphere center Z more slowly than verts at the belly button.  Otherwise you stretch the ones near the body too much
                var lerpZ = Mathf.Lerp(0, GetInflationShiftZ(), (smoothedVectorLs.z - sphereCenterLs.z)/(sphereRadius *2));
                var finalLerpPos = smoothedVectorLs + Vector3.forward * lerpZ;

                smoothedVectorLs = finalLerpPos;
            }

            return smoothedVectorLs;
        }


        /// <summary>   
        /// This will stretch the belly mesh wider
        /// </summary>        
        internal Vector3 GetUserStretchXTransform(Transform meshRootTf, Vector3 smoothedVectorLs, Vector3 sphereCenterLs) 
        {
            //local Distance left or right from sphere center
            var distFromXCenterLs = smoothedVectorLs.x - sphereCenterLs.x;                

            var changeInDist = distFromXCenterLs * (GetInflationStretchX() + 1);  
            //Get new local space X position
            smoothedVectorLs.x = (sphereCenterLs + Vector3.right * changeInDist).x;

            return smoothedVectorLs;  
        }


        internal Vector3 GetUserStretchYTransform(Transform meshRootTf, Vector3 smoothedVectorLs, Vector3 sphereCenterLs) 
        {
            //Allow user adjustment of the height of the belly
            //local Distance up or down from sphere center
            var distFromYCenterLs = smoothedVectorLs.y - sphereCenterLs.y; 
            
            //have to change growth direction above and below center line
            var changeInDist = distFromYCenterLs * (GetInflationStretchY() + 1);  
            //Get new local space X position
            smoothedVectorLs.y = (sphereCenterLs + Vector3.up * changeInDist).y;
            
            return smoothedVectorLs; 
        }


        /// <summary>   
        /// This sill taper the belly shape based on user input slider. shrinking the top width, and expanding the bottom width along the YX adis
        /// </summary>
        internal Vector3 GetUserTaperYTransform(Transform meshRootTf, Vector3 smoothedVectorLs, Vector3 sphereCenterLs, float sphereRadius) 
        {
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

            return smoothedVectorLs;
        }


        /// <summary>   
        /// This sill taper the belly shape based on user input slider. pulling out the bottom and pushing in the top along the XZ axis
        /// </summary>
        internal Vector3 GetUserTaperZTransform(Transform meshRootTf, Vector3 smoothedVectorLs, Vector3 sphereCenterLs, float sphereRadius) 
        {
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

            return smoothedVectorLs;
        }


        /// <summary>   
        /// This will add a fat fold across the middle of the belly
        /// </summary>        
        internal Vector3 GetUserFatFoldTransform(Transform meshRootTf, Vector3 originalVerticeLs, Vector3 smoothedVectorLs, Vector3 sphereCenterLs, float sphereRadius) {
            var origSmoothVectorLs = smoothedVectorLs;
            var inflationFatFold = GetInflationFatFold();

            //Define how hight and low from center we want to pull the skin inward
            var distFromCenter = sphereRadius;                
            var svDistFromCenter = Math.Abs(smoothedVectorLs.y - sphereCenterLs.y);

            var resultVert = smoothedVectorLs;
            //Make V shape in the middle of the belly horizontally
            if (svDistFromCenter <= distFromCenter) {        
                //The closer to Y = 0 the more inwards the pull            
                smoothedVectorLs = Vector3.Slerp(originalVerticeLs, smoothedVectorLs, svDistFromCenter/distFromCenter + (inflationFatFold -1));
            }

            //Shrink skin above center line.  Want it bigger down below the line to look more realistic
            if (smoothedVectorLs.y > sphereCenterLs.y) {                    
                //As the verts get higher, move them back towards their original position
                smoothedVectorLs = Vector3.Slerp(smoothedVectorLs, originalVerticeLs, (smoothedVectorLs.y - sphereCenterLs.y)/(sphereRadius*1.5f));
            }

            return smoothedVectorLs;
        }


        /// <summary>
        /// Dampen any mesh changed near edged of the belly (sides, top, and bottom) to prevent too much vertex stretching.  The more forward the vertex is from Z the more it's allowd to be altered by sliders
        /// </summary>        
        internal Vector3 RoundToSides(Transform meshRootTf, Vector3 originalVerticeLs, Vector3 smoothedVectorLs, float pmShpereRadius, Vector3 backExtentPosLs, Vector3 pmSphereCenterLs) 
        {        
            //The distance forward that we will lerp to a curve
            var zForwardSmoothDist = pmShpereRadius/2;

            // Get the disnce the original vector is forward from characters back (use originial and not inflated to exclude multiplier interference)
            var forwardFromBack = (originalVerticeLs.z - backExtentPosLs.z * bellyInfo.TotalScale.z);
            //As the vert.z approaches the front lerp it less
            var lerpScale = forwardFromBack/zForwardSmoothDist;

            // if (PregnancyPlusPlugin.debugLog && smoothedVectorLs.z < -0.2)  
            // {
            //     PregnancyPlusPlugin.Logger.LogInfo($" ");
            //     PregnancyPlusPlugin.Logger.LogInfo($" smoothedVectorLs {smoothedVectorLs} pmSphereCenterLs {pmSphereCenterLs} backExtentPosLs {backExtentPosLs}");
            //     PregnancyPlusPlugin.Logger.LogInfo($" zForwardSmoothDist {zForwardSmoothDist} forwardFromBack {forwardFromBack} lerpScale {lerpScale}");
            // }

            smoothedVectorLs = Vector3.Lerp(originalVerticeLs, smoothedVectorLs, lerpScale);
            
            return smoothedVectorLs;
        }
        






        //Allow user plugin config values to be added in during story mode
        internal float GetInflationMultiplier() 
        {
            if (StudioAPI.InsideStudio || MakerAPI.InsideMaker) return infConfig.inflationMultiplier;
            var globalOverrideVal = PregnancyPlusPlugin.StoryModeInflationMultiplier != null ? PregnancyPlusPlugin.StoryModeInflationMultiplier.Value : 0;
            return (infConfig.inflationMultiplier + globalOverrideVal);
        }
        internal float GetInflationMoveY() 
        {
            if (StudioAPI.InsideStudio || MakerAPI.InsideMaker) return infConfig.inflationMoveY;
            var globalOverrideVal = PregnancyPlusPlugin.StoryModeInflationMoveY != null ? PregnancyPlusPlugin.StoryModeInflationMoveY.Value : 0;
            return (infConfig.inflationMoveY + globalOverrideVal);
        }

        internal float GetInflationMoveZ() 
        {
            if (StudioAPI.InsideStudio || MakerAPI.InsideMaker) return infConfig.inflationMoveZ;
            var globalOverrideVal = PregnancyPlusPlugin.StoryModeInflationMoveZ != null ? PregnancyPlusPlugin.StoryModeInflationMoveZ.Value : 0;
            return (infConfig.inflationMoveZ + globalOverrideVal);
        }

        internal float GetInflationStretchX() 
        {
            if (StudioAPI.InsideStudio || MakerAPI.InsideMaker) return infConfig.inflationStretchX;
            var globalOverrideVal = PregnancyPlusPlugin.StoryModeInflationStretchX != null ? PregnancyPlusPlugin.StoryModeInflationStretchX.Value : 0;
            return (infConfig.inflationStretchX + globalOverrideVal);
        }

        internal float GetInflationStretchY() 
        {
            if (StudioAPI.InsideStudio || MakerAPI.InsideMaker) return infConfig.inflationStretchY;
            var globalOverrideVal = PregnancyPlusPlugin.StoryModeInflationStretchY != null ? PregnancyPlusPlugin.StoryModeInflationStretchY.Value : 0;
            return (infConfig.inflationStretchY + globalOverrideVal);
        }

        internal float GetInflationShiftY() 
        {
            if (StudioAPI.InsideStudio || MakerAPI.InsideMaker) return infConfig.inflationShiftY;
            var globalOverrideVal = PregnancyPlusPlugin.StoryModeInflationShiftY != null ? PregnancyPlusPlugin.StoryModeInflationShiftY.Value : 0;
            return (infConfig.inflationShiftY + globalOverrideVal);
        }

        internal float GetInflationShiftZ() 
        {
            if (StudioAPI.InsideStudio || MakerAPI.InsideMaker) return infConfig.inflationShiftZ;
            var globalOverrideVal = PregnancyPlusPlugin.StoryModeInflationShiftZ != null ? PregnancyPlusPlugin.StoryModeInflationShiftZ.Value : 0;
            return (infConfig.inflationShiftZ + globalOverrideVal);
        }

        internal float GetInflationTaperY() 
        {
            if (StudioAPI.InsideStudio || MakerAPI.InsideMaker) return infConfig.inflationTaperY;
            var globalOverrideVal = PregnancyPlusPlugin.StoryModeInflationTaperY != null ? PregnancyPlusPlugin.StoryModeInflationTaperY.Value : 0;
            return (infConfig.inflationTaperY + globalOverrideVal);
        }

        internal float GetInflationTaperZ() 
        {
            if (StudioAPI.InsideStudio || MakerAPI.InsideMaker) return infConfig.inflationTaperZ;
            var globalOverrideVal = PregnancyPlusPlugin.StoryModeInflationTaperZ != null ? PregnancyPlusPlugin.StoryModeInflationTaperZ.Value : 0;
            return (infConfig.inflationTaperZ + globalOverrideVal);
        }

        internal float GetInflationClothOffset() 
        {
            if (StudioAPI.InsideStudio || MakerAPI.InsideMaker) return infConfig.inflationClothOffset;
            var globalOverrideVal = PregnancyPlusPlugin.StoryModeInflationClothOffset != null ? PregnancyPlusPlugin.StoryModeInflationClothOffset.Value : 0;
            return (infConfig.inflationClothOffset + globalOverrideVal);
        }

        internal float GetInflationFatFold() 
        {
            if (StudioAPI.InsideStudio || MakerAPI.InsideMaker) return infConfig.inflationFatFold;
            var globalOverrideVal = PregnancyPlusPlugin.StoryModeInflationFatFold != null ? PregnancyPlusPlugin.StoryModeInflationFatFold.Value : 0;
            return (infConfig.inflationFatFold + globalOverrideVal);
        }

        
    }
}


