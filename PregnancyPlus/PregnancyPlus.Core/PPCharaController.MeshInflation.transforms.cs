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
        internal Vector3 GetUserMoveTransform() 
        {
            return Vector3.up * GetInflationMoveY() + Vector3.forward * GetInflationMoveZ();
        }


        /// <summary>
        /// This will help pvent too much XY direction change, keeping the belly more round than disk like at large sizes
        /// </summary>
        internal Vector3 SculptBaseShape(Vector3 originalVerticeLs, Vector3 smoothedVectorLs, Vector3 sphereCenterLs) 
        {
            //We only want to limit expansion n XY plane for this lerp
            var sphereCenterXY = new Vector2(sphereCenterLs.x, sphereCenterLs.y);
            var origVertXY = new Vector2(originalVerticeLs.x, originalVerticeLs.y);
            var smoothedVertXY = new Vector2(smoothedVectorLs.x, smoothedVectorLs.y);

            //As the inflatied vert moves further than the original sphere radius lerp its movement slower
            var radiusLerpScale = Vector2.Distance(sphereCenterXY, smoothedVertXY)/((bellyInfo.ScaledOrigRadius(BellyDir.y) + bellyInfo.ScaledOrigRadius(BellyDir.x)) * 5);
            var lerpXY = Vector3.Lerp(smoothedVertXY, origVertXY, radiusLerpScale);

            //set limited XY, but keep the new z postion
            return new Vector3(lerpXY.x, lerpXY.y, smoothedVectorLs.z);
        }


        /// <summary>   
        /// This will shift the sphereCenter position *After sphereifying* on Y or Z axis (This stretches the mesh, where pre sphereifying, it would move the sphere within the mesh like Move Y)
        /// </summary>
        internal Vector3 GetUserShiftTransform(PregnancyPlusData infConfigClone, Vector3 smoothedVectorLs, Vector3 sphereCenterLs, float skinToCenterDist) 
        {                     
            //IF the user has selected a y value, lerp the top and bottom slower and lerp any verts closer to z = 0 slower
            if (GetInflationShiftY(infConfigClone) != 0) 
            {
                var distFromYCenterLs = smoothedVectorLs.y - sphereCenterLs.y;
                //Lerp up and down positon more when the belly is near the center Y, and less for top and bottom
                var lerpY = Mathf.Lerp(GetInflationShiftY(infConfigClone), GetInflationShiftY(infConfigClone)/4, Math.Abs(distFromYCenterLs/((skinToCenterDist/bellyInfo.TotalCharScale.y)*1.8f)));
                var yLerpedsmoothedVector = smoothedVectorLs + Vector3.up * lerpY;//Since its all local space here, we dont have to use meshRootTf.up

                //Finally lerp sides slightly slower than center
                var distanceSide = Math.Abs(smoothedVectorLs.x - sphereCenterLs.x); 
                var finalLerpPos = Vector3.Slerp(yLerpedsmoothedVector, smoothedVectorLs, Math.Abs(distanceSide/((skinToCenterDist/bellyInfo.TotalCharScale.x)*3) + 0.1f));

                //return the shift up/down 
                smoothedVectorLs = finalLerpPos;
            }

            //If the user has selected a z value
            if (GetInflationShiftZ(infConfigClone) != 0) 
            {
                //Move the verts closest to sphere center Z more slowly than verts at the belly button.  Otherwise you stretch the ones near the body too much
                var lerpZ = Mathf.Lerp(0, GetInflationShiftZ(infConfigClone), (smoothedVectorLs.z - sphereCenterLs.z)/((skinToCenterDist/bellyInfo.TotalCharScale.z) *2));
                var finalLerpPos = smoothedVectorLs + Vector3.forward * lerpZ;

                smoothedVectorLs = finalLerpPos;
            }

            return smoothedVectorLs;
        }


        /// <summary>   
        /// This will stretch the belly mesh wider
        /// </summary>        
        internal Vector3 GetUserStretchXTransform(PregnancyPlusData infConfigClone, Vector3 smoothedVectorLs, Vector3 sphereCenterLs) 
        {
            //local Distance left or right from sphere center
            var distFromXCenterLs = smoothedVectorLs.x - sphereCenterLs.x;                

            var changeInDist = distFromXCenterLs * (GetInflationStretchX(infConfigClone) + 1);  
            //Get new local space X position
            smoothedVectorLs.x = (sphereCenterLs + Vector3.right * changeInDist).x;

            return smoothedVectorLs;  
        }


        internal Vector3 GetUserStretchYTransform(PregnancyPlusData infConfigClone, Vector3 smoothedVectorLs, Vector3 sphereCenterLs) 
        {
            //Allow user adjustment of the height of the belly
            //local Distance up or down from sphere center
            var distFromYCenterLs = smoothedVectorLs.y - sphereCenterLs.y; 
            
            //have to change growth direction above and below center line
            var changeInDist = distFromYCenterLs * (GetInflationStretchY(infConfigClone) + 1);  
            //Get new local space X position
            smoothedVectorLs.y = (sphereCenterLs + Vector3.up * changeInDist).y;
            
            return smoothedVectorLs; 
        }


        /// <summary>   
        /// This sill taper the belly shape based on user input slider. shrinking the top width, and expanding the bottom width along the YX adis
        /// </summary>
        internal Vector3 GetUserTaperYTransform(PregnancyPlusData infConfigClone, Vector3 smoothedVectorLs, Vector3 sphereCenterLs, float skinToCenterDist) 
        {
            //local Distance up or down from sphere center
            var distFromYCenterLs = smoothedVectorLs.y - sphereCenterLs.y; 
            var distFromXCenterLs = smoothedVectorLs.x - sphereCenterLs.x; 
            //Left side tilts one way, right side the opposite
            var isTop = distFromYCenterLs > 0; 
            var isRight = distFromXCenterLs > 0; 

            //Increase taper amount for vecters further above or below center.  No shift along center
            var taperY = Mathf.Lerp(0, GetInflationTaperY(infConfigClone), Math.Abs(distFromYCenterLs)/(skinToCenterDist/bellyInfo.TotalCharScale.y));
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
        internal Vector3 GetUserTaperZTransform(PregnancyPlusData infConfigClone, Vector3 originalVerticeLs, Vector3 smoothedVectorLs, Vector3 sphereCenterLs, 
                                                float skinToCenterDist, Vector3 backExtentPosLs) 
        {
            //local Distance up or down from sphere center
            var distFromYCenterLs = smoothedVectorLs.y - sphereCenterLs.y; 
            var distFromZCenterLs = smoothedVectorLs.z - sphereCenterLs.z; 
            //top of belly shifts one way, bottom shifts the opposite
            var isTop = distFromYCenterLs > 0; 

            //Increase taper amount for vecters further above or below center.  No shifting at center
            var taperZ = Mathf.Lerp(0, GetInflationTaperZ(infConfigClone), Math.Abs(distFromYCenterLs)/(skinToCenterDist/bellyInfo.TotalCharScale.y));
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
        internal Vector3 GetUserFatFoldTransform(PregnancyPlusData infConfigClone, Vector3 originalVerticeLs, Vector3 smoothedVectorLs, Vector3 sphereCenterLs, float sphereRadius) 
        {
            var origSmoothVectorLs = smoothedVectorLs;
            var inflationFatFold = GetInflationFatFold(infConfigClone);            
            var scaledSphereRadius = bellyInfo.ScaledRadius(BellyDir.y);
            var inflationFatFoldHeightOffset = GetInflationFatFoldHeight(infConfigClone) * scaledSphereRadius;

            //Define how hight and low from center we want to pull the skin inward
            var svDistFromCenter = Math.Abs(smoothedVectorLs.y - (sphereCenterLs.y + inflationFatFoldHeightOffset));

            var resultVert = smoothedVectorLs;
            //Make V shape in the middle of the belly horizontally
            if (svDistFromCenter <= scaledSphereRadius) 
            {        
                //The closer to Y = 0 the more inwards the pull            
                smoothedVectorLs = Vector3.Slerp(originalVerticeLs, smoothedVectorLs, svDistFromCenter/scaledSphereRadius + (inflationFatFold -1));
            }

            //Shrink skin above center line.  Want it bigger down below the line to look more realistic
            if (smoothedVectorLs.y > (sphereCenterLs.y + inflationFatFoldHeightOffset)) 
            {                    
                //As the verts get higher, move them back towards their original position
                smoothedVectorLs = Vector3.Slerp(smoothedVectorLs, originalVerticeLs, svDistFromCenter/(scaledSphereRadius * 1.5f));
            }

            return smoothedVectorLs;
        }



        /// <summary>   
        /// This will make the front of the belly more, or less round
        /// </summary>  
        internal Vector3 GetUserRoundnessTransform(PregnancyPlusData infConfigClone, Vector3 originalVerticeLs, Vector3 smoothedVectorLs, 
                                                   Vector3 sphereCenterLs, float skinToCenterDist, ThreadsafeCurve bellyEdgeAC)
        {
            var zDistFromCenter = smoothedVectorLs.z - sphereCenterLs.z;

            //As the distance forward gets further from sphere center make the shape more round (shifted outward slightly from center)
            var xyLerp = Mathf.Lerp(0, GetInflationRoundness(infConfigClone), (zDistFromCenter - (bellyInfo.WaistThick/4f))/bellyInfo.ScaledRadius(BellyDir.z));

            //As the original vert gets closer to the sphere radius, apply less change since we want smooth transitions at belly's edge
            var moveDistanceLerp = Mathf.Lerp(xyLerp, 0, bellyEdgeAC.Evaluate(skinToCenterDist/bellyInfo.ScaledRadius(BellyDir.z)));

            //Get the direction to move the vert (offset center a little forward from sphere center)
            var directionToMove = (smoothedVectorLs - (sphereCenterLs + Vector3.forward * (bellyInfo.ScaledRadius(BellyDir.z)/3))).normalized;

            //set the new vert position in that direction + the new lerp scale distance
            return smoothedVectorLs + directionToMove * moveDistanceLerp;
        }


        /// <summary>   
        /// This Drop the belly down
        /// </summary>
        internal Vector3 GetUserDropTransform(PregnancyPlusData infConfigClone, Vector3 meshRootTfUp, Vector3 smoothedVectorLs, Vector3 sphereCenterLs, 
                                              float skinToCenterDist, float sphereRadius) 
        {                     
            //Move the verts closest to sphere center Z more slowly than verts at the belly button.  Otherwise you stretch the ones near the body too much
            var lerpZ = Mathf.Lerp(0, GetInflationDrop(infConfigClone), (smoothedVectorLs.z - sphereCenterLs.z)/(bellyInfo.ScaledRadius(BellyDir.z) * 1.5f));
            return smoothedVectorLs + meshRootTfUp * -(sphereRadius * lerpZ);
        }


        /// <summary>
        /// Dampen any mesh changed near edged of the belly (sides, top, and bottom) to prevent too much vertex stretching.  The more forward the vertex is from Z the more it's allowd to be altered by sliders
        /// </summary>        
        internal Vector3 RoundToSides(Vector3 originalVerticeLs, Vector3 smoothedVectorLs, Vector3 backExtentPosLs, ThreadsafeCurve bellySidesAC) 
        {        
            var origRad = bellyInfo.ScaledOrigRadius(BellyDir.z)/1.8f;
            var multipliedRad = bellyInfo.ScaledRadius(BellyDir.z)/2.5f;

            //The distance forward that we will lerp to a curve (as multiplier grows, apply less of it)
            var zForwardSmoothDist = multipliedRad > origRad ? (origRad + (multipliedRad - origRad)/3f) : origRad;

            // Get the disnce the original vector is forward from characters back (use originial and not inflated to exclude multiplier interference)
            var forwardFromBack = (originalVerticeLs.z - backExtentPosLs.z);

            //Dont bother lerping if not needed
            if (forwardFromBack > zForwardSmoothDist) return smoothedVectorLs;
            
            //As the vert.z approaches the front lerp it less
            return Vector3.Lerp(originalVerticeLs, smoothedVectorLs, bellySidesAC.Evaluate(forwardFromBack/zForwardSmoothDist));        
        }
        

        /// <summary>
        /// Reduce the stretching of the skin at the top of the belly where it connects to the ribs at large Multiplier values
        /// </summary>
        internal Vector3 ReduceRibStretchingZ(Vector3 originalVerticeLs, Vector3 smoothedVectorLs, Vector3 topExtentPosLs, ThreadsafeCurve bellyTopAC)
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
                var animCurveVal = bellyTopAC.Evaluate(distanceFromTopExtent/topExtentOffset);

                //Reduce the amount they are allowed to stretch forward, the higher the verts are
                var newVector = Vector3.Lerp(originalVerticeLs, smoothedVectorLs, animCurveVal);  
                smoothedVectorLs.z = newVector.z;
            }           

            return smoothedVectorLs;
        }



        //Allow user plugin config values to be added in during story mode
        internal float GetInflationMultiplier(PregnancyPlusData infConfigInstance = null) 
        {
            //If an instance of preg+ data is passed in, use it
            var multiplier = infConfigInstance != null ? infConfigInstance.inflationMultiplier : infConfig.inflationMultiplier;

            if (StudioAPI.InsideStudio || MakerAPI.InsideMaker) return multiplier;
            var globalOverrideVal = PregnancyPlusPlugin.StoryModeInflationMultiplier != null ? PregnancyPlusPlugin.StoryModeInflationMultiplier.Value : 0;
            return (multiplier + globalOverrideVal);
        }
        internal float GetInflationMoveY(PregnancyPlusData infConfigInstance = null) 
        {
            //If an instance of preg+ data is passed in, use it
            var moveY = infConfigInstance != null ? infConfigInstance.inflationMoveY : infConfig.inflationMoveY;

            if (StudioAPI.InsideStudio || MakerAPI.InsideMaker) return moveY;
            var globalOverrideVal = PregnancyPlusPlugin.StoryModeInflationMoveY != null ? PregnancyPlusPlugin.StoryModeInflationMoveY.Value : 0;
            return (moveY + globalOverrideVal);
        }

        internal float GetInflationMoveZ(PregnancyPlusData infConfigInstance = null) 
        {
            //If an instance of preg+ data is passed in, use it
            var moveZ = infConfigInstance != null ? infConfigInstance.inflationMoveZ : infConfig.inflationMoveZ;

            if (StudioAPI.InsideStudio || MakerAPI.InsideMaker) return moveZ;
            var globalOverrideVal = PregnancyPlusPlugin.StoryModeInflationMoveZ != null ? PregnancyPlusPlugin.StoryModeInflationMoveZ.Value : 0;
            return (moveZ + globalOverrideVal);
        }

        internal float GetInflationStretchX(PregnancyPlusData infConfigInstance = null) 
        {
            //If an instance of preg+ data is passed in, use it
            var stretchX = infConfigInstance != null ? infConfigInstance.inflationStretchX : infConfig.inflationStretchX;

            if (StudioAPI.InsideStudio || MakerAPI.InsideMaker) return stretchX;
            var globalOverrideVal = PregnancyPlusPlugin.StoryModeInflationStretchX != null ? PregnancyPlusPlugin.StoryModeInflationStretchX.Value : 0;
            return (stretchX + globalOverrideVal);
        }

        internal float GetInflationStretchY(PregnancyPlusData infConfigInstance = null) 
        {
            //If an instance of preg+ data is passed in, use it
            var stretchY = infConfigInstance != null ? infConfigInstance.inflationStretchY : infConfig.inflationStretchY;

            if (StudioAPI.InsideStudio || MakerAPI.InsideMaker) return stretchY;
            var globalOverrideVal = PregnancyPlusPlugin.StoryModeInflationStretchY != null ? PregnancyPlusPlugin.StoryModeInflationStretchY.Value : 0;
            return (stretchY + globalOverrideVal);
        }

        internal float GetInflationShiftY(PregnancyPlusData infConfigInstance = null) 
        {
            //If an instance of preg+ data is passed in, use it
            var shiftY = infConfigInstance != null ? infConfigInstance.inflationShiftY : infConfig.inflationShiftY;

            if (StudioAPI.InsideStudio || MakerAPI.InsideMaker) return shiftY;
            var globalOverrideVal = PregnancyPlusPlugin.StoryModeInflationShiftY != null ? PregnancyPlusPlugin.StoryModeInflationShiftY.Value : 0;
            return (shiftY + globalOverrideVal);
        }

        internal float GetInflationShiftZ(PregnancyPlusData infConfigInstance = null) 
        {
            //If an instance of preg+ data is passed in, use it
            var shiftZ = infConfigInstance != null ? infConfigInstance.inflationShiftZ : infConfig.inflationShiftZ;

            if (StudioAPI.InsideStudio || MakerAPI.InsideMaker) return shiftZ;
            var globalOverrideVal = PregnancyPlusPlugin.StoryModeInflationShiftZ != null ? PregnancyPlusPlugin.StoryModeInflationShiftZ.Value : 0;
            return (shiftZ + globalOverrideVal);
        }

        internal float GetInflationTaperY(PregnancyPlusData infConfigInstance = null) 
        {
            //If an instance of preg+ data is passed in, use it
            var taperY = infConfigInstance != null ? infConfigInstance.inflationTaperY : infConfig.inflationTaperY;

            if (StudioAPI.InsideStudio || MakerAPI.InsideMaker) return taperY;
            var globalOverrideVal = PregnancyPlusPlugin.StoryModeInflationTaperY != null ? PregnancyPlusPlugin.StoryModeInflationTaperY.Value : 0;
            return (taperY + globalOverrideVal);
        }

        internal float GetInflationTaperZ(PregnancyPlusData infConfigInstance = null) 
        {
            //If an instance of preg+ data is passed in, use it
            var taperZ = infConfigInstance != null ? infConfigInstance.inflationTaperZ : infConfig.inflationTaperZ;

            if (StudioAPI.InsideStudio || MakerAPI.InsideMaker) return taperZ;
            var globalOverrideVal = PregnancyPlusPlugin.StoryModeInflationTaperZ != null ? PregnancyPlusPlugin.StoryModeInflationTaperZ.Value : 0;
            return (taperZ + globalOverrideVal);
        }

        internal float GetInflationDrop(PregnancyPlusData infConfigInstance = null) 
        {
            //If an instance of preg+ data is passed in, use it
            var drop = infConfigInstance != null ? infConfigInstance.inflationDrop : infConfig.inflationDrop;

            if (StudioAPI.InsideStudio || MakerAPI.InsideMaker) return drop;
            var globalOverrideVal = PregnancyPlusPlugin.StoryModeInflationDrop != null ? PregnancyPlusPlugin.StoryModeInflationDrop.Value : 0;
            return (drop + globalOverrideVal);
        }

        internal float GetInflationClothOffset(PregnancyPlusData infConfigInstance = null) 
        {
            //If an instance of preg+ data is passed in, use it
            var clothOffset = infConfigInstance != null ? infConfigInstance.inflationClothOffset : infConfig.inflationClothOffset;

            if (StudioAPI.InsideStudio || MakerAPI.InsideMaker) return clothOffset;
            var globalOverrideVal = PregnancyPlusPlugin.StoryModeInflationClothOffset != null ? PregnancyPlusPlugin.StoryModeInflationClothOffset.Value : 0;
            return (clothOffset + globalOverrideVal);
        }

        internal float GetInflationFatFold(PregnancyPlusData infConfigInstance = null) 
        {
            //If an instance of preg+ data is passed in, use it
            var fatFold = infConfigInstance != null ? infConfigInstance.inflationFatFold : infConfig.inflationFatFold;

            if (StudioAPI.InsideStudio || MakerAPI.InsideMaker) return fatFold;
            var globalOverrideVal = PregnancyPlusPlugin.StoryModeInflationFatFold != null ? PregnancyPlusPlugin.StoryModeInflationFatFold.Value : 0;
            return (fatFold + globalOverrideVal);
        }


        internal float GetInflationFatFoldHeight(PregnancyPlusData infConfigInstance = null) 
        {
            //If an instance of preg+ data is passed in, use it
            var fatFoldHeight = infConfigInstance != null ? infConfigInstance.inflationFatFoldHeight : infConfig.inflationFatFoldHeight;

            if (StudioAPI.InsideStudio || MakerAPI.InsideMaker) return fatFoldHeight;
            var globalOverrideVal = PregnancyPlusPlugin.StoryModeInflationFatFoldHeight != null ? PregnancyPlusPlugin.StoryModeInflationFatFoldHeight.Value : 0;
            return (fatFoldHeight + globalOverrideVal);
        }


        internal float GetInflationRoundness(PregnancyPlusData infConfigInstance = null) 
        {
            //If an instance of preg+ data is passed in, use it
            var roundness = infConfigInstance != null ? infConfigInstance.inflationRoundness : infConfig.inflationRoundness;

            if (StudioAPI.InsideStudio || MakerAPI.InsideMaker) return roundness;
            var globalOverrideVal = PregnancyPlusPlugin.StoryModeInflationRoundness != null ? PregnancyPlusPlugin.StoryModeInflationRoundness.Value : 0;
            return (roundness + globalOverrideVal);
        }

        
    }
}


