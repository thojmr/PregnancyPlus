using KKAPI;
using KKAPI.Chara;
using UnityEngine;
using System;
using System.Collections.Generic;
#if HS2 || AI
    using AIChara;
#endif

namespace KK_PregnancyPlus
{

    //This partial class contains the belly measurement logic
    public partial class PregnancyPlusCharaController: CharaCustomFunctionController
    {           

        /// <summary>
        /// Get the characters waist width and calculate the appropriate belly sphere radius from it
        ///     Smaller characters have smaller bellies, wider characters have wider bellies etc...
        /// </summary>
        /// <param name="chaControl">The character to measure</param>
        /// <param name="forceRecalc">For debuggin, will recalculate from scratch each time when true</param>
        /// <returns>Boolean if all measurements are valid</returns>
        internal bool MeasureWaistAndSphere(ChaControl chaControl, bool forceRecalc = false) 
        { 
            var bodyTopScale = PregnancyPlusHelper.GetBodyTopScale(ChaControl);
            var nHeightScale = PregnancyPlusHelper.GetN_HeightScale(ChaControl);
            var charScale = ChaControl.transform.localScale;
            var needsWaistRecalc = bellyInfo != null ? bellyInfo.NeedsBoneDistanceRecalc(bodyTopScale, nHeightScale, charScale) : true;
            var needsSphereRecalc = bellyInfo != null ? bellyInfo.NeedsSphereRecalc(infConfig, GetInflationMultiplier()) : true;

            if (forceRecalc) bellyInfo = null;

            //We should reuse existing measurements when we can, because characters waise bone distance chan change with animation, which affects belly size.
            if (bellyInfo != null)
            {
                if (needsSphereRecalc && !needsWaistRecalc)//Sphere radius calc needed
                {
                    var _valid = MeasureSphere(chaControl, bodyTopScale, nHeightScale);
                    if (PregnancyPlusPlugin.DebugCalcs.Value)  PregnancyPlusPlugin.Logger.LogInfo(bellyInfo.Log()); 
                    return _valid;
                }
                else if (needsWaistRecalc && !needsSphereRecalc)//Measurements needed which also requires sphere recalc
                {
                    var _valid = MeasureWaist(chaControl, bodyTopScale, out float _waistToRibDist, out float _waistToBackThickness, out float _waistWidth, out float _bellyToBreastDist);
                    MeasureSphere(chaControl, bodyTopScale, nHeightScale);

                    //Store all these values for reuse later
                    bellyInfo = new BellyInfo(_waistWidth, _waistToRibDist, bellyInfo.SphereRadius, bellyInfo.OriginalSphereRadius, bodyTopScale, 
                                              GetInflationMultiplier(), _waistToBackThickness, nHeightScale, _bellyToBreastDist,
                                              charScale, bellyInfo.MeshRootDidMove);

                    if (PregnancyPlusPlugin.DebugCalcs.Value)  PregnancyPlusPlugin.Logger.LogInfo(bellyInfo.Log());                                             
                    return _valid;
                }
                else if (!needsSphereRecalc && !needsWaistRecalc)//No changed needed
                {
                    //Just return the original measurements and sphere radius when no updates needed
                    if (PregnancyPlusPlugin.DebugCalcs.Value)  PregnancyPlusPlugin.Logger.LogInfo(bellyInfo.Log()); 

                    //Measeurements are fine and can be reused if above 0
                    return (bellyInfo.WaistWidth > 0 && bellyInfo.SphereRadius > 0 && bellyInfo.WaistThick > 0);
                } 
            }

            //Measeurements need to be recalculated from scratch
            if (PregnancyPlusPlugin.DebugLog.Value)  PregnancyPlusPlugin.Logger.LogInfo($" MeasureWaistAndSphere init ");

            //Get waist measurements from bone distances
            var valid = MeasureWaist(chaControl, bodyTopScale, out float waistToRibDist, out float waistToBackThickness, out float waistWidth, out float bellyToBreastDist);

            //Check for bad values
            if (!valid) return false;

            //Calculate sphere radius based on distance from waist to ribs (seems big, but lerping later will trim much of it), added Math.Min for skinny waists
            var sphereRadius = GetSphereRadius(waistToRibDist, waistWidth);
            var sphereRadiusMultiplied = sphereRadius * (GetInflationMultiplier() + 1);   

            //Store all these values for reuse later
            bellyInfo = new BellyInfo(waistWidth, waistToRibDist, sphereRadiusMultiplied, sphereRadius, bodyTopScale, 
                                      GetInflationMultiplier(), waistToBackThickness, nHeightScale, bellyToBreastDist,
                                      charScale);

            if (PregnancyPlusPlugin.DebugCalcs.Value)  PregnancyPlusPlugin.Logger.LogInfo(bellyInfo.Log());            

            return (waistWidth > 0 && sphereRadiusMultiplied > 0 && waistToBackThickness > 0 && bellyToBreastDist > 0);
        }


