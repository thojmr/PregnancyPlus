using KKAPI;
using KKAPI.Chara;
using UnityEngine;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UniRx;
#if HS2
using AIChara;
#endif

namespace KK_PregnancyPlus
{
    public class PregnancyPlusCharaController: CharaCustomFunctionController
    {
        internal bool debug = false;//In debug mode, all verticies are affected.  Makes it easier to see what is actually happening in studio mode.  Also creates nightmares

#region props
        //Contsins the mesh inflation configuration
        public PregnancyPlusData infConfig = new PregnancyPlusData();
        internal PregnancyPlusData infConfigHistory = new PregnancyPlusData();        


        //Keeps track of all belly verticies
        public Dictionary<string, Vector3[]> originalVertices = new Dictionary<string, Vector3[]>();
        public Dictionary<string, Vector3[]> inflatedVertices = new Dictionary<string, Vector3[]>();
        public Dictionary<string, Vector3[]> currentVertices = new Dictionary<string, Vector3[]>();
        public Dictionary<string, bool[]> bellyVerticieIndexes = new Dictionary<string, bool[]>();//List of verticie indexes that belong to the belly area



        //For fetching uncensor body guid data (bugfix for uncensor body vertex positions)
        public const string UncensorCOMName = "com.deathweasel.bepinex.uncensorselector";
        public const string DefaultBodyFemaleGUID = "Default.Body.Female";

        public const string KK_PregnancyPluginName = "KK_Pregnancy";//Allows us to pull KK_pregnancy data values

#endregion


#region overrides
        protected override void OnCardBeingSaved(GameMode currentGameMode)
        {
            SetExtendedData(infConfig.Save());
        }

        protected override void Awake() 
        {                    
            if (PregnancyPlusPlugin.StoryMode != null) {
                if (PregnancyPlusPlugin.StoryMode.Value) CharacterApi.CharacterReloaded += OnCharacterReloaded;            
            }

            base.Awake();
        }
        protected override void Start() 
        {
#if KK            
            //Detect clothing change in KK
            CurrentCoordinate.Subscribe(value => { OnCoordinateLoaded(); });
#endif
            ReadCardData();

            base.Start();
        }

#if HS2
        //The Hs2 way to detect clothing change in studio
        protected override void OnCoordinateBeingLoaded(ChaFileCoordinate coordinate) {
            // PregnancyPlusPlugin.Logger.LogInfo($" OnCoordinateBeingLoaded > "); 
            OnCoordinateLoaded();

            base.OnCoordinateBeingLoaded(coordinate);
        }
#endif

        protected override void OnReload(GameMode currentGameMode)
        {
            if (PregnancyPlusPlugin.StoryMode != null) {
                if (PregnancyPlusPlugin.StoryMode.Value) GetWeeksAndSetInflation();
            }
            ReadCardData();
        }

        protected override void Update()
        {
            //just for testing, pretty compute heavy for Update()
            // MeshInflate(true);
        }
        
#endregion

        internal void ReadCardData()
        {
            var data = GetExtendedData();
            infConfig = PregnancyPlusData.Load(data) ?? new PregnancyPlusData();
        }

        internal void OnCharacterReloaded(object sender, CharaReloadEventArgs e)  
        {  
            //When loading the character, if pregnant, apply the new inflated belly too
            if (ChaControl == null || e.ReloadedCharacter == null || e.ReloadedCharacter.name != ChaControl.name) return;

            GetWeeksAndSetInflation();
        } 

        internal void OnCoordinateLoaded()  
        {  
            //When clothing changes, reload inflation state
            // PregnancyPlusPlugin.Logger.LogInfo($" OnCoordinateLoaded > ");  
            StartCoroutine(WaitForMeshToSettle());
        } 

        //After clothes change you have to wait a second if you want shadows to calculate correctly
        IEnumerator WaitForMeshToSettle()
        {   
            var waitTime = 0.10f;
            yield return new WaitForSeconds(waitTime);
            MeshInflate(true);
        }

