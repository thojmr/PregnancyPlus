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


        /// <summary>
        /// Triggers belly mesh inflation for the current ChaControl.  
        /// It will check the inflationSize dictionary for a valid value (last set via config slider or MeshInflate(value))
        /// If size 0 is used it will clear all active mesh inflations
        /// This will not run twice for the same parameters, a change of config value is required
        /// </summary>
        /// <param name="reRunWithCurrentParams">Lets you force bypass the check for values changed</param>
        /// <returns>Will return True if the mesh was altered and False if not</returns>
        public bool MeshInflate(bool reRunWithCurrentParams = false)
        {
            if (ChaControl.objBodyBone == null) return false;//Make sure chatacter objs exists first   

            //Only continue if one of the config values changed
            if (!NeedsMeshUpdate() && !reRunWithCurrentParams) {                
                return false;            
            }
            ResetInflation();

            //Only continue when size above 0
            if (infConfig.inflationSize <= 0) {
                infConfigHistory.inflationSize = 0;
                return false;                                
            }
                        
            var measuerments = MeasureWaist(ChaControl);         
            var waistWidth = measuerments.Item1; 
            var sphereRadius = measuerments.Item2;
            
            var anyMeshChanges = false;

            //Get and apply all clothes render mesh changes
            var clothRenderers = PregnancyPlusHelper.GetMeshRenderers(ChaControl.objClothes);
            foreach(var skinnedMeshRenderer in clothRenderers) 
            {                
                var foundVerts = ComputeMeshVerts(skinnedMeshRenderer, sphereRadius, waistWidth, true);
                if (!foundVerts) continue;    

                var appliedClothMeshChanges = ApplyInflation(skinnedMeshRenderer, GetMeshKey(skinnedMeshRenderer));
                if (appliedClothMeshChanges) anyMeshChanges = true;
            }             

            //do the same for body meshs
            var bodyRenderers = PregnancyPlusHelper.GetMeshRenderers(ChaControl.objBody);
            foreach(var skinnedMeshRenderer in bodyRenderers) 
            {
                var foundVerts = ComputeMeshVerts(skinnedMeshRenderer, sphereRadius, waistWidth);  
                if (!foundVerts) continue;
                
                var appliedBodyMeshChanges = ApplyInflation(skinnedMeshRenderer, GetMeshKey(skinnedMeshRenderer));
                if (appliedBodyMeshChanges) anyMeshChanges = true;                      
            }

            return anyMeshChanges;
        }


        /// <summary>
        /// Get the characters waist width and calculate the appropriate sphere radius from it
        /// </summary>
        /// <param name="chaControl">The character to measure</param>
        internal Tuple<float, float> MeasureWaist(ChaControl chaControl) 
        {
            var charScale = PregnancyPlusHelper.GetCharacterScale(ChaControl);

#if KK
            var ribName = "cf_s_spine02";
            var waistName = "cf_s_waist02";
#elif HS2 || AI
            var ribName = "cf_J_Spine02_s";
            var waistName = "cf_J_Kosi02";
#endif            
            //Get the characters bones to measure from           
            var ribBone = chaControl.objBodyBone.GetComponentsInChildren<Transform>().FirstOrDefault(x => x.name == ribName);
            var waistBone = chaControl.objBodyBone.GetComponentsInChildren<Transform>().FirstOrDefault(x => x.name == waistName);
            var waistToRibDist = Vector3.Distance(ribBone.position, waistBone.position);
            if (PregnancyPlusPlugin.debugLog)  PregnancyPlusPlugin.Logger.LogInfo($" waistToRibDist {waistToRibDist}");

            //Adjust based on character scale
            waistToRibDist = Math.Abs(waistToRibDist - (waistToRibDist * charScale.y - waistToRibDist)/charScale.y);


#if KK
            var thighLName = "cf_j_thigh00_L";
            var thighRName = "cf_j_thigh00_R";                    
#elif HS2 || AI
            var thighLName = "cf_J_LegUp00_L";
            var thighRName = "cf_J_LegUp00_R";
#endif
            var thighLBone = chaControl.objBodyBone.GetComponentsInChildren<Transform>().FirstOrDefault(x => x.name == thighLName);        
            var thighRBone = chaControl.objBodyBone.GetComponentsInChildren<Transform>().FirstOrDefault(x => x.name == thighRName); 
            var waistWidth = Vector3.Distance(thighLBone.position, thighRBone.position); 
            if (PregnancyPlusPlugin.debugLog)  PregnancyPlusPlugin.Logger.LogInfo($" waistWidth {waistWidth}");

            //Adjust based on character scale
            waistWidth = Math.Abs(waistWidth - (waistWidth * charScale.x - waistWidth)/charScale.x);

            //Calculate sphere radius based on distance from waist to ribs (seems big, but lerping later will trim much of it), added Math.Min for skinny waists
            var sphereRadius = Math.Min(waistToRibDist/1.25f, waistWidth/1.2f) * (GetInflationMultiplier() + 1);   

            if (PregnancyPlusPlugin.debugLog)  PregnancyPlusPlugin.Logger.LogInfo($" scaled waistToRibDist {waistToRibDist} scaled waistWidth {waistWidth} sphereRadius {sphereRadius}");
            if (PregnancyPlusPlugin.debugLog)  PregnancyPlusPlugin.Logger.LogInfo($" ---------- ");

            return Tuple.Create(waistWidth, sphereRadius);
        }


        /// <summary>
        /// Does the vertex morph calculations to make a sphere out of the belly verticies, and updates the vertex
        /// dictionaries apprporiately
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

