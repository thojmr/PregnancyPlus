using KKAPI;
using KKAPI.Chara;
using UnityEngine;
using System;
using System.Collections.Generic;
using System.Linq;
#if HS2 || AI
    using AIChara;
#endif

#if HS2 || AI || KKS
    using Unity.Jobs;
    using Unity.Collections;
#endif

namespace KK_PregnancyPlus
{

    //This partial class contains the clothing offset calculation logic, for better placement of clothing on the belly after inflation
    public partial class PregnancyPlusCharaController: CharaCustomFunctionController
    {           

        internal Vector3[] rayCastTargetPositions = new Vector3[4];
        
        #if KK      
            //The bones we want to make raycast targets.  They must be skinned by the body mesh as well
            internal string[] rayCastTargetNames = new string[4] { "cf_s_spine02", "cf_s_waist01", "cf_s_thigh01_L", "cf_s_thigh01_R" };        
            //Clothing layers, based on clothing name
            internal string[] innerLayers = {"o_bra_a", "o_bra_b", "o_shorts_a", "o_shorts_b", "o_panst_garter1", "o_panst_a", "o_panst_b"};

        #elif HS2 || AI                
            internal string[] rayCastTargetNames = new string[4] { "cf_J_Spine02_s", "cf_J_Kosi01_s", "cf_J_LegUp01_s_L", "cf_J_LegUp01_s_R" };
            internal string[] innerLayers = {"o_bra_a", "o_bra_b", "o_shorts_a", "o_shorts_b", "o_panst_garter1", "o_panst_a", "o_panst_b"};
            
        #endif   


        /// <summary>
        /// Create a new mesh collider on a skinned mesh renderer
        /// </summary>
        public Mesh CreateMeshCollider(SkinnedMeshRenderer bodySmr = null)
        {        
            var colliderExists = GetMeshCollider(bodySmr);
            if (colliderExists != null) return null;

            //Check for body mesh data dict
            md.TryGetValue(GetMeshKey(bodySmr), out MeshData _md);

            //Create the collider component
            var collider = bodySmr.transform.gameObject.AddComponent<MeshCollider>();

            //Shift collider verticies into localspace so they line up with the BindPose verts positions (Basically ignoring charracter animations)           
            var originalVerts = _md.originalVertices;

            //Create mesh instance
            bodySmr.sharedMesh = bodySmr.sharedMesh;
            //Copy the current base body mesh to use as the collider
            var meshCopy = (Mesh)UnityEngine.Object.Instantiate(bodySmr.sharedMesh); 

            meshCopy.vertices = OffSetMeshCollider(bodySmr, originalVerts);
            collider.sharedMesh = meshCopy;            
            return meshCopy;
        }


        /// <summary>
        /// Get an existing body mesh collider
        /// </summary>
        public MeshCollider GetMeshCollider(SkinnedMeshRenderer bodySmr = null)
        {
            if (bodySmr == null) bodySmr = GetBodyMeshRenderer();
            if (bodySmr == null) return null;

            //Get the collider component if it exists
            var collider = bodySmr.gameObject.GetComponent<MeshCollider>();
            if (collider == null) return null;

            // if (PregnancyPlusPlugin.DebugLog.Value) DebugTools.DebugMeshVerts(collider.sharedMesh.vertices);

            return collider;
        }


        /// <summary>
        /// In order to line up the mesh collider with the bindpose mesh, we need to offset it
        /// </summary>
        public Vector3[] OffSetMeshCollider(SkinnedMeshRenderer bodySmr, Vector3[] originalVerts)
        {
            var shiftedVerts = new Vector3[originalVerts.Length];

            #if KK
                //Some kk body meshes need an offset to align the meshCollider verts 
                var offsetType = MeshOffSet.GetMeshOffsetType(bodySmr);
                var offset = ChaControl.transform.InverseTransformPoint(bodySmr.transform.position);
            #endif

            for (int i = 0; i < originalVerts.Length; i++)
            {
                //Align the verts to 0,0,0
                shiftedVerts[i] = ChaControl.transform.InverseTransformPoint(originalVerts[i]);
                #if KK     
                    //Apply KK offset when needed               
                    if (offsetType == MeshOffsetType.DefaultMesh) shiftedVerts[i] = shiftedVerts[i] - offset;  
                    // if (offsetType == MeshOffsetType.AlmostDefaultMesh) shiftedVerts[i] = shiftedVerts[i] - offset/2;  //TODO
                #endif
            } 

            if (PregnancyPlusPlugin.DebugLog.Value) DebugTools.DebugMeshVerts(shiftedVerts);
            if (PregnancyPlusPlugin.DebugLog.Value) DebugTools.DebugMeshVerts(bodySmr.sharedMesh.vertices);

            return shiftedVerts;
        }


