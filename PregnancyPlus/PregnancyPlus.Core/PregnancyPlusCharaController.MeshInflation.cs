using KKAPI;
using KKAPI.Chara;
using UnityEngine;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UniRx;
#if HS2 || AI
    using AIChara;
#endif

namespace KK_PregnancyPlus
{

    //This partial class contains the mesh inflation logic
    public partial class PregnancyPlusCharaController: CharaCustomFunctionController
    {           

        public class BellyInfo {
            public float WaistWidth;
            public float WaistHeight;
            public float SphereRadius;
            public float OriginalSphereRadius;
            public Vector3 CharacterScale;
            public bool IsInitialized {
                get { return WaistWidth > 0 && WaistHeight > 0; }
            }

            internal BellyInfo(float waistWidth, float waistHeight, float sphereRadius, float originalSphereRadius, Vector3 characterScale) {
                WaistWidth = waistWidth;
                WaistHeight = waistHeight;
                SphereRadius = sphereRadius;
                OriginalSphereRadius = originalSphereRadius;
                CharacterScale = characterScale;
            }

        }

        /// <summary>
        /// Triggers belly mesh inflation for the current ChaControl.  
        /// It will check the inflationSize dictionary for a valid value (last set via config slider or MeshInflate(value))
        /// If size 0 is used it will clear all active mesh inflations
        /// This will not run twice for the same parameters, a change of config value is required
        /// </summary>
        /// <param name="reRunWithCurrentParams">Lets you force bypass the check for values changed</param>
        /// <returns>Will return True if the mesh was altered and False if not</returns>
        public bool MeshInflate(bool reRunWithCurrentParams = false, bool forceRecalcVerts = false)
        {
            if (ChaControl.objBodyBone == null) return false;//Make sure chatacter objs exists first  
            if (ChaControl.sex == 0) return false;//Only females            

            var sliderHaveChanged = NeedsMeshUpdate();
            //Only continue if one of the config values changed
            if (!sliderHaveChanged) {
                //Only stop here, if no recalculation needed
                if (!forceRecalcVerts && !reRunWithCurrentParams) {
                    return false; 
                }
            }
            ResetInflation();

            if (!AllowedToInflate()) return false;//if outside studio/maker, make sure StoryMode is enabled first
            if (!infConfig.GameplayEnabled) return false;//Only if gameplay enabled

            if (forceRecalcVerts) {
                //Resets all stored vert values, so the script will have to recalculate all from base body
                var keyList = new List<string>(originalVertices.Keys);
                RemoveRenderKeys(keyList);
            }

            //Only continue when size above 0
            if (infConfig.inflationSize <= 0) {
                infConfigHistory.inflationSize = 0;
                return false;                                
            }
            
            if (PregnancyPlusPlugin.debugLog)  PregnancyPlusPlugin.Logger.LogInfo($" ---------- ");
            
            var measuerments = MeasureWaist(ChaControl);                     
            var waistWidth = measuerments.Item1; 
            var sphereRadius = measuerments.Item2;            
            if (waistWidth <= 0 || sphereRadius <= 0) return false;
            
            var anyMeshChanges = false;

            //Get and apply all clothes render mesh changes
            var clothRenderers = PregnancyPlusHelper.GetMeshRenderers(ChaControl.objClothes);
            foreach(var skinnedMeshRenderer in clothRenderers) 
            {                
                if (NeedsComputeVerts(skinnedMeshRenderer, sliderHaveChanged)){
                    var didCompute = ComputeMeshVerts(skinnedMeshRenderer, sphereRadius, waistWidth, true);
                    if (!didCompute) continue;    
                }

                var appliedClothMeshChanges = ApplyInflation(skinnedMeshRenderer, GetMeshKey(skinnedMeshRenderer));
                if (appliedClothMeshChanges) anyMeshChanges = true;
            }             

            //do the same for body meshs
            var bodyRenderers = PregnancyPlusHelper.GetMeshRenderers(ChaControl.objBody);
            foreach(var skinnedMeshRenderer in bodyRenderers) 
            {
                if (NeedsComputeVerts(skinnedMeshRenderer, sliderHaveChanged)){
                    var didCompute = ComputeMeshVerts(skinnedMeshRenderer, sphereRadius, waistWidth);  
                    if (!didCompute) continue;
                }
                
                var appliedBodyMeshChanges = ApplyInflation(skinnedMeshRenderer, GetMeshKey(skinnedMeshRenderer));
                if (appliedBodyMeshChanges) anyMeshChanges = true;                      
            }

            //If any changes were applied, updated the last used shape for the Restore gui button
            if (infConfig.HasAnyValue()) {
                PregnancyPlusPlugin.lastBellyState = (PregnancyPlusData)infConfig.Clone();//CLone so we don't accidently overwright the lastState
            }

            //Update history when changes made
            if (anyMeshChanges) infConfigHistory = (PregnancyPlusData)infConfig.Clone();

            return anyMeshChanges;
        }