        internal void GetWeeksAndSetInflation() 
        {
            var week = PregnancyPlusHelper.GetWeeksFromPregnancyPluginData(ChaControl, KK_PregnancyPluginName);
            // PregnancyPlusPlugin.Logger.LogInfo($" Week >  {week}");
            if (week < 0) return;

            MeshInflate(week);
        }




        





#region inflation

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
                // PregnancyPlusPlugin.Logger.LogInfo($"   > {skinnedMeshRenderer.name}");         
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
        /// An overload for MeshInflate() that allows you to pass an initial inflationSize param
        /// For quickly setting the size, without worrying about the other config params
        /// </summary>
        /// <param name="inflationSize">Sets inflation size from 0 to 40</param>
        public bool MeshInflate(float inflationSize)
        {                  
            if (inflationSize.Equals(null)) return false;

            //Allow an initial size to be passed in, and sets it to the config
            if (inflationSize > 0) {
                infConfig.inflationSize = inflationSize;
            }   

            return MeshInflate();
        }

        /// <summary>
        /// Get the characters waist width and calculate the appropriate sphere radius from it
        /// </summary>
        /// <param name="chaControl">The character to measure</param>
        internal Tuple<float, float> MeasureWaist(ChaControl chaControl) 
        {
#if KK
            var ribName = "cf_s_spine02";
            var waistName = "cf_s_waist02";
#elif HS2
            var ribName = "cf_J_Spine02_s";
            var waistName = "cf_J_Kosi02";
#endif            
            //Get the characters bones to measure from           
            var ribBone = chaControl.objBodyBone.GetComponentsInChildren<Transform>().FirstOrDefault(x => x.name == ribName);
            var waistBone = chaControl.objBodyBone.GetComponentsInChildren<Transform>().FirstOrDefault(x => x.name == waistName);
            var waistToRibDist = Math.Abs(FastDistance(ribBone.position, waistBone.position));  

#if KK
            var thighLName = "cf_j_thigh00_L";
            var thighRName = "cf_j_thigh00_R";                    
#elif HS2
            var thighLName = "cf_J_LegUp00_L";
            var thighRName = "cf_J_LegUp00_R";
#endif
            var thighLBone = chaControl.objBodyBone.GetComponentsInChildren<Transform>().FirstOrDefault(x => x.name == thighLName);        
            var thighRBone = chaControl.objBodyBone.GetComponentsInChildren<Transform>().FirstOrDefault(x => x.name == thighRName); 
            var waistWidth = Math.Abs(FastDistance(thighLBone.position, thighRBone.position)); 

            //Calculate sphere radius based on distance from waist to ribs (seems big, but lerping later will trim much of it), added Math.Min for skinny waists
            var sphereRadius = Math.Min(waistToRibDist/1.25f, waistWidth/1.2f) * (infConfig.inflationMultiplier + 1); 

            return Tuple.Create(waistWidth, sphereRadius);
        }

