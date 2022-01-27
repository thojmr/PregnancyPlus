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
        /// It will check the infConfig for any slider values to apply
        /// This will not run twice for the same input parameters, a change of config value is required
        /// First time this Method is called will be CPU intensive, but values get cached and subsiquent calls are cheaper
        /// Performance Note: Changing the infConfig.inflationSize is basically free, it only updates the blendshape weight
        ///     Changing any other slider requires Preg+ re-compute the belly shape which is very costly (But not as costly as a freshStart)
        /// </summary>
        /// <param name="meshInflateFlags">Contains any flags needed for mesh computation decisions</param>
        /// <param name="callee">Lets you see what method called this one (Just for logging purposes)</param>
        /// <returns>Will return True if the mesh was altered and False if not</returns>
        public void MeshInflate(MeshInflateFlags meshInflateFlags, string callee)
        {
            //Make sure chatacter objs exists first 
            if (ChaControl.objBodyBone == null) return; 
            // Only female characters, unless plugin config says otherwise 
            if (!PregnancyPlusPlugin.AllowMale.Value && ChaControl.sex == 0) return;         

            //Only continue if one of the config values changed, or we need to recompute a mesh
            if (!meshInflateFlags.NeedsToRun) return;

            //if outside studio/maker, make sure StoryMode is enabled first
            if (!AllowedToInflate()) return;
            if (!infConfig.GameplayEnabled) 
            {
                //Remove belly if gameplay disabled, and char has a belly
                if (infConfig.inflationSize > 0 && md?.Keys.Count > 0) 
                {
                    CleanSlate();
                }
                return;
            }

            //Resets all stored vert values, so the script will have to recalculate all from base body (Expensive, avoid if possible)
            if (meshInflateFlags.freshStart) CleanSlate();

            //Only continue when size above 0
            if (infConfig.inflationSize <= 0 && !meshInflateFlags.bypassWhen0 && !isDuringInflationScene) 
            {
                infConfigHistory.inflationSize = 0;
                ResetInflation();
                return;                                
            }
            
            //Lets us see what method called this one, and the meshInflateFlags it includes
            if (PregnancyPlusPlugin.DebugLog.Value || PregnancyPlusPlugin.DebugCalcs.Value)  PregnancyPlusPlugin.Logger.LogInfo($" ");
            if (PregnancyPlusPlugin.DebugLog.Value || PregnancyPlusPlugin.DebugCalcs.Value)  PregnancyPlusPlugin.Logger.LogInfo($" ---------- {callee}() ");
            if (PregnancyPlusPlugin.DebugLog.Value || PregnancyPlusPlugin.DebugCalcs.Value)  PregnancyPlusPlugin.Logger.LogInfo($" inflationSize > {Math.Round(infConfig.inflationSize, 2)} for {charaFileName} ");            
            meshInflateFlags.Log();
            if (PregnancyPlusPlugin.DebugLog.Value || PregnancyPlusPlugin.DebugCalcs.Value)  PregnancyPlusPlugin.Logger.LogInfo($" ");

            //Get the measurements that determine the base belly size
            var hasMeasuerments = MeasureWaistAndSphere(ChaControl, meshInflateFlags.reMeasure);    
            //If the character is visible and can't get measurements, throw warning                 
            if (!hasMeasuerments && lastVisibleState) 
            {
                PregnancyPlusPlugin.errorCodeCtrl.LogErrorCode(charaFileName, ErrorCode.PregPlus_BadMeasurement, 
                    $"Could not get one or more belly measurements from character (This is normal when a character is loaded but inactive)");
                return;
            } 
            else if (!hasMeasuerments && !lastVisibleState) 
            {
                if (PregnancyPlusPlugin.DebugLog.Value) PregnancyPlusPlugin.Logger.LogInfo($" Character not visible, can't measure yet {charaFileName}");  
                return; 
            }       

            //Inflate the body mesh.  When it is done it will trigger the same for all other meshes since others depend on the body to be computed first
           var renderKey = FindAndAffectBodyMesh(meshInflateFlags);

            //If the body mesh originalVerticies have been cached, we can go ahead and start computing all other meshes too
            //  Otherwise this will be triggered after ComputeBindPoseMesh()  (Boy do I wish I didnt have to deal with old threading in KK....)
            if (renderKey == null) return;
            if (md.TryGetValue(renderKey, out MeshData _md))
                if (_md.HasOriginalVerts)
                    FindAndAffectAllMeshes(meshInflateFlags, renderKey);  
        }


        /// <summary>
        /// First find the body mesh and compute its inflation.  Other meshes depend on the body's computed verts so we do this first
        /// </summary>
        internal string FindAndAffectBodyMesh(MeshInflateFlags meshInflateFlags)
        {
            var bodyMeshRenderer = GetBodyMeshRenderer();

            //On first pass (or when uncensor changed), compute bind pose bone lists from the body mesh
            bindPoseList.ComputeBindPose(ChaControl, bodyMeshRenderer, meshInflateFlags.uncensorChanged); 
            //Stop if none found, since something went wrong
            if (bindPoseList.bindPoses.Count <= 0) return null;

            //Start computing body mesh changes
            StartMeshChanges(bodyMeshRenderer, meshInflateFlags, isMainBody: true);    

            //Return the render key to check for cached verts
            return GetMeshKey(bodyMeshRenderer);

        }


        /// <summary>
        /// Find every objBody, objClothes, objAccessory mesh and decide whether it needs to be modified by Preg+
        ///     Depends on the Body Mesh originalVerticies already existing
        /// </summary>
        /// <param name="ignoreKey">Any smr matching key will be ignored (Prevent computing main body mesh twice)</param>
        internal void FindAndAffectAllMeshes(MeshInflateFlags meshInflateFlags, string ignoreKey = null)
        {
            var bodyMeshRenderer = GetBodyMeshRenderer();

            //Get all body mesh renderers, calculate, and apply inflation changes
            var bodyRenderers = PregnancyPlusHelper.GetMeshRenderers(ChaControl.objBody, findAll: true);                           
            LoopAndApplyMeshChanges(bodyRenderers, meshInflateFlags, ignoreKey: ignoreKey);
            

            //Dont check cloth mesh on accessory change
            if (!meshInflateFlags.checkForNewAcchMesh)
            {
                //Get all clothing mesh renderers, calculate, and apply inflation changes
                var clothRenderers = PregnancyPlusHelper.GetMeshRenderers(ChaControl.objClothes);            
                LoopAndApplyMeshChanges(clothRenderers, meshInflateFlags, isClothingMesh: true, bodyMeshRenderer);    
            }

            //Only affect accessories, when the user wills it
            if (PregnancyPlusPlugin.IgnoreAccessories.Value) return;
            var accessoryRenderers = PregnancyPlusHelper.GetMeshRenderers(ChaControl.objAccessory);            
            LoopAndApplyMeshChanges(accessoryRenderers, meshInflateFlags, isClothingMesh: true, bodyMeshRenderer);             
        }


        /// <summary>
        /// Loop through each skinned mesh renderer and start mesh changes
        /// </summary>
        /// <param name="smrs">List of skinnedMeshRenderes</param>
        /// <param name="meshInflateFlags">Contains any flags needed for mesh computation</param>
        /// <param name="isClothingMesh">If this smr is a cloth mesh</param>
        /// <returns>boolean true if any meshes were changed</returns>
        internal void LoopAndApplyMeshChanges(List<SkinnedMeshRenderer> smrs, MeshInflateFlags meshInflateFlags, 
                                              bool isClothingMesh = false, SkinnedMeshRenderer bodySmr = null, string ignoreKey = null) 
        {
            foreach (var smr in smrs) 
            {           
                StartMeshChanges(smr, meshInflateFlags, isClothingMesh, bodySmr, ignoreKey:ignoreKey);              
            }  
        }


        /// <summary>
        /// For a skinned mesh renderer check for cached mesh info, then apply inflation when needed
        /// </summary>
        /// <param name="smr">skinnedMeshRender to change</param>
        /// <param name="meshInflateFlags">Contains any flags needed for mesh computation</param>
        /// <param name="isClothingMesh">If this smr is a cloth mesh</param>
        /// <returns>boolean true if threaded</returns>
        internal bool StartMeshChanges(SkinnedMeshRenderer smr, MeshInflateFlags meshInflateFlags, bool isClothingMesh = false, 
                                       SkinnedMeshRenderer bodySmr = null, bool isMainBody = false, string ignoreKey = null)
        {        
            var threadedCompute = false;//Whether the computation has been threaded
            var renderKey = GetMeshKey(smr);
            if (renderKey == null) return false;
            if (renderKey == ignoreKey) return false;

            //Dont recompute verts if no sliders have changed or clothing added
            var needsComputeVerts = NeedsComputeVerts(smr, renderKey, meshInflateFlags);
            if (needsComputeVerts)
                threadedCompute = ComputeMeshVerts(smr, isClothingMesh, bodySmr, meshInflateFlags, renderKey, isMainBody);                                                                                               

            //When threaded, the belly will be set later so we can skip it here (only used when full re-computation is needed)
            if (threadedCompute) return true; 
            if (ignoreMeshList.Contains(renderKey)) return false;       

            //We only make it this far when the shape was previously computed, but we need to upddate the blendshape weight
            FinalizeInflation(smr, meshInflateFlags, blendShapeTempTagName);                   
            return false;
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
            //When no mesh found key, we need to recompute
            if (!isMeshInitialized) return true;
            
            //If the vertex count has not changed then we can skip this if no critical sliders changed
            if (_md.bellyVerticieIndexes.Length == smr.sharedMesh.vertexCount) 
            {
                if (meshInflateFlags.OnlyInflationSizeChanged) return false;
                return meshInflateFlags.SliderHaveChanged;
            }            

            //When incorrect vert count, the mesh changed so we need to recompute
            return true;
        } 


        /// <summary>
        /// Just a helper function to combine searching for verts in a mesh, and then applying the transforms
        /// </summary>
        /// <returns>Will return true if threaded</returns>
        internal bool ComputeMeshVerts(SkinnedMeshRenderer smr, bool isClothingMesh, SkinnedMeshRenderer bodyMeshRenderer, MeshInflateFlags meshInflateFlags, 
                                       string renderKey, bool isMainBody = false) 
        {
            //The list of bones to get verticies for (Belly area verts).  If a mesh does not contain one of these bones in smr.bones, it is skipped
            #if KK            
                var boneFilters = new string[] { "cf_s_spine02", "cf_s_waist01", "cf_s_waist02" };//"cs_s_spine01" optionally for wider affected area
            #elif HS2 || AI
                var boneFilters = new string[] { "cf_J_Spine02_s", "cf_J_Kosi01_s", "cf_J_Kosi02_s" };
            #endif

            var hasVertsToProcess = true;
            var isMeshInitialized = md.TryGetValue(renderKey, out MeshData _md);

            //Only fetch belly vert list when needed since its fairly expensive
            if (meshInflateFlags.NeedsToComputeIndex || !isMeshInitialized)            
                hasVertsToProcess = GetFilteredVerticieIndexes(smr, PregnancyPlusPlugin.MakeBalloon.Value ? null : boneFilters);                    

            //If mesh was just added to the ignore list, stop here
            if (ignoreMeshList.Contains(renderKey)) return false; 

            //If no belly verts found, or verts already cached, then we can skip this mesh
            if (!hasVertsToProcess) return false;             

            //Get the newly created/or existing MeshData obj
            md.TryGetValue(renderKey, out _md);

            //On first pass we need to skin the mesh to a T-pose before computing the inflated verts (Threaded as well)
            if (_md.isFirstPass)
            {                
                return ComputeBindPoseMesh(smr, bodyMeshRenderer, isClothingMesh, meshInflateFlags, isMainBody);            
            }

            return GetInflatedVerticies(smr, bellyInfo.SphereRadius, isClothingMesh, bodyMeshRenderer, meshInflateFlags);
        }


        /// <summary>
        /// Apply the inflation to a blendshape once done computing inflated verts and deltas
        /// </summary>
        internal void FinalizeInflation(SkinnedMeshRenderer smr, MeshInflateFlags meshInflateFlags, string blendShapeTag = null)
        {
            var rendererName = GetMeshKey(smr);   

            //Apply computed mesh back to body as a blendshape
            var appliedMeshChanges = ApplyInflation(smr, rendererName, meshInflateFlags.OverWriteMesh, blendShapeTempTagName, meshInflateFlags.bypassWhen0);

            //When inflation is actively happening as clothing changes, make sure the new clothing grows too
            if (isDuringInflationScene) AppendToQuickInflateList(smr);

            //If the inflation is applied, update the previous slider config values
            if (appliedMeshChanges) infConfigHistory = (PregnancyPlusData)infConfig.Clone();
        }


        /// <summary>
        /// Compute and cache the bind pose (T-pose) mesh vertex positions, we need this to ignore all character animations when computing the belly shape, 
        ///     and to align the meshes together. Results get cached for subsiquent passes
        /// </summary>
        /// <returns>Will return true if threaded</returns>
        internal bool ComputeBindPoseMesh(SkinnedMeshRenderer smr, SkinnedMeshRenderer bodySmr, bool isClothingMesh, MeshInflateFlags meshInflateFlags, bool isMainBody = false)
        {
            if (smr == null) 
            {
                if (PregnancyPlusPlugin.DebugLog.Value) PregnancyPlusPlugin.Logger.LogWarning($" ComputeBindPoseMesh smr was null"); 
                return false;
            }

            if (PregnancyPlusPlugin.DebugCalcs.Value) PregnancyPlusPlugin.Logger.LogInfo($" Computing BindPoseMesh for {smr.name}");

            //If mesh is not readable, make it so
            if (!smr.sharedMesh.isReadable) nativeDetour.Apply();

            var rendererName = GetMeshKey(smr);
            var exists = md.TryGetValue(rendererName, out MeshData _md);     
            if (!exists) 
            {
                if (PregnancyPlusPlugin.DebugLog.Value) PregnancyPlusPlugin.Logger.LogWarning($" ComputeBindPoseMesh cant find MeshData for {rendererName}"); 
                return false;
            }

            //initialize original verts if not already
            if (md[rendererName].originalVertices == null || !md[rendererName].HasOriginalVerts) 
                md[rendererName].originalVertices = new Vector3[smr.sharedMesh.vertexCount];                      

            Matrix4x4[] boneMatrices = null;
            BoneWeight[] boneWeights = null;
            Vector3[] unskinnedVerts = null; 
        
            //Plugin config option lets us visalize bindpose positions 
            if (PregnancyPlusPlugin.ShowBindPose.Value) 
                MeshSkinning.ShowBindPose(ChaControl, smr, bindPoseList);  

            //Matricies used to compute the T-pose mesh
            boneMatrices = MeshSkinning.GetBoneMatrices(ChaControl, smr, bindPoseList);
            boneWeights = smr.sharedMesh.boneWeights;
            unskinnedVerts = smr.sharedMesh.vertices;   

            //Thread safe lists and objects below                    
            var skinnedVerts = md[rendererName].originalVertices;    
            var vertsLength = smr.sharedMesh.vertexCount;
            var smrTfTransPt = smr.transform.localToWorldMatrix;
            
            nativeDetour.Undo();

            //Heavy compute task below, run in separate thread
            WaitCallback threadAction = (System.Object stateInfo) => 
            {
                //Compute and store T-pose skinned mesh verts
                for (int i = 0; i < vertsLength; i++)
                {
                    //Get the skinned vert position from the bindpose matrix we computed earlier
                    skinnedVerts[i] = MeshSkinning.UnskinnedToSkinnedVertex(unskinnedVerts[i], smrTfTransPt, boneMatrices, boneWeights[i]);
                }

                //When this thread task is complete, execute the below in main thread
                Action threadActionResult = () => 
                {                    
                    //This one here is critical.  Once the originalVertices have been computed for the body mesh, the other meshes can now also be computed too
                    if (isMainBody && md[rendererName].isFirstPass)
                        FindAndAffectAllMeshes(meshInflateFlags, rendererName);

                    md[rendererName].isFirstPass = false;

                    //Now that the mesh is skinned to T-pose, we can compute the inflated state
                    GetInflatedVerticies(smr, bellyInfo.SphereRadius, isClothingMesh, bodySmr, meshInflateFlags);
                };

                //Append to result queue.  Will execute on next Update()
                threading.AddResultToThreadQueue(threadActionResult);
            };
            
            
            //Start this threaded task, and will be watched in Update() for completion
            threading.Start(threadAction);            

            return true;
        }


        /// <summary>
        /// Does the vertex morph calculations to make a sphere out of the belly verticies, and updates the vertex dictionaries apprporiately
        /// </summary>
        /// <param name="skinnedMeshRenderer">The mesh renderer target</param>
        /// <param name="sphereRadius">The radius of the inflation sphere</param>
        /// <param name="isClothingMesh">Clothing requires a few tweaks to match skin morphs</param>
        /// <returns>Will return true if threaded</returns>
        internal bool GetInflatedVerticies(SkinnedMeshRenderer smr, float sphereRadius, bool isClothingMesh, 
                                           SkinnedMeshRenderer bodySmr, MeshInflateFlags meshInflateFlags) 
        {
            if (smr == null) 
            {
                if (PregnancyPlusPlugin.DebugLog.Value) PregnancyPlusPlugin.Logger.LogWarning($" GetInflatedVerticies smr was null"); 
                return false;
            }

            if (PregnancyPlusPlugin.DebugCalcs.Value) PregnancyPlusPlugin.Logger.LogInfo($" GetInflatedVerticies for {smr.name}");

            //Found out body mesh can be nested under cloth game objects...   Make sure to flag it as non-clothing
            if (isClothingMesh && BodyNestedUnderCloth(smr, bodySmr)) 
            {
                PregnancyPlusPlugin.errorCodeCtrl.LogErrorCode(charaFileName, ErrorCode.PregPlus_BodyMeshDisguisedAsCloth, 
                    $" body mesh {smr.name} was nested under cloth object {smr.transform.parent.name}.  This is usually not an issue.");
                isClothingMesh = false;            
            }
            
            // if (PregnancyPlusPlugin.DebugLog.Value) PregnancyPlusPlugin.Logger.LogInfo($" SMR pos {smr.transform.position} rot {smr.transform.rotation} parent {smr.transform.parent}");                     
            if (!smr.sharedMesh.isReadable) nativeDetour.Apply();          

            var rendererName = GetMeshKey(smr);   
            var exists = md.TryGetValue(rendererName, out MeshData _md);     
            if (!exists) 
            {
                if (PregnancyPlusPlugin.DebugLog.Value) PregnancyPlusPlugin.Logger.LogWarning($" GetInflatedVerticies cant find MeshData for {rendererName}"); 
                return false;
            }
            md[rendererName].inflatedVertices = new Vector3[smr.sharedMesh.vertexCount];
            md[rendererName].alteredVerticieIndexes = new bool[smr.sharedMesh.vertexCount];

            //set sphere center and allow for adjusting its position from the UI sliders  
            Vector3 sphereCenter = GetSphereCenter();            

            //Create mesh collider to make clothing measurements from skin (if it doesnt already exists)         
            if (NeedsClothMeasurement(smr, bodySmr, sphereCenter, isClothingMesh)) CreateMeshCollider(bodySmr); 
           
            //Get the cloth offset for each cloth vertex via raycast to skin
            //  Unfortunately this cant be inside the thread below because Unity Raycast are not thread safe...
            float[] clothOffsets = DoClothMeasurement(smr, bodySmr, sphereCenter);
            if (clothOffsets == null) clothOffsets = new float[md[rendererName].originalVertices.Length];

            //Store verts in thread safe variables, just in case
            var origVerts = md[rendererName].originalVertices;
            var inflatedVerts = md[rendererName].inflatedVertices;
            var bellyVertIndex = md[rendererName].bellyVerticieIndexes;
            var alteredVerts = md[rendererName].alteredVerticieIndexes;

            //Pre compute some values needed by SculptInflatedVerticie, doin it here saves on compute in the big loop
            var vertsLength = origVerts.Length;
            var preMorphSphereCenter = sphereCenter - GetUserMoveTransform();
            //calculate the furthest back morph point based on the back bone position, include character rotation
            var backExtentPos = new Vector3(preMorphSphereCenter.x, sphereCenter.y, preMorphSphereCenter.z) + Vector3.forward * -bellyInfo.ZLimit;
            //calculate the furthest top morph point based under the breast position, include character animated height differences
            var topExtentPos = new Vector3(preMorphSphereCenter.x, preMorphSphereCenter.y, preMorphSphereCenter.z) + Vector3.up * bellyInfo.YLimit;
            var vertNormalCaluRadius = sphereRadius + bellyInfo.WaistWidth/10;//Only recalculate normals for verts within this radius to prevent shadows under breast at small belly sizes          
            var waistWidth = bellyInfo.WaistWidth;

            //Lock in current slider values for threaded calculation
            var infConfigClone = (PregnancyPlusData)infConfig.Clone();

            //Animation curves are not thread safe, so make copies here
            var bellySidesAC = new ThreadsafeCurve(BellySidesAC);
            var bellyTopAC = new ThreadsafeCurve(BellyTopAC);
            var bellyEdgeAC = new ThreadsafeCurve(BellyEdgeAC);

            logCharMeshInfo(md[rendererName], smr, sphereCenter, isClothingMesh);

            nativeDetour.Undo();

            //Heavy compute task below, run in separate thread
            WaitCallback threadAction = (System.Object stateInfo) => 
            {
                var reduceClothFlattenOffset = 0f;

                #if DEBUG
                    // var bellyVertsCount = 0;
                    // for (int i = 0; i < bellyVertIndex.Length; i++)
                    // {
                    //     if (bellyVertIndex[i]) bellyVertsCount++;
                    // }
                    // if (PregnancyPlusPlugin.DebugCalcs.Value) PregnancyPlusPlugin.Logger.LogInfo($" Mesh affected vert count {bellyVertsCount} {smr.name}");
                #endif               

                //For each vert, compute it's new inflated position if it is a belly vert
                for (int i = 0; i < vertsLength; i++)
                {
                    //Only care about altering belly verticies
                    if (!bellyVertIndex[i] && !PregnancyPlusPlugin.DebugVerts.Value) 
                    {
                        inflatedVerts[i] = origVerts[i];
                        continue;                
                    }

                    //Get the bindpose skinned vertex position
                    var origVertLs = origVerts[i];                
                    var vertDistance = Vector3.Distance(origVertLs, sphereCenter);                    

                    //Ignore verts outside the sphere radius
                    if (vertDistance > vertNormalCaluRadius && !PregnancyPlusPlugin.DebugVerts.Value) 
                    {
                        inflatedVerts[i] = origVerts[i];
                        continue;                
                    }
                    
                    Vector3 inflatedVertLs;                    
                    Vector3 verticieToSpherePos;       
                    reduceClothFlattenOffset = 0f; 

                    // If the vert is within the calculated normals radius, then consider it as an altered vert that needs normal recalculation when applying inflation
                    // This also means we can ignore other verts later saving compute time
                    //  Hopefully this will reduce breast shadows for smaller bellies
                    if (vertDistance <= vertNormalCaluRadius) alteredVerts[i] = true;                                                                          
                    
                    if (isClothingMesh) 
                    {                        
                        //Calculate clothing offset distance                   
                        reduceClothFlattenOffset = GetClothesFixOffset(infConfigClone, sphereCenter, sphereRadius, waistWidth, origVertLs, smr.name, clothOffsets[i]);
                    }
                        
                    //Shift each belly vertex away from sphere center in a sphere pattern.  This is the core of the Preg+ belly shape
                    verticieToSpherePos = (origVertLs - sphereCenter).normalized * (sphereRadius + reduceClothFlattenOffset) + sphereCenter;                                                    

                    //Make adjustments to the shape to make it smooth, and feed in user slider input
                    inflatedVertLs =  SculptInflatedVerticie(infConfigClone, origVertLs, verticieToSpherePos, sphereCenter, waistWidth, 
                                                             preMorphSphereCenter, sphereRadius, backExtentPos, topExtentPos, 
                                                             bellySidesAC, bellyTopAC, bellyEdgeAC);   

                    //store the new inflated vert, unshifted from 0,0,0                                                           
                    inflatedVerts[i] = inflatedVertLs;                                                  
                }                  

                //When this thread task is complete, execute the below in main thread
                Action threadActionResult = () => 
                {
                    //Debug verts when needed
                    PostInflationDebugStuff(smr, md[rendererName], isClothingMesh);            

                    //Now that we have the before and after inflated verts we can get the delta of each
                    var threaded = ComputeDeltas(smr, rendererName, meshInflateFlags);

                    //When we have already pre-computed the deltas we can go ahead and finalize now
                    if (!threaded)
                        FinalizeInflation(smr, meshInflateFlags, blendShapeTempTagName);
                };

                //Append to result queue.  Will execute on next Update()
                threading.AddResultToThreadQueue(threadActionResult);

            };

            //Start this threaded task, and will be watched in Update() for completion
            threading.Start(threadAction);

            return true;                 
        }


        /// <summary>
        /// Depending on plugin config state, shows calculated verts on screen
        /// </summary>
        internal void PostInflationDebugStuff(SkinnedMeshRenderer smr, MeshData md, bool isClothingMesh)
        {
            //If you need to debug the calculated vert positions visually
            if (PregnancyPlusPlugin.DebugLog.Value) 
            {

                //Debug mesh with spheres, and include mesh offset
                // DebugTools.DebugMeshVerts(smr, origVerts, new Vector3(0, md[rendererName].yOffset, 0));

                //Some other internally measured points/boundaries
                // if (PregnancyPlusPlugin.DebugLog.Value) DebugTools.DrawSphereAndAttach(smr.transform, 0.2f, sphereCenter);
                // if (PregnancyPlusPlugin.DebugLog.Value) DebugTools.DrawLine(topExtentPos + Vector3.back * 0.5f, topExtentPos);                        
                // if (PregnancyPlusPlugin.DebugLog.Value) DebugTools.DrawLine(topExtentPos + Vector3.down * bellyInfo.YLimitOffset + Vector3.back * 0.5f, topExtentPos + Vector3.down * bellyInfo.YLimitOffset);                        
                // if (PregnancyPlusPlugin.DebugLog.Value) DebugTools.DrawLine(backExtentPos, backExtentPos + Vector3.left * 4);                        
                // if (PregnancyPlusPlugin.DebugLog.Value) DebugTools.DrawLine(sphereCenter, sphereCenter + Vector3.forward * 1);  
                // if (PregnancyPlusPlugin.DebugLog.Value) DebugTools.DrawSphere(0.1f, preMorphSphereCenter);
                // if (PregnancyPlusPlugin.DebugLog.Value) DebugTools.DrawLine(Vector3.zero, Vector3.zero + Vector3.forward * 1);  
                
                // if (PregnancyPlusPlugin.DebugLog.Value && isClothingMesh) DebugTools.DrawLineAndAttach(smr.transform, 1, smr.sharedMesh.bounds.center - yOffsetDir);
            }        

            //Show verts on screen when this debug option is enabled (smaller spheres for body meshes)
            if (PregnancyPlusPlugin.ShowUnskinnedVerts.Value)  
            {
                if (!smr.sharedMesh.isReadable) nativeDetour.Apply();  
                DebugTools.DebugMeshVerts(smr.sharedMesh.vertices, size: (isClothingMesh ? 0.01f : 0.005f));                                          
                nativeDetour.Undo();
            }

            if (PregnancyPlusPlugin.ShowSkinnedVerts.Value)  
                DebugTools.DebugMeshVerts(md.originalVertices, color: Color.cyan, size: (isClothingMesh ? 0.01f : 0.005f));                                          

            if (PregnancyPlusPlugin.ShowInflatedVerts.Value)  
                DebugTools.DebugMeshVerts(md.inflatedVertices, color: Color.green, size: (isClothingMesh ? 0.01f : 0.005f)); 
        }


        /// <summary>
        /// Calculates the center position of the belly sphere.  Including the user slider value
        /// </summary>
        internal Vector3 GetSphereCenter() 
        {          
            float bbHeight;

            //in 6.0+ we can use a static sphere center height
            if (infConfig.IsPluginVersionBelow(6.0))
            {
                //Measure from feet to belly 
                bbHeight = GetBellyButtonLocalHeight();
            }
            else 
            {
                #if KK
                    bbHeight = 0.97f;           
                #else
                    bbHeight = 10f;
                #endif
            }
            bellyInfo.BellyButtonHeight = bbHeight;           
            Vector3 bellyButtonPos = Vector3.up * bbHeight; 

            //Include user slider values "Move Y" and "Move Z"
            return bellyButtonPos + GetUserMoveTransform() + GetBellyButtonOffsetVector(bbHeight);                                         
        }


        /// <summary>
        /// This will take the sphere-ified verticies and apply smoothing to them to round out sharp edges, and limit the bellys' position
        /// </summary>
        /// <param name="originalVertice">The original verticie position</param>
        /// <param name="inflatedVerticie">The target verticie position, after sphere-ifying</param>
        /// <param name="sphereCenterPos">The center of the imaginary sphere</param>
        /// <param name="waistWidth">The characters waist width that limits the width of the belly (future implementation)</param>
        /// <param name="preMorphSphereCenter">The original sphere center location, before user slider input</param>
        /// <param name="backExtentPos">The point behind which no mesh changes allowed</param>
        /// <param name="topExtentPos">The point above which no mesh changes allowed</param>
        internal Vector3 SculptInflatedVerticie(PregnancyPlusData infConfigClone, Vector3 originalVerticeLs, Vector3 inflatedVerticieLs, Vector3 sphereCenterLs, float waistWidth, 
                                                Vector3 preMorphSphereCenter, float sphereRadius, Vector3 backExtentPos, Vector3 topExtentPos, 
                                                ThreadsafeCurve bellySidesAC, ThreadsafeCurve bellyTopAC, ThreadsafeCurve bellyEdgeAC) 
        {
            //No smoothing modification in debug mode
            if (PregnancyPlusPlugin.MakeBalloon.Value || PregnancyPlusPlugin.DebugVerts.Value) return inflatedVerticieLs;                       
            
            //get the smoothing distance limits so we don't have weird polygons and shapes on the edges, and prevents morphs from shrinking past original skin boundary
            var pmSkinToCenterDist = Math.Abs(Vector3.Distance(preMorphSphereCenter, originalVerticeLs));
            var pmInflatedToCenterDist = Math.Abs(Vector3.Distance(preMorphSphereCenter, inflatedVerticieLs));
            var skinToCenterDist = Math.Abs(Vector3.Distance(sphereCenterLs, originalVerticeLs));
            var inflatedToCenterDist = Math.Abs(Vector3.Distance(sphereCenterLs, inflatedVerticieLs));
            

            //Only apply morphs if the imaginary sphere is outside of the skins boundary (Don't want to shrink anything inwards, only out)
            if (skinToCenterDist >= inflatedToCenterDist || pmSkinToCenterDist > pmInflatedToCenterDist) return originalVerticeLs; 
            
            //Get the base shape with XY plane size limits
            var smoothedVectorLs = SculptBaseShape(originalVerticeLs, inflatedVerticieLs, sphereCenterLs);                 

            //Allow user adjustment of the height and width placement of the belly
            if (GetInflationShiftY(infConfigClone) != 0 || GetInflationShiftZ(infConfigClone) != 0) 
            {
                smoothedVectorLs = GetUserShiftTransform(infConfigClone, smoothedVectorLs, sphereCenterLs, skinToCenterDist);            
            }

            //Allow user adjustment of the width of the belly
            if (GetInflationStretchX(infConfigClone) != 0) 
            {   
                smoothedVectorLs = GetUserStretchXTransform(infConfigClone, smoothedVectorLs, sphereCenterLs);
            }

            //Allow user adjustment of the height of the belly
            if (GetInflationStretchY(infConfigClone) != 0) 
            {   
                smoothedVectorLs = GetUserStretchYTransform(infConfigClone, smoothedVectorLs, sphereCenterLs);
            }

            if (GetInflationRoundness(infConfigClone) != 0) 
            {  
                smoothedVectorLs = GetUserRoundnessTransform(infConfigClone, originalVerticeLs, smoothedVectorLs, sphereCenterLs, skinToCenterDist, bellyEdgeAC);
            }

            //Allow user adjustment of the egg like shape of the belly
            if (GetInflationTaperY(infConfigClone) != 0) 
            {
                smoothedVectorLs = GetUserTaperYTransform(infConfigClone, smoothedVectorLs, sphereCenterLs, skinToCenterDist);
            }

            //Allow user adjustment of the front angle of the belly
            if (GetInflationTaperZ(infConfigClone) != 0) 
            {
                smoothedVectorLs = GetUserTaperZTransform(infConfigClone, originalVerticeLs, smoothedVectorLs, sphereCenterLs, skinToCenterDist, backExtentPos);
            }

            //Allow user adjustment of the fat fold line through the middle of the belly
            if (GetInflationFatFold(infConfigClone) > 0) 
            {
                smoothedVectorLs = GetUserFatFoldTransform(infConfigClone, originalVerticeLs, smoothedVectorLs, sphereCenterLs, sphereRadius);
            }            

            //If the user has selected a drop slider value
            if (GetInflationDrop(infConfigClone) > 0) 
            {
                smoothedVectorLs = GetUserDropTransform(infConfigClone, Vector3.up, smoothedVectorLs, sphereCenterLs, skinToCenterDist, sphereRadius);
            }

            //After all user transforms are applied, remove the edges from the sides/top of the belly
            smoothedVectorLs = RoundToSides(originalVerticeLs, smoothedVectorLs, backExtentPos, bellySidesAC);            

            //Less skin stretching under breast area with large slider values
            if (originalVerticeLs.y > preMorphSphereCenter.y)
            {                
                smoothedVectorLs = ReduceRibStretchingZ(originalVerticeLs, smoothedVectorLs, topExtentPos, bellyTopAC);
            }

            //At this point if the smoothed vector is still the originalVector just return it
            if (smoothedVectorLs.Equals(originalVerticeLs)) return smoothedVectorLs;


            //**** All of the below are post vert calculation checks to make sure the vertex position don't go outside of bounds (or inside the character)
  
            //Get core point on the same y plane as the original vert
            var coreLineVertLs = Vector3.up * originalVerticeLs.y;
            //Get core line from feet to head that verts must respect distance from
            var origCoreDist = Math.Abs(Vector3.Distance(originalVerticeLs, coreLineVertLs));
            //Do the same for the smoothed vert
            var coreLineSmoothedVertLs = Vector3.up * smoothedVectorLs.y;       
            var currentCoreDist = Math.Abs(Vector3.Distance(smoothedVectorLs, coreLineSmoothedVertLs)); 


            //** Order matters below **  <I even wrote this comment and still managed to ignore it....  :/>


            //Don't allow any morphs to shrink towards the characters core line any more than the original distance
            if (currentCoreDist < origCoreDist) 
            {
                //Since this is just an XZ distance check, don't modify the new y value
                smoothedVectorLs = new Vector3(originalVerticeLs.x, smoothedVectorLs.y, originalVerticeLs.z);
            }

            //Compute the new distances from vert to sphereCenters
            var currentVectorDistance = Math.Abs(Vector3.Distance(sphereCenterLs, smoothedVectorLs));
            var pmCurrentVectorDistance = Math.Abs(Vector3.Distance(preMorphSphereCenter, smoothedVectorLs)); 

            //Don't allow any morphs to shrink towards the sphere center more than its original distance, only outward morphs allowed
            if (skinToCenterDist > currentVectorDistance || pmSkinToCenterDist > pmCurrentVectorDistance)             
                return originalVerticeLs;            

            //Don't allow any morphs to move behind the character's.z = 0 + extentOffset position, otherwise skin sometimes pokes out the back side :/
            if (backExtentPos.z > smoothedVectorLs.z)             
                return originalVerticeLs;            

            //Don't allow any morphs to move behind the original verticie z position, only forward expansion (ignoring ones already behind sphere center)
            if (originalVerticeLs.z > smoothedVectorLs.z && originalVerticeLs.z > sphereCenterLs.z) 
            {
                //Get the average(not really average after all...) x and y change to move the new position halfway back to the oiriginal vert (hopefullt less strange triangles near belly to body edge)
                var yChangeAvg = (smoothedVectorLs.y - originalVerticeLs.y)/3;
                var xChangeAvg = (smoothedVectorLs.x - originalVerticeLs.x)/3;
                smoothedVectorLs = new Vector3(smoothedVectorLs.x - xChangeAvg, smoothedVectorLs.y - yChangeAvg, originalVerticeLs.z);
            }

            return smoothedVectorLs;             
        }


        /// <summary>
        /// Compute the deltas between the original mesh and the inflated one. (This is threaded now)  We use these deltas to build the blendshape
        /// </summary>
        /// <returns>returns bool whether the action needs to be threaded or not</returns>
        internal bool ComputeDeltas(SkinnedMeshRenderer smr, string rendererName, MeshInflateFlags meshInflateFlags) 
        {           
            //Check for mesh data object
            var isMeshInitialized = md.TryGetValue(rendererName, out MeshData _md);
            if (!isMeshInitialized) return false;

            //If we already have the deltas then skip
            if (_md.HasDeltas && !meshInflateFlags.OverWriteMesh) return false;

            //Get the virtual inflated mesh with normal, and tangent recalculation applied
            var inflatedMesh = PrepForBlendShape(smr, rendererName);
            if (!inflatedMesh) return false;

            if (PregnancyPlusPlugin.DebugCalcs.Value) PregnancyPlusPlugin.Logger.LogInfo($" Compute BlendShape Deltas for {smr.name}");

            //When SMR has local rotation undo it in the deltas
            var rotationUndo = Matrix4x4.TRS(Vector3.zero, smr.transform.localRotation, Vector3.one).inverse;
            //When a smr bindpose has scale, we need to undo it in the delta similar to rotation
            var scaleUndo = MeshSkinning.GetBindPoseScale(smr).inverse;
            var undoTfMatrix = rotationUndo * scaleUndo;

            if (PregnancyPlusPlugin.DebugLog.Value && scaleUndo != Matrix4x4.identity) 
                PregnancyPlusPlugin.Logger.LogWarning($" smr {smr.name} has bindpose scale {Matrix.GetScale(scaleUndo)}");

            if (!smr.sharedMesh.isReadable) nativeDetour.Apply();

            //Store values in thread safe variable
            var sourceNormals = smr.sharedMesh.normals;
            var targetNormals = inflatedMesh.normals;
            var sourceTangents = smr.sharedMesh.tangents;
            var targetTangents = inflatedMesh.tangents;
            var originalVerts = _md.originalVertices;
            var inflatedVerts = _md.inflatedVertices;
            var alteredVerts = _md.alteredVerticieIndexes;

            nativeDetour.Undo();

            //Heavy compute task below, run in separate thread
            WaitCallback threadAction = (System.Object stateInfo) => 
            {

                //Get delta diffs of the two meshes used to make the blendshape
                var deltaVerticies = BlendShapeTools.GetV3Deltas(originalVerts, inflatedVerts, undoTfMatrix, alteredVerts);
                var deltaNormals = BlendShapeTools.GetV3Deltas(sourceNormals, targetNormals, undoTfMatrix, alteredVerts);
                var deltaTangents = BlendShapeTools.GetV3Deltas(sourceTangents, targetTangents, undoTfMatrix, alteredVerts);                            

                //When this thread task is complete, execute the below in main thread
                Action threadActionResult = () => 
                {
                    //Cache the computed deltas in MeshData
                    _md.deltaVerticies = deltaVerticies;
                    _md.deltaNormals = deltaNormals;
                    _md.deltaTangents = deltaTangents;

                    //When we need to debug the deltas visually
                    if (PregnancyPlusPlugin.ShowDeltaVerts.Value) 
                    {
                        for (int i = 0; i < deltaVerticies.Length; i++)
                        {
                            //Undo delta rotation visually so we can make sure it aligns with the other meshes deltas
                            DebugTools.DrawLine(_md.originalVertices[i], _md.originalVertices[i] + rotationUndo.inverse.MultiplyPoint3x4(deltaVerticies[i]));     
                        }                          
                    }

                    //Now we can create and apply the blendshape
                    FinalizeInflation(smr, meshInflateFlags, blendShapeTempTagName);                
                };

                //Append to result queue.  Will execute on next Update()
                threading.AddResultToThreadQueue(threadActionResult);
            };

            //Start this threaded task, and will be watched in Update() for completion
            threading.Start(threadAction);

            return true;
        }
                
    }
}