        /// <summary>
        /// Destroy an existing mesh collider
        /// </summary>
        public void RemoveMeshCollider()
        {
            var collider = GetMeshCollider();
            if (collider != null) Destroy(collider);
        }


        /// <summary>
        /// Compute the clothVert offset for each clothing vert from the distance it is away from the skin mesh
        ///  BodySmr must have mesh collider attached at this point with CreateMeshCollider()
        /// </summary>
        internal float[] DoClothMeasurement(SkinnedMeshRenderer clothSmr, SkinnedMeshRenderer bodySmr, 
                                            Vector3 sphereCenter, bool needsRecomputeOffsets = false)
        {     
            if (!bodySmr) return null;   

            //skip body meshes  (but this can be incorrect when a clothing mesh contains o_body_a or _cf in rare cases (Bad mesh makers! bad!))
            if (clothSmr.name.Contains(BodyMeshName)) return null;    
            if (infConfig.clothingOffsetVersion == 0) return null;

            //Get the pre calculated preg verts for this mesh
            var renderKey = GetMeshKey(clothSmr);        
            md.TryGetValue(renderKey, out MeshData _md);            
            if (!_md.HasOriginalVerts) return null;//Hopefully this never happens
            var origVerts = md[renderKey].originalVertices;

            var bellyVerticieIndexes = md[renderKey].bellyVerticieIndexes;

            //Check for existing offset values, init if none found
            var clothingOffsetsHasValue = md[renderKey].HasClothingOffsets;
            var clothOffsets = new float[0];
            if (!clothingOffsetsHasValue) 
            {
                md[renderKey].clothingOffsets = new float[origVerts.Length];
                clothOffsets = md[renderKey].clothingOffsets;
            }
            //If we have already computed these for this mesh, just return the existing values
            else if (!needsRecomputeOffsets && clothingOffsetsHasValue)
            {
                return md[renderKey].clothingOffsets;
            }

            //Lerp the final offset based on the inflation size.  Since clothes will be most flatteded at the largest size (40), and no change needed at default belly size
            var rayCastDist = bellyInfo.OriginalSphereRadius/2;            
            var minOffset = bellyInfo.WaistWidth/200;                

            //Get the mesh collider we will raycast to (The body mesh)
            var meshCollider = GetMeshCollider();
            if (meshCollider == null) 
            {
                if (PregnancyPlusPlugin.DebugLog.Value) PregnancyPlusPlugin.Logger.LogWarning($" MeshCollider is null when it shouldn't be");
                return null;
            }
  
            //Get the 4 or 5 points inside the body we want to raycast to
            GetRayCastTargetPositions(sphereCenter);

            if (PregnancyPlusPlugin.DebugLog.Value) PregnancyPlusPlugin.Logger.LogInfo($" Pre-calculating clothing offset values");

            //In newer versions of Unity we can use RaycastCommand to run a list of raycast in parallel for speed!
            #if HS2 || AI || KKS

                // the last index (+1) is the sphere center position.  The rest of the indexes are a list of bone positions
                var raycastTargetCount = rayCastTargetPositions.Length + 1;
                var rayCastHits = ProcessRayCastCommands(clothSmr, origVerts, bellyVerticieIndexes, sphereCenter, rayCastDist);                

            #endif

            // Clear existing lines on this mesh
            // if (PregnancyPlusPlugin.DebugLog.Value) DebugTools.DrawLineAndAttach(clothSmr.transform, Vector3.zero, Vector3.zero, Vector3.zero, true); 

            //When we need to initially caluculate the offsets.  For each vert triger raycast and check for hit
            for (var i = 0; i < origVerts.Length; i++)
            {
                //Skip untouched verts
                if (!bellyVerticieIndexes[i]) 
                {
                    continue;
                }

                //Convert to worldspace since thats where the mesh collider lives, apply any offset needed to align meshes to same y height
                var origVertLs = origVerts[i];                
                
                #if KK && !KKS

                    //For each of the 4 or 5 raycasts, get the closest hit vlaue
                    GetClosestRayCast(meshCollider, origVertLs, sphereCenter, rayCastDist, out float closestHit, out Vector3 direction, out var hit);

                #elif HS2 || AI || KKS

                    //Raycast were done in parallel earlier, compare the hit disances for each target
                    var closestHit = rayCastDist;
                    //Just for visualizing the hits
                    // var direction = sphereCenter - origVertLs;
                    // var hit = new RaycastHit();

                    //For each ray cast target group, unroll and compute closest hit
                    for (int t = 0; t < raycastTargetCount; t++)
                    {
                        //Compute true index of this raycaast command
                        var indexPos = (i * raycastTargetCount) + t;          
                        if (rayCastHits[indexPos].collider == null) continue;
                        if (rayCastHits[indexPos].collider.GetType() != typeof(MeshCollider)) continue;

                        if (rayCastHits[indexPos].distance < closestHit) 
                        {                            
                            closestHit = rayCastHits[indexPos].distance;
                            // direction = rayCastHits[indexPos].point - origVertLs; 
                            // hit = rayCastHits[indexPos];
                        }
                    }                    

                #endif

                //Show rays and hits
                // if (PregnancyPlusPlugin.DebugCalcs.Value) DebugTools.ShowRayCast(origVertLs, direction, hit);

                //Ignore any raycast that didnt hit the mesh collider
                if (closestHit >= rayCastDist) 
                {
                    clothOffsets[i] = minOffset;
                    continue;
                }

                //Always add a min distance to the final offset to prevent skin tight clothing clipping
                clothOffsets[i] = closestHit + minOffset;              
            }                       

            return clothOffsets;
        }


