﻿using KKAPI;
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

        //Used to determine belly scale direction
        public enum BellyDir
        {
            x,y,z
        }

        public class BellyInfo 
        {
            public float WaistWidth;
            public float ScaledWaistWidth
            {
                get { return WaistWidth * TotalCharScale.x; }
            }
            
            public float WaistHeight;
            public float ScaledWaistHeight
            {
                get { return WaistHeight * TotalCharScale.y; }
            }

            public float WaistThick;
            public float ScaledWaistThick
            {
                get { return WaistThick * TotalCharScale.z; }
            }

            public Vector3 CharacterScale;//ChaControl.transform scale (set by the Axis scale control)
            public Vector3 BodyTopScale;//BodyTop bone scale
            public Vector3 NHeightScale;//n_height bone scale
            public Vector3 TotalCharScale
            {
                //Multiply x*x, y*y etc to get the toal character scale
                get { return new Vector3(BodyTopScale.x * CharacterScale.x, BodyTopScale.y * CharacterScale.y, BodyTopScale.z * CharacterScale.z); }
            }

            public float SphereRadius;
            public float OriginalSphereRadius;
            public float CurrentMultiplier;

            //From char z=0 position
            public float ZLimit
            {
                //Get the distance from center -> spine, where the belly is allowed to wrap around to (total distance from 0 to back bone /some scale that looks good)
                get { return ScaledWaistThick/2f; }
            }

            public float BellyToBreastDist;//Belly button to breast distance
            public float ScaledBellyToBreastDist
            {
                get { return BellyToBreastDist * TotalCharScale.y; }
            }

            //From char belly button to breast distance
            public float YLimit
            {
                //Get the distance from center -> ribs, with scale applied
                get { return ScaledBellyToBreastDist; }
            }

            public float BellyButtonHeight;//Foot to belly button height
            public bool MeshRootDidMove = false;//Keep track when we move the meshroot position for certain clothing positional calculations

            
            public bool IsInitialized 
            {
                get { return WaistWidth > 0 && WaistHeight > 0; }
            }

            //Get the sphere radius asjusted by the characters scale
            public float ScaledRadius(BellyDir dir)
            {
                if (dir == BellyDir.x) return SphereRadius/TotalCharScale.x;
                if (dir == BellyDir.y) return SphereRadius/TotalCharScale.y;
                if (dir == BellyDir.z) return SphereRadius/TotalCharScale.z;
                return -1;
            }

            public float ScaledOrigRadius(BellyDir dir)
            {
                if (dir == BellyDir.x) return OriginalSphereRadius/TotalCharScale.x;
                if (dir == BellyDir.y) return OriginalSphereRadius/TotalCharScale.y;
                if (dir == BellyDir.z) return OriginalSphereRadius/TotalCharScale.z;
                return -1;
            }

            internal BellyInfo(float waistWidth, float waistHeight, float sphereRadius, float originalSphereRadius, 
                               Vector3 bodyTopScale, float currentMultiplier, float waistThick, Vector3 nHeightScale,
                               float bellyToBreastDist, Vector3 characterScale, bool meshRootDidMove = false) 
            {
                WaistWidth = waistWidth;
                WaistHeight = waistHeight;
                SphereRadius = sphereRadius;
                OriginalSphereRadius = originalSphereRadius;
                BodyTopScale = bodyTopScale;
                CurrentMultiplier = currentMultiplier;
                WaistThick = waistThick;
                NHeightScale = nHeightScale;
                BellyToBreastDist = bellyToBreastDist;
                CharacterScale = characterScale;
                MeshRootDidMove = meshRootDidMove;
            }

            //Determine if we need to recalculate the sphere radius (hopefully to avoid change in hip bones causing belly size to sudenly change)
            internal bool NeedsSphereRecalc(PregnancyPlusData data, float newMultiplier) 
            {
                if (!IsInitialized) return true;
                if (CurrentMultiplier != newMultiplier) return true;

                return false;
            }


            //Determine if we need to recalculate the bone distances (typically when character scale changes)
            internal bool NeedsBoneDistanceRecalc(Vector3 bodyTopScale, Vector3 nHeightScale, Vector3 charScale) 
            {
                if (!IsInitialized) return true;
                if (BodyTopScale != bodyTopScale) return true;
                if (NHeightScale != nHeightScale) return true;
                if (CharacterScale != charScale) return true;

                return false;
            }


            public string Log()
            {
                return $@" WaistWidth {WaistWidth} WaistHeight {WaistHeight} WaistThick {WaistThick} BellyToBreastDist {BellyToBreastDist}
                           BodyTopScale {BodyTopScale} NHeightScale {NHeightScale} CharacterScale {CharacterScale} TotalCharScale {TotalCharScale}
                           SphereRadius {SphereRadius} OriginalSphereRadius {OriginalSphereRadius}
                           ";
            }

        }


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
            var totalScale = new Vector3(bodyTopScale.x * charScale.x, bodyTopScale.y * charScale.y, bodyTopScale.z * charScale.z);
            var needsWaistRecalc = bellyInfo != null ? bellyInfo.NeedsBoneDistanceRecalc(bodyTopScale, nHeightScale, charScale) : true;
            var needsSphereRecalc = bellyInfo != null ? bellyInfo.NeedsSphereRecalc(infConfig, GetInflationMultiplier()) : true;

            //We should reuse existing measurements when we can, because characters waise bone distance chan change with animation, which affects belly size.
            if (bellyInfo != null)
            {
                if (!forceRecalc && needsSphereRecalc && !needsWaistRecalc)//Sphere radius calc needed
                {
                    var _valid = MeasureSphere(chaControl, bodyTopScale, nHeightScale, totalScale);
                    if (PregnancyPlusPlugin.debugLog)  PregnancyPlusPlugin.Logger.LogInfo(bellyInfo.Log()); 
                    return _valid;
                }
                else if (!forceRecalc && needsWaistRecalc && !needsSphereRecalc)//Measurements needed which also requires sphere recalc
                {
                    var _valid = MeasureWaist(chaControl, charScale, nHeightScale, 
                                        out float _waistToRibDist, out float _waistToBackThickness, out float _waistWidth, out float _bellyToBreastDist);
                    MeasureSphere(chaControl, bodyTopScale, nHeightScale, totalScale);

                    //Store all these values for reuse later
                    bellyInfo = new BellyInfo(_waistWidth, _waistToRibDist, bellyInfo.SphereRadius, bellyInfo.OriginalSphereRadius, bodyTopScale, 
                                              GetInflationMultiplier(), _waistToBackThickness, nHeightScale, _bellyToBreastDist,
                                              charScale, bellyInfo.MeshRootDidMove);

                    if (PregnancyPlusPlugin.debugLog)  PregnancyPlusPlugin.Logger.LogInfo(bellyInfo.Log());                                             
                    return _valid;
                }
                else if (!forceRecalc && !needsSphereRecalc && !needsWaistRecalc)//No changed needed
                {
                    //Just return the original measurements and sphere radius when no updates needed
                    if (PregnancyPlusPlugin.debugLog)  PregnancyPlusPlugin.Logger.LogInfo(bellyInfo.Log()); 

                    //Measeurements are fine and can be reused if above 0
                    return (bellyInfo.WaistWidth > 0 && bellyInfo.SphereRadius > 0 && bellyInfo.WaistThick > 0);
                } 
            }

            //Measeurements need to be recalculated from scratch
            if (PregnancyPlusPlugin.debugLog)  PregnancyPlusPlugin.Logger.LogInfo($" MeasureWaistAndSphere init ");

            //Get waist measurements from bone distances
            var valid = MeasureWaist(chaControl, charScale, nHeightScale, 
                                           out float waistToRibDist, out float waistToBackThickness, out float waistWidth, out float bellyToBreastDist);

            //Check for bad values
            if (!valid) return false;

            //Calculate sphere radius based on distance from waist to ribs (seems big, but lerping later will trim much of it), added Math.Min for skinny waists
            var sphereRadius = GetSphereRadius(waistToRibDist, waistWidth, totalScale);
            var sphereRadiusMultiplied = sphereRadius * (GetInflationMultiplier() + 1);   

            //Store all these values for reuse later
            bellyInfo = new BellyInfo(waistWidth, waistToRibDist, sphereRadiusMultiplied, sphereRadius, bodyTopScale, 
                                      GetInflationMultiplier(), waistToBackThickness, nHeightScale, bellyToBreastDist,
                                      charScale);

            if (PregnancyPlusPlugin.debugLog)  PregnancyPlusPlugin.Logger.LogInfo(bellyInfo.Log());            

            return (waistWidth > 0 && sphereRadiusMultiplied > 0 && waistToBackThickness > 0 && bellyToBreastDist > 0);
        }


        /// <summary>
        /// Calculate the waist mesaurements that are used to set the default belly size
        /// </summary>
        /// <returns>Boolean if all measurements are valid</returns>
        internal bool MeasureWaist(ChaControl chaControl, Vector3 charScale, Vector3 nHeightScale, 
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
            #elif HS2 || AI   
                var breastRoot = "cf_J_Mune00";                             
                var ribName = "cf_J_Spine02_s";
                var waistName = "cf_J_Kosi02_s";
                var thighLName = "cf_J_LegUp00_L";
                var thighRName = "cf_J_LegUp00_R";
                var backName = "N_Back";  
                var bellyButton = "cf_J_Kosi01";
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
            waistToRibDist = Vector3.Distance(waistBone.transform.InverseTransformPoint(waistBone.position), waistBone.transform.InverseTransformPoint(ribBone.position));


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
            bellyToBreastDist = Math.Abs(bellyButtonBone.transform.InverseTransformPoint(ribBone.position).y) + Math.Abs(ribBone.transform.InverseTransformPoint(breastBone.position).y);  

            if (PregnancyPlusPlugin.debugLog)  PregnancyPlusPlugin.Logger.LogInfo($" MeasureWaist Recalc ");            
            return (waistWidth > 0 && waistToBackThickness > 0 && waistToRibDist > 0); 
        }


        /// <summary>
        /// Get the measurements used to determine the sphere position and radius
        /// </summary>
        /// <returns>Boolean if all measurements are valid</returns>
        internal bool MeasureSphere(ChaControl chaControl, Vector3 charScale, Vector3 nHeightScale, Vector3 totalScale) 
        {
            //Measeurements need to be recalculated from saved values (Does not change waistWidth! or height)
            var newSphereRadius = GetSphereRadius(bellyInfo.WaistHeight, bellyInfo.WaistWidth, totalScale);
            var newSphereRadiusMult = newSphereRadius * (GetInflationMultiplier() + 1); 

            //Store new values for later checks
            bellyInfo = new BellyInfo(bellyInfo.WaistWidth, bellyInfo.WaistHeight, newSphereRadiusMult, newSphereRadius, 
                                        charScale, GetInflationMultiplier(), bellyInfo.WaistThick, nHeightScale, bellyInfo.BellyToBreastDist,
                                        chaControl.transform.localScale, bellyInfo.MeshRootDidMove);

            if (PregnancyPlusPlugin.debugLog)  PregnancyPlusPlugin.Logger.LogInfo($" MeasureSphere Recalc ");            
            
            return (bellyInfo.WaistWidth > 0 && newSphereRadius > 0 && bellyInfo.WaistThick > 0);    
        }
    }
}