#if KK            
            var isDefaultBody = !PregnancyPlusHelper.IsUncensorBody(ChaControl, UncensorCOMName, DefaultBodyFemaleGUID); 
            if (isClothingMesh) 
            {
                //KK just has to have strange vert positions, so we have to use adjust the sphere center location for body and clothes
                var clothesMeshRoot = GameObject.Find("cf_o_root").transform.position;
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

            if (PregnancyPlusPlugin.debugLog)  PregnancyPlusPlugin.Logger.LogInfo($" isClothingMesh {isClothingMesh} needsPositionFix {needsPositionFix} ");

            var rendererName = GetMeshKey(smr);         
            originalVertices[rendererName] = smr.sharedMesh.vertices;
            inflatedVertices[rendererName] = new Vector3[originalVertices[rendererName].Length];
            currentVertices[rendererName] = new Vector3[originalVertices[rendererName].Length];

            var origVerts = originalVertices[rendererName];
            var inflatedVerts = inflatedVertices[rendererName];
            var currentVerts = currentVertices[rendererName];
            var bellyVertIndex = bellyVerticieIndexes[rendererName];    

            var vertsLength = origVerts.Length;
            //Set each verticies inflated postion, with some constraints (SculptInflatedVerticie) to make it look more natural
            for (int i = 0; i < vertsLength; i++)
            {
                var origVert = origVerts[i];

                //Only care about inflating belly verticies
                if (bellyVertIndex[i]) 
                {                    
                    Vector3 inflatedVertWS;                    
                    Vector3 verticieToSphere;                      
                    var origVertWS = meshRootTf.TransformPoint(origVerts[i]);//Convert to worldspace                    

                    //Shift each belly vertex away from sphere center
                    if (!isClothingMesh) 
                    {                        
                        verticieToSphere = (origVertWS - bodySphereCenterOffset).normalized * sphereRadius + bodySphereCenterOffset;
                    }
                    else 
                    {                       
                        float reduceClothFlattenOffset = GetClothesFixOffset(clothSphereCenterOffset, sphereRadius, waistWidth, origVertWS);//Reduce cloth flattening at largest inflation values 
                        //Clothes need some more loving to get them to stop clipping at max size
                        verticieToSphere = (origVertWS - clothSphereCenterOffset).normalized * (sphereRadius + reduceClothFlattenOffset) + clothSphereCenterOffset;
                    }     

                    //Make adjustments to the shape, and feed in user slider input
                    inflatedVertWS =  SculptInflatedVerticie(origVertWS, verticieToSphere, sphereCenter, waistWidth, meshRootTf);                    
                    inflatedVerts[i] = meshRootTf.InverseTransformPoint(inflatedVertWS);//Convert back to local space
                    // if (i % 100 == 0) PregnancyPlusPlugin.Logger.LogInfo($" origVertWS {origVertWS}  verticieToSphere {verticieToSphere}");
                }
                else 
                {
                    inflatedVerts[i] = origVert;
                }
                
                currentVerts[i] = origVert;
            }      

            return true;                 
        }


        internal Transform GetMeshRoot() 
        {                                
#if KK
            //Get normal mesh root attachment position, and if its not near 0,0,0 fix it so that it is
            var kkMeshRoot = GameObject.Find("cf_o_root"); 
            if (!kkMeshRoot) return null;
            
            var meshRoot = kkMeshRoot.transform;
            meshRoot.transform.position = kkMeshRoot.transform.position + kkMeshRoot.transform.up * (-ChaControl.transform.InverseTransformPoint(kkMeshRoot.transform.position).y);
            
#elif HS2 || AI
            //For HS2, get the equivalent position game object (near bellybutton)
            var meshRootGo = GameObject.Find("n_o_root");
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
            Vector3 bellyButtonHeight = boneOrMeshTf.up * GetBellyButtonLocalHeight(boneOrMeshTf); 
            Vector3 sphereCenter = boneOrMeshTf.position + bellyButtonHeight + GetUserMoveTransform(boneOrMeshTf) + GetBellyButtonOffset(boneOrMeshTf);                                 

            if (PregnancyPlusPlugin.debugLog) PregnancyPlusPlugin.Logger.LogInfo($" sphereCenter {sphereCenter} meshRoot {boneOrMeshTf.position} char origin {ChaControl.transform.position}");
            if (PregnancyPlusPlugin.debugLog) PregnancyPlusPlugin.Logger.LogInfo($" bellyButtonHeight {bellyButtonHeight}");            
            return sphereCenter;
        }


        /// <summary>
        /// This will take the sphere-ified verticies and apply smoothing to them via Lerps, to remove sharp edges, 
        /// and make the belly more belly like
        /// </summary>
        /// <param name="originalVertice">The original verticie position</param>
        /// <param name="inflatedVerticie">The target verticie position, after sphere-ifying</param>
        /// <param name="sphereCenterPos">The center of the imaginary sphere</param>
        /// <param name="waistWidth">The characters waist width that limits the width of the belly (future implementation)</param>
        /// <param name="meshRootTf">The transform used to convert a mesh vector from local space to worldspace and back, also servers as the point where we want to stop making mesh changes when Z < 0</param>
        internal Vector3 SculptInflatedVerticie(Vector3 originalVertice, Vector3 inflatedVerticie, Vector3 sphereCenterPos, float waistWidth, Transform meshRootTf) 
        {
            //No smoothing modification in debug mode
            if (debug) return inflatedVerticie;            
            
            //get the smoothing distance limits so we don't have weird polygons and shapes on the edges, and prevents morphs from shrinking past original skin boundary
            var preMorphSphereCenter = sphereCenterPos - GetUserMoveTransform(meshRootTf);
            var pmSkinToCenterDist = Math.Abs(FastDistance(preMorphSphereCenter, originalVertice));
            var pmInflatedToCenterDist = Math.Abs(FastDistance(preMorphSphereCenter, inflatedVerticie));
            var skinToCenterDist = Math.Abs(FastDistance(sphereCenterPos, originalVertice));
            var inflatedToCenterDist = Math.Abs(FastDistance(sphereCenterPos, inflatedVerticie));
            // var waistRadius = waistWidth/2;
            
            // PregnancyPlusPlugin.Logger.LogInfo($" preMorphSphereCenter {preMorphSphereCenter} sphereCenterPos {sphereCenterPos} meshRootTf.pos {meshRootTf.position}");

            //Only apply morphs if the imaginary sphere is outside of the skins boundary (Don't want to shrink anything inwards, only out)
            if (skinToCenterDist >= inflatedToCenterDist || pmSkinToCenterDist > pmInflatedToCenterDist) return originalVertice;        

            var smoothedVector = GetUserShiftTransform(meshRootTf, inflatedVerticie, sphereCenterPos, skinToCenterDist);
            var zSmoothDist = pmInflatedToCenterDist/3f;//Just pick a float that looks good
            var ySmoothDist = pmInflatedToCenterDist/2f;//Only smooth the top half of y

            //Allow user adjustment of the width of the belly
            if (infConfig.inflationStretchX != 0) {   
                smoothedVector = GetUserStretchXTransform(meshRootTf, smoothedVector, sphereCenterPos);
            }

            //Allow user adjustment of the height of the belly
            if (infConfig.inflationStretchY != 0) {   
                smoothedVector = GetUserStretchYTransform(meshRootTf, smoothedVector, sphereCenterPos);
            }

            if (GetInflationTaperY() != 0) {
                smoothedVector = GetUserTaperTransform(meshRootTf, smoothedVector, sphereCenterPos, skinToCenterDist);
            }

            //Remove the skin cliff where the inflation begins
            //To calculate vectors z difference, we need to do it from local space to eliminate any character rotation in world space
            var forwardFromCenter = meshRootTf.InverseTransformPoint(smoothedVector).z - meshRootTf.InverseTransformPoint(preMorphSphereCenter).z;            
            if (forwardFromCenter <= zSmoothDist) {
                //Get local space vectors to eliminate rotation in world space
                var smoothedVectorLs = meshRootTf.InverseTransformPoint(smoothedVector);
                var originalVerticeLs = meshRootTf.InverseTransformPoint(originalVertice);
                var lerpScale = Mathf.Abs(forwardFromCenter/zSmoothDist);
                //Back to world space
                smoothedVector = meshRootTf.TransformPoint(Vector3.Lerp(originalVerticeLs, smoothedVectorLs, lerpScale));
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

            var currentVectorDistance = Math.Abs(FastDistance(sphereCenterPos, smoothedVector));
            var pmCurrentVectorDistance = Math.Abs(FastDistance(preMorphSphereCenter, smoothedVector));
            //Don't allow any morphs to shrink skin smaller than its original position, only outward morphs allowed (check this last)
            if (skinToCenterDist > currentVectorDistance || pmSkinToCenterDist > pmCurrentVectorDistance) {
                return originalVertice;
            }

            //Don't allow any morphs to move behind the character's.z = 0 position, otherwise skin sometimes pokes out the back side :/
            if (meshRootTf.InverseTransformPoint(meshRootTf.position).z > meshRootTf.InverseTransformPoint(smoothedVector).z) {
                return originalVertice;
            }

            //Don't allow any morphs to move behind the original verticie z = 0 position
            if (meshRootTf.InverseTransformPoint(originalVertice).z > meshRootTf.InverseTransformPoint(smoothedVector).z) {
                return new Vector3(smoothedVector.x, smoothedVector.y, originalVertice.z);
            }

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

            //Do a quick check to see if we need to fetch the bone indexes again.  ex: on second call we should allready have them
            //This saves a lot on compute apparently!            
            var isInitialized = bellyVerticieIndexes.TryGetValue(renderKey, out bool[] existingValues);
            if (isInitialized)
            {
                //If the vertex count has not changed then we can skip this
                if (existingValues.Length == skinnedMeshRenderer.sharedMesh.vertexCount) return true;
            }

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
                    if ((boneWeights[i] > 0.05f && bellyBoneIndexes.Contains(boneIndicies[i]) || debug))
                    {
                        //Make sure to exclude verticies on characters back, we only want to modify the front.  No back bellies!
                        //add all vertexes in debug mode
                        if (verticies[c].z >= 0 || debug) {
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


