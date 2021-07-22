using KKAPI;
using KKAPI.Chara;
using UnityEngine;
using System;
using System.Threading;
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
        /// <param name="meshInflateFlags">Contains any flags needed for mesh computation</param>
        /// <returns>Will return True if the mesh was altered and False if not</returns>
        public void MeshInflate(MeshInflateFlags meshInflateFlags, string callee)
        {
            if (ChaControl.objBodyBone == null) return;//Make sure chatacter objs exists first  
            if (!PregnancyPlusPlugin.AllowMale.Value && ChaControl.sex == 0) return;// Only female characters, unless plugin config says otherwise          

            //Only continue if one of the config values changed, or we need to recompute a mesh
            if (!meshInflateFlags.NeedsToRun) return;
            if (!meshInflateFlags.bypassWhen0 && !isDuringInflationScene) ResetInflation();

            if (!AllowedToInflate()) return;//if outside studio/maker, make sure StoryMode is enabled first
            if (!infConfig.GameplayEnabled) return;//Only if gameplay enabled

            //Resets all stored vert values, so the script will have to recalculate all from base body
            if (meshInflateFlags.freshStart) CleanSlate();

            //Only continue when size above 0
            if (infConfig.inflationSize <= 0 && !meshInflateFlags.bypassWhen0 && !isDuringInflationScene) 
            {
                infConfigHistory.inflationSize = 0;
                return;                                
            }
            
            if (PregnancyPlusPlugin.DebugLog.Value || PregnancyPlusPlugin.DebugCalcs.Value)  PregnancyPlusPlugin.Logger.LogInfo($" ---------- {callee}() ");
            if (PregnancyPlusPlugin.DebugLog.Value || PregnancyPlusPlugin.DebugCalcs.Value)  PregnancyPlusPlugin.Logger.LogInfo($" inflationSize > {infConfig.inflationSize} for {charaFileName} ");            
            meshInflateFlags.Log();
            if (PregnancyPlusPlugin.DebugLog.Value || PregnancyPlusPlugin.DebugCalcs.Value)  PregnancyPlusPlugin.Logger.LogInfo($" ");

            //Get the measurements that determine the base belly size
            var hasMeasuerments = MeasureWaistAndSphere(ChaControl, meshInflateFlags.reMeasure);                     
            if (!hasMeasuerments) 
            {
                PregnancyPlusPlugin.errorCodeCtrl.LogErrorCode(ChaControl.chaID, ErrorCode.PregPlus_BadMeasurement, 
                    $"Could not get one or more belly measurements from character");
                return;
            }           

            //Get all mesh renderers, calculate, and apply inflation changes
            var bodyRenderers = PregnancyPlusHelper.GetMeshRenderers(ChaControl.objBody, findAll: true);
            LoopAndApplyMeshChanges(bodyRenderers, meshInflateFlags);
            var clothRenderers = PregnancyPlusHelper.GetMeshRenderers(ChaControl.objClothes);            
            LoopAndApplyMeshChanges(clothRenderers, meshInflateFlags, true, GetBodyMeshRenderer());          

            //If any changes were applied, updated the last used shape for the Restore GUI button
            if (infConfig.HasAnyValue()) 
            {
                PregnancyPlusPlugin.lastBellyState = (PregnancyPlusData)infConfig.Clone();//CLone so we don't accidently overwright the lastState later
            }           
        }


        /// <summary>
        /// Loop through each skinned mesh rendere and get its belly verts, then apply inflation when needed
        /// </summary>
        /// <param name="smrs">List of skinnedMeshRenderes</param>
        /// <param name="meshInflateFlags">Contains any flags needed for mesh computation</param>
        /// <param name="anyMeshChanges">If any mesh changes have happened so far</param>
        /// <param name="isClothingMesh">If this smr is a cloth mesh</param>
        /// <returns>boolean true if any meshes were changed</returns>
        internal void LoopAndApplyMeshChanges(List<SkinnedMeshRenderer> smrs, MeshInflateFlags meshInflateFlags, 
                                              bool isClothingMesh = false, SkinnedMeshRenderer bodyMeshRenderer = null) 
        {
            foreach (var smr in smrs) 
            {           
                var threadedCompute = false;//Whether the computation has been threaded
                var renderKey = GetMeshKey(smr);

                //Dont recompute verts if no sliders have changed or clothing added
                var needsComputeVerts = NeedsComputeVerts(smr, renderKey, meshInflateFlags);
                if (needsComputeVerts)
                {
                    threadedCompute = ComputeMeshVerts(smr, isClothingMesh, bodyMeshRenderer, meshInflateFlags, renderKey);                                                                                   
                }

                //When threaded, the belly will be set later so we can skip it here (only used when full re-computation is needed)
                if (threadedCompute) continue;           

                //We only make it to here when the shape was previously computed, but we need to alter the blendshape weight
                var appliedMeshChanges = ApplyInflation(smr, renderKey, meshInflateFlags.OverWriteMesh, blendShapeTempTagName, meshInflateFlags.bypassWhen0);

                //When inflation is actively happening as clothing changes, make sure the new clothing grows too
                if (isDuringInflationScene) AppendToQuickInflateList(smr);
                if (appliedMeshChanges) infConfigHistory = (PregnancyPlusData)infConfig.Clone(); 
            }  

        }


        /// <summary>
        /// See if we already have this mesh's indexes stored, if the slider values haven't changed then we dont need to recompute, just apply existing cumputed verts
        /// </summary>
        public bool NeedsComputeVerts(SkinnedMeshRenderer smr, string renderKey, MeshInflateFlags meshInflateFlags) 
        {    
            //If mesh is on ignore list, skip it
            if (ignoreMeshList.Contains(renderKey)) return false;           
            if (meshInflateFlags.freshStart) return true;

            //Do a quick check to see if we need to fetch the bone indexes again.  ex: on second call we should allready have them
            //This saves a lot on compute apparently!            
            var isMeshInitialized = md.TryGetValue(renderKey, out MeshData _md);
            if (isMeshInitialized)
            {
                //If the vertex count has not changed then we can skip this if no critical sliders changed
                if (_md.bellyVerticieIndexes.Length == smr.sharedMesh.vertexCount) 
                {
                    if (meshInflateFlags.OnlyInflationSizeChanged) return false;
                    return meshInflateFlags.SliderHaveChanged;
                }
            }

            //When no mesh found key, or incorrect vert count, the mesh changed so we need to recompute
            return true;
        } 


        /// <summary>
        /// Just a helper function to combine searching for verts in a mesh, and then applying the transforms
        /// </summary>
        internal bool ComputeMeshVerts(SkinnedMeshRenderer smr, bool isClothingMesh, SkinnedMeshRenderer bodyMeshRenderer, MeshInflateFlags meshInflateFlags, string renderKey) 
        {
            //The list of bones to get verticies for
            #if KK            
                var boneFilters = new string[] { "cf_s_spine02", "cf_s_waist01", "cf_s_waist02" };//"cs_s_spine01" optionally for wider affected area
            #elif HS2 || AI
                var boneFilters = new string[] { "cf_J_Spine02_s", "cf_J_Kosi01_s", "cf_J_Kosi02_s" };
            #endif

            var hasVerticies = true;
            var isMeshInitialized = md.TryGetValue(renderKey, out MeshData _md);

            //Only fetch belly vert list when needed since its fairly expensive
            if (meshInflateFlags.NeedsToComputeIndex || !isMeshInitialized)
            {
                hasVerticies = GetFilteredVerticieIndexes(smr, PregnancyPlusPlugin.MakeBalloon.Value ? null : boneFilters);        
            }

            //If no belly verts found, or verts already exists, then we can skip this mesh
            if (!hasVerticies) return false; 

            if (PregnancyPlusPlugin.DebugLog.Value || PregnancyPlusPlugin.DebugCalcs.Value) PregnancyPlusPlugin.Logger.LogInfo($" ");
            if (PregnancyPlusPlugin.DebugLog.Value || PregnancyPlusPlugin.DebugCalcs.Value) PregnancyPlusPlugin.Logger.LogInfo($" ComputeMeshVerts for {smr.name}"); 
            return GetInflatedVerticies(smr, bellyInfo.SphereRadius, bellyInfo.WaistWidth, isClothingMesh, bodyMeshRenderer, meshInflateFlags);
        }


        /// <summary>
        /// Does the vertex morph calculations to make a sphere out of the belly verticies, and updates the vertex dictionaries apprporiately
        /// </summary>
        /// <param name="skinnedMeshRenderer">The mesh renderer target</param>
        /// <param name="sphereRadius">The radius of the inflation sphere</param>
        /// <param name="waistWidth">The width of the characters waist</param>
        /// <param name="isClothingMesh">Clothing requires a few tweaks to match skin morphs</param>
        /// <returns>Will return True if mesh verticies > 0 were found  Some meshes wont have any verticies for the belly area, returning false</returns>
        internal bool GetInflatedVerticies(SkinnedMeshRenderer smr, float sphereRadius, float waistWidth, bool isClothingMesh, 
                                           SkinnedMeshRenderer bodySmr, MeshInflateFlags meshInflateFlags) 
        {
            Vector3 bodySphereCenterOffset = Vector3.zero;//For defaultt KK body mesh custom offset correction

            if (smr == null) 
            {
                if (PregnancyPlusPlugin.DebugLog.Value) PregnancyPlusPlugin.Logger.LogWarning($" GetInflatedVerticies smr was null"); 
                return false;
            }

            //Found out body mesh can be nested under cloth game objects...   Make sure to flag it as non-clothing
            if (isClothingMesh && BodyNestedUnderCloth(smr, bodySmr)) 
            {
                PregnancyPlusPlugin.errorCodeCtrl.LogErrorCode(ChaControl.chaID, ErrorCode.PregPlus_BodyMeshDisguisedAsCloth, 
                    $" body mesh {smr.name} was nested under cloth object {smr.transform.parent.name}.  This is usually not an issue.");
                isClothingMesh = false;            
            }

            var meshRootTf = GetMeshRoot(smr);
            if (meshRootTf == null) 
            {
                if (PregnancyPlusPlugin.DebugLog.Value) PregnancyPlusPlugin.Logger.LogWarning($" GetInflatedVerticies meshRootTf was null"); 
                return false;
            }
            
            // if (PregnancyPlusPlugin.DebugLog.Value) PregnancyPlusPlugin.Logger.LogInfo($" SMR pos {smr.transform.position} rot {smr.transform.rotation} parent {smr.transform.parent}");                     

            var rendererName = GetMeshKey(smr);         
            md[rendererName].originalVertices = smr.sharedMesh.vertices;
            md[rendererName].inflatedVertices = smr.sharedMesh.vertices;
            md[rendererName].alteredVerticieIndexes = new bool[smr.sharedMesh.vertexCount];

            //set sphere center and allow for adjusting its position from the UI sliders  
            Vector3 sphereCenter = GetSphereCenter(meshRootTf);
            md[rendererName].yOffset = ApplyConditionalSphereCenterOffset(isClothingMesh, sphereCenter, smr, meshRootTf); 

            //Create mesh collider to make clothing measurements from skin (if it doesnt already exists)            
            if (NeedsClothMeasurement(smr, bodySmr, sphereCenter)) CreateMeshCollider(bodySmr); 
           
            //Get the cloth offset for each cloth vertex via raycast to skin
            //  Unfortunately this cant be inside the thread below because Unity Raycast are not thread safe...
            var clothOffsets = DoClothMeasurement(smr, bodySmr, sphereCenter);
            if (clothOffsets == null) clothOffsets = new float[md[rendererName].originalVertices.Length];

            var origVerts = md[rendererName].originalVertices;
            var inflatedVerts = md[rendererName].inflatedVertices;
            var bellyVertIndex = md[rendererName].bellyVerticieIndexes;
            var alteredVerts = md[rendererName].alteredVerticieIndexes;

            //Heavy compute task below, run in separate thread
            WaitCallback threadAction = (System.Object stateInfo) => 
            {

                #if DEBUG
                    var bellyVertsCount = 0;
                    for (int i = 0; i < bellyVertIndex.Length; i++)
                    {
                        if (bellyVertIndex[i]) bellyVertsCount++;
                    }
                    if (PregnancyPlusPlugin.DebugCalcs.Value) PregnancyPlusPlugin.Logger.LogInfo($" Mesh affected vert count {bellyVertsCount}");
                #endif

                //Pre compute some values needed by SculptInflatedVerticie, doin it here saves on compute in the big loop
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
                var yOffsetDir = Vector3.up * md[rendererName].yOffset;//Any offset direction needed to align all meshes to the same local y height
                var reduceClothFlattenOffset = 0f;

                // if (PregnancyPlusPlugin.DebugLog.Value) DebugTools.DrawSphereAndAttach(smr.transform, 0.2f, sphereCenter);
                // if (PregnancyPlusPlugin.DebugLog.Value) DebugTools.DrawLineAndAttach(meshRootTf, 5, meshRootTf.InverseTransformPoint(topExtentPos) - meshRootTf.up * GetBellyButtonOffset(bellyInfo.BellyButtonHeight));
                // if (PregnancyPlusPlugin.DebugLog.Value) DebugTools.DrawLineAndAttach(meshRootTf, new Vector3(-3, 0, 0), new Vector3(3, 0, 0), meshRootTf.InverseTransformPoint(backExtentPos) - GetBellyButtonOffsetVector(meshRootTf, bellyInfo.BellyButtonHeight));
                // if (PregnancyPlusPlugin.DebugLog.Value) DebugTools.DrawLineAndAttach(meshRootTf, 5, meshRootTf.InverseTransformPoint(sphereCenter));    

                //Set each verticies inflated postion, with some constraints (SculptInflatedVerticie) to make it look more natural
                for (int i = 0; i < vertsLength; i++)
                {
                    //Only care about inflating belly verticies
                    if (!bellyVertIndex[i] && !PregnancyPlusPlugin.DebugVerts.Value) continue;
                                    
                    //Convert to worldspace, and apply and mesh offset needed
                    var origVertWs = smr.transform.TransformPoint(origVerts[i] - yOffsetDir);
                    var vertDistance = Vector3.Distance(origVertWs, sphereCenter);                    

                    //Ignore verts outside the sphere radius
                    if (vertDistance > vertNormalCaluRadius && !PregnancyPlusPlugin.DebugVerts.Value) continue;
                    
                    Vector3 inflatedVertWs;                    
                    Vector3 verticieToSpherePos;       
                    reduceClothFlattenOffset = 0f; 

                    // If the vert is within the calculated normals radius, then consider it as an altered vert that needs normal recalculation when applying inflation
                    //  Hopefully this will reduce breast shadows for smaller bellies
                    if (vertDistance <= vertNormalCaluRadius) alteredVerts[i] = true;                                                                          
                    
                    if (isClothingMesh) 
                    {                        
                        //Calculate clothing offset distance                   
                        reduceClothFlattenOffset = GetClothesFixOffset(sphereCenter, sphereRadius, waistWidth, origVertWs, smr.name, clothOffsets[i]);
                    }
                        
                    //Shift each belly vertex away from sphere center in a sphere pattern.  This is the core of the Preg+ belly shape
                    verticieToSpherePos = (origVertWs - sphereCenter).normalized * (sphereRadius + reduceClothFlattenOffset) + sphereCenter;                                                    

                    //Make adjustments to the shape to make it smooth, and feed in user slider input
                    inflatedVertWs =  SculptInflatedVerticie(origVertWs, verticieToSpherePos, sphereCenter, waistWidth, 
                                                                meshRootTf, preMorphSphereCenter, sphereRadius, backExtentPos, 
                                                                topExtentPos, sphereCenterLs, pmSphereCenterLs, backExtentPosLs, 
                                                                topExtentPosLs);   

                    //Convert back to local space, and undo any temporary offset                                                                
                    inflatedVerts[i] = smr.transform.InverseTransformPoint(inflatedVertWs) + yOffsetDir;                                      

                    // if (PregnancyPlusPlugin.DebugLog.Value) DebugTools.DrawLineAndAttach(smr.transform, 0.02f, origVerts[i]- yOffsetDir, false);
                    // if (PregnancyPlusPlugin.DebugLog.Value) DebugTools.DrawSphereAndAttach(smr.transform, 0.02f, origVerts[i] - yOffsetDir, false);            
                    // if (PregnancyPlusPlugin.DebugLog.Value) DebugTools.DrawSphereAndAttach(ChaControl.transform, 0.02f, smr.transform.TransformPoint(origVerts[i]), false);            
                }  

                //When this thread task is complete, execute the below in main thread
                Action threadActionResult = () => 
                {
                    //Apply computed mesh back to body
                    var appliedMeshChanges = ApplyInflation(smr, rendererName, meshInflateFlags.OverWriteMesh, blendShapeTempTagName, meshInflateFlags.bypassWhen0);

                    //When inflation is actively happening as clothing changes, make sure the new clothing grows too
                    if (isDuringInflationScene) AppendToQuickInflateList(smr);
                    if (appliedMeshChanges) infConfigHistory = (PregnancyPlusData)infConfig.Clone();   
                };

                threading.AddResultToThreadQueue(threadActionResult);

            };

            //Start this threaded task, and will be watched in Update() for completion
            threading.Start(threadAction);

            return true;                 
        }


        /// <summary>
        /// Get the root position of the mesh, so we can calculate the true position of its mesh verticies later
        /// </summary>
        internal Transform GetMeshRoot(SkinnedMeshRenderer smr) 
        {                                        
            #if KK       
                //In KK we can't use .n_o_root because there are too many improperly imported meshes (wried local positions or rotations), use the parent body mesh bone instead
                var bodyBone = ChaControl.sex == 0 ? "p_cm_body_00" : "p_cf_body_00";                            
            #elif HS2 || AI
                var bodyBone = ChaControl.sex == 0 ? "p_cm_body_00.n_o_root" : "p_cf_body_00.n_o_root";
            #endif            

            var meshRootGo = PregnancyPlusHelper.GetBoneGO(ChaControl, bodyBone);
            if (meshRootGo == null) return null;
            return meshRootGo.transform;
        }


        /// <summary>
        /// Calculates the position of the inflation sphere.  It appends some users selected slider values as well.  This sure got messy fast
        /// </summary>
        /// <param name="boneOrMeshTf">The transform that defined the center of the sphere X, Y, and Z for KK and X, Z for HS2 with calculated Y</param>
        /// <param name="isClothingMesh"></param>
        internal Vector3 GetSphereCenter(Transform meshRootTf) 
        { 
            
            //Sphere slider adjustments need to be transformed to local space first to eliminate any character rotation in world space   
            var bbHeight = GetBellyButtonLocalHeight();
            bellyInfo.BellyButtonHeight = bbHeight;//Store for later use
            Vector3 bellyButtonPos = meshRootTf.up * bbHeight; 
            Vector3 sphereCenter = meshRootTf.position + bellyButtonPos + GetUserMoveTransform(meshRootTf) + GetBellyButtonOffsetVector(meshRootTf, bbHeight);                                 

            if (PregnancyPlusPlugin.DebugCalcs.Value) PregnancyPlusPlugin.Logger.LogInfo($" bbHeight {bbHeight}");            
            if (PregnancyPlusPlugin.DebugCalcs.Value) PregnancyPlusPlugin.Logger.LogInfo($" sphereCenter {sphereCenter} meshRoot {meshRootTf.position} char origin {ChaControl.transform.position}");            
            return sphereCenter;
        }


        /// <summary>
        /// In special cases we need to apply a small offset to the sphereCenter to align the mesh correctly with the other meshes.  Otherwise you get tons of clipping
        ///  Mostly used to fix the default KK body which seems to be mis aligned from uncensor, and AI/HS2 meshes
        /// </summary>
        public float ApplyConditionalSphereCenterOffset(bool isClothingMesh, Vector3 _sphereCenter, SkinnedMeshRenderer smr, Transform meshRootTf)
        {
            #if KK      
                //When mesh is an uncensor body, we have to adjust the mesh center offset, to match its special localspace positioning
                //  This lines up the body mesh infaltion with clothing mesh inflation
                var yOffset = 0f;
                var isDefaultBody = !PregnancyPlusPlugin.Hooks_Uncensor.IsUncensorBody(ChaControl, UncensorCOMName);
                //When the mesh shares similar local vertex positions as the default body.  Bounds are the only way I can reliably detect this...
                var isLikeDefaultBody = smr.localBounds.center.y < 0 && smr.sharedMesh.bounds.center.y < 0;
                var isOffsetClothMesh = smr.localBounds.center.y < 0 && smr.sharedMesh.bounds.center.y > Math.Abs(smr.localBounds.center.y * 1.5f);

                //If uncensor body
                if (!isClothingMesh && !isDefaultBody && !isLikeDefaultBody) 
                {
                    //Uncensor mesh is about twice the height in local space than default mesh, so save the current offset to be used later
                    yOffset = meshRootTf.InverseTransformPoint(smr.transform.position).y;
                    if (PregnancyPlusPlugin.DebugCalcs.Value) PregnancyPlusPlugin.Logger.LogInfo($" [KK only] setting yOffset {yOffset} isDefaultBody {isDefaultBody} isLikeDefaultBody {isLikeDefaultBody}");                           
                }
                else if (isClothingMesh && isOffsetClothMesh) 
                {
                    //there is one type of clothing that is not locally positioned correctly, similar to uncensor meshes. Though it's rare
                    yOffset = meshRootTf.InverseTransformPoint(smr.transform.position).y/2;//Who knows why these need to be divided by 2 to correct the offset, when the uncensor check above is not divided by 2 ...
                    if (PregnancyPlusPlugin.DebugCalcs.Value) PregnancyPlusPlugin.Logger.LogInfo($" [KK only] setting yOffset {yOffset} isOffsetClothMesh {isOffsetClothMesh}");                                                                                       
                }                                                           

                if (PregnancyPlusPlugin.DebugCalcs.Value) PregnancyPlusPlugin.Logger.LogInfo($" [KK only] local bounds {smr.localBounds.center.y} sm.bounds {smr.sharedMesh.bounds.center.y}");                           

                return yOffset;

            #else
                return 0f;
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
            var pmSkinToCenterDist = Math.Abs(Vector3.Distance(preMorphSphereCenterWs, originalVerticeWs));
            var pmInflatedToCenterDist = Math.Abs(Vector3.Distance(preMorphSphereCenterWs, inflatedVerticieWs));
            var skinToCenterDist = Math.Abs(Vector3.Distance(sphereCenterWs, originalVerticeWs));
            var inflatedToCenterDist = Math.Abs(Vector3.Distance(sphereCenterWs, inflatedVerticieWs));
            
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
                smoothedVectorLs = GetUserRoundnessTransform(meshRootTf, originalVerticeLs, smoothedVectorLs, sphereCenterLs, skinToCenterDist);
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

            //If the user has selected a drop slider value
            if (GetInflationDrop() > 0) 
            {
                smoothedVectorLs = GetUserDropTransform(meshRootTf, smoothedVectorLs, sphereCenterLs, skinToCenterDist, sphereRadius);
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

            //Smoothed vert back to worldspace
            var smoothedVectorWs = meshRootTf.TransformPoint(smoothedVectorLs);
            var currentVectorDistance = Math.Abs(Vector3.Distance(sphereCenterWs, smoothedVectorWs));
            var pmCurrentVectorDistance = Math.Abs(Vector3.Distance(preMorphSphereCenterWs, smoothedVectorWs));     
            //Get core point on the same y plane as the original vert
            var coreLineVertWs = meshRootTf.position + meshRootTf.up * (meshRootTf.InverseTransformPoint(originalVerticeWs).y * bellyInfo.TotalCharScale.y);
            var origCoreDist = Math.Abs(Vector3.Distance(originalVerticeWs, coreLineVertWs));//Get line from feet to head that verts must respect distance from
            //Get core point on the same y plane as the smoothed vert
            var coreLineSmoothedVertWs = meshRootTf.position + meshRootTf.up * (meshRootTf.InverseTransformPoint(smoothedVectorWs).y * bellyInfo.TotalCharScale.y);       
            var currentCoreDist = Math.Abs(Vector3.Distance(smoothedVectorWs, coreLineSmoothedVertWs)); 

            //Don't allow any morphs to shrink towards the sphere center more than its original distance, only outward morphs allowed
            if (skinToCenterDist > currentVectorDistance || pmSkinToCenterDist > pmCurrentVectorDistance) 
            {
                return originalVerticeWs;
            }

            //Don't allow any morphs to shrink towards the characters core line any more than the original distance
            if (currentCoreDist < origCoreDist) 
            {
                //Since this is just an XZ distance line check, don't modify the new y value
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