        /// <summary>
        /// Just a helper function to combine searching for verts in a mesh, and then applying the transforms
        /// </summary>
        internal bool ComputeMeshVerts(SkinnedMeshRenderer smr, float sphereRadius, float waistWidth, bool isClothingMesh = false) 
        {
            //The list of bones to get verticies for
#if KK            
            var boneFilters = new string[] { "cf_s_spine02", "cf_s_waist01" };//"cs_s_spine01" "cf_s_waist02" optionally for wider affected area
#elif HS2
            var boneFilters = new string[] { "cf_J_Spine02_s", "cf_J_Kosi01_s" };
#endif
            var hasVerticies = GetFilteredVerticieIndexes(smr, debug ? null : boneFilters);        

            //If no belly verts found, then we can skip this mesh
            if (!hasVerticies) return false; 

            return GetInflatedVerticies(smr, sphereRadius, waistWidth, isClothingMesh);
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
            if (smr == null) return false;
                      
#if KK
            //Get normal mesh root attachment position  
            var meshRoot = GameObject.Find("cf_o_root");
#elif HS2
            //For HS2, get the equivalent position game object (near bellybutton)//TODO maybe just make this the belly button for all
            var meshRoot = GameObject.Find("cf_J_Spine01");
#endif
            if (meshRoot == null) return false;
                        
            //set sphere center and allow for adjusting its position from the UI sliders  
            Vector3 sphereCenter = GetSphereCenter(meshRoot.transform, isClothingMesh);

            var rendererName = GetMeshKey(smr);         
            originalVertices[rendererName] = smr.sharedMesh.vertices;
            inflatedVertices[rendererName] = new Vector3[originalVertices[rendererName].Length];
            currentVertices[rendererName] = new Vector3[originalVertices[rendererName].Length];

            var origVerts = originalVertices[rendererName];
            var inflatedVerts = inflatedVertices[rendererName];
            var currentVerts = currentVertices[rendererName];
            var bellyVertIndex = bellyVerticieIndexes[rendererName];    

#if KK
            float clothesOffset = 0.003f;       
#elif HS2
            //Everything is bigger in HS2 :/
            float clothesOffset = 0.035f;       
#endif            

            //Set each verticies inflated postion, with some constraints (SculptInflatedVerticie) to make it look more natural
            for (int i = 0; i < origVerts.Length; i++)
            {
                var origVert = origVerts[i];

                //Only care about inflating belly verticies
                if (bellyVertIndex[i]) 
                {                    
                    Vector3 inflatedVertWS;                    
                    Vector3 verticieToSphere;                      
                    var origVertWS = meshRoot.transform.TransformPoint(origVerts[i]);//Convert to worldspace

                    //Shift each belly vertex away from sphere center
                    if (!isClothingMesh) 
                    {                        
                        verticieToSphere = (origVertWS - sphereCenter).normalized * sphereRadius + sphereCenter + GetUserShiftTransform(meshRoot.transform);                     
                    }
                    else 
                    {
                        //Clothes need some more loving to get them to stop clipping at max size
                        verticieToSphere = (origVertWS - sphereCenter).normalized * (sphereRadius + clothesOffset) + sphereCenter + GetUserShiftTransform(meshRoot.transform);                                           
                    }     

                    //Make minor adjustments to the shape
                    inflatedVertWS =  SculptInflatedVerticie(origVertWS, verticieToSphere, sphereCenter, waistWidth, meshRoot.transform);                    
                    inflatedVerts[i] = meshRoot.transform.InverseTransformPoint(inflatedVertWS);//Convert back to local space
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

        /// <summary>
        /// Calculates the position of the inflation sphere.  It appends users selected slider values as well.
        /// </summary>
        /// <param name="boneOrMeshTf">The transform where you want the sphere to be located at (near belly buttom)</param>
        /// <param name="isClothingMesh"></param>
        internal Vector3 GetSphereCenter(Transform boneOrMeshTf, bool isClothingMesh = false) {
            //set sphere center and allow for adjusting its position from the UI sliders  

            var isUncensorBody = PregnancyPlusHelper.IsUncensorBody(ChaControl, UncensorCOMName, DefaultBodyFemaleGUID); 

            //Sphere slider adjustments need to be transformed to local space first to eliminate any character rotation in world space   
            Vector3 sphereCenter = boneOrMeshTf.transform.position + GetUserMoveTransform(boneOrMeshTf.transform) + GetBellyButtonOffset(boneOrMeshTf.transform); 
            //For uncensor, move the mesh vectors up by an additional meshRoot.y to match the default body mesh position
            Vector3 sphereCenterUncesorFix = boneOrMeshTf.transform.position + (boneOrMeshTf.transform.up * FastDistance(boneOrMeshTf.transform.position, ChaControl.transform.position)) + GetUserMoveTransform(boneOrMeshTf.transform) + GetBellyButtonOffset(boneOrMeshTf.transform);             

#if HS2
            //All mesh origins are character origin 0,0,0 in HS2, and mixed positions in KK
            sphereCenter = sphereCenterUncesorFix;            
#elif KK
            //Fix for uncensor mesh position
            if (!isClothingMesh) {
                sphereCenter = isUncensorBody ? sphereCenterUncesorFix : sphereCenter;//Fix for uncensor local vertex positions being different than default body mesh
            }
#endif

            // PregnancyPlusPlugin.Logger.LogInfo($" sphereCenter {sphereCenter} char origin {ChaControl.transform.position}");
            return sphereCenter;
        }

        internal Vector3 GetUserMoveTransform(Transform fromPosition) {
            return fromPosition.up * infConfig.inflationMoveY + fromPosition.forward * infConfig.inflationMoveZ;
        }

        internal Vector3 GetBellyButtonOffset(Transform fromPosition) {
            //If there is not a bone close enough to belly button.y position, this can offset it to match the correct height
            return fromPosition.up * -0.02f;
        }

        internal Vector3 GetUserShiftTransform(Transform fromPosition) {
            return fromPosition.up * infConfig.inflationShiftY + fromPosition.forward * infConfig.inflationShiftZ;
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
            var waistRadius = waistWidth/2;
            
            // PregnancyPlusPlugin.Logger.LogInfo($" ");
            // PregnancyPlusPlugin.Logger.LogInfo($" preMorphSphereCenter {preMorphSphereCenter} sphereCenterPos {sphereCenterPos} meshRootTf.pos {meshRootTf.position}");
            // PregnancyPlusPlugin.Logger.LogInfo($" skinToCenterDist {skinToCenterDist} inflatedToCenterDist {inflatedToCenterDist}");
            // PregnancyPlusPlugin.Logger.LogInfo($" morphedSphereRadius {morphedSphereRadius}  waistRadius {waistRadius}");

            //Only apply morphs if the imaginary sphere is outside of the skins boundary (Don't want to shrink anything inwards, only out)
            if (skinToCenterDist >= inflatedToCenterDist || pmSkinToCenterDist > pmInflatedToCenterDist) return originalVertice;        

            var smoothedVector = inflatedVerticie;
            var zSmoothDist = pmInflatedToCenterDist/3f;//Just pick a float that looks good
            var ySmoothDist = pmInflatedToCenterDist/2f;//Only smooth the top half of y

            //Allow user adjustment of the width of the belly
            if (infConfig.inflationStretchX != 0) {   
                //Get local space position to eliminate rotation in world space
                var smoothedVectorLs = meshRootTf.InverseTransformPoint(smoothedVector);
                var sphereCenterLs = meshRootTf.InverseTransformPoint(sphereCenterPos);
                //local Distance left or right from sphere center
                var distFromXCenterLs = smoothedVectorLs.x - sphereCenterLs.x;                

                var changeInDist = distFromXCenterLs * (infConfig.inflationStretchX + 1);  
                //Get new local space X position
                smoothedVectorLs.x = (sphereCenterLs + Vector3.right * changeInDist).x;

                //Convert back to world space
                smoothedVector = meshRootTf.TransformPoint(smoothedVectorLs);         
            }

            //Allow user adjustment of the height of the belly
            if (infConfig.inflationStretchY != 0) {   
                //Get local space position to eliminate rotation in world space
                var smoothedVectorLs = meshRootTf.InverseTransformPoint(smoothedVector);
                var sphereCenterLs = meshRootTf.InverseTransformPoint(sphereCenterPos);

                //local Distance up or down from sphere center
                var distFromYCenterLs = smoothedVectorLs.y - sphereCenterLs.y; 
                
                //have to change growth direction above and below center line
                var changeInDist = distFromYCenterLs * (infConfig.inflationStretchY + 1);  
                //Get new local space X position
                smoothedVectorLs.y = (sphereCenterLs + Vector3.up * changeInDist).y;
                
                //Convert back to world space
                smoothedVector = meshRootTf.TransformPoint(smoothedVectorLs);        
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

            //Make the belly egg shaped from top to bottom
            // if ((smoothedVector.y - sphereCenterPos.y) > ySmoothDist) {
            //     var lerpScale = Mathf.Abs(((smoothedVector.y - sphereCenterPos.y)/ySmoothDist) - 1f);
            //     smoothedVector = Vector3.Slerp(smoothedVector, originalVertice, lerpScale);
            // }

            var currentVectorDistance = Math.Abs(FastDistance(sphereCenterPos, smoothedVector));
            var pmCurrentVectorDistance = Math.Abs(FastDistance(preMorphSphereCenter, smoothedVector));
            //Don't allow any morphs to shrink skin smaller than its original position, only outward morphs allowed (check this last)
            if (skinToCenterDist > currentVectorDistance || pmSkinToCenterDist > pmCurrentVectorDistance) {
                return originalVertice;
            }

            //Don't allow any morphs to move behind the character's.z = 0 position, otherwise skin sometimes pokes out the back side :/
            if (meshRootTf.position.z > smoothedVector.z) {
                return originalVertice;
            }

            //Don't allow any morphs to move behind the original verticie z = 0 position
            if (originalVertice.z > smoothedVector.z) {
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
                PregnancyPlusPlugin.Logger.LogInfo(
                    $"GetFilteredVerticieIndexes > smr '{renderKey}' is not readable, skipping");
                    return false;
            }

            //Do a quick check to see if we need to fetch the bone indexes again.  ex: on second call we should allready have them
            //This saves a lot on compute apparently!            
            var isInitialized = bellyVerticieIndexes.TryGetValue(renderKey, out bool[] existingValues);
            if (isInitialized)
            {
                //If the vertex count has not changed then we can skip this
                if (existingValues.Length == skinnedMeshRenderer.sharedMesh.vertexCount) return true;
            }

            //For each bone, see if it matches a belly boneFilter
            for (int i = 0; i < bones.Length; i++)
            {   
                if (!bones[i]) continue;
                var boneName = bones[i].name;

                if (!hasBoneFilters) {
                    bellyBoneIndexes.Add(i);
                    continue;
                }

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
            foreach (BoneWeight bw in sharedMesh.boneWeights) 
            {
                int[] boneIndices = new int[] { bw.boneIndex0, bw.boneIndex1, bw.boneIndex2, bw.boneIndex3 };
                float[] boneWeights = new float[] { bw.weight0, bw.weight1, bw.weight2, bw.weight3 };

                //For each bone weight
                for (int i = 0; i < 4; i++)
                {                    
                    //If it has a weight, and the bone is a belly bone. Weight goes (0-1f) Ignore 0 and maybe filter below 0.1 as well
                    //Include all if debug = true
                    if (debug || (boneWeights[i] > 0.05f && bellyBoneIndexes.Contains(boneIndices[i])))
                    {
                        //Make sure to exclude verticies on characters back, we only want to modify the front.  No back bellies!
                        //add all vertexes in debug mode
                        if (debug || verticies[c].z >= 0) {
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
                // PregnancyPlusPlugin.Logger.LogInfo($"bellyVerticieIndexes >  removing {renderKey}"); 
                RemoveRenderKey(renderKey);
            }

            return hasBellyVerticies;
        }

        internal float FastDistance(Vector3 firstPosition, Vector3 secondPosition) 
        {
            //Calculates distance faster than vector3.distance.
            Vector3 heading;
            float distanceSquared;
    
            heading.x = firstPosition.x - secondPosition.x;
            heading.y = firstPosition.y - secondPosition.y;
            heading.z = firstPosition.z - secondPosition.z;
    
            distanceSquared = heading.x * heading.x + heading.y * heading.y + heading.z * heading.z;
            return Mathf.Sqrt(distanceSquared);
        }
       
        internal bool NeedsMeshUpdate() 
        {
            bool hasChanges = false;

            if (infConfig.inflationSize != infConfigHistory.inflationSize) hasChanges = true;              
            if (infConfig.inflationMoveY != infConfigHistory.inflationMoveY) hasChanges = true;
            if (infConfig.inflationMoveZ != infConfigHistory.inflationMoveZ) hasChanges = true;
            if (infConfig.inflationStretchX != infConfigHistory.inflationStretchX) hasChanges = true;
            if (infConfig.inflationStretchY != infConfigHistory.inflationStretchY) hasChanges = true;
            if (infConfig.inflationShiftY != infConfigHistory.inflationShiftY) hasChanges = true;
            if (infConfig.inflationShiftZ != infConfigHistory.inflationShiftZ) hasChanges = true;
            if (infConfig.inflationMultiplier != infConfigHistory.inflationMultiplier) hasChanges = true;

            return hasChanges;
        }
        
        internal void RemoveRenderKeys(List<string> keysToRemove) 
        {
            foreach(var key in keysToRemove) 
            {
                RemoveRenderKey(key);
            }
        }

        internal void RemoveRenderKey(string keyToRemove) 
        {
            if (originalVertices.ContainsKey(keyToRemove)) originalVertices.Remove(keyToRemove);
            if (inflatedVertices.ContainsKey(keyToRemove)) inflatedVertices.Remove(keyToRemove);
            if (currentVertices.ContainsKey(keyToRemove)) currentVertices.Remove(keyToRemove);
            if (bellyVerticieIndexes.ContainsKey(keyToRemove)) bellyVerticieIndexes.Remove(keyToRemove);        
        }

        //Tries to uniquly identify a mesh by its name and number of verticies
        internal string GetMeshKey(SkinnedMeshRenderer smr) 
        {
            return smr.name + smr.sharedMesh.vertexCount.ToString();
        }

        /// <summary>
        /// This will update all verticies with a lerp from originalVertices to inflatedVertices depending on the inflationSize config
        /// Only modifies belly verticies, and if none are found, no action taken.
        /// </summary>
        /// <param name="mesh">Target mesh to update</param>
        /// <param name="renderKey">The Shared Mesh render name, used in dictionary keys to get the current verticie values</param>
        /// <returns>Will return True if any verticies are changed</returns>
        internal bool ApplyInflation(SkinnedMeshRenderer smr, string renderKey) 
        {
            var infSize = infConfig.inflationSize;
            //Only inflate if the value changed        
            if (infSize.Equals(null) || infSize == 0) return false;      

            //Create an instance of sharedMesh so we don't modify the mesh shared between characters
            Mesh meshCopy = (Mesh)UnityEngine.Object.Instantiate(smr.sharedMesh);    
            smr.sharedMesh = meshCopy;

            var sharedMesh = smr.sharedMesh;

            if (!sharedMesh.isReadable) {
                PregnancyPlusPlugin.Logger.LogInfo(
                    $"ApplyInflation > smr '{renderKey}' is not readable, skipping");
                    return false;
            } 

            // StartInflate(balloon);
            var origVert = originalVertices[renderKey];
            var currentVert = currentVertices[renderKey];
            var bellyVertIndex = bellyVerticieIndexes[renderKey];

            if (bellyVertIndex.Length == 0) return false;
            infConfigHistory.inflationSize = infSize;

            for (int i = 0; i < currentVert.Length; i++)
            {
                //If not a belly index verticie then skip the morph
                if (bellyVertIndex[i] != true) continue;

                currentVert[i] = Vector3.Lerp(origVert[i], inflatedVertices[renderKey][i], (infSize/40));
            }

            if (currentVert.Length != sharedMesh.vertexCount) 
            {
                PregnancyPlusPlugin.Logger.LogInfo(
                            $"ApplyInflation > smr.sharedMesh '{renderKey}' has incorrect vert count {currentVert.Length}|{sharedMesh.vertexCount}");
                return false;
            }

            sharedMesh.vertices = currentVert;
            sharedMesh.RecalculateBounds();
            sharedMesh.RecalculateNormals();
            sharedMesh.RecalculateTangents();

            return true;
        }    
        
        internal void ResetInflation() 
        {   
            //Resets all mesh inflations
            var keyList = new List<string>(originalVertices.Keys);

            //For every active meshRenderer.name
            foreach(var renderKey in keyList) 
            {
                var smr = PregnancyPlusHelper.GetMeshRenderer(ChaControl, renderKey);
                //Normally triggered when user changes clothes, the old clothes render wont be found
                if (smr == null) continue;                

                //Create an instance of sharedMesh so we don't modify the mesh shared between characters
                Mesh meshCopy = (Mesh)UnityEngine.Object.Instantiate(smr.sharedMesh);
                smr.sharedMesh = meshCopy;

                var sharedMesh = smr.sharedMesh;
                var success = originalVertices.TryGetValue(renderKey, out Vector3[] origVerts); 

                //On change clothes original verts become useless, so skip this
                if (!success) return;          
                if (!sharedMesh.isReadable) {
                    PregnancyPlusPlugin.Logger.LogInfo(
                        $"ResetInflation > smr '{renderKey}' is not readable, skipping");
                        continue;
                } 

                if (!sharedMesh || origVerts.Equals(null) || origVerts.Length == 0) continue;
                if (origVerts.Length != sharedMesh.vertexCount) 
                {
                    PregnancyPlusPlugin.Logger.LogInfo(
                        $"ResetInflation > smr '{renderKey}' has incorrect vert count {origVerts.Length}|{sharedMesh.vertexCount}");
                    continue;
                }

                sharedMesh.vertices = origVerts;
                sharedMesh.RecalculateBounds();
                sharedMesh.RecalculateNormals();
                sharedMesh.RecalculateTangents();
            }
        }

#endregion

    }
}
