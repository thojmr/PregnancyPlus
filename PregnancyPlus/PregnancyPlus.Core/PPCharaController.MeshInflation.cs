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

    //This partial class contains the mesh inflation logic
    public partial class PregnancyPlusCharaController: CharaCustomFunctionController
    {           

        /// <summary>
        /// Triggers belly mesh inflation for the current ChaControl for any active meshs (not hidden clothes)
        /// It will check the inflationSize dictionary for a valid value (last set via config slider or MeshInflate(value))
        /// If size 0 is used it will clear all active mesh inflations
        /// This will not run twice for the same parameters, a change of config value is required
        /// </summary>
        /// <param name="checkForNewMesh">Lets you force bypass the check for values changed to check for new meshes</param>
        /// <param name="freshStart">Will recalculate verts like a first time run</param>
        /// <param name="pluginConfigSliderChanged">Will treat as if some slider values changed, which they did in global plugin config</param>
        /// <returns>Will return True if the mesh was altered and False if not</returns>
        public bool MeshInflate(bool checkForNewMesh = false, bool freshStart = false, bool pluginConfigSliderChanged = false)
        {
            if (ChaControl.objBodyBone == null) return false;//Make sure chatacter objs exists first  
            if (!PregnancyPlusPlugin.AllowMale.Value && ChaControl.sex == 0) return false;// Only female characters, unless plugin config says otherwise          

            var sliderHaveChanged = NeedsMeshUpdate(pluginConfigSliderChanged);
            //Only continue if one of the config values changed
            if (!sliderHaveChanged) 
            {
                //Only stop here, if no recalculation needed
                if (!freshStart && !checkForNewMesh)  return false; 
            }
            ResetInflation();

            if (!AllowedToInflate()) return false;//if outside studio/maker, make sure StoryMode is enabled first
            if (!infConfig.GameplayEnabled) return false;//Only if gameplay enabled

            //Resets all stored vert values, so the script will have to recalculate all from base body
            if (freshStart) CleanSlate();

            //Only continue when size above 0
            if (infConfig.inflationSize <= 0) 
            {
                infConfigHistory.inflationSize = 0;
                return false;                                
            }
            
            if (PregnancyPlusPlugin.debugLog)  PregnancyPlusPlugin.Logger.LogInfo($" ---------- ");
            if (PregnancyPlusPlugin.debugLog)  PregnancyPlusPlugin.Logger.LogInfo($" inflationSize > {infConfig.inflationSize} for {charaFileName} ");
            
            //Get the measurements that determine the base belly size
            var hasMeasuerments = MeasureWaistAndSphere(ChaControl);                     
            if (!hasMeasuerments) return false;
            
            var anyMeshChanges = false;

            //Get and apply all clothes render mesh changes
            var clothRenderers = PregnancyPlusHelper.GetMeshRenderers(ChaControl.objClothes);
            anyMeshChanges = LoopAndApplyMeshChanges(clothRenderers, sliderHaveChanged, anyMeshChanges, true);

            //do the same for body meshs
            var bodyRenderers = PregnancyPlusHelper.GetMeshRenderers(ChaControl.objBody, true);
            anyMeshChanges = LoopAndApplyMeshChanges(bodyRenderers, sliderHaveChanged, anyMeshChanges);

            //If any changes were applied, updated the last used shape for the Restore GUI button
            if (infConfig.HasAnyValue()) 
            {
                PregnancyPlusPlugin.lastBellyState = (PregnancyPlusData)infConfig.Clone();//CLone so we don't accidently overwright the lastState later
            }

            //Update config history when mesh changes were made
            if (anyMeshChanges) infConfigHistory = (PregnancyPlusData)infConfig.Clone();            

            return anyMeshChanges;
        }


        /// <summary>
        /// Loop through each skinned mesh rendere and get its belly verts, then apply inflation when needed
        /// </summary>
        /// <param name="smrs">List of skinnedMeshRenderes</param>
        /// <param name="sliderHaveChanged">If any Plugin Config sliders changed</param>
        /// <param name="anyMeshChanges">If any mesh changes have happened so far</param>
        /// <param name="isClothingMesh">If this smr is a cloth mesh</param>
        /// <returns>boolean true if any meshes were changed</returns>
        internal bool LoopAndApplyMeshChanges(List<SkinnedMeshRenderer> smrs, bool sliderHaveChanged, bool anyMeshChanges, bool isClothingMesh = false) 
        {
            foreach(var smr in smrs) 
            {                
                //Dont recompute verts if no sliders have changed
                if (NeedsComputeVerts(smr, sliderHaveChanged))
                {
                    var didCompute = ComputeMeshVerts(smr, isClothingMesh);
                    //If it fails to compute, skip (mesn.IsReadable = false will cause this)
                    if (!didCompute) continue;    
                }

                var appliedMeshChanges = ApplyInflation(smr, GetMeshKey(smr));            
                if (appliedMeshChanges) anyMeshChanges = true;                
            }  

            return anyMeshChanges;
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

            // if (PregnancyPlusPlugin.debugLog && ChaControl.sex == 1) DebugTools.DrawLineAndAttach(breastBone, 5);

            if (PregnancyPlusPlugin.debugLog)  PregnancyPlusPlugin.Logger.LogInfo($" MeasureWaist Recalc ");
             

            return (waistWidth > 0 && waistToBackThickness > 0 && waistToRibDist > 0); 
        }


        /// <summary>
        /// Recalculate the existing sphere measurements when character scale changes
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


        /// <summary>
        /// Does the vertex morph calculations to make a sphere out of the belly verticies, and updates the vertex dictionaries apprporiately
        /// </summary>
        /// <param name="skinnedMeshRenderer">The mesh renderer target</param>
        /// <param name="sphereRadius">The radius of the inflation sphere</param>
        /// <param name="waistWidth">The width of the characters waist</param>
        /// <param name="isClothingMesh">Clothing requires a few tweaks to match skin morphs (different offset, not sure why)</param>
        /// <returns>Will return True if mesh verticies > 0 were found  Some meshes wont have any for the belly area, returning false</returns>
        internal bool GetInflatedVerticies(SkinnedMeshRenderer smr, float sphereRadius, float waistWidth, bool isClothingMesh = false) 
        {
            Vector3 bodySphereCenterOffset = Vector3.zero;
            Vector3 clothSphereCenterOffset = Vector3.zero;

            if (smr == null) return false;

            GetMeshRoot(out Transform meshRootTf, out float meshRootDistMoved);
            if (meshRootTf == null) return false;
                        
            //set sphere center and allow for adjusting its position from the UI sliders  
            Vector3 sphereCenter = GetSphereCenter(meshRootTf, isClothingMesh);
            var needsPositionFix = smr.transform.position != meshRootTf.position;                        

#region Fixes for different mesh localspace positions between KK and HS2/AI
            #if KK            
                var isDefaultBody = !PregnancyPlusHelper.IsUncensorBody(ChaControl, UncensorCOMName); 
                var clothOffsetFix = 1.0212f;//Where does this offset even come from?  Seems like and default KK body, cloth all have a small error in position, or maybe its the other way around...
                var defaultBodyOffsetFix = 0.0231f;//Where does this offset even come from?

                if (isClothingMesh && needsPositionFix) 
                {
                    //Special logic for clothing that isn't local positioned correctly                    

                    //KK just has to have strange vert positions, so we have to adjust the sphere center location for body and clothes
                    var clothesMeshRoot = PregnancyPlusHelper.GetBoneGO(ChaControl, "p_cf_body_00.cf_o_root").transform;                     
                    // var specialCloth = PregnancyPlusHelper.GetParentGoByName(smr.gameObject, "clothes_00")?.transform;    

                    //Used to determine if the child smr of cf_o_root has not been positioned correctly at 0,0,0.  Which needs special clothSphereCenterOffset logic
                    var needsSpecialClothFix = false;//bellyInfo.MeshRootDidMove && specialCloth?.position != clothesMeshRoot.position && needsPositionFix;
                    if (PregnancyPlusPlugin.debugLog && needsSpecialClothFix) PregnancyPlusPlugin.Logger.LogInfo($" [KK only] needsSpecialClothFix {needsSpecialClothFix}");

                    //Calculate the custom clothes offset distance needed for certain clothing (Why can't it all be the same....)
                    var clothRootToSphereCenter = needsSpecialClothFix 
                        ? meshRootTf.up * FastDistance(clothesMeshRoot.position, sphereCenter)/2 * clothOffsetFix - (GetUserMoveTransform(meshRootTf))/2//this line not used at the moment, will come back to this is more reports come in
                        : meshRootTf.up * FastDistance(clothesMeshRoot.position, sphereCenter) * clothOffsetFix;                    
                    
                    //Get distance from bb to clothesMeshRoot if needs fix
                    sphereCenter = clothSphereCenterOffset = sphereCenter - clothRootToSphereCenter;//At belly button - offset from meshroot (plus some tiny dumb offset that I cant find the source of)
                } 
                else if (isDefaultBody) 
                {
                    bodySphereCenterOffset = meshRootTf.position + GetUserMoveTransform(meshRootTf) - meshRootTf.up * defaultBodyOffsetFix;////at 0,0,0, once again what is this crazy small offset?
                    sphereCenter = meshRootTf.position + GetUserMoveTransform(meshRootTf);//at belly button - offset from meshroot
                }
                else 
                {
                    //For uncensor body mesh, and most clothing items
                    clothSphereCenterOffset = bodySphereCenterOffset = sphereCenter;//at belly button
                }
                if (PregnancyPlusPlugin.debugLog) PregnancyPlusPlugin.Logger.LogInfo($" [KK only] corrected sphereCenter {sphereCenter} clothSphereCenterOffset {clothSphereCenterOffset} isDefaultBody {isDefaultBody} needsPositionFix {needsPositionFix}");

            #elif HS2 || AI
                //Its so simple when its not KK default mesh :/
                clothSphereCenterOffset = bodySphereCenterOffset = sphereCenter;
            #endif    
#endregion                    

            var rendererName = GetMeshKey(smr);         
            originalVertices[rendererName] = smr.sharedMesh.vertices;
            inflatedVertices[rendererName] = new Vector3[originalVertices[rendererName].Length];
            currentVertices[rendererName] = new Vector3[originalVertices[rendererName].Length];

            var origVerts = originalVertices[rendererName];
            var inflatedVerts = inflatedVertices[rendererName];
            var currentVerts = currentVertices[rendererName];
            var bellyVertIndex = bellyVerticieIndexes[rendererName];    

            #if DEBUG
                var bellyVertsCount = 0;
                for (int i = 0; i < bellyVertIndex.Length; i++)
                {
                    if (bellyVertIndex[i]) bellyVertsCount++;
                }
                if (PregnancyPlusPlugin.debugLog) PregnancyPlusPlugin.Logger.LogInfo($" Mesh affected vert count {bellyVertsCount}");
            #endif

            //Pre compute some values needed by SculptInflatedVerticie
            var vertsLength = origVerts.Length;
            var sphereCenterLs = meshRootTf.InverseTransformPoint(sphereCenter);
            var preMorphSphereCenter = sphereCenter - GetUserMoveTransform(meshRootTf);
            var pmSphereCenterLs = meshRootTf.InverseTransformPoint(preMorphSphereCenter); 
            //calculate the furthest back morph point based on the back bone position, include character rotation
            var backExtentPos = new Vector3(preMorphSphereCenter.x, sphereCenter.y, preMorphSphereCenter.z) + meshRootTf.forward * -bellyInfo.ZLimit;
            var backExtentPosLs = meshRootTf.InverseTransformPoint(backExtentPos);                        
            //calculate the furthest top morph point based under the breast position, include character animated height differences
            var topExtentPos = new Vector3(preMorphSphereCenter.x, preMorphSphereCenter.y, preMorphSphereCenter.z) + meshRootTf.up * bellyInfo.YLimit;
            var topExtentPosLs = meshRootTf.InverseTransformPoint(topExtentPos);
            var vertNormalCaluRadius = sphereRadius + waistWidth/10;//Only recalculate normals for verts within this radius to prevent shadows under breast at small belly sizes

            // if (PregnancyPlusPlugin.debugLog) DebugTools.DrawLineAndAttach(meshRootTf, 5, meshRootTf.InverseTransformPoint(topExtentPos) - meshRootTf.up * GetBellyButtonOffset(bellyInfo.BellyButtonHeight));
            // if (PregnancyPlusPlugin.debugLog) DebugTools.DrawLineAndAttach(meshRootTf, new Vector3(-3, 0, 0), new Vector3(3, 0, 0), meshRootTf.InverseTransformPoint(backExtentPos) - GetBellyButtonOffsetVector(meshRootTf, bellyInfo.BellyButtonHeight));
            // if (PregnancyPlusPlugin.debugLog) DebugTools.DrawLineAndAttach(meshRootTf, 5, meshRootTf.InverseTransformPoint(sphereCenter));

            //Set each verticies inflated postion, with some constraints (SculptInflatedVerticie) to make it look more natural
            for (int i = 0; i < vertsLength; i++)
            {
                var origVert = origVerts[i];

                //Only care about inflating belly verticies
                if (bellyVertIndex[i] || PregnancyPlusPlugin.debugAllVerts) 
                {                    
                    var origVertWs = meshRootTf.TransformPoint(origVerts[i]);//Convert to worldspace 
                    var vertDistance = FastDistance(origVertWs, sphereCenter);

                    CalculateNormalsBoundary(vertDistance, vertNormalCaluRadius, i, rendererName);

                    //Ignore verts outside the sphere radius
                    if (vertDistance <= vertNormalCaluRadius || PregnancyPlusPlugin.debugAllVerts) 
                    {
                        Vector3 inflatedVertWs;                    
                        Vector3 verticieToSpherePos;                                                                                    

                        //Shift each belly vertex away from sphere center in a sphere pattern
                        if (!isClothingMesh) 
                        {                        
                            //You have to normalize to sphere center instead of 0,0,0.  This way the belly will expand out as expected.  So shift all mesh verts to be origin at sphereCenter first, then normalize, then shift back
                            verticieToSpherePos = (origVertWs - bodySphereCenterOffset).normalized * sphereRadius + bodySphereCenterOffset;
                        }
                        else 
                        {   
                            //Reduce cloth flattening at largest inflation values                     
                            float reduceClothFlattenOffset = GetClothesFixOffset(meshRootTf, clothSphereCenterOffset, sphereRadius, waistWidth, origVertWs, smr.name);
                            verticieToSpherePos = (origVertWs - clothSphereCenterOffset).normalized * (sphereRadius + reduceClothFlattenOffset) + clothSphereCenterOffset;
                        }     

                        //Make adjustments to the shape, and feed in user slider input
                        inflatedVertWs =  SculptInflatedVerticie(origVertWs, verticieToSpherePos, sphereCenter, waistWidth, 
                                                                 meshRootTf, preMorphSphereCenter, sphereRadius, backExtentPos, 
                                                                 topExtentPos, sphereCenterLs, pmSphereCenterLs, backExtentPosLs, 
                                                                 topExtentPosLs);                    
                        inflatedVerts[i] = meshRootTf.InverseTransformPoint(inflatedVertWs);//Convert back to local space
                    }
                    else 
                    {                        
                        inflatedVerts[i] = origVert;
                    }
                }
                else 
                {
                    inflatedVerts[i] = origVert;
                }
                
                currentVerts[i] = origVert;
            }      

            return true;                 
        }


        /// <summary>
        /// Get the root position of the mesh, so we can calculate the true position of its mesh verticies later
        /// </summary>
        internal void GetMeshRoot(out Transform meshRootTf, out float distanceMoved) 
        {                   
            distanceMoved = 0f;
            meshRootTf = null;

            #if KK
                //Get normal mesh root attachment position, and if its not near 0,0,0 fix it so that it is (Match it to the chacontrol y pos)
                var kkMeshRoot = PregnancyPlusHelper.GetBoneGO(ChaControl, "p_cf_body_00.cf_o_root");
                if (kkMeshRoot == null) return;                
                
                //If the mesh root y is too far from the ChaControl origin
                if (ChaControl.transform.InverseTransformPoint(kkMeshRoot.transform.position).y > 0.01f)
                {
                    // if (PregnancyPlusPlugin.debugLog) PregnancyPlusPlugin.Logger.LogInfo($"$ GetMeshRoot pos {kkMeshRoot.transform.position}");
                    // if (PregnancyPlusPlugin.debugLog) PregnancyPlusPlugin.Logger.LogInfo($"$ char pos {ChaControl.transform.position}");
                    distanceMoved = FastDistance(ChaControl.transform.position, kkMeshRoot.transform.position);
                    if (PregnancyPlusPlugin.debugLog) PregnancyPlusPlugin.Logger.LogInfo($" MeshRoot moved to charRoot by {distanceMoved}f");

                    //Set the meshroot.pos to the chaControl.pos to make it more in line with HS2/AI, and KK Uncensor mesh
                    kkMeshRoot.transform.position = ChaControl.transform.position;
                    bellyInfo.MeshRootDidMove = true;

                    // if (PregnancyPlusPlugin.debugLog) PregnancyPlusPlugin.Logger.LogInfo($"$ GetMeshRoot pos after {meshRoot.transform.position}");                    
                }     

                meshRootTf = kkMeshRoot.transform;           
            
            #elif HS2 || AI
                //For HS2, get the equivalent position game object (near bellybutton)
                var meshRootGo = PregnancyPlusHelper.GetBoneGO(ChaControl, "n_o_root");
                if (meshRootGo == null) return;
                meshRootTf = meshRootGo.transform;

            #endif            
        }


        /// <summary>
        /// Calculates the position of the inflation sphere.  It appends some users selected slider values as well.  This sure got messy fast
        /// </summary>
        /// <param name="boneOrMeshTf">The transform that defined the center of the sphere X, Y, and Z for KK and X, Z for HS2 with calculated Y</param>
        /// <param name="isClothingMesh"></param>
        internal Vector3 GetSphereCenter(Transform boneOrMeshTf, bool isClothingMesh = false) 
        { 
            
            //Sphere slider adjustments need to be transformed to local space first to eliminate any character rotation in world space   
            var bbHeight = GetBellyButtonLocalHeight(boneOrMeshTf);
            bellyInfo.BellyButtonHeight = bbHeight;//Store for later use
            Vector3 bellyButtonPos = boneOrMeshTf.up * bbHeight; 
            Vector3 sphereCenter = boneOrMeshTf.position + bellyButtonPos + GetUserMoveTransform(boneOrMeshTf) + GetBellyButtonOffsetVector(boneOrMeshTf, bbHeight);                                 

            if (PregnancyPlusPlugin.debugLog) PregnancyPlusPlugin.Logger.LogInfo($" bbHeight {bbHeight}");            
            if (PregnancyPlusPlugin.debugLog) PregnancyPlusPlugin.Logger.LogInfo($" sphereCenter {sphereCenter} meshRoot {boneOrMeshTf.position} char origin {ChaControl.transform.position}");            
            return sphereCenter;
        }


        /// <summary>
        /// This will take the sphere-ified verticies and apply smoothing to them via Lerps, to remove sharp edges, and make the belly more belly like
        /// </summary>
        /// <param name="originalVertice">The original verticie position</param>
        /// <param name="inflatedVerticie">The target verticie position, after sphere-ifying</param>
        /// <param name="sphereCenterPos">The center of the imaginary sphere</param>
        /// <param name="waistWidth">The characters waist width that limits the width of the belly (future implementation)</param>
        /// <param name="meshRootTf">The transform used to convert a mesh vector from local space to worldspace and back, also servers as the point where we want to stop making mesh changes when Z < 0</param>
        internal Vector3 SculptInflatedVerticie(Vector3 originalVerticeWs, Vector3 inflatedVerticieWs, Vector3 sphereCenterWs, 
                                                float waistWidth, Transform meshRootTf, Vector3 preMorphSphereCenterWs, float sphereRadius, 
                                                Vector3 backExtentPos, Vector3 topExtentPos, Vector3 sphereCenterLs, Vector3 pmSphereCenterLs, 
                                                Vector3 backExtentPosLs, Vector3 topExtentPosLs) 
        {
            //No smoothing modification in debug mode
            if (PregnancyPlusPlugin.MakeBalloon.Value || PregnancyPlusPlugin.debugAllVerts) return inflatedVerticieWs;                       
            
            //get the smoothing distance limits so we don't have weird polygons and shapes on the edges, and prevents morphs from shrinking past original skin boundary
            var pmSkinToCenterDist = Math.Abs(FastDistance(preMorphSphereCenterWs, originalVerticeWs));
            var pmInflatedToCenterDist = Math.Abs(FastDistance(preMorphSphereCenterWs, inflatedVerticieWs));
            var skinToCenterDist = Math.Abs(FastDistance(sphereCenterWs, originalVerticeWs));
            var inflatedToCenterDist = Math.Abs(FastDistance(sphereCenterWs, inflatedVerticieWs));
            
            // PregnancyPlusPlugin.Logger.LogInfo($" preMorphSphereCenter {preMorphSphereCenter} sphereCenterWs {sphereCenterWs} meshRootTf.pos {meshRootTf.position}");

            //Only apply morphs if the imaginary sphere is outside of the skins boundary (Don't want to shrink anything inwards, only out)
            if (skinToCenterDist >= inflatedToCenterDist || pmSkinToCenterDist > pmInflatedToCenterDist) return originalVerticeWs; 

            //Pre compute some constant Vert values so we dont have to do it for each transform
            //Most all of the measurements below are done in local space to ignore character rotation and position
            var originalVerticeLs = meshRootTf.InverseTransformPoint(originalVerticeWs);
            var inflatedVerticieLs = meshRootTf.InverseTransformPoint(inflatedVerticieWs);

            //Get the base shape with XY plane size limits
            var smoothedVectorLs = SculptBaseShape(meshRootTf, originalVerticeLs, inflatedVerticieLs, sphereCenterLs);      

            //Allow user adjustment of the height and width placement of the belly
            if (GetInflationShiftY() != 0 || GetInflationShiftZ() != 0) 
            {
                smoothedVectorLs = GetUserShiftTransform(meshRootTf, smoothedVectorLs, sphereCenterLs, skinToCenterDist);            
            }

            //Allow user adjustment of the width of the belly
            if (GetInflationStretchX() != 0) 
            {   
                smoothedVectorLs = GetUserStretchXTransform(meshRootTf, smoothedVectorLs, sphereCenterLs);
            }

            //Allow user adjustment of the height of the belly
            if (GetInflationStretchY() != 0) 
            {   
                smoothedVectorLs = GetUserStretchYTransform(meshRootTf, smoothedVectorLs, sphereCenterLs);
            }

            if (GetInflationRoundness() != 0) 
            {  
                smoothedVectorLs = GetUserRoundnessTransform(meshRootTf, originalVerticeLs, smoothedVectorLs, sphereCenterLs);
            }

            //Allow user adjustment of the egg like shape of the belly
            if (GetInflationTaperY() != 0) 
            {
                smoothedVectorLs = GetUserTaperYTransform(meshRootTf, smoothedVectorLs, sphereCenterLs, skinToCenterDist);
            }

            //Allow user adjustment of the front angle of the belly
            if (GetInflationTaperZ() != 0) 
            {
                smoothedVectorLs = GetUserTaperZTransform(meshRootTf, originalVerticeLs, smoothedVectorLs, sphereCenterLs, skinToCenterDist, backExtentPosLs);
            }

            //Allow user adjustment of the fat fold line through the middle of the belly
            if (GetInflationFatFold() > 0) 
            {
                smoothedVectorLs = GetUserFatFoldTransform(meshRootTf, originalVerticeLs, smoothedVectorLs, sphereCenterLs, sphereRadius);
            }


            //After all user transforms are applied, remove the edges from the sides/top of the belly
            smoothedVectorLs = RoundToSides(meshRootTf, originalVerticeLs, smoothedVectorLs, backExtentPosLs);


            //Less skin stretching under breast area with large slider values
            if (originalVerticeLs.y > pmSphereCenterLs.y)
            {                
                smoothedVectorLs = ReduceRibStretchingZ(meshRootTf, originalVerticeLs, smoothedVectorLs, topExtentPosLs);
            }

            // //Experimental, move more polygons to the front of the belly at max, Measured by trying to keep belly button size the same at 0 and max inflation size
            // var bellyTipZ = (center.z + maxSphereRadius);
            // if (smoothedVector.z >= center.z)
            // {
            //     var invertLerpScale = smoothedVector.z/bellyTipZ - 0.75f;
            //     var bellyTipVector = new Vector3(0, center.y, bellyTipZ);
            //     //lerp towards belly point
            //     smoothedVector = Vector3.Slerp(smoothedVector, bellyTipVector, invertLerpScale);
            // }


            //At this point if the smoothed vector is the originalVector just return it
            if (smoothedVectorLs.Equals(originalVerticeLs)) return meshRootTf.TransformPoint(smoothedVectorLs);


            //**** All of the below are post mesh change checks to make sure the vertex position don't go outside of bounds

            //Smoothed back to workdspace
            var smoothedVectorWs = meshRootTf.TransformPoint(smoothedVectorLs);
            var currentVectorDistance = Math.Abs(FastDistance(sphereCenterWs, smoothedVectorWs));
            var pmCurrentVectorDistance = Math.Abs(FastDistance(preMorphSphereCenterWs, smoothedVectorWs));     
            //Get core point on the same y plane as the original vert
            var coreLineVertWs = meshRootTf.position + meshRootTf.up * (meshRootTf.InverseTransformPoint(originalVerticeWs).y * bellyInfo.TotalCharScale.y);
            var origCoreDist = Math.Abs(FastDistance(originalVerticeWs, coreLineVertWs));//Get line from feet to head that verts must respect distance from
            //Get core point on the same y plane as the smoothed vert
            var coreLineSmoothedVertWs = meshRootTf.position + meshRootTf.up * (meshRootTf.InverseTransformPoint(smoothedVectorWs).y * bellyInfo.TotalCharScale.y);       
            var currentCoreDist = Math.Abs(FastDistance(smoothedVectorWs, coreLineSmoothedVertWs)); 

            //Don't allow any morphs to shrink towards the sphere center more than its original distance, only outward morphs allowed
            if (skinToCenterDist > currentVectorDistance || pmSkinToCenterDist > pmCurrentVectorDistance) 
            {
                return originalVerticeWs;
            }

            //Don't allow any morphs to shrink towards the characters core any more than the original distance
            if (currentCoreDist < origCoreDist) 
            {
                //Since this is just an XZ distance plane check, don't modify the new y value
                return new Vector3(originalVerticeWs.x, smoothedVectorWs.y, originalVerticeWs.z);
            }

            //Don't allow any morphs to move behind the character's.z = 0 + extentOffset position, otherwise skin sometimes pokes out the back side :/
            if (backExtentPosLs.z > smoothedVectorLs.z) 
            {
                return originalVerticeWs;
            }

            //Don't allow any morphs to move behind the original verticie z position (ignoring ones already behind sphere center)
            if (originalVerticeLs.z > smoothedVectorLs.z && originalVerticeLs.z > sphereCenterLs.z) 
            {
                //Get the average(not really average) x and y change to move the new position halfway back to the oiriginal vert (hopefullt less strange triangles near belly to body edge)
                var yChangeAvg = (smoothedVectorWs.y - originalVerticeWs.y)/3;
                var xChangeAvg = (smoothedVectorWs.x - originalVerticeWs.x)/3;
                smoothedVectorWs = new Vector3(smoothedVectorWs.x - xChangeAvg, smoothedVectorWs.y - yChangeAvg, originalVerticeWs.z);
            }

            return smoothedVectorWs;             
        }
    

        /// <summary>
        /// This will get all of the indexes of verticies that have a weight attached to a belly bone (bone filter).
        /// This lets us filter out all other verticies since we only care about the belly anyway. Saves on compute time over all.
        /// </summary>
        /// <param name="skinnedMeshRenderer">The target mesh renderer</param>
        /// <param name="boneFilters">The bones that must have weights, if none are passed it will get all bone indexes</param>
        /// <returns>Returns True if any verticies are found with matching boneFilter</returns>
        internal bool GetFilteredVerticieIndexes(SkinnedMeshRenderer skinnedMeshRenderer, string[] boneFilters) 
        {
            var sharedMesh = skinnedMeshRenderer.sharedMesh;
            var renderKey = GetMeshKey(skinnedMeshRenderer);
            var bones = skinnedMeshRenderer.bones;
            var bellyBoneIndexes = new List<int>();
            var hasBellyVerticies = false;            

            if (!sharedMesh.isReadable) 
            {
                if (PregnancyPlusPlugin.debugLog)  PregnancyPlusPlugin.Logger.LogInfo(
                     $"GetFilteredVerticieIndexes > smr '{renderKey}' is not readable, skipping");
                    return false;
            }

            //return early if no bone weights found
            if (sharedMesh.boneWeights.Length == 0) return false; 

            var indexesFound = GetFilteredBoneIndexes(bones, boneFilters, bellyBoneIndexes);
            if (!indexesFound) return false;             

            //Create new mesh dictionary key for bone indexes
            bellyVerticieIndexes[renderKey] = new bool[sharedMesh.vertexCount];
            alteredVerticieIndexes[renderKey] = new bool[sharedMesh.vertexCount];
            var bellyVertIndex = bellyVerticieIndexes[renderKey];

            var verticies = sharedMesh.vertices;

            //The distance backwards from characters center that verts are allowed to be modified
            var backExtent = bellyInfo.ZLimit;
            
            var c = 0;
            var meshBoneWeights = sharedMesh.boneWeights;
            foreach (BoneWeight bw in meshBoneWeights) 
            {
                int[] boneIndicies = new int[] { bw.boneIndex0, bw.boneIndex1, bw.boneIndex2, bw.boneIndex3 };
                float[] boneWeights = new float[] { bw.weight0, bw.weight1, bw.weight2, bw.weight3 };

                //For each bone weight
                for (int i = 0; i < 4; i++)
                {                    
                    //If it has a weight, and the bone is a belly bone. Weight goes (0-1f) Ignore 0 and maybe filter below 0.1 as well
                    //Include all if debug = true
                    if ((boneWeights[i] > 0.02f && bellyBoneIndexes.Contains(boneIndicies[i]) || PregnancyPlusPlugin.MakeBalloon.Value))
                    {
                        //Make sure to exclude verticies on characters back, we only want to modify the front.  No back bellies!
                        //add all vertexes in debug mode
                        if (verticies[c].z >= 0 - backExtent || PregnancyPlusPlugin.MakeBalloon.Value) 
                        {
                            bellyVertIndex[c] = true;
                            hasBellyVerticies = true;
                            break;
                        }                        
                    }                
                }
                c++;//lol                                          
            }

            //Dont need to remember this mesh if there are no belly verts in it
            if (!hasBellyVerticies) 
            {
                // PregnancyPlusPlugin.Logger.LogInfo($"bellyVerticieIndexes > removing {renderKey}"); 
                RemoveRenderKey(renderKey);
            }

            return hasBellyVerticies;
        }

        /// <summary>
        /// From a list of bone filters, get all the bone indexes that have matching bone names
        /// </summary>
        /// <param name="bones">the mesh's bones list</param>
        /// <param name="boneFilters">The bones that must have weights, if none are passed it will get all bone indexes</param>
        /// <param name="bellyBoneIndexes">Where we store the matching index values</param>
        /// <returns>Returns false if no bones found, or no indexes found</returns>
        internal bool GetFilteredBoneIndexes(Transform[] bones, string[] boneFilters, List<int> bellyBoneIndexes) {
            //Don't even know if this is possible, so why not
            if (bones.Length <= 0) return false;
            var hasBoneFilters = boneFilters != null && boneFilters.Length > 0;

            var bonesLength = bones.Length;

            //For each bone, see if it matches a belly boneFilter
            for (int i = 0; i < bonesLength; i++)
            {   
                if (!bones[i]) continue;  

                //Get all the bone indexes if no filters are used              
                if (!hasBoneFilters) {
                    bellyBoneIndexes.Add(i);
                    continue;
                }

                var boneName = bones[i].name;

                //If the current bone matches the current boneFilter, add it's index
                foreach(var boneFilter in boneFilters)
                {
                    if (boneFilter == boneName) 
                    {
                        bellyBoneIndexes.Add(i);
                        break;
                    }  
                }
            }
            
            return bellyBoneIndexes.Count > 0;
        }
                
    }
}