        /// <summary>
        /// Calculate the waist mesaurements that are used to set the default belly size
        /// </summary>
        /// <returns>Boolean if all measurements are valid</returns>
        internal bool MeasureWaist(ChaControl chaControl, Vector3 bodyTopScale,
                                   out float waistToRibDist, out float waistToBackThickness, out float waistWidth, out float bellyToBreastDist) 
        {
            //Bone names
            #if KK
                var breastRoot = "cf_d_bust00";
                var ribName = "cf_s_spine02";
                var waistName = "cf_s_waist02";
                var thighLName = "cf_j_thigh00_L";
                var thighRName = "cf_j_thigh00_R";  
                var backName = "a_n_back";  
                var bellyButton = "cf_j_waist01";
                var hipName = "cf_j_hips";
            #elif HS2 || AI   
                var breastRoot = "cf_J_Mune00";                             
                var ribName = "cf_J_Spine02_s";
                var waistName = "cf_J_Kosi02_s";
                var thighLName = "cf_J_LegUp00_L";
                var thighRName = "cf_J_LegUp00_R";
                var backName = "N_Back";  
                var bellyButton = "cf_J_Kosi01";
                var hipName = "cf_J_hips";
            #endif  

            //Init out params
            waistToRibDist = 0;
            waistToBackThickness = 0;
            waistWidth = 0;
            bellyToBreastDist = 0;

            //Get the characters Y bones to measure from
            var ribBone = PregnancyPlusHelper.GetBone(ChaControl, ribName);
            var waistBone = PregnancyPlusHelper.GetBone(ChaControl, waistName);
            if (ribBone == null || waistBone == null) return (waistWidth > 0 && waistToBackThickness > 0 && waistToRibDist > 0);

            //Measures from the wasist to the bottom of the ribs
            waistToRibDist = BoneChainYDistance(waistName, hipName) + BoneChainYDistance(ribName, hipName, includeNegative: true);

            //Get the characters z waist thickness
            var backBone = PregnancyPlusHelper.GetBone(ChaControl, backName);
            var breastBone = PregnancyPlusHelper.GetBone(ChaControl, breastRoot);  
            if (ribBone == null || breastBone == null) return (waistWidth > 0 && waistToBackThickness > 0 && waistToRibDist > 0);

            //Measures from breast root to the back spine distance
            waistToBackThickness = Math.Abs(breastBone.transform.InverseTransformPoint(backBone.position).z);

            //Get the characters X bones to measure from, in localspace to ignore n_height scale
            var thighLBone = PregnancyPlusHelper.GetBone(ChaControl, thighLName);
            var thighRBone = PregnancyPlusHelper.GetBone(ChaControl, thighRName);
            if (thighLBone == null || thighRBone == null) return (waistWidth > 0 && waistToBackThickness > 0 && waistToRibDist > 0);
            
            //Measures Left to right hip bone distance
            waistWidth = Vector3.Distance(thighLBone.transform.InverseTransformPoint(thighLBone.position), thighLBone.transform.InverseTransformPoint(thighRBone.position)); 

            //Verts above this position are not allowed to move
            var bellyButtonBone = PregnancyPlusHelper.GetBone(ChaControl, bellyButton);      
            //Distance from waist to breast root              
            bellyToBreastDist = BoneChainYDistance(bellyButton, hipName) + BoneChainYDistance(breastRoot, hipName, includeNegative: true);

            if (PregnancyPlusPlugin.DebugLog.Value)  PregnancyPlusPlugin.Logger.LogInfo($" MeasureWaist Recalc ");            
            return (waistWidth > 0 && waistToBackThickness > 0 && waistToRibDist > 0); 
        }