        #if HS2 || AI || KKS

            /// <summary>
            /// Compute a list of origins, and distances to be used in RaycastCommands (computed in parallel)
            /// </summary>
            internal RaycastHit[] ProcessRayCastCommands(SkinnedMeshRenderer clothSmr, Vector3[] origVerts, bool[] bellyVerticieIndexes,
                                                         Vector3 sphereCenter, float maxDistance)
            {
                // +1 is where we will insert the, sphere center position.  The rest of the targets are a list of bones
                var raycastTargetCount = rayCastTargetPositions.Length + 1;

                //Every x item in the list belongs to a single vert cast against multuple targets
                var rayCastOrigins = new Vector3[origVerts.Length * raycastTargetCount];
                var rayCastDirections = new Vector3[origVerts.Length * raycastTargetCount];

                //Build list of raycast origins and directions
                for (var i = 0; i < origVerts.Length; i++)
                {
                    //Skip untouched verts
                    if (!bellyVerticieIndexes[i]) 
                    {
                        continue;
                    }

                    //Convert to worldspace since thats where the mesh collider lives, apply any offset needed to align meshes to same y height
                    var origVertLs = origVerts[i];
                    var dir = Vector3.zero;

                    //For each ray cast target
                    for (int t = 0; t < raycastTargetCount; t++)
                    {
                        //Compute true index of this raycast command
                        var indexPos = (i * raycastTargetCount) + t;                   
                        
                        //Include raycast to sphere center as the last target
                        if (t == (raycastTargetCount - 1)) 
                        {
                            dir = sphereCenter - origVertLs;
                        }
                        else
                        {
                            //Otherwise just get the current bone target
                            dir = rayCastTargetPositions[t] - origVertLs;                            
                        }                        

                        rayCastOrigins[indexPos] = origVertLs;
                        rayCastDirections[indexPos] = dir;   
                    }
                }                

                //Create and execute raycast commands that run in parallel
                return ExecuteRayCastCommands(rayCastOrigins, rayCastDirections, maxDistance);
            }


