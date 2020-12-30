﻿using KKAPI;
using KKAPI.Chara;
using UnityEngine;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UniRx;

namespace KK_PregnancyPlus
{
    public class PregnancyPlusCharaController: CharaCustomFunctionController
    {
        internal bool debug = false;//In debug mode, all verticies are affected.  Makes it easier to see what is actually happening in studio mode.  Also creates nightmares
        public  bool storyMode = false;//Some bugs to work out here


#region props
        //Contsins the mesh inflation configuration
        public Dictionary<string, float> infConfig = CreateConfig();
        internal Dictionary<string, float> infConfigHistory = CreateConfig();
        public Dictionary<string, float> configDefaults = CreateConfig();
        


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

        //Allows an easy way to create a default belly config dictionary, you can change the values from there
        public static Dictionary<string, float> CreateConfig() 
        {
            //Default values
            return new Dictionary<string, float> {
                ["inflationSize"] = 0, 
                ["inflationMoveY"] = 0, 
                ["inflationMoveZ"] = 0, 
                ["inflationStretchX"] = 0, 
                ["inflationStretchY"] = 0, 
                ["inflationShiftY"] = 0, 
                ["inflationShiftZ"] = 0, 
                ["inflationMultiplier"] = 0
            };
        }

        public PregnancyPlusCharaController()
        {
            // Data = new PregnancyData();               
        }

#region overrides
        protected override void OnCardBeingSaved(GameMode currentGameMode)
        {

        }

        protected override void Awake() 
        {          
            InitInflationConfig();            
            if (storyMode) CharacterApi.CharacterReloaded += OnCharacterReloaded;            

            base.Awake();
        }
        protected override void Start() 
        {
            CurrentCoordinate.Subscribe(value => { OnCoordinateLoaded(); });

            base.Start();
        }

        protected override void OnReload(GameMode currentGameMode)
        {
            if (storyMode) GetWeeksAndSetInflation();
        }

        protected override void Update()
        {
            //Just for testing story mode
            // if (Data != null && Data.Week >= 0) {
                // MeshInflate(true);
            // }
        }
        
#endregion

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
            yield return new WaitForSeconds(0.05f);
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
            if (infConfig["inflationSize"] <= 0) {
                infConfigHistory["inflationSize"] = 0;
                return false;                                
            }
            
            var measuerments = MeasureWaist(ChaControl);         
            var waistWidth = measuerments.Item1; 
            var sphereRadius = measuerments.Item2;

            // var allMeshRenderers = ChaControl.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            // PregnancyPlusPlugin.Logger.LogInfo($"allMeshRenderers > {allMeshRenderers.Length}");
            
            var anyMeshChanges = false;

            //Get and apply all clothes render mesh changes
            var clothRenderers = PregnancyPlusHelper.GetMeshRenderers(ChaControl.objClothes);
            foreach(var skinnedMeshRenderer in clothRenderers) 
            {
                // PregnancyPlusPlugin.Logger.LogInfo($"   > {skinnedMeshRenderer.name}");         
                var foundVerts = ComputeMeshVerts(skinnedMeshRenderer, sphereRadius, waistWidth, true);
                if (!foundVerts) continue;
                var appliedClothMeshChanges = ApplyInflation(skinnedMeshRenderer.sharedMesh, GetMeshKey(skinnedMeshRenderer));

                if (appliedClothMeshChanges) anyMeshChanges = true;
            }             

            //do the same for body meshs
            var bodyRenderers = PregnancyPlusHelper.GetMeshRenderers(ChaControl.objBody);
            foreach(var skinnedMeshRenderer in bodyRenderers) 
            {
                var foundVerts = ComputeMeshVerts(skinnedMeshRenderer, sphereRadius, waistWidth);  
                if (!foundVerts) continue;
                var appliedBodyMeshChanges = ApplyInflation(skinnedMeshRenderer.sharedMesh, GetMeshKey(skinnedMeshRenderer));
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

            InitInflationConfig();  

            //Allow an initial size to be passed in, and sets it to the config
            if (inflationSize > 0) {
                infConfig["inflationSize"] = inflationSize;
            }   

            return MeshInflate();
        }

