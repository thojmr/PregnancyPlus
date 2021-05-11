using KKAPI;
using KKAPI.Chara;
using UnityEngine;
using System;
using KKAPI.Studio;
using KKAPI.Maker;

namespace KK_PregnancyPlus
{

    //This partial class contains all the lerp transforms used to smooth the belly verticies
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
            var radiusLerpScale = Vector2.Distance(sphereCenterXY, smoothedVertXY)/((bellyInfo.ScaledOrigRadius(BellyDir.y) + bellyInfo.ScaledOrigRadius(BellyDir.x))/2 * 10);
            var lerpXY = Vector3.Lerp(smoothedVertXY, origVertXY, radiusLerpScale);

            //set limited XY, but keep the new z postion
            smoothedVectorLs = new Vector3(lerpXY.x, lerpXY.y, smoothedVectorLs.z);

            return smoothedVectorLs;
        }


        /// <summary>   
        /// This will shift the sphereCenter position *After sphereifying* on Y or Z axis (This stretches the mesh, where pre sphereifying, it would move the sphere within the mesh like Move Y)
        /// </summary>
        internal Vector3 GetUserShiftTransform(Transform meshRootTf, Vector3 smoothedVectorLs, Vector3 sphereCenterLs, float skinToCenterDist) 
        {                     
            //IF the user has selected a y value, lerp the top and bottom slower and lerp any verts closer to z = 0 slower
            if (GetInflationShiftY() != 0) 
            {
                var distFromYCenterLs = smoothedVectorLs.y - sphereCenterLs.y;
                //Lerp up and down positon more when the belly is near the center Y, and less for top and bottom
                var lerpY = Mathf.Lerp(GetInflationShiftY(), GetInflationShiftY()/4, Math.Abs(distFromYCenterLs/((skinToCenterDist/bellyInfo.TotalCharScale.y)*1.8f)));
                var yLerpedsmoothedVector = smoothedVectorLs + Vector3.up * lerpY;//Since its all local space here, we dont have to use meshRootTf.up

                //Finally lerp sides slightly slower than center
                var distanceSide = Math.Abs(smoothedVectorLs.x - sphereCenterLs.x); 
                var finalLerpPos = Vector3.Slerp(yLerpedsmoothedVector, smoothedVectorLs, Math.Abs(distanceSide/((skinToCenterDist/bellyInfo.TotalCharScale.x)*3) + 0.1f));

                //return the shift up/down 
                smoothedVectorLs = finalLerpPos;
            }

            //If the user has selected a z value
            if (GetInflationShiftZ() != 0) 
            {
                //Move the verts closest to sphere center Z more slowly than verts at the belly button.  Otherwise you stretch the ones near the body too much
                var lerpZ = Mathf.Lerp(0, GetInflationShiftZ(), (smoothedVectorLs.z - sphereCenterLs.z)/((skinToCenterDist/bellyInfo.TotalCharScale.z) *2));
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
        internal Vector3 GetUserTaperYTransform(Transform meshRootTf, Vector3 smoothedVectorLs, Vector3 sphereCenterLs, float skinToCenterDist) 
        {
            //local Distance up or down from sphere center
            var distFromYCenterLs = smoothedVectorLs.y - sphereCenterLs.y; 
            var distFromXCenterLs = smoothedVectorLs.x - sphereCenterLs.x; 
            //Left side tilts one way, right side the opposite
            var isTop = distFromYCenterLs > 0; 
            var isRight = distFromXCenterLs > 0; 

            //Increase taper amount for vecters further above or below center.  No shift along center
            var taperY = Mathf.Lerp(0, GetInflationTaperY(), Math.Abs(distFromYCenterLs)/(skinToCenterDist/bellyInfo.TotalCharScale.y));
            //Second lerp to limit how much it shifts l/r when near x=0 line, no shift along center
            taperY = Mathf.Lerp(0, taperY, Math.Abs(distFromXCenterLs)/(skinToCenterDist/bellyInfo.TotalCharScale.x));
            //Reverse the direction based on which side the vert is on
            taperY = (isRight ? taperY : -taperY);
            taperY = (isTop ? taperY : -taperY);

            smoothedVectorLs.x = (smoothedVectorLs + Vector3.right * taperY).x;

            return smoothedVectorLs;
        }


        /// <summary>   
        /// This sill taper the belly shape based on user input slider. pulling out the bottom and pushing in the top along the XZ axis
        /// </summary>
        internal Vector3 GetUserTaperZTransform(Transform meshRootTf, Vector3 originalVerticeLs, Vector3 smoothedVectorLs, Vector3 sphereCenterLs, 
                                                float skinToCenterDist, Vector3 backExtentPosLs) 
        {
            //local Distance up or down from sphere center
            var distFromYCenterLs = smoothedVectorLs.y - sphereCenterLs.y; 
            var distFromZCenterLs = smoothedVectorLs.z - sphereCenterLs.z; 
            //top of belly shifts one way, bottom shifts the opposite
            var isTop = distFromYCenterLs > 0; 

            //Increase taper amount for vecters further above or below center.  No shifting at center
            var taperZ = Mathf.Lerp(0, GetInflationTaperZ(), Math.Abs(distFromYCenterLs)/(skinToCenterDist/bellyInfo.TotalCharScale.y));
            //Reverse the direction based on which side the vert is on
            taperZ = (isTop ? taperZ : -taperZ);
            var taperedZVert = smoothedVectorLs + Vector3.forward * taperZ;

            //decrease movement speed near the back
            var forwardFromBack = (originalVerticeLs.z - backExtentPosLs.z);
            //Leave a little dead zone at the back where the verts don't change.  
            var backLerp = Vector3.Lerp(smoothedVectorLs, taperedZVert, forwardFromBack/(skinToCenterDist/bellyInfo.TotalCharScale.z) - ((skinToCenterDist/bellyInfo.TotalCharScale.z)/2));
         
            //Move verts closest to z=0 more slowly than those out front to reduce skin stretching
            smoothedVectorLs = Vector3.Lerp(backLerp, taperedZVert, distFromZCenterLs/(skinToCenterDist/bellyInfo.TotalCharScale.z));

            return smoothedVectorLs;
        }


        /// <summary>   
        /// This will add a fat fold across the middle of the belly
        /// </summary>        
        internal Vector3 GetUserFatFoldTransform(Transform meshRootTf, Vector3 originalVerticeLs, Vector3 smoothedVectorLs, Vector3 sphereCenterLs, float sphereRadius) 
        {
            var origSmoothVectorLs = smoothedVectorLs;
            var inflationFatFold = GetInflationFatFold();
            var scaledSphereRadius = bellyInfo.ScaledRadius(BellyDir.y);

            //Define how hight and low from center we want to pull the skin inward
            var svDistFromCenter = Math.Abs(smoothedVectorLs.y - sphereCenterLs.y);

            var resultVert = smoothedVectorLs;
            //Make V shape in the middle of the belly horizontally
            if (svDistFromCenter <= scaledSphereRadius) 
            {        
                //The closer to Y = 0 the more inwards the pull            
                smoothedVectorLs = Vector3.Slerp(originalVerticeLs, smoothedVectorLs, svDistFromCenter/scaledSphereRadius + (inflationFatFold -1));
            }

            //Shrink skin above center line.  Want it bigger down below the line to look more realistic
            if (smoothedVectorLs.y > sphereCenterLs.y) 
            {                    
                //As the verts get higher, move them back towards their original position
                smoothedVectorLs = Vector3.Slerp(smoothedVectorLs, originalVerticeLs, svDistFromCenter/(scaledSphereRadius * 1.5f));
            }

            return smoothedVectorLs;
        }



        /// <summary>   
        /// This will make the front of the belly more, or less round
        /// </summary>  
        internal Vector3 GetUserRoundnessTransform(Transform meshRootTf, Vector3 originalVerticeLs, Vector3 smoothedVectorLs, Vector3 sphereCenterLs, float skinToCenterDist)
        {
            var zDistFromCenter = smoothedVectorLs.z - sphereCenterLs.z;

            //As the distance forward gets further from sphere center make the shape more round (shifted outward slightly from center)
            var xyLerp = Mathf.Lerp(0, GetInflationRoundness(), (zDistFromCenter - (bellyInfo.WaistThick/4f))/bellyInfo.ScaledRadius(BellyDir.z));

            //As the original vert gets closer to the sphere radius, apply less change since we want smooth transitions at belly's edge
            var totalLerp = Mathf.Lerp(xyLerp, 0, BellyEdgeAC.Evaluate(skinToCenterDist/bellyInfo.ScaledRadius(BellyDir.z)));

            //Get the direction to move the vert (offset center a little forward from sphere center)
            var xyDirection = (smoothedVectorLs - (sphereCenterLs + Vector3.forward * (bellyInfo.ScaledRadius(BellyDir.z)/3))).normalized;

            //set the new vert position in that direction + the new lerp scale distance
            return smoothedVectorLs + xyDirection * totalLerp;
        }


        /// <summary>
        /// Dampen any mesh changed near edged of the belly (sides, top, and bottom) to prevent too much vertex stretching.  The more forward the vertex is from Z the more it's allowd to be altered by sliders
        /// </summary>        
        internal Vector3 RoundToSides(Transform meshRootTf, Vector3 originalVerticeLs, Vector3 smoothedVectorLs, Vector3 backExtentPosLs) 
        {        
            var origRad = bellyInfo.ScaledOrigRadius(BellyDir.z)/1.8f;
            var multipliedRad = bellyInfo.ScaledRadius(BellyDir.z)/2.5f;

            //The distance forward that we will lerp to a curve (as multiplier grows, apply less of it)
            var zForwardSmoothDist = multipliedRad > origRad ? (origRad + (multipliedRad - origRad)/3f) : origRad;

            // Get the disnce the original vector is forward from characters back (use originial and not inflated to exclude multiplier interference)
            var forwardFromBack = (originalVerticeLs.z - backExtentPosLs.z);
            
            //As the vert.z approaches the front lerp it less
            return Vector3.Lerp(originalVerticeLs, smoothedVectorLs, BellySidesAC.Evaluate(forwardFromBack/zForwardSmoothDist));        
        }
        

        /// <summary>
        /// Reduce the stretching of the skin at the top of the belly where it connects to the ribs at large Multiplier values
        /// </summary>
        internal Vector3 ReduceRibStretchingZ(Transform meshRootTf, Vector3 originalVerticeLs, Vector3 smoothedVectorLs, Vector3 topExtentPosLs)
        {         
            //The distance from topExtent that we want to start lerping movement more slowly
            var topExtentOffset = topExtentPosLs.y/10;

            //When above the breast bone, dont allow changes
            if (originalVerticeLs.y > topExtentPosLs.y)
            {                
                return originalVerticeLs;
            } 

            //When verts are near the ribs (top of belly area)
            if (originalVerticeLs.y >= topExtentPosLs.y -topExtentOffset)
            {                
                var distanceFromTopExtent = topExtentPosLs.y - originalVerticeLs.y;
                var animCurveVal = BellyTopAC.Evaluate(distanceFromTopExtent/topExtentOffset);

                //Reduce the amount they are allowed to stretch forward, the higher the verts are
                var newVector = Vector3.Lerp(originalVerticeLs, smoothedVectorLs, animCurveVal);  
                smoothedVectorLs.z = newVector.z;
            }           

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


        internal float GetInflationRoundness() 
        {
            if (StudioAPI.InsideStudio || MakerAPI.InsideMaker) return infConfig.inflationRoundness;
            var globalOverrideVal = PregnancyPlusPlugin.StoryModeInflationRoundness != null ? PregnancyPlusPlugin.StoryModeInflationRoundness.Value : 0;
            return (infConfig.inflationRoundness + globalOverrideVal);
        }

        
    }
}


