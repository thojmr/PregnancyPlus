using KKAPI;
using KKAPI.Chara;
using UnityEngine;
using System;
using System.Collections;
using System.Collections.Generic;
#if HS2 || AI
    using AIChara;
#endif

namespace KK_PregnancyPlus
{

    //This partial class contains the main mesh inflation logic
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
            
            if (PregnancyPlusPlugin.DebugLog.Value)  PregnancyPlusPlugin.Logger.LogInfo($" ---------- ");
            if (PregnancyPlusPlugin.DebugLog.Value)  PregnancyPlusPlugin.Logger.LogInfo($" inflationSize > {infConfig.inflationSize} for {charaFileName} ");
            
            //Get the measurements that determine the base belly size
            var hasMeasuerments = MeasureWaistAndSphere(ChaControl);                     
            if (!hasMeasuerments) 
            {
                PregnancyPlusPlugin.errorCodeCtrl.LogErrorCode(ChaControl.chaID, ErrorCode.PregPlus_BadMeasurement, 
                    $"Could not get belly measurements from character");
                return false;
            }
            
            var anyMeshChanges = false;            

            //Get and apply all clothes render mesh changes, then do body mesh too
            var clothRenderers = PregnancyPlusHelper.GetMeshRenderers(ChaControl.objClothes);            
            anyMeshChanges = LoopAndApplyMeshChanges(clothRenderers, sliderHaveChanged, anyMeshChanges, true, GetBodyMeshRenderer());          
            var bodyRenderers = PregnancyPlusHelper.GetMeshRenderers(ChaControl.objBody, true);
            anyMeshChanges = LoopAndApplyMeshChanges(bodyRenderers, sliderHaveChanged, anyMeshChanges);

            RemoveMeshCollider();

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
        internal bool LoopAndApplyMeshChanges(List<SkinnedMeshRenderer> smrs, bool sliderHaveChanged, bool anyMeshChanges, 
                                              bool isClothingMesh = false, SkinnedMeshRenderer bodyMeshRenderer = null) 
        {
            foreach(var smr in smrs) 
            {           
                var didCompute = false;  

                //Dont recompute verts if no sliders have changed
                var needsComputeVerts = NeedsComputeVerts(smr, sliderHaveChanged);
                if (needsComputeVerts)
                {
                    didCompute = ComputeMeshVerts(smr, isClothingMesh);                                                                                   
                }

                //Use ray cast for each belly vert to get the clothing offset
                if (isClothingMesh) DoClothMeasurement(smr, bodyMeshRenderer, needsComputeVerts);

                //If mesh fails to compute, skip (mesn.IsReadable = false will cause this) 
                if (needsComputeVerts && !didCompute) continue;

                var appliedMeshChanges = ApplyInflation(smr, GetMeshKey(smr));            
                if (appliedMeshChanges) anyMeshChanges = true;                
            }  

            return anyMeshChanges;
        }


        /// <summary>
        /// Just a helper function to combine searching for verts in a mesh, and then applying the transforms
        /// </summary>
        internal bool ComputeMeshVerts(SkinnedMeshRenderer smr, bool isClothingMesh = false) 
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