        /// <summary>
        /// Get the measurements used to determine the sphere position and radius
        /// </summary>
        /// <returns>Boolean if all measurements are valid</returns>
        internal bool MeasureSphere(ChaControl chaControl, Vector3 charScale, Vector3 nHeightScale) 
        {
            //Measeurements need to be recalculated from saved values (Does not change waistWidth! or height)
            var newSphereRadius = GetSphereRadius(bellyInfo.WaistHeight, bellyInfo.WaistWidth);
            var newSphereRadiusMult = newSphereRadius * (GetInflationMultiplier() + 1); 

            //Store new values for later checks
            bellyInfo = new BellyInfo(bellyInfo.WaistWidth, bellyInfo.WaistHeight, newSphereRadiusMult, newSphereRadius, 
                                        charScale, GetInflationMultiplier(), bellyInfo.WaistThick, nHeightScale, bellyInfo.BellyToBreastDist,
                                        chaControl.transform.localScale, bellyInfo.MeshRootDidMove);

            if (PregnancyPlusPlugin.DebugLog.Value)  PregnancyPlusPlugin.Logger.LogInfo($" MeasureSphere Recalc ");            
            
            return (bellyInfo.WaistWidth > 0 && newSphereRadius > 0 && bellyInfo.WaistThick > 0);    
        }


        /// <summary>
        /// Calculates the length of a set of chained bones from bottom up.  It will only caluculate the true Y distance, so it effectively ignores any animations (behaves like a TPose measurement).  Will include bones scales as well
        /// </summary>
        /// <param name="boneStart">The starting (bottom of tree) bone name</param>        
        /// <param name="boneEnd">The optional (top level) end bone name.  If null, the entire bone tree from bottom to top will be calculated.</param>
        /// <param name="totalCharScale">If you want to apply wordsspace char scale to the resulting messurement</param>
        internal float BoneChainYDistance(string boneStart, string boneEnd = null, Vector3 totalCharScale = default(Vector3), bool includeNegative = false) 
        {
            //loops through each bone starting bottom going up through parent to destination (or root)
            var currentBone = PregnancyPlusHelper.GetBoneGO(ChaControl, boneStart);
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
                var newDifference = (lastBone != null ? currentBone.transform.InverseTransformPoint(currentBone.transform.position).y 
                                     - currentBone.transform.InverseTransformPoint(lastBone.transform.position).y : 0);
                // if (PregnancyPlusPlugin.DebugCalcs.Value) PregnancyPlusPlugin.Logger.LogInfo($" newDifference {newDifference}  currentBone.name {currentBone.name}  scale {currentBone.transform.localScale} corrected {((newDifference * currentBone.transform.localScale.y) - newDifference)}");
                
                //Ignore any negative bone differences (like char root bone which is at 0,0,0)
                if (includeNegative || newDifference > 0) 
                {                    
                    distance = distance + Math.Abs(newDifference);
                    lastBone = currentBone;
                }                

                currentBone = currentBone.transform.parent.gameObject;
            }

            //Check for BodyTop scale to apply it to distance
            if (totalCharScale.y != 0 && totalCharScale.y != 1) 
            {                
                if (PregnancyPlusPlugin.DebugCalcs.Value) PregnancyPlusPlugin.Logger.LogInfo($" applying BodyTop scale to distance: {distance} scale: {totalCharScale.y}");
                distance = distance * totalCharScale.y;
            }

            // if (PregnancyPlusPlugin.DebugCalcs.Value) PregnancyPlusPlugin.Logger.LogInfo($" total bone chain dist {distance}  cm:{PregnancyPlusHelper.ConvertToCm(distance)}");
            return distance;
        }

    }
}


