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
        internal Vector3 SculptBaseShape(Transform meshRootTf, Vector3 originalVerticeLs, Vector3 smoothedVector, Vector3 sphereCenter) 
        {
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
        /// This will shift the sphereCenter position *After sphereifying* on Y or Z axis (This stretches the mesh, where pre sphereifying, it would move the sphere within the mesh like Move Y)
        /// </summary>
        internal Vector3 GetUserShiftTransform(Transform meshRootTf, Vector3 smoothedVector, Vector3 sphereCenterPos, float sphereRadius) 
        {            
            //Get local space equivalents            
            var sphereCenterLs = meshRootTf.InverseTransformPoint(sphereCenterPos);

            //IF the user has selected a y value, lerp the top and bottom slower and lerp any verts closer to z = 0 slower
            if (GetInflationShiftY() != 0) 
            {
                var smoothedVectorLs = meshRootTf.InverseTransformPoint(smoothedVector);
                var distFromYCenterLs = smoothedVectorLs.y - sphereCenterLs.y;
                //Lerp up and down positon more when the belly is near the center Y, and less for top and bottom
                var lerpY = Mathf.Lerp(GetInflationShiftY(), GetInflationShiftY()/4, Math.Abs(distFromYCenterLs/(sphereRadius*1.1f)));
                var yLerpedsmoothedVector = smoothedVectorLs + Vector3.up * lerpY;//Since its all local space here, we dont have to use meshRootTf.up

                //Finally lerp sides slightly slower than center
                var distanceSide = Math.Abs(smoothedVectorLs.x - sphereCenterLs.x); 
                var finalLerpPos = Vector3.Slerp(yLerpedsmoothedVector, smoothedVectorLs, Math.Abs(distanceSide/(sphereRadius*3) + 0.1f));

                //return the shift up/down 
                smoothedVector = meshRootTf.TransformPoint(finalLerpPos);
            }

            //If the user has selected a z value
            if (GetInflationShiftZ() != 0) 
            {
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
        /// This will add a fat fold across the middle of the belly
        /// </summary>        
        internal Vector3 GetUserFatFoldTransform(Transform meshRootTf, Vector3 originalVerticeLs, Vector3 smoothedVector, Vector3 sphereCenterPos, float sphereRadius) {
            var sphereCenterLs = meshRootTf.InverseTransformPoint(sphereCenterPos);
            var smoothVectorLs = meshRootTf.InverseTransformPoint(smoothedVector);
            var origSmoothVectorLs = smoothVectorLs;
            var inflationFatFold = GetInflationFatFold();

            //Define how hight and low from center we want to pull the skin inward
            var distFromCenter = sphereRadius;                
            var svDistFromCenter = Math.Abs(smoothVectorLs.y - sphereCenterLs.y);

            var resultVert = smoothVectorLs;
            //Make V shape in the middle of the belly horizontally
            if (svDistFromCenter <= distFromCenter) {        
                //The closer to Y = 0 the more inwards the pull            
                smoothVectorLs = Vector3.Slerp(originalVerticeLs, smoothVectorLs, svDistFromCenter/distFromCenter + (inflationFatFold -1));
            }

            //Shrink skin above center line.  Want it bigger down below the line to look more realistic
            if (smoothVectorLs.y > sphereCenterLs.y) {                    
                //As the verts get higher, move them back towards their original position
                smoothVectorLs = Vector3.Slerp(smoothVectorLs, originalVerticeLs, (smoothVectorLs.y - sphereCenterLs.y)/(sphereRadius*1.5f));
            }

            return meshRootTf.TransformPoint(smoothVectorLs);
        }


        /// <summary>
        /// Dampen any mesh changed near edged of the belly (sides, top, and bottom) to prevent too much vertex stretching.false  The more forward the vertex is from Z the more it's allowd to be altered by sliders
        /// </summary>        
        internal Vector3 RoundToSides(Transform meshRootTf, Vector3 originalVerticeLs, Vector3 smoothedVector, Vector3 sphereCenter, float inflatedToCenterDist) 
        {        
            var zSmoothDist = inflatedToCenterDist/3f;//Just pick a float that looks good as a z limiter
            //Get local space vectors to eliminate rotation in world space
            var smoothedVectorLs = meshRootTf.InverseTransformPoint(smoothedVector);

            // To calculate vectors z difference, we need to do it from local space to eliminate any character rotation in world space
            var forwardFromCenter = smoothedVectorLs.z - meshRootTf.InverseTransformPoint(sphereCenter).z;            
            if (forwardFromCenter <= zSmoothDist) {                                
                var lerpScale = Mathf.Abs(forwardFromCenter/zSmoothDist);//As vert.z approaches our z limit, allow it to move more
                //Back to world space
                smoothedVector = meshRootTf.TransformPoint(Vector3.Lerp(originalVerticeLs, smoothedVectorLs, lerpScale));
            }

            return smoothedVector;
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