        /// <summary>
        /// An overload for MeshInflate() that lets you pass a dictionary object with inflation params to alter the way the belly looks
        /// Create a new Config dict from the CreateConfig() method first, then modify its values as needed
        /// ex: you can make the belly 0.2f wider by chainging key inflationStretchX from default 0 -> 0.2, or decrease it by 0 -> -0.2
        /// </summary>
        /// <param name="inflationConfig">Dictionary of config values used to alter the belly size and shape.false  Create from CreateConfig() in this class </param>
        public bool MeshInflate(Dictionary<string, float> inflationConfig)
        {                  
            if (inflationConfig == null) return false;            

            //Overwrite current config with users new values
            infConfig = inflationConfig;

            //Will check and fill empty or missing values with defaults
            InitInflationConfig(); 

            return MeshInflate();
        }

        /// <summary>
        /// Get the characters waist width and calculate the appropriate sphere radius from it
        /// </summary>
        /// <param name="chaControl">The character to measure</param>
        internal Tuple<float, float> MeasureWaist(ChaControl chaControl) 
        {
            //Get belly size base calculations
            var ribBone = chaControl.objBodyBone.GetComponentsInChildren<Transform>().FirstOrDefault(x => x.name == "cf_s_spine02");
            var waistBone = chaControl.objBodyBone.GetComponentsInChildren<Transform>().FirstOrDefault(x => x.name == "cf_s_waist02");
            var waistToRibDist = Math.Abs(FastDistance(ribBone.position, waistBone.position));  

            var thighLBone = chaControl.objBodyBone.GetComponentsInChildren<Transform>().FirstOrDefault(x => x.name == "cf_j_thigh00_L");        
            var thighRBone = chaControl.objBodyBone.GetComponentsInChildren<Transform>().FirstOrDefault(x => x.name == "cf_j_thigh00_R");                    
            var waistWidth = Math.Abs(FastDistance(thighLBone.position, thighRBone.position));  
          
            //Calculate sphere radius based on distance from waist to ribs (seems big, but lerping later will trim much of it), added Math.Min for skinny waists
            var sphereRadius = Math.Min(waistToRibDist/1.25f, waistWidth/1.2f) * (infConfig["inflationMultiplier"] + 1); 

            return Tuple.Create(waistWidth, sphereRadius);
        }

        /// <summary>
        /// Just a simple function to combine searching for verts in a mesh, and then applying the transforms
        /// </summary>
        internal bool ComputeMeshVerts(SkinnedMeshRenderer smr, float sphereRadius, float waistWidth, bool isClothingMesh = false) 
        {
            //The list of bones to get verticies for
            var boneFilters = new string[] { "cf_s_spine02", "cf_s_waist01" };//"cs_s_spine01" "cf_s_waist02" optionally for wider affected area
            var hasVerticies = GetFilteredVerticieIndexes(smr, debug ? null : boneFilters);

            //If no belly verts found, then we can skip this mesh
            if (!hasVerticies) return false; 

            return GetInflatedVerticies(smr, sphereRadius, waistWidth, isClothingMesh);
        }

        /// <summary>
        /// Does the verticie morph calculations to make a sphere out of the belly verticies, and updates the verticie
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

            //User modifications to vertex position.  Moving (skin slides over sphere) vs stretch (pulls skin), defined by config sliders
            Vector3 userMoveTransforms = Vector3.down * 0.02f + Vector3.up * infConfig["inflationMoveY"] + Vector3.forward * infConfig["inflationMoveZ"];
            Vector3 userShiftTransforms = Vector3.down * infConfig["inflationShiftY"] + Vector3.forward * infConfig["inflationShiftZ"];

            //Get normal mesh root attachment position
            var meshRoot = GameObject.Find("cf_o_root");
            if (meshRoot == null) return false;

            // PregnancyPlusPlugin.Logger.LogInfo(" ");            
            // PregnancyPlusPlugin.Logger.LogInfo(
            //     $" {smr.name} >  smr {smr.transform.position}   meshRoot {meshRoot.transform.position}");
                        
            var isUncensorBody = PregnancyPlusHelper.IsUncensorBody(ChaControl, UncensorCOMName, DefaultBodyFemaleGUID);

            //set sphere center and allow for adjusting its position from the UI sliders     
            Vector3 sphereCenter = meshRoot.transform.position + userMoveTransforms; 
            Vector3 sphereCenterUncesorFix = meshRoot.transform.position + (Vector3.up * (meshRoot.transform.position.y - ChaControl.transform.position.y)) + userMoveTransforms;               

