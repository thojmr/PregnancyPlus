using KKAPI;
using KKAPI.Chara;
using UnityEngine;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
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
            //Only allow one MeshInflate process at a time (Since it runs across multiple frames at a time)
            if (isProcessing) 
            {
                //Store the last attempted flags to re-run once processing is finished
                lastMeshInflateFlags = (MeshInflateFlags)meshInflateFlags.Clone();
                return;
            }
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

            //start the mesh inflation logic
            DoInflation(meshInflateFlags);            
        }


        /// <summary>
        /// Start the belly inflation logic flow.  Prevent more than one instance of it from running at a time.
        ///     If an incoming request occurs during processing, stop it and trigger it again when done
        /// </summary>
        internal async void DoInflation(MeshInflateFlags meshInflateFlags)
        {
            if (isProcessing) 
            {
                //Store the last attempted flags to re-run once processing is finished
                lastMeshInflateFlags = (MeshInflateFlags)meshInflateFlags.Clone();
                return;
            }
            //Prevent multiple processes from running at the same time
            isProcessing = true;

            //Inflate the body mesh first.  When done do the same for all meshes
            var renderKey = await FindAndAffectBodyMesh(meshInflateFlags);
            await FindAndAffectAllMeshes(meshInflateFlags, renderKey);             

            //Debug verts when needed
            PostInflationDebugStuff(); 

            //Mark done processing
            isProcessing = false;

            //If any MeshInflate() calls were triggered while isProcessing==true, re-apply that request since it was skipped
            if (lastMeshInflateFlags == null) return;
            
            //Make clone so we can clear the old value now
            var lastFlagsClone = (MeshInflateFlags)lastMeshInflateFlags.Clone();
            lastMeshInflateFlags = null;

            MeshInflate(lastFlagsClone, "lastMeshInflateFlags");                
            
            //Finally remove the mesh collider since it is no longer needed
            RemoveMeshCollider();
        }


        /// <summary>
        /// Find the body mesh and compute its inflated verts.  
        ///     Other meshes depend on the body's computed verts so we do this first
        /// </summary>
        internal async Task<string> FindAndAffectBodyMesh(MeshInflateFlags meshInflateFlags)
        {
            var bodyMeshRenderer = GetBodyMeshRenderer();

            //On first pass (or when uncensor changed), compute bind pose bone lists from the body mesh
            bindPoseList.ComputeBindPose(ChaControl, bodyMeshRenderer, meshInflateFlags.uncensorChanged); 
            //Stop if none found, since something went wrong
            if (bindPoseList.bindPoses.Count <= 0) return null;

            //Start computing body mesh changes
            await StartMeshChanges(bodyMeshRenderer, meshInflateFlags, isMainBody: true);    

            //Return the render key to check for cached verts
            return GetMeshKey(bodyMeshRenderer);
        }


        /// <summary>
        /// Find every objBody, objClothes, objAccessory mesh and decide whether it needs to be modified by Preg+
        ///     Depends on the Body Mesh originalVerticies already existing
        /// </summary>
        /// <param name="ignoreKey">Any smr matching key will be ignored (Prevent computing main body mesh twice)</param>
        internal async Task FindAndAffectAllMeshes(MeshInflateFlags meshInflateFlags, string ignoreKey = null)
        {
            var bodyMeshRenderer = GetBodyMeshRenderer();
            //Collect all running mesh tasks
            List<Task> tasks = new List<Task>();

            //Get all body mesh renderers, calculate, and apply inflation changes
            var bodyRenderers = PregnancyPlusHelper.GetMeshRenderers(ChaControl.objBody, findAll: true);                           
            tasks.AddRange(LoopAndApplyMeshChanges(bodyRenderers, meshInflateFlags, ignoreKey: ignoreKey));
            

            //Dont check cloth mesh on accessory change
            if (!meshInflateFlags.checkForNewAcchMesh)
            {
                //Get all clothing mesh renderers, calculate, and apply inflation changes
                var clothRenderers = PregnancyPlusHelper.GetMeshRenderers(ChaControl.objClothes);         
                tasks.AddRange(LoopAndApplyMeshChanges(clothRenderers, meshInflateFlags, isClothingMesh: true, bodyMeshRenderer));    
            }

            //Only affect accessories, when the user wills it
            if (PregnancyPlusPlugin.IgnoreAccessories.Value) return;
            var accessoryRenderers = PregnancyPlusHelper.GetMeshRenderers(ChaControl.objAccessory);            
            tasks.AddRange(LoopAndApplyMeshChanges(accessoryRenderers, meshInflateFlags, isClothingMesh: true, bodyMeshRenderer));           

            //Wait for all to be done
            await Task.WhenAll(tasks);
        }


        /// <summary>
        /// Loop through each skinned mesh renderer and start mesh changes
        /// </summary>
        /// <param name="smrs">List of skinnedMeshRenderes</param>
        /// <param name="meshInflateFlags">Contains any flags needed for mesh computation</param>
        /// <param name="isClothingMesh">If this smr is a cloth mesh</param>
        /// <param name="ignoreKey">Any smr matching key will be ignored (Prevent computing main body mesh twice)</param>
        /// <returns>boolean true if any meshes were changed</returns>
        internal Task[] LoopAndApplyMeshChanges(List<SkinnedMeshRenderer> smrs, MeshInflateFlags meshInflateFlags, 
                                              bool isClothingMesh = false, SkinnedMeshRenderer bodySmr = null, string ignoreKey = null) 
        {
            var tasks = new Task[smrs.Count];

            for (var i = 0; i < smrs.Count; i++) 
            {           
                tasks[i] = StartMeshChanges(smrs[i], meshInflateFlags, isClothingMesh, bodySmr, ignoreKey:ignoreKey);              
            }  

            return tasks;
        }


        /// <summary>
        /// For a skinned mesh renderer check for cached mesh info, then apply inflation when needed
        ///     This is technically the core logic loop for a single mesh
        /// </summary>
        /// <param name="smr">skinnedMeshRender to change</param>
        /// <param name="meshInflateFlags">Contains any flags needed for mesh computation</param>
        /// <param name="isClothingMesh">If this smr is a cloth mesh</param>
        /// <param name="bodySmr">The characters body mesh</param>
        /// <param name="isMainBody">Whether this mesh is the character body mesh</param>
        /// <param name="ignoreKey">Any smr matching key will be ignored (Prevent computing main body mesh twice)</param>
        internal async Task StartMeshChanges(SkinnedMeshRenderer smr, MeshInflateFlags meshInflateFlags, bool isClothingMesh = false, 
                                       SkinnedMeshRenderer bodySmr = null, bool isMainBody = false, string ignoreKey = null)
        {        
            var renderKey = GetMeshKey(smr);
            if (renderKey == null) return;
            if (renderKey == ignoreKey) return;

            //Dont recompute verts if no sliders have changed or clothing added
            var needsComputeVerts = NeedsComputeVerts(smr, renderKey, meshInflateFlags);
            if (needsComputeVerts)
            {
                //If it turns out that the current mesh is cached, then this will just return early
                var vertsChanged = await ComputeVerts(smr, isClothingMesh, bodySmr, meshInflateFlags, renderKey, isMainBody);   

                //Compute the deltas used to make blendshapes
                if (vertsChanged)
                    await ComputeDeltas(smr, renderKey, meshInflateFlags);                                                                                          
            }

            if (ignoreMeshList.Contains(renderKey)) return;       

            //Take the vert deltas we computed earlier and make a blendshape from them.  Otherwise just adjust the existing blendshape weight
            FinalizeInflation(smr, meshInflateFlags, blendShapeTempTagName); 
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
        /// Searching for valid verts in a mesh, and compute the mesh shapes
        /// </summary>
        /// <returns>Will return true if verts were re-computed</returns>
        internal async Task<bool> ComputeVerts(SkinnedMeshRenderer smr, bool isClothingMesh, SkinnedMeshRenderer bodyMeshRenderer, 
                                               MeshInflateFlags meshInflateFlags, string renderKey, bool isMainBody = false) 
        {
            //The list of bones to get verticies for (Belly area verts).  If a mesh does not contain one of these bones in smr.bones, it is skipped
            #if KKS            
                var boneFilters = new string[] { "cf_s_spine02", "cf_s_waist01", "cf_s_waist02" };//"cs_s_spine01" optionally for wider affected area
            #elif HS2 || AI
                var boneFilters = new string[] { "cf_J_Spine02_s", "cf_J_Kosi01_s", "cf_J_Kosi02_s" };
            #endif

            var hasVertsToProcess = true;
            var isMeshInitialized = md.TryGetValue(renderKey, out MeshData _md);

            //Only fetch belly vert list when needed to save compute
            if (meshInflateFlags.NeedsToComputeIndex || !isMeshInitialized)            
                hasVertsToProcess = await GetFilteredVerticieIndexes(smr, PregnancyPlusPlugin.MakeBalloon.Value ? null : boneFilters);                    

            //If mesh was just added to the ignore list, stop here
            if (ignoreMeshList.Contains(renderKey)) return false; 

            //If no belly verts found, or verts already cached, then we can skip this mesh
            if (!hasVertsToProcess) return false;             

            //Get the newly created/or existing MeshData obj
            md.TryGetValue(renderKey, out _md);
            
            if (_md.isFirstPass)
            {                
                //On first pass we need to skin the mesh to a T-pose before computing the inflated verts (Threaded as well)
                await ComputeBindPoseMesh(smr, bodyMeshRenderer, isClothingMesh, meshInflateFlags, isMainBody);            
            }

            //Inflate the mesh to give it the round appearance
            await GetInflatedVerticies(smr, bellyInfo.SphereRadius, isClothingMesh, bodyMeshRenderer, meshInflateFlags);

            return true;
        }


        /// <summary>
        /// Apply the computed shape to a blendshape. If the shape didn't change, apply the new blendshape weights
        /// </summary>
        internal void FinalizeInflation(SkinnedMeshRenderer smr, MeshInflateFlags meshInflateFlags, string blendShapeTag = null)
        {
            var rendererName = GetMeshKey(smr);   

            //Apply computed mesh back to body as a blendshape
            var appliedMeshChanges = ApplyInflation(smr, rendererName, meshInflateFlags.OverWriteMesh, blendShapeTempTagName, meshInflateFlags.bypassWhen0);

            //When HScene inflation is actively happening while user changes clothing, make sure the new clothing updates shape too
            if (isDuringInflationScene) AppendToQuickInflateList(smr);

            //If the inflation is applied, update the previous slider config values
            if (appliedMeshChanges) infConfigHistory = (PregnancyPlusData)infConfig.Clone();
        }


        /// <summary>
        /// Compute and cache the bind pose (T-pose) mesh vertex positions, we need this in order to ignore all character animations when computing the belly shape. 
        ///     It also aligns the meshes together. Cache the computed results
        /// </summary>
        internal async Task ComputeBindPoseMesh(SkinnedMeshRenderer smr, SkinnedMeshRenderer bodySmr, bool isClothingMesh, 
                                                MeshInflateFlags meshInflateFlags, bool isMainBody = false)
        {
            if (smr == null) 
            {
                if (PregnancyPlusPlugin.DebugLog.Value) PregnancyPlusPlugin.Logger.LogWarning($" ComputeBindPoseMesh smr was null"); 
                return;
            }

            if (PregnancyPlusPlugin.DebugCalcs.Value) PregnancyPlusPlugin.Logger.LogInfo($" Computing BindPoseMesh for {smr.name}");

            //If mesh is not readable, make it so
            if (!smr.sharedMesh.isReadable) nativeDetour.Apply();

            var rendererName = GetMeshKey(smr);
            var exists = md.TryGetValue(rendererName, out MeshData _md);     
            if (!exists) 
            {
                if (PregnancyPlusPlugin.DebugLog.Value) PregnancyPlusPlugin.Logger.LogWarning($" ComputeBindPoseMesh cant find MeshData for {rendererName}"); 
                return;
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
            var bellyVertIndex = md[rendererName].bellyVerticieIndexes;
            var vertsLength = smr.sharedMesh.vertexCount;
            var smrTfTransPt = smr.transform.localToWorldMatrix;
            //The highest and lowest a vert can be, to be considerd in the belly area
            var yBottomLimit = GetSphereCenter().y + (bellyInfo.OriginalSphereRadius * 1.5f);
            var yTopLimit = GetSphereCenter().y - (bellyInfo.OriginalSphereRadius * 1.5f);
            
            nativeDetour.Undo();

            //Put threadpool work inside task and await the results
            await Task.Run(() => 
            {
                //Check again since some time will have passed since task start
                var _exists = md.TryGetValue(rendererName, out MeshData _meshData);     
                if (!_exists) 
                {
                    if (PregnancyPlusPlugin.DebugLog.Value) PregnancyPlusPlugin.Logger.LogWarning($" ComputeBindPoseMesh.Task.Run cant find MeshData for {rendererName}");
                    return;
                }  

                //Spread work across multiple threads
                md[rendererName].originalVertices = Threading.RunParallel(unskinnedVerts, (_, i) => {
                    //Get the skinned vert position from the bindpose matrix we computed earlier
                    var skinnedVert = MeshSkinning.UnskinnedToSkinnedVertex(unskinnedVerts[i], smrTfTransPt, boneMatrices, boneWeights[i]);

                    //Hijacking this threaded loop
                    //If any verts are found near the belly append them to the bellyVertIndexes, 
                    //  We need this because verts in clothing like skirts will be missed at first when the clothing has custom bones
                    //We could only do this after getting the skinned vert position anyway, so this is the best spot
                    if (isClothingMesh && !bellyVertIndex[i] && (skinnedVert.y < yBottomLimit && skinnedVert.y > yTopLimit))
                    {                            
                        bellyVertIndex[i] = true;
                    }

                    return skinnedVert;
                });  

                md[rendererName].isFirstPass = false;   
            });
                                
        }


        /// <summary>
        /// Does the vertex morph calculations to make a sphere out of the belly verticies, and updates the vertex dictionaries apprporiately
        /// </summary>
        /// <param name="skinnedMeshRenderer">The mesh renderer target</param>
        /// <param name="sphereRadius">The radius of the inflation sphere</param>
        /// <param name="isClothingMesh">Clothing requires a few tweaks to match skin morphs</param>
        /// <returns>Will return true if threaded</returns>
        internal async Task GetInflatedVerticies(SkinnedMeshRenderer smr, float sphereRadius, bool isClothingMesh, 
                                           SkinnedMeshRenderer bodySmr, MeshInflateFlags meshInflateFlags) 
        {
            if (smr == null) 
            {
                if (PregnancyPlusPlugin.DebugLog.Value) PregnancyPlusPlugin.Logger.LogWarning($" GetInflatedVerticies smr was null"); 
                return;
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
                return;
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
            var vertNormalCaluRadius = sphereRadius + bellyInfo.OriginalSphereRadius/20;//Only recalculate normals for verts within this radius to prevent shadows under breast at small belly sizes          
            var waistWidth = bellyInfo.WaistWidth;

            //Lock in current slider values for threaded calculation
            var infConfigClone = (PregnancyPlusData)infConfig.Clone();

            //Animation curves are not thread safe, so make copies here
            var bellySidesAC = new ThreadsafeCurve(BellySidesAC);
            var bellyTopAC = new ThreadsafeCurve(BellyTopAC);
            var bellyEdgeAC = new ThreadsafeCurve(BellyEdgeAC);
            var bellyGapLerpAC = new ThreadsafeCurve(BellyGapLerpAC);

            //Compute any user set, individual cloth offset
            var offsets = infConfig.IndividualClothingOffsets;
            var hasIndividualOffset = offsets != null && offsets.Any(o => o.Key == rendererName);
            var individualOffset = hasIndividualOffset ? (offsets.FirstOrDefault(o => o.Key == rendererName).Value * 4) : 0f;

            logCharMeshInfo(md[rendererName], smr, sphereCenter, isClothingMesh);

            nativeDetour.Undo();

            //Put threadpool work inside task and await the results
            await Task.Run(() => 
            {
                //Check again since some time will have passed since task start
                var _exists = md.TryGetValue(rendererName, out MeshData _meshData);     
                if (!_exists) 
                {
                    if (PregnancyPlusPlugin.DebugLog.Value) PregnancyPlusPlugin.Logger.LogWarning($" GetInflatedVerticies.Task.Run cant find MeshData for {rendererName}"); 
                    return;
                }  

                //Spread work across multiple threads
                md[rendererName].inflatedVertices = Threading.RunParallel(origVerts, (_, i) => 
                {                    
                    //Only care about altering belly verticies
                    if (!bellyVertIndex[i] && !PregnancyPlusPlugin.DebugVerts.Value)                     
                        return origVerts[i];               
                    
                    //Get the bindpose skinned vertex position
                    var origVertLs = origVerts[i];                
                    var vertDistance = Vector3.Distance(origVertLs, sphereCenter);                    

                    //Ignore verts outside the sphere radius
                    if (vertDistance > vertNormalCaluRadius && !PregnancyPlusPlugin.DebugVerts.Value)                 
                        return origVerts[i];                                    
                    
                    Vector3 inflatedVertLs;                    
                    Vector3 verticieToSpherePos;       

                    // If the vert is within the calculated normals radius, then consider it as an altered vert that needs normal recalculation when applying inflation
                    // This also means we can ignore other verts later saving compute time
                    //  Hopefully this will reduce breast shadows for smaller bellies
                    if (vertDistance <= vertNormalCaluRadius || PregnancyPlusPlugin.DebugVerts.Value) 
                        alteredVerts[i] = true;                                                                          
                    
                    var restoreClothThickness = 0f;

                    //Calculate clothing offset distance 
                    if (isClothingMesh)                                                                                      
                        restoreClothThickness = GetClothesFixOffset(infConfigClone, sphereCenter, sphereRadius, waistWidth, origVertLs, smr.name, clothOffsets[i], individualOffset);                    
                        
                    //Shift each belly vertex away from sphere center in a sphere pattern.  This is the core of the Preg+ belly shape
                    verticieToSpherePos = (origVertLs - sphereCenter).normalized * (sphereRadius + restoreClothThickness) + sphereCenter;                                                    

                    //Make adjustments to the shape to make it smooth, and feed in user slider input
                    var inflationResult =  SculptInflatedVerticie(infConfigClone, origVertLs, verticieToSpherePos, sphereCenter, waistWidth, 
                                                             preMorphSphereCenter, sphereRadius, backExtentPos, topExtentPos, 
                                                             bellySidesAC, bellyTopAC, bellyEdgeAC, bellyGapLerpAC);   

                    //TODO At the moment this needs to include more connected verts otherwise the shadows stop abruptly where the belly meets the body... leave this out for now
                    //If we did not need to alter the vert, reset its altered index
                    // if (inflationResult.Item2 == false && !PregnancyPlusPlugin.DebugVerts.Value)                    
                    //     alteredVerts[i] = false;                     

                    //Return the new inflated vert                                                          
                    return inflationResult.Item1;    
                });                
            });
        }


        /// <summary>
        /// Calculates the center position of the belly sphere.  Including the user slider value
        /// </summary>
        internal Vector3 GetSphereCenter() 
        {          
            #if KKS
                var bbHeight = 0.97f;           
            #else
                var bbHeight = 10f;
            #endif
            
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
        /// <returns>Tuple containing the final morphed vert, and whether the vert actually changed position</returns>
        internal Tuple<Vector3, bool> SculptInflatedVerticie(PregnancyPlusData infConfigClone, Vector3 originalVerticeLs, Vector3 inflatedVerticieLs, Vector3 sphereCenterLs, float waistWidth, 
                                                Vector3 preMorphSphereCenter, float sphereRadius, Vector3 backExtentPos, Vector3 topExtentPos, 
                                                ThreadsafeCurve bellySidesAC, ThreadsafeCurve bellyTopAC, ThreadsafeCurve bellyEdgeAC, ThreadsafeCurve bellyGapLerpAC) 
        {
            //No smoothing modification in debug mode
            if (PregnancyPlusPlugin.MakeBalloon.Value || PregnancyPlusPlugin.DebugVerts.Value) 
                return new Tuple<Vector3, bool>(inflatedVerticieLs, true); 
            
            //get the smoothing distance limits so we don't have weird polygons and shapes on the edges, and prevents morphs from shrinking past original skin boundary
            var pmSkinToCenterDist = Math.Abs(Vector3.Distance(preMorphSphereCenter, originalVerticeLs));
            var pmInflatedToCenterDist = Math.Abs(Vector3.Distance(preMorphSphereCenter, inflatedVerticieLs));
            var skinToCenterDist = Math.Abs(Vector3.Distance(sphereCenterLs, originalVerticeLs));
            var inflatedToCenterDist = Math.Abs(Vector3.Distance(sphereCenterLs, inflatedVerticieLs));
            

            //Only apply morphs if the imaginary sphere is outside of the skins boundary (Don't want to shrink anything inwards, only out)
            if (skinToCenterDist >= inflatedToCenterDist || pmSkinToCenterDist > pmInflatedToCenterDist) 
                return new Tuple<Vector3, bool>(originalVerticeLs, false); 
            
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
                smoothedVectorLs = GetUserFatFoldTransform(infConfigClone, originalVerticeLs, smoothedVectorLs, sphereCenterLs, sphereRadius, bellyGapLerpAC);
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
            if (smoothedVectorLs.Equals(originalVerticeLs)) 
                return new Tuple<Vector3, bool>(smoothedVectorLs, false);


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
                return new Tuple<Vector3, bool>(originalVerticeLs, false);            

            //Don't allow any morphs to move behind the character's.z = 0 + extentOffset position, otherwise skin sometimes pokes out the back side :/
            if (backExtentPos.z > smoothedVectorLs.z)             
                smoothedVectorLs = new Vector3(smoothedVectorLs.x, smoothedVectorLs.y, originalVerticeLs.z);            

            //Don't allow any morphs to move behind the original verticie z position, only forward expansion (ignoring ones already behind sphere center)
            if (originalVerticeLs.z > smoothedVectorLs.z && originalVerticeLs.z > sphereCenterLs.z)     
                smoothedVectorLs = new Vector3(smoothedVectorLs.x, smoothedVectorLs.y, originalVerticeLs.z);            

            return new Tuple<Vector3, bool>(smoothedVectorLs, true);  
        }


        /// <summary>
        /// Compute the deltas between the original mesh and the inflated one. (This is threaded now)  We use these deltas to build the blendshape
        /// </summary>
        /// <returns>returns bool whether the action needs to be threaded or not</returns>
        internal async Task ComputeDeltas(SkinnedMeshRenderer smr, string rendererName, MeshInflateFlags meshInflateFlags) 
        {           
            //Check for mesh data object
            var isMeshInitialized = md.TryGetValue(rendererName, out MeshData _md);
            if (!isMeshInitialized) return;

            //If we already have the deltas then skip
            if (_md.HasDeltas && !meshInflateFlags.OverWriteMesh) return;

            //Get the virtual inflated mesh with normal, and tangent recalculation applied
            var inflatedMesh = PrepForBlendShape(smr, rendererName);
            if (!inflatedMesh) return;

            if (PregnancyPlusPlugin.DebugCalcs.Value) PregnancyPlusPlugin.Logger.LogInfo($" Compute BlendShape Deltas for {smr.name}");

            //When SMR has local rotation undo it in the deltas
            var rotationUndo = Matrix4x4.TRS(Vector3.zero, smr.transform.localRotation, Vector3.one).inverse;
            //When a smr bindpose has scale, we need to undo it in the delta similar to rotation
            var scaleUndo = MeshSkinning.GetBindPoseScale(smr).inverse;
            var undoTfMatrix = rotationUndo * scaleUndo;

            // if (PregnancyPlusPlugin.DebugLog.Value && scaleUndo != Matrix4x4.identity) 
            //     PregnancyPlusPlugin.Logger.LogWarning($" smr {smr.name} has bindpose scale {Matrix.GetScale(scaleUndo)}");

            if (!smr.sharedMesh.isReadable) nativeDetour.Apply();

            //Store values in thread safe variable
            var sourceNormals = smr.sharedMesh.normals;
            var targetNormals = inflatedMesh.normals;
            var sourceTangents = smr.sharedMesh.tangents;
            var targetTangents = inflatedMesh.tangents;
            var originalVerts = _md.originalVertices;
            var inflatedVerts = _md.inflatedVertices;
            var alteredVerts = _md.alteredVerticieIndexes;
            var hasTransform = undoTfMatrix != Matrix4x4.identity;

            nativeDetour.Undo();

            //Put threadpool work inside task and await the results
            await Task.Run(() => 
            {
                var deltaVerts = new Vector3[originalVerts.Length];
                var deltaNormals = new Vector3[originalVerts.Length];

                //Spread work across multiple threads
                Threading.RunParallel(sourceNormals, (_, i) => 
                {
                    //If the vert has not been altered, no delta change
                    if (alteredVerts[i])
                    {
                        //Get Vertex deltas
                        deltaVerts[i] = BlendShapeTools.GetV3Delta(originalVerts[i], inflatedVerts[i], undoTfMatrix, hasTransform);
                        //Get normal deltas
                        deltaNormals[i] = BlendShapeTools.GetV3Delta(sourceNormals[i], targetNormals[i], undoTfMatrix, hasTransform);
                    }

                    //WE dont care about return type in this case
                    return Vector3.zero;
                });

                //Capture results
                _md.deltaVerticies = deltaVerts;
                _md.deltaNormals = deltaNormals;

                //Spread work across multiple threads
                _md.deltaTangents = Threading.RunParallel<Vector4, Vector3>(sourceTangents, (_, i) => 
                {
                    //Get tangent deltas
                    if (alteredVerts[i])
                        return BlendShapeTools.GetV3Delta(sourceTangents[i], targetTangents[i], undoTfMatrix, hasTransform);

                    return sourceTangents[i];
                });
            });
        }

                /// <summary>
        /// Shoe debug spheres on screen when enabled in plugin config
        /// </summary>
        internal void PostInflationDebugStuff()
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

            //Skip when no debug mode active
            if (!PregnancyPlusPlugin.ShowBellyVerts.Value 
                && !PregnancyPlusPlugin.ShowUnskinnedVerts.Value 
                && !PregnancyPlusPlugin.ShowSkinnedVerts.Value
                && !PregnancyPlusPlugin.ShowInflatedVerts.Value
                && !PregnancyPlusPlugin.ShowDeltaVerts.Value)
                return;
            
            //Gather all SMR's
            var bodyRenderers = PregnancyPlusHelper.GetMeshRenderers(ChaControl.objBody, findAll: true);                           
            var clothRenderers = PregnancyPlusHelper.GetMeshRenderers(ChaControl.objClothes);         
            var accessoryRenderers = PregnancyPlusHelper.GetMeshRenderers(ChaControl.objAccessory);     

            bodyRenderers.ForEach((SkinnedMeshRenderer smr) => PostInflationDebugMesh(smr));
            clothRenderers.ForEach((SkinnedMeshRenderer smr) => PostInflationDebugMesh(smr, isClothingMesh: true));
            accessoryRenderers.ForEach((SkinnedMeshRenderer smr) => PostInflationDebugMesh(smr, isClothingMesh: true));        
        }


        /// <summary>
        /// Depending on plugin config state, shows calculated verts on screen (Do not run inside a Task, lol)
        /// </summary>
        internal void PostInflationDebugMesh(SkinnedMeshRenderer smr, bool isClothingMesh = false)
        {
            //If the mesh has been touched it has a key
            var hasKey = md.TryGetValue(GetMeshKey(smr), out var _md);
            if (!hasKey) return;

            //Show verts on screen when this debug option is enabled
            if (PregnancyPlusPlugin.ShowUnskinnedVerts.Value)  
            {
                if (!smr.sharedMesh.isReadable) nativeDetour.Apply();  
                //Smaller spheres for body meshes
                DebugTools.DebugMeshVerts(smr.sharedMesh.vertices, size: (isClothingMesh ? 0.01f : 0.005f));                                          
                nativeDetour.Undo();
            }

            if (PregnancyPlusPlugin.ShowSkinnedVerts.Value && _md.HasOriginalVerts)  
                DebugTools.DebugMeshVerts(_md.originalVertices, color: Color.cyan, size: (isClothingMesh ? 0.01f : 0.005f));                                          

            if (PregnancyPlusPlugin.ShowInflatedVerts.Value && _md.HasInflatedVerts)  
                DebugTools.DebugMeshVerts(_md.inflatedVertices, color: Color.green, size: (isClothingMesh ? 0.01f : 0.005f));

            //When we need to debug the deltas visually
            if (PregnancyPlusPlugin.ShowDeltaVerts.Value && _md.HasDeltas) 
            {
                //When SMR has local rotation undo it in the deltas
                var rotationUndo = Matrix4x4.TRS(Vector3.zero, smr.transform.localRotation, Vector3.one).inverse;                
                for (int i = 0; i < _md.deltaVerticies.Length; i++)
                {
                    //Undo delta rotation so we can make sure it aligns with the other meshes deltas
                    DebugTools.DrawLine(_md.originalVertices[i], _md.originalVertices[i] + rotationUndo.inverse.MultiplyPoint3x4(_md.deltaVerticies[i]));     
                }                          
            }

            if (PregnancyPlusPlugin.ShowBellyVerts.Value && _md.HasOriginalVerts) 
            {
                for (int i = 0; i < _md.bellyVerticieIndexes.Length; i++)
                {
                    //Place spheres on each vert to debug the mesh calculated position relative to other meshes      
                    if (_md.bellyVerticieIndexes[i])          
                        DebugTools.DrawSphere((isClothingMesh ? 0.01f : 0.005f), _md.originalVertices[i], color: Color.grey);                                  
                } 
            }
        }
                
    }
}