            /// <summary>
            /// Compute a list of raycast in parallel for faster processing
            ///     We could optimze this by only raycasing belly verts, instead of all verts.  But since RaycastCommand is parallel does it really help that much?  Its still way faster now
            /// </summary>
            internal RaycastHit[] ExecuteRayCastCommands(Vector3[] origins, Vector3[] directions, float maxDistance)
            {
                // Perform a single raycast using RaycastCommand and wait for it to complete
                // Setup the command and result buffers
                var results = new NativeArray<RaycastHit>(origins.Length, Allocator.Temp);
                var commands = new NativeArray<RaycastCommand>(directions.Length, Allocator.Temp);

                for (int i = 0; i < origins.Length; i++)
                {
                    commands[i] = new RaycastCommand(origins[i], directions[i], distance: maxDistance, maxHits: 1);   
                }                

                // Schedule the batch of raycasts
                JobHandle handle = RaycastCommand.ScheduleBatch(commands, results, 1, default(JobHandle));

                // Wait for the batch processing job to complete
                handle.Complete();

                // Copy the result. If batchedHit.collider is null there was no hit
                RaycastHit[] batchedHit = results.ToArray();

                // Dispose the buffers
                results.Dispose();
                commands.Dispose();

                return batchedHit;
            }
            
        #endif


        /// <summary>
        /// Raycast from the clothing vert to the direction passed and get the distance if it hits the mesh collider
        /// </summary>
        public RaycastHit RayCastToMeshCollider(Vector3 origin, float maxDistance, Vector3 direction, MeshCollider meshCollider)
        {
            var ray = new Ray(origin, direction);

            //Ray cast to the mesh collider
            meshCollider.Raycast(ray, out var hit, maxDistance);

            //Will return maxDistance if nothing is hit
            return hit;
        }


        /// <summary>
        /// Compute the clothVert offset for each clothing vert from the distance it is away from the skin mesh
        ///  BodySmr must have mesh collider attached at this point
        /// </summary>
        internal bool NeedsClothMeasurement(SkinnedMeshRenderer clothSmr, SkinnedMeshRenderer bodySmr, Vector3 sphereCenter)
        {  
            if (!bodySmr) return false;   

            //skip body meshes  (but this can be incorrect when a clothing mesh contains o_body_a or _cf in rare cases (Bad mesh makers! bad!))
            if (clothSmr.name.Contains(BodyMeshName)) return false;    
            if (infConfig.clothingOffsetVersion == 0) return false;

            var renderKey = GetMeshKey(clothSmr); 
            var clothingOffsetsHasValue = md[renderKey].HasClothingOffsets;

            //Does the offset list already exists for this mesh?
            if (clothingOffsetsHasValue) return false;

            return true;
        }


        /// <summary>
        /// Get the lowest distance of the cloth mesh to skin mesh based on a number of different raycast
        /// </summary>
        public void GetClosestRayCast(MeshCollider meshCollider, Vector3 clothVertLs, Vector3 sphereCenter, float maxDistance, out float dist, out Vector3 direction, out RaycastHit hit)
        {
            var lowestDist = maxDistance;    
            direction = Vector3.zero;        
            dist = maxDistance;
            hit = new RaycastHit();

            //For each bone we want to raycast to
            foreach(var bonePosition in rayCastTargetPositions)
            {
                var _dir = bonePosition - clothVertLs;
                var _tryHit = RayCastToMeshCollider(clothVertLs, maxDistance, _dir, meshCollider);
                //Get closest raycast hit
                if (_tryHit.distance < lowestDist) 
                {
                    lowestDist = _tryHit.distance;
                    direction = _dir;
                    hit = _tryHit;
                }
            }

            //Also check raycast to the current sphereCenter
            var _direction = sphereCenter - clothVertLs;
            var tryHit = RayCastToMeshCollider(clothVertLs, maxDistance, _direction, meshCollider);
            if (tryHit.distance < lowestDist) 
            {
                lowestDist = tryHit.distance;
                direction = _direction;
                hit = tryHit;
            }

            dist = lowestDist;  
        }