            var rendererName = GetMeshKey(smr);         
            originalVertices[rendererName] = smr.sharedMesh.vertices;
            inflatedVertices[rendererName] = new Vector3[originalVertices[rendererName].Length];
            currentVertices[rendererName] = new Vector3[originalVertices[rendererName].Length];

            var origVerts = originalVertices[rendererName];
            var inflatedVerts = inflatedVertices[rendererName];
            var currentVerts = currentVertices[rendererName];
            var bellyVertIndex = bellyVerticieIndexes[rendererName];           

            //Set each verticies inflated postion, with some constraints (SculptInflatedVerticie) to make it look more natural
            for (int i = 0; i < origVerts.Length; i++)
            {
                var origVert = origVerts[i];
                var origVertWS = meshRoot.transform.TransformPoint(origVerts[i]);//Convert to worldspace

                //Only care about inflating belly verticies
                if (bellyVertIndex[i]) 
                {
                    Vector3 inflatedVertWS;                    
                    Vector3 verticieToSphere;  

                    //Shift each belly vertex away from sphere center
                    if (!isClothingMesh) 
                    {                        
                        sphereCenter = isUncensorBody ? sphereCenterUncesorFix : sphereCenter;//Fix for uncensor local vertex positions being different than default body mesh
                        verticieToSphere = (origVertWS - sphereCenter).normalized * sphereRadius + sphereCenter + userShiftTransforms;                     
                    }
                    else 
                    {
                        //Clothes need some more loving to get them to stop clipping at max size
                        verticieToSphere = (origVertWS - sphereCenter).normalized * (sphereRadius + 0.003f) + sphereCenter + userShiftTransforms;                                           
                    }     

                    //Make minor adjustments to the shape
                    inflatedVertWS =  SculptInflatedVerticie(origVertWS, verticieToSphere, sphereCenter, waistWidth);                    
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
        /// This will take the sphere-ified verticies and apply smoothing to them via Lerps, to remove sharp edges, 
        /// and make the belly more belly like
        /// </summary>
        /// <param name="originalVertice">The original verticie position</param>
        /// <param name="inflatedVerticie">The target verticie position, after sphere-ifying</param>
        /// <param name="sphereCenterPos">The center of the imaginary sphere</param>
        /// <param name="rootPosition">The characters world root position</param>
        /// <param name="isClothingMesh">Needed for clothing meshes for special positional logic to match the skin mesh position</param>
        internal Vector3 SculptInflatedVerticie(Vector3 originalVertice, Vector3 inflatedVerticie, Vector3 sphereCenterPos, float waistWidth) 
        {
            //No smoothing modification in debug mode
            if (debug) return inflatedVerticie;            
            
            //Clothing needs to be referenced form zero as the center?
            var skinToCenterDist = Math.Abs(FastDistance(sphereCenterPos, originalVertice));
            var maxSphereRadius = Math.Abs(FastDistance(sphereCenterPos, inflatedVerticie));
            var waistRadius = waistWidth/2;
            
            //Only apply morphs if the imaginary sphere is outside of the skins boundary (Don't want to shrink anything inwards, only out)
            if (skinToCenterDist >= maxSphereRadius) return originalVertice;        

            var smoothedVector = inflatedVerticie;
            var zSmoothDist = maxSphereRadius/1.75f;//Just pick a float that looks good
            var ySmoothDist = maxSphereRadius/2f;//Only smooth the top half of y

            //Allow user adjustment of the width of the belly
            if (infConfig["inflationStretchX"] != 0) {       
                float xPos; 
                //local Distance left or right from sphere center
                var distFromXCenter = smoothedVector.x - sphereCenterPos.x;

                var changeInDist = distFromXCenter * (infConfig["inflationStretchX"] + 1);   
                xPos = sphereCenterPos.x + changeInDist;

                smoothedVector = new Vector3(xPos, smoothedVector.y, smoothedVector.z);         
            }

            //Allow user adjustment of the height of the belly
            if (infConfig["inflationStretchY"] != 0) {    
                float yPos;
                //local Distance up or down from sphere center
                var distFromYCenter = smoothedVector.y - sphereCenterPos.y;
                
                //have to change growth direction above and below center line
                var changeInDist = distFromYCenter * (infConfig["inflationStretchY"] + 1);  
                yPos = sphereCenterPos.y + changeInDist;
                
                smoothedVector = new Vector3(smoothedVector.x, yPos, smoothedVector.z);         
            }

            var forwardFromCenter = smoothedVector.z - sphereCenterPos.z;
            //Remove the skin cliff where the inflation begins
            if (forwardFromCenter <= zSmoothDist) {
                var lerpScale = Mathf.Abs(forwardFromCenter/zSmoothDist);
                smoothedVector = Vector3.Lerp(originalVertice, smoothedVector, lerpScale);
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
            //Don't allow any morphs to shrink skin smaller than its original position, only outward morphs allowed (check this last)
            if (skinToCenterDist > currentVectorDistance) {
                return originalVertice;
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

        /// <summary>
        /// This should be the first thing called on class start
        /// It will set up the necessary dictionary config default values
        /// </summary>
        /// <returns>Will return True on the initial init, and false if the default values are already set</returns>
        internal bool InitInflationConfig() 
        {
            var init = false;
            var configKeys = configDefaults.Keys;

            //Check each config option for a value, otherwise create the dictionary key
            foreach(var key in configKeys) 
            {
                //Check for existing value
                if (!infConfig.TryGetValue(key, out float value)) {
                    //Create the default dict value if it did not exists
                    infConfig[key] = configDefaults[key];
                    init = true;
                }
                if (!infConfigHistory.TryGetValue(key, out float valueHistory)) {
                    infConfigHistory[key] = configDefaults[key];
                    init = true;
                }
            }

            //If initializing the values for the first time return true
            return init;
        }
       
        internal bool NeedsMeshUpdate() 
        {
            var hasChanges = false;
            var configKeys = configDefaults.Keys;

            //Check each config option for a value
            foreach(var key in configKeys) 
            {
                //See if the user has changed any config values that were not caught by the config UI triggers
                if (!infConfig.TryGetValue(key, out float value)) {
                    PregnancyPlusPlugin.Logger.LogInfo($"NeedsMeshUpdate > {key} was not properly initialized.  Should call InitInflationConfig() first");
                    continue;
                }
                if (!infConfigHistory.TryGetValue(key, out float valueHistory)) {
                    PregnancyPlusPlugin.Logger.LogInfo($"NeedsMeshUpdate > {key} was not properly initialized.  Should call InitInflationConfig() first");
                    continue;
                }

                if (infConfig[key] != infConfigHistory[key]) {
                    // PregnancyPlusPlugin.Logger.LogInfo($"NeedsMeshUpdate > {key} {Math.Round(infConfigHistory[key], 3)}->{Math.Round(infConfig[key], 3)}");
                    hasChanges = true;                
                }
            }

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
        internal bool ApplyInflation(Mesh mesh, string renderKey) 
        {
            var infSize = infConfig["inflationSize"];
            //Only inflate if the value changed        
            if (infSize.Equals(null) || infSize == 0) return false;            

            // StartInflate(balloon);
            var origVert = originalVertices[renderKey];
            var currentVert = currentVertices[renderKey];
            var bellyVertIndex = bellyVerticieIndexes[renderKey];

            if (bellyVertIndex.Length == 0) return false;
            infConfigHistory["inflationSize"] = infSize;

            for (int i = 0; i < currentVert.Length; i++)
            {
                //If not a belly index verticie then skip the morph
                if (bellyVertIndex[i] != true) continue;

                currentVert[i] = Vector3.Lerp(origVert[i], inflatedVertices[renderKey][i], (infSize/40));
            }

            if (currentVert.Length != mesh.vertexCount) 
            {
                PregnancyPlusPlugin.Logger.LogInfo(
                            $"ApplyInflation > smr '{renderKey}' has incorrect vert count {currentVert.Length}|{mesh.vertexCount}");
            }

            mesh.vertices = currentVert;
            mesh.RecalculateBounds();
            mesh.RecalculateNormals();
            mesh.RecalculateTangents();

            return true;
        }    
        
        internal void ResetInflation() 
        {   
            //Resets all mesh inflations
            var keyList = new List<string>(originalVertices.Keys);

            //For every active meshRenderer.name
            foreach(var renderKey in keyList) 
            {
                var renderer = PregnancyPlusHelper.GetMeshRenderer(ChaControl, renderKey);
                //Normally triggered when user changes clothes, the old clothes render wont be found
                if (renderer == null) continue;                

                var sharedMesh = renderer.sharedMesh;
                var success = originalVertices.TryGetValue(renderKey, out Vector3[] origVerts); 

                //On change clothes original verts become useless, so skip this
                if (!success) return;           

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