            if (PregnancyPlusPlugin.DebugLog.Value) PregnancyPlusPlugin.Logger.LogInfo($" ");
            if (PregnancyPlusPlugin.DebugLog.Value) PregnancyPlusPlugin.Logger.LogInfo($"  ComputeMeshVerts > {smr.name}"); 
            return GetInflatedVerticies(smr, bellyInfo.SphereRadius, bellyInfo.WaistWidth, isClothingMesh);
        }


        /// <summary>
        /// Does the vertex morph calculations to make a sphere out of the belly verticies, and updates the vertex dictionaries apprporiately
        /// </summary>
        /// <param name="skinnedMeshRenderer">The mesh renderer target</param>
        /// <param name="sphereRadius">The radius of the inflation sphere</param>
        /// <param name="waistWidth">The width of the characters waist</param>
        /// <param name="isClothingMesh">Clothing requires a few tweaks to match skin morphs</param>
        /// <returns>Will return True if mesh verticies > 0 were found  Some meshes wont have any verticies for the belly area, returning false</returns>
        internal bool GetInflatedVerticies(SkinnedMeshRenderer smr, float sphereRadius, float waistWidth, bool isClothingMesh = false) 
        {
            Vector3 bodySphereCenterOffset = Vector3.zero;//For defaultt KK body mesh custom offset correction

            if (smr == null) return false;

            //Found out body mesh can be nested under cloth game objects...   Make sure to flag it as non-clothing
            if (smr.name.Contains("o_body_cf") || smr.name.Contains("o_body_a")) 
            {
                PregnancyPlusPlugin.errorCodeCtrl.LogErrorCode(ChaControl.chaID, ErrorCode.PregPlus_BodyMeshDisguisedAsCloth, 
                    $" body mesh {smr.name} was nested under cloth object {smr.transform.parent.name}.  This is usually not an issue.");
                isClothingMesh = false;            
            }

            GetMeshRoot(out Transform meshRootTf, out float meshRootDistMoved);
            if (meshRootTf == null) return false;

            // if (PregnancyPlusPlugin.DebugLog.Value) PregnancyPlusPlugin.Logger.LogInfo($" SMR pos {smr.transform.position} rot {smr.transform.rotation} parent {smr.transform.parent}");
                        
            //set sphere center and allow for adjusting its position from the UI sliders  
            Vector3 sphereCenter = GetSphereCenter(meshRootTf, isClothingMesh);
            ApplyConditionalSphereCenterOffset(meshRootTf, isClothingMesh, sphereCenter, out sphereCenter, out bodySphereCenterOffset);  
            currentMeshSphereCenter = sphereCenter;                

            var rendererName = GetMeshKey(smr);         
            originalVertices[rendererName] = smr.sharedMesh.vertices;
            inflatedVertices[rendererName] = new Vector3[originalVertices[rendererName].Length];
            inflatedVerticesOffsets[rendererName] = new Vector3[originalVertices[rendererName].Length];
            currentVertices[rendererName] = new Vector3[originalVertices[rendererName].Length];
            clothingOffsets[rendererName] = new float[originalVertices[rendererName].Length];

            var origVerts = originalVertices[rendererName];
            var inflatedVerts = inflatedVertices[rendererName];
            var inflatedVertOffsets = inflatedVerticesOffsets[rendererName];
            var currentVerts = currentVertices[rendererName];
            var bellyVertIndex = bellyVerticieIndexes[rendererName];    

            #if DEBUG
                var bellyVertsCount = 0;
                for (int i = 0; i < bellyVertIndex.Length; i++)
                {
                    if (bellyVertIndex[i]) bellyVertsCount++;
                }
                if (PregnancyPlusPlugin.DebugLog.Value) PregnancyPlusPlugin.Logger.LogInfo($" Mesh affected vert count {bellyVertsCount}");
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

            // if (PregnancyPlusPlugin.DebugLog.Value) DebugTools.DrawLineAndAttach(meshRootTf, 5, meshRootTf.InverseTransformPoint(topExtentPos) - meshRootTf.up * GetBellyButtonOffset(bellyInfo.BellyButtonHeight));
            // if (PregnancyPlusPlugin.DebugLog.Value) DebugTools.DrawLineAndAttach(meshRootTf, new Vector3(-3, 0, 0), new Vector3(3, 0, 0), meshRootTf.InverseTransformPoint(backExtentPos) - GetBellyButtonOffsetVector(meshRootTf, bellyInfo.BellyButtonHeight));
            // if (PregnancyPlusPlugin.DebugLog.Value) DebugTools.DrawLineAndAttach(meshRootTf, 5, meshRootTf.InverseTransformPoint(sphereCenter));

            //Set each verticies inflated postion, with some constraints (SculptInflatedVerticie) to make it look more natural
            for (int i = 0; i < vertsLength; i++)
            {
                var origVert = origVerts[i];

                //Only care about inflating belly verticies
                if (bellyVertIndex[i] || PregnancyPlusPlugin.DebugVerts.Value) 
                {                    
                    var origVertWs = smr.transform.TransformPoint(origVerts[i]);//Convert to worldspace 
                    var vertDistance = FastDistance(origVertWs, sphereCenter);

                    CalculateNormalsBoundary(vertDistance, vertNormalCaluRadius, i, rendererName);

                    //Ignore verts outside the sphere radius
                    if (vertDistance <= vertNormalCaluRadius || PregnancyPlusPlugin.DebugVerts.Value) 
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
                            float reduceClothFlattenOffset = GetClothesFixOffset(meshRootTf, sphereCenter, sphereRadius, waistWidth, origVertWs, smr.name);
                            verticieToSpherePos = (origVertWs - sphereCenter).normalized * (sphereRadius + reduceClothFlattenOffset) + sphereCenter;                            
                        }     

                        //Make adjustments to the shape, and feed in user slider input
                        inflatedVertWs =  SculptInflatedVerticie(origVertWs, verticieToSpherePos, sphereCenter, waistWidth, 
                                                                 meshRootTf, preMorphSphereCenter, sphereRadius, backExtentPos, 
                                                                 topExtentPos, sphereCenterLs, pmSphereCenterLs, backExtentPosLs, 
                                                                 topExtentPosLs);                    
                        inflatedVertOffsets[i] = inflatedVerts[i] = smr.transform.InverseTransformPoint(inflatedVertWs);//Convert back to local space
                    }
                    else 
                    {                        
                        inflatedVertOffsets[i] = inflatedVerts[i] = origVert;
                    }
                }
                else 
                {
                    inflatedVertOffsets[i] = inflatedVerts[i] = origVert;
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
                    // if (PregnancyPlusPlugin.DebugLog.Value) PregnancyPlusPlugin.Logger.LogInfo($"$ GetMeshRoot pos {kkMeshRoot.transform.position}");
                    // if (PregnancyPlusPlugin.DebugLog.Value) PregnancyPlusPlugin.Logger.LogInfo($"$ char pos {ChaControl.transform.position}");
                    distanceMoved = FastDistance(ChaControl.transform.position, kkMeshRoot.transform.position);
                    if (PregnancyPlusPlugin.DebugLog.Value) PregnancyPlusPlugin.Logger.LogInfo($" MeshRoot moved to charRoot by {distanceMoved}f");

                    //Set the meshroot.pos to the chaControl.pos to make it more in line with HS2/AI, and KK Uncensor mesh
                    kkMeshRoot.transform.position = ChaControl.transform.position;
                    bellyInfo.MeshRootDidMove = true;

                    // if (PregnancyPlusPlugin.DebugLog.Value) PregnancyPlusPlugin.Logger.LogInfo($"$ GetMeshRoot pos after {meshRoot.transform.position}");                    
                }     

                meshRootTf = kkMeshRoot.transform;           
            
            #elif HS2 || AI
                //For HS2, get the equivalent position game object (near bellybutton)
                var meshRootGo = PregnancyPlusHelper.GetBoneGO(ChaControl, "p_cf_body_00.n_o_root");
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
            var bbHeight = GetBellyButtonLocalHeight();
            bellyInfo.BellyButtonHeight = bbHeight;//Store for later use
            Vector3 bellyButtonPos = boneOrMeshTf.up * bbHeight; 
            Vector3 sphereCenter = boneOrMeshTf.position + bellyButtonPos + GetUserMoveTransform(boneOrMeshTf) + GetBellyButtonOffsetVector(boneOrMeshTf, bbHeight);                                 

            if (PregnancyPlusPlugin.DebugLog.Value) PregnancyPlusPlugin.Logger.LogInfo($" bbHeight {bbHeight}");            
            if (PregnancyPlusPlugin.DebugLog.Value) PregnancyPlusPlugin.Logger.LogInfo($" sphereCenter {sphereCenter} meshRoot {boneOrMeshTf.position} char origin {ChaControl.transform.position}");            
            return sphereCenter;
        }


        /// <summary>
        /// In special cases we need to apply a small offset to the sphereCenter to align the mesh correctly with the other meshes.  Otherwise you get tons of clipping
        ///  Mostly used to fix the default KK body which seems to be mis aligned from uncensor, and AI/HS2 meshes
        /// </summary>
        public void ApplyConditionalSphereCenterOffset(Transform meshRootTf, bool isClothingMesh, Vector3 _sphereCenter, out Vector3 sphereCenter, out Vector3 bodySphereCenterOffset)
        {
            //Fixes for different mesh localspace positions/rotations between KK and HS2/AI
            #if KK            
                //When mesh is the default kk body, we have to adjust the mesh to match some strange offset that comes up
                var isDefaultBody = !PregnancyPlusHelper.IsUncensorBody(ChaControl, UncensorCOMName); 
                var defaultBodyOffsetFix = 0.0277f * bellyInfo.TotalCharScale.y * bellyInfo.NHeightScale.y;//Where does this offset even come from?

                if (!isClothingMesh && isDefaultBody) 
                {
                    bodySphereCenterOffset = meshRootTf.position + GetUserMoveTransform(meshRootTf) - meshRootTf.up * defaultBodyOffsetFix;////at 0,0,0, once again what is this crazy small offset?
                    sphereCenter = meshRootTf.position + GetUserMoveTransform(meshRootTf);//at belly button - offset from meshroot
                }
                else 
                {
                    //For uncensor body mesh, and any clothing
                    bodySphereCenterOffset = sphereCenter = _sphereCenter;//at belly button
                }
                if (PregnancyPlusPlugin.DebugLog.Value) PregnancyPlusPlugin.Logger.LogInfo($" [KK only] corrected sphereCenter {_sphereCenter} isDefaultBody {isDefaultBody}");

            #elif HS2 || AI
                //Its so simple when its not KK default mesh :/
                bodySphereCenterOffset = sphereCenter = _sphereCenter;                
            #endif    
        }


        /// <summary>
        /// This will take the sphere-ified verticies and apply smoothing to them via Lerps to remove sharp edges.  Make the belly more round
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
            if (PregnancyPlusPlugin.MakeBalloon.Value || PregnancyPlusPlugin.DebugVerts.Value) return inflatedVerticieWs;                       
            
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
                
    }
}