        /// <summary>
        /// Get the positions of a few bones that we will raycast to
        /// </summary>
        public void GetRayCastTargetPositions(Vector3 sphereCenter)
        {
            //Get the t-pose positions of these bones that we want to raycast to
            var bodySmr = GetBodyMeshRenderer();
            if (bodySmr == null) return;
            var bindPoseOffset = md[GetMeshKey(bodySmr)].bindPoseCorrection;

            if (PregnancyPlusPlugin.DebugCalcs.Value) DebugTools.DrawSphere(0.05f, sphereCenter);

            for (int i = 0; i < rayCastTargetNames.Length; i++)
            {
                //Incase the below fails, fall back to sphere center
                rayCastTargetPositions[i] = sphereCenter;

                //Get the target bone name index
                var j = Array.FindIndex(bodySmr.bones, b => b.name == rayCastTargetNames[i]);
                if (j < 0) 
                {
                    PregnancyPlusPlugin.errorCodeCtrl.LogErrorCode(charaFileName, ErrorCode.PregPlus_MeshNotSkinnedToBone, 
                        $" This this bone `{rayCastTargetNames[i]}` is not skinned to the body mesh, so a measurement was skipped."); 
                    return;
                };                

                //Compute the bind pose bone position
                MeshSkinning.GetBindPoseBoneTransform(bodySmr, bodySmr.sharedMesh.bindposes[j], bindPoseOffset, out var position, out var rotation);
                rayCastTargetPositions[i] = position;

                if (PregnancyPlusPlugin.DebugCalcs.Value) DebugTools.DrawSphere(0.05f, position); 
            }            
        }


        /// <summary>
        /// Reduce the total offset when a clothing vert is near the sphere radius boundary, for a smooth transition to non modified verts
        /// </summary>
        public float GetEdgeLerp(Vector3 origVertWs, Vector3 center, float offset)
        {
            //Get the distance the original cloth vert is from the sphere radius
            var distancePastRadius = Vector3.Distance(origVertWs, center) - bellyInfo.SphereRadius;
            //The further the vert is outside the radius, the less it is offset (begin lerp just before the radius is reached for best results)
            var edgeLerpOffset = Mathf.Lerp(offset, 0, (distancePastRadius - bellyInfo.WaistWidth/15)/(bellyInfo.WaistWidth/10));

            return edgeLerpOffset;
        }


#region Slider Controlled Offset

        /// <summary>
        /// Allows users to adjust the offset of clothing by a small amount, uses V2 by default with characters saved on v1.27+
        /// </summary>
        internal float GetClothesFixOffset(PregnancyPlusData infConfigClone, Vector3 sphereCenterWs, float sphereRadius, float waistWidth, 
                                           Vector3 origVertWS, string meshName, float offset) 
        {
            //Figure out which version of the clothing offset logic the character was made with, and apply the offset
            if (infConfig.clothingOffsetVersion == 1)
            {
                //V2 is just a simple offset based on slider value, since DoClothMeasurement takes care of making sure any cloth bypasses the cloth flattening issue.
                //This method remains as a way for the user to further offset clothing items if they need to
                return GetClothesFixOffsetV2(infConfigClone, sphereCenterWs, sphereRadius, waistWidth, origVertWS, meshName, offset);
            } 
            else 
            {
                //V1 is much more complicated and tries to overcome the cloth flattening issues all on its own, while at the same time allowing user custom offset amount
                return GetClothesFixOffsetV1(infConfigClone, sphereCenterWs, sphereRadius, waistWidth, origVertWS, meshName);
            }
        }


        /// <summary>
        /// Allows users to adjust the offset of clothing by a small amount.  In V2 we simplified this logic since DoClothMeasurement() does most of the heavy lifting
        /// </summary>
        /// <param name="meshRootTf">The transform used to convert a mesh vector from local space to worldspace and back</param>
        /// <param name="sphereCenterWs">The center position of the inflation sphere</param>
        /// <param name="sphereRadius">The desired sphere radius</param>
        /// <param name="waistWidth">The average width of the characters waist</param>
        /// <param name="origVertWS">The original verticie's worldspace position</param>
        /// <param name="meshName">Used to determine inner vs outer mesh layers from a known list of names</param>
        internal float GetClothesFixOffsetV2(PregnancyPlusData infConfigClone, Vector3 sphereCenterWs, float sphereRadius, float waistWidth, 
                                             Vector3 origVertWS, string meshName, float offset) 
        {  
            //Check that the slider has a non zero value
            var inflationOffset = GetInflationClothOffset(infConfigClone);

            //The size of the area to spread the flattened offsets over like shrinking center dist -> inflated dist into a small area shifted outside the radius.  So hard to explin with words...
            var shrinkedOffset = offset + (bellyInfo.WaistWidth/100 * inflationOffset);

            // //The closer the cloth is to the end of the sphere radius, the less we want to move it on offset
            var clothFromEndDistLerp = Vector3.Distance(sphereCenterWs, origVertWS)/sphereRadius;    
            // diving by 3 just gave the best results      
            var lerpedOffset = Mathf.Lerp(shrinkedOffset, shrinkedOffset/3, clothFromEndDistLerp);

            //This is the total additional distance we want to move this vert away from sphere center.  Move it inwards just a tad
            return lerpedOffset + GetClothLayerOffsetV2(infConfigClone, meshName);
        }