        /// <summary>
        /// Get the characters waist width and calculate the appropriate sphere radius from it
        /// </summary>
        /// <param name="chaControl">The character to measure</param>
        internal Tuple<float, float> MeasureWaist(ChaControl chaControl) 
        {
            var charScale = PregnancyPlusHelper.GetBodyTopScale(ChaControl);

            #if KK
                var ribName = "cf_s_spine02";
                var waistName = "cf_s_waist02";
            #elif HS2 || AI
                var ribName = "cf_J_Spine02_s";
                var waistName = "cf_J_Kosi02";
            #endif            

            //Get the characters Y bones to measure from
            var ribBone = PregnancyPlusHelper.GetBone(ChaControl, ribName);
            var waistBone = PregnancyPlusHelper.GetBone(ChaControl, waistName);
            if (ribBone == null || waistBone == null) return Tuple.Create<float, float>(0, 0);

            var waistToRibDist = waistBone.transform.InverseTransformPoint(ribBone.position).y;
            if (PregnancyPlusPlugin.debugLog)  PregnancyPlusPlugin.Logger.LogInfo($" waistToRibDist {waistToRibDist}");

            #if KK
                var thighLName = "cf_j_thigh00_L";
                var thighRName = "cf_j_thigh00_R";                    
            #elif HS2 || AI
                var thighLName = "cf_J_LegUp00_L";
                var thighRName = "cf_J_LegUp00_R";
            #endif

            //Get the characters X bones to measure from, in localspace to ignore n_height scale
            var thighLBone = PregnancyPlusHelper.GetBone(ChaControl, thighLName);
            var thighRBone = PregnancyPlusHelper.GetBone(ChaControl, thighRName);
            if (thighLBone == null || thighRBone == null) return Tuple.Create<float, float>(0, 0);
            
            var waistWidth = Math.Abs(thighLBone.transform.InverseTransformPoint(thighLBone.position).x - thighLBone.transform.InverseTransformPoint(thighRBone.position).x); 
            if (PregnancyPlusPlugin.debugLog)  PregnancyPlusPlugin.Logger.LogInfo($" waistWidth {waistWidth}");

            //Calculate sphere radius based on distance from waist to ribs (seems big, but lerping later will trim much of it), added Math.Min for skinny waists
            var sphereRadius = Math.Min(waistToRibDist/1.25f, waistWidth/1.3f) * charScale.y; 
            var sphereRadiusMultiplied = sphereRadius * (GetInflationMultiplier() + 1);   

            bellyInfo = new BellyInfo(waistWidth, waistToRibDist, sphereRadiusMultiplied, sphereRadius, charScale);

            if (PregnancyPlusPlugin.debugLog)  PregnancyPlusPlugin.Logger.LogInfo($" scaled waistToRibDist {waistToRibDist} scaled waistWidth {waistWidth} sphereRadiusM {sphereRadiusMultiplied}");            

            return Tuple.Create(waistWidth, sphereRadiusMultiplied);
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

            var meshRootTf = GetMeshRoot();

            if (meshRootTf == null) return false;
                        
            //set sphere center and allow for adjusting its position from the UI sliders  
            Vector3 sphereCenter = GetSphereCenter(meshRootTf, isClothingMesh);
            var needsPositionFix = smr.transform.position != meshRootTf.position;                        

#region Fixes for different mesh localspace positions between KK and HS2/AI
            #if KK            
                var isDefaultBody = !PregnancyPlusHelper.IsUncensorBody(ChaControl, UncensorCOMName, DefaultBodyFemaleGUID); 
                if (isClothingMesh) 
                {
                    //KK just has to have strange vert positions, so we have to use adjust the sphere center location for body and clothes
                    var clothesMeshRoot = PregnancyPlusHelper.GetBone(ChaControl, "cf_o_root").position;
                    //Get distance from bb to clothesMeshRoot if needs fix
                    clothSphereCenterOffset = needsPositionFix ? sphereCenter - meshRootTf.up * FastDistance(clothesMeshRoot, sphereCenter) * 1.021f : sphereCenter;//At belly button - meshRoot position (plus some tiny dumb offset that I cant find the source of)
                    sphereCenter = needsPositionFix ? clothSphereCenterOffset : sphereCenter;
                } 
                else if (isDefaultBody) 
                {
                    bodySphereCenterOffset = meshRootTf.position + GetUserMoveTransform(meshRootTf) + meshRootTf.up * -0.021f;////at 0,0,0, once again what is this crazy small offset?
                    sphereCenter = meshRootTf.position + GetUserMoveTransform(meshRootTf);//at belly button - meshRoot position
                }
                else 
                {
                    //For uncensor body mesh
                    clothSphereCenterOffset = bodySphereCenterOffset = sphereCenter;//at belly button
                }
                if (PregnancyPlusPlugin.debugLog) PregnancyPlusPlugin.Logger.LogInfo($" corrected sphereCenter {sphereCenter} isDefaultBody {isDefaultBody}");

            #elif (HS2 || AI)
                //Its so simple when its not KK default mesh :/
                clothSphereCenterOffset = bodySphereCenterOffset = sphereCenter;
            #endif    
#endregion        

            if (PregnancyPlusPlugin.debugLog)  PregnancyPlusPlugin.Logger.LogInfo($" isClothingMesh {isClothingMesh} needsPositionFix {needsPositionFix} ");

            var rendererName = GetMeshKey(smr);         
            originalVertices[rendererName] = smr.sharedMesh.vertices;
            inflatedVertices[rendererName] = new Vector3[originalVertices[rendererName].Length];
            currentVertices[rendererName] = new Vector3[originalVertices[rendererName].Length];

            var origVerts = originalVertices[rendererName];
            var inflatedVerts = inflatedVertices[rendererName];
            var currentVerts = currentVertices[rendererName];
            var bellyVertIndex = bellyVerticieIndexes[rendererName];    

            //Pre compute some values needed by SculptInflatedVerticie
            var preMorphSphereCenter = sphereCenter - GetUserMoveTransform(meshRootTf);

            var vertsLength = origVerts.Length;
            //Set each verticies inflated postion, with some constraints (SculptInflatedVerticie) to make it look more natural
            for (int i = 0; i < vertsLength; i++)
            {
                var origVert = origVerts[i];

                //Only care about inflating belly verticies
                if (bellyVertIndex[i]) 
                {                    
                    var origVertWS = meshRootTf.TransformPoint(origVerts[i]);//Convert to worldspace 
                    var originIsInsideRadius = FastDistance(origVertWS, sphereCenter) <= sphereRadius;

                    //Ignore verts outside the sphere radius
                    if (originIsInsideRadius) {
                        Vector3 inflatedVertWS;                    
                        Vector3 verticieToSphere;                                                             

                        //Shift each belly vertex away from sphere center in a sphere pattern
                        if (!isClothingMesh) 
                        {                        
                            //You have to normalize to sphere center instead of 0,0,0.  This way the belly will expand out as expected.  So shift all mesh verts to be origin at sphereCenter first, then normalize, then shift back
                            verticieToSphere = (origVertWS - bodySphereCenterOffset).normalized * sphereRadius + bodySphereCenterOffset;
                        }
                        else 
                        {                       
                            float reduceClothFlattenOffset = GetClothesFixOffset(clothSphereCenterOffset, sphereRadius, waistWidth, origVertWS, smr.name);//Reduce cloth flattening at largest inflation values 
                            verticieToSphere = (origVertWS - clothSphereCenterOffset).normalized * (sphereRadius + reduceClothFlattenOffset) + clothSphereCenterOffset;
                        }     

                        //Make adjustments to the shape, and feed in user slider input
                        inflatedVertWS =  SculptInflatedVerticie(origVertWS, verticieToSphere, sphereCenter, waistWidth, meshRootTf, preMorphSphereCenter, sphereRadius);                    
                        inflatedVerts[i] = meshRootTf.InverseTransformPoint(inflatedVertWS);//Convert back to local space
                        // if (i % 100 == 0) PregnancyPlusPlugin.Logger.LogInfo($" origVertWS {origVertWS}  verticieToSphere {verticieToSphere}");
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
        internal Transform GetMeshRoot() 
        {                                
            #if KK
                //Get normal mesh root attachment position, and if its not near 0,0,0 fix it so that it is
                var kkMeshRoot = PregnancyPlusHelper.GetBoneGO(ChaControl, "cf_o_root");
                if (!kkMeshRoot) return null;
                
                var meshRoot = kkMeshRoot.transform;
                meshRoot.transform.position = kkMeshRoot.transform.position + kkMeshRoot.transform.up * (-ChaControl.transform.InverseTransformPoint(kkMeshRoot.transform.position).y);
            
            #elif HS2 || AI
                //For HS2, get the equivalent position game object (near bellybutton)
                var meshRootGo = PregnancyPlusHelper.GetBoneGO(ChaControl, "n_o_root");
                if (!meshRootGo) return null;
                var meshRoot = meshRootGo.transform;
            #endif

            return meshRoot;
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
            Vector3 bellyButtonPos = boneOrMeshTf.up * bbHeight; 
            Vector3 sphereCenter = boneOrMeshTf.position + bellyButtonPos + GetUserMoveTransform(boneOrMeshTf) + GetBellyButtonOffset(boneOrMeshTf, bbHeight);                                 

            if (PregnancyPlusPlugin.debugLog) PregnancyPlusPlugin.Logger.LogInfo($" sphereCenter {sphereCenter} meshRoot {boneOrMeshTf.position} char origin {ChaControl.transform.position}");
            if (PregnancyPlusPlugin.debugLog) PregnancyPlusPlugin.Logger.LogInfo($" bellyButtonPos {bellyButtonPos} bbHeight {bbHeight}");            
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
        internal Vector3 SculptInflatedVerticie(Vector3 originalVertice, Vector3 inflatedVerticie, Vector3 sphereCenterPos, float waistWidth, Transform meshRootTf, Vector3 preMorphSphereCenter, float sphereRadius) 
        {
            //No smoothing modification in debug mode
            if (PregnancyPlusPlugin.MakeBalloon.Value) return inflatedVerticie;            
            
            //get the smoothing distance limits so we don't have weird polygons and shapes on the edges, and prevents morphs from shrinking past original skin boundary
            var pmSkinToCenterDist = Math.Abs(FastDistance(preMorphSphereCenter, originalVertice));
            var pmInflatedToCenterDist = Math.Abs(FastDistance(preMorphSphereCenter, inflatedVerticie));
            var skinToCenterDist = Math.Abs(FastDistance(sphereCenterPos, originalVertice));
            var inflatedToCenterDist = Math.Abs(FastDistance(sphereCenterPos, inflatedVerticie));
            
            // PregnancyPlusPlugin.Logger.LogInfo($" preMorphSphereCenter {preMorphSphereCenter} sphereCenterPos {sphereCenterPos} meshRootTf.pos {meshRootTf.position}");

            //Only apply morphs if the imaginary sphere is outside of the skins boundary (Don't want to shrink anything inwards, only out)
            if (skinToCenterDist >= inflatedToCenterDist || pmSkinToCenterDist > pmInflatedToCenterDist) return originalVertice; 

            //Get the base shape with XY plane size limits
            var smoothedVector = SculptBaseShape(meshRootTf, originalVertice, inflatedVerticie, sphereCenterPos);       

            //Allow user adjustment of the height and width placement of the belly
            if (GetInflationShiftY() != 0 || GetInflationShiftZ() != 0) {
                smoothedVector = GetUserShiftTransform(meshRootTf, smoothedVector, sphereCenterPos, skinToCenterDist);            
            }

            //Allow user adjustment of the width of the belly
            if (GetInflationStretchX() != 0) {   
                smoothedVector = GetUserStretchXTransform(meshRootTf, smoothedVector, sphereCenterPos);
            }

            //Allow user adjustment of the height of the belly
            if (GetInflationStretchY() != 0) {   
                smoothedVector = GetUserStretchYTransform(meshRootTf, smoothedVector, sphereCenterPos);
            }

            //Allow user adjustment of the egg like shape of the belly
            if (GetInflationTaperY() != 0) {
                smoothedVector = GetUserTaperYTransform(meshRootTf, smoothedVector, sphereCenterPos, skinToCenterDist);
            }

            //Allow user adjustment of the front angle of the belly
            if (GetInflationTaperZ() != 0) {
                smoothedVector = GetUserTaperZTransform(meshRootTf, smoothedVector, sphereCenterPos, skinToCenterDist);
            }

            //After all user transforms are applied, remove the edges from the sides/top of the belly
            smoothedVector = RoundToSides(meshRootTf, originalVertice, smoothedVector, sphereCenterPos, inflatedToCenterDist);

            // //Experimental, move more polygons to the front of the belly at max, Measured by trying to keep belly button size the same at 0 and max inflation size
            // var bellyTipZ = (center.z + maxSphereRadius);
            // if (smoothedVector.z >= center.z)
            // {
            //     var invertLerpScale = smoothedVector.z/bellyTipZ - 0.75f;
            //     var bellyTipVector = new Vector3(0, center.y, bellyTipZ);
            //     //lerp towards belly point
            //     smoothedVector = Vector3.Slerp(smoothedVector, bellyTipVector, invertLerpScale);
            // }


            //**** All of the below are post mesh change checks to make sure the vertex position don't go outside of bounds

            var currentVectorDistance = Math.Abs(FastDistance(sphereCenterPos, smoothedVector));
            var pmCurrentVectorDistance = Math.Abs(FastDistance(preMorphSphereCenter, smoothedVector));
            var smoothedVectorLs = meshRootTf.InverseTransformPoint(smoothedVector);

            //Don't allow any morphs to shrink skin smaller than its original position, only outward morphs allowed (check this after all morphs)
            if (skinToCenterDist > currentVectorDistance || pmSkinToCenterDist > pmCurrentVectorDistance) {
                return originalVertice;
            }

            //Don't allow any morphs to move behind the character's.z = 0 position, otherwise skin sometimes pokes out the back side :/
            if (meshRootTf.InverseTransformPoint(meshRootTf.position).z > smoothedVectorLs.z) {
                return originalVertice;
            }

            //Don't allow any morphs to move behind the original verticie z = 0 position
            if (meshRootTf.InverseTransformPoint(originalVertice).z > smoothedVectorLs.z) {
                //Get the average x and y change to move the new position halfway back to the oiriginal vert (hopefullt less strange triangles near belly to body edge)
                var yChangeAvg = (smoothedVector.y - originalVertice.y)/3;
                var xChangeAvg = (smoothedVector.x - originalVertice.x)/3;
                smoothedVector = new Vector3(smoothedVector.x - xChangeAvg, smoothedVector.y - yChangeAvg, originalVertice.z);
            }

            //TODO at this point we really need some form of final mesh smoothing pass for where the belly meets the body to remove the sharp edges that the transforms above create.
            //Just don't want to make the sliders any slower than they already are

            return smoothedVector;             
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
            var hasBoneFilters = boneFilters != null && boneFilters.Length > 0;

            if (!sharedMesh.isReadable) {
                if (PregnancyPlusPlugin.debugLog)  PregnancyPlusPlugin.Logger.LogInfo(
                     $"GetFilteredVerticieIndexes > smr '{renderKey}' is not readable, skipping");
                    return false;
            }

            //Don't even know if this is possible, so why not
            if (bones.Length <= 0) return false;

            var bonesLength = bones.Length;

            //For each bone, see if it matches a belly boneFilter
            for (int i = 0; i < bonesLength; i++)
            {   
                if (!bones[i]) continue;                
                if (!hasBoneFilters) {
                    bellyBoneIndexes.Add(i);
                    continue;
                }

                var boneName = bones[i].name;

                foreach(var boneFilter in boneFilters)
                {
                    if (boneFilter == boneName) {
                        bellyBoneIndexes.Add(i);
                        break;
                    }  
                }
            }

            //return early if no filtered weights found
            if (sharedMesh.boneWeights.Length == 0) return false;              

            //Create new mesh dictionary key for bone indexes
            bellyVerticieIndexes[renderKey] = new bool[sharedMesh.vertexCount];
            var bellyVertIndex = bellyVerticieIndexes[renderKey];

            var boneWeightsLength = sharedMesh.boneWeights.Length;
            var bbIndexCount = bellyBoneIndexes.Count;
            var verticies = sharedMesh.vertices;
            
            //For each weight, see if it has a weight above 0, meaning it is affected by a bone
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
                    if ((boneWeights[i] > 0.05f && bellyBoneIndexes.Contains(boneIndicies[i]) || PregnancyPlusPlugin.MakeBalloon.Value))
                    {
                        //Make sure to exclude verticies on characters back, we only want to modify the front.  No back bellies!
                        //add all vertexes in debug mode
                        if (verticies[c].z >= 0 || PregnancyPlusPlugin.MakeBalloon.Value) {
                            bellyVertIndex[c] = true;
                            hasBellyVerticies = true;
                            break;
                        }                        
                    }                
                }
                c++;//lol                                          
            }

            //Dont need to remember this mesh if there are no belly verts in it
            if (!hasBellyVerticies) {
                // PregnancyPlusPlugin.Logger.LogInfo($"bellyVerticieIndexes > removing {renderKey}"); 
                RemoveRenderKey(renderKey);
            }

            return hasBellyVerticies;
        }
        

    }
}