        /// <summary>
        /// There are two cloth layers, inner and outer. I've assigned each cloth layer a static offset. 
        ///    layers: 1 = skin tight, 2 = above skin tight.  This way each layer will have less chance of cliping through to the next
        /// </summary>
        internal float GetClothLayerOffsetV2(PregnancyPlusData infConfigClone, string meshName) 
        {                     
            //If inner layer then it doesnt need an additional offset
            if (innerLayers.Contains(meshName)) 
            {
                return 0;
            }

            //The mininum distance offset for each cloth layer, adjusted by user
            float additonalOffset = (bellyInfo.WaistWidth/120) + (bellyInfo.WaistWidth/100) * GetInflationClothOffset(infConfigClone);

            //If outer layer then add the offset
            return additonalOffset;
        }


        /// <summary>
        /// Tried to correct cloth flattening when inflation is at max, by offsetting each vert based on the distance it is from the sphere center to the max sphere radius
        /// </summary>
        /// <param name="meshRootTf">The transform used to convert a mesh vector from local space to worldspace and back</param>
        /// <param name="sphereCenterWs">The center position of the inflation sphere</param>
        /// <param name="sphereRadius">The desired sphere radius</param>
        /// <param name="waistWidth">The average width of the characters waist</param>
        /// <param name="origVertWS">The original verticie's worldspace position</param>
        /// <param name="meshName">Used to determine inner vs outer mesh layers from a known list of names</param>
        internal float GetClothesFixOffsetV1(PregnancyPlusData infConfigClone, Vector3 sphereCenterWs, float sphereRadius, 
                                             float waistWidth, Vector3 origVertWS, string meshName) 
        {  
            //The size of the area to spread the flattened offsets over like shrinking center dist -> inflated dist into a small area shifted outside the radius.  So hard to explin with words...
            float shrinkBy = bellyInfo.WaistWidth/20 + (bellyInfo.WaistWidth/20 * GetInflationClothOffset(infConfigClone));

            var inflatedVerWS = (origVertWS - sphereCenterWs).normalized * sphereRadius + sphereCenterWs;//Get the line we want to do measurements on            
            //We dont care about empty space at sphere center, move outwards a bit before determining vector location on the line
            float awayFromCenter = (bellyInfo.WaistWidth/3);

            //The total radial distance after removing the distance we want to ignore
            var totatDist = (sphereRadius - awayFromCenter);
            var chothToEndDist = Vector3.Distance(origVertWS, inflatedVerWS);
            //The closer the cloth is to the end of the sphere radius, the less we want to move it on offset
            var clothFromEndDistLerp = Vector3.Distance(sphereCenterWs, origVertWS)/sphereRadius;
            //Get the positon on a line that this vector exists between flattenExtensStartAt -> to sphereRadius. Then shrink it down to a thin layer
            var offset = (totatDist - chothToEndDist) * shrinkBy;            
            var lerpedOffset = Mathf.Lerp(offset, offset/5, clothFromEndDistLerp);

            //This is the total additional distance we want to move this vert away from sphere center.  Move it inwards just a tad
            return lerpedOffset + GetClothLayerOffsetV1(infConfigClone, meshName);
        }


        /// <summary>
        /// There are two cloth layers, inner and outer. I've assigned each cloth layer a static offset. layers: 1 = skin tight, 2 = above skin tight.  This way each layer will have less chance of cliping through to the next
        /// </summary>
        internal float GetClothLayerOffsetV1(PregnancyPlusData infConfigClone, string meshName) 
        {                  
            //If inner layer then it doesnt need an additional offset
            if (innerLayers.Contains(meshName)) 
            {
                return 0;
            }

            //The mininum distance offset for each cloth layer, adjusted by user
            float additonalOffset = (bellyInfo.WaistWidth/60) + ((bellyInfo.WaistWidth/60) * GetInflationClothOffset(infConfigClone));

            //If outer layer then add the offset
            return additonalOffset;
        } 

#endregion Slider Controlled Offset

    }
}


