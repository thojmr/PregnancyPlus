using KKAPI;
using KKAPI.Chara;
using UnityEngine;
using System;
using System.Collections.Generic;
using System.Linq;
#if HS2 || AI
    using AIChara;
#endif
using Unity.Jobs;
using Unity.Collections;

namespace KK_PregnancyPlus
{

    //This partial class contains the clothing offset calculation logic, for better placement of clothing on the belly after inflation
    public partial class PregnancyPlusCharaController: CharaCustomFunctionController
    {           

        internal Vector3[] rayCastTargetPositions = new Vector3[4];
        
        #if KKS      
            //The bones we want to make raycast targets.  They must be skinned by the body mesh as well
            internal string[] rayCastTargetNames = new string[4] { "cf_s_spine02", "cf_s_waist01", "cf_s_thigh01_L", "cf_s_thigh01_R" };        
            //Clothing layers, based on clothing name
            internal string[] innerLayers = {"o_bra_a", "o_bra_b", "o_shorts_a", "o_shorts_b", "o_panst_garter1", "o_panst_a", "o_panst_b"};

        #elif HS2 || AI                
            internal string[] rayCastTargetNames = new string[4] { "cf_J_Spine02_s", "cf_J_Kosi01_s", "cf_J_LegUp01_s_L", "cf_J_LegUp01_s_R" };
            internal string[] innerLayers = {"o_bra_a", "o_bra_b", "o_shorts_a", "o_shorts_b", "o_panst_garter1", "o_panst_a", "o_panst_b"};
            
        #endif   


        /// <summary>
        /// Create a new mesh collider on a skinned mesh renderer, if one already exists, skip this step
        /// </summary>
        public Mesh CreateMeshCollider(SkinnedMeshRenderer bodySmr = null)
        {        
            var colliderExists = GetMeshCollider(bodySmr);
            if (colliderExists != null) return null;


            //Check for body mesh data dict
            var rendererName = GetMeshKey(bodySmr);
            var exists = md.TryGetValue(rendererName, out MeshData _md);
            if (!exists) 
            {
                if (PregnancyPlusPlugin.DebugLog.Value) PregnancyPlusPlugin.Logger.LogWarning($" CreateMeshCollider cant find MeshData for {rendererName}"); 
                return null;
            }

            if (!_md.HasOriginalVerts)
            {
                if (PregnancyPlusPlugin.DebugLog.Value) PregnancyPlusPlugin.Logger.LogWarning($" CreateMeshCollider bodySmr original verts have not computed yet");
                return null;
            }

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
        /// In order to line up the mesh collider with the bindpose mesh, we need to move the mesh to smr localspace
        /// </summary>
        public Vector3[] OffSetMeshCollider(SkinnedMeshRenderer bodySmr, Vector3[] originalVerts)
        {
            var shiftedVerts = new Vector3[originalVerts.Length];

            //Convert the verts back into locaalspace, so when skinned by mesh collider they line up with our md[].originalverticies at 0,0,0
            //  Otherwise the raycast wont pass through the collider mesh
            for (int i = 0; i < originalVerts.Length; i++)
            {                
                shiftedVerts[i] = bodySmr.transform.InverseTransformPoint(originalVerts[i]);
            } 

            // if (PregnancyPlusPlugin.DebugLog.Value) DebugTools.DebugMeshVerts(shiftedVerts);

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
        internal float[] DoClothMeasurement(SkinnedMeshRenderer clothSmr, SkinnedMeshRenderer bodySmr, Vector3 sphereCenter, bool needsRecomputeOffsets = false)
        {     
            if (!bodySmr) return null;   
            //skip body meshes  (but this can be incorrect when a clothing mesh contains o_body_a or _cf in rare cases (Bad mesh makers! bad!))
            if (clothSmr.name.Contains(BodyMeshName)) return null;    

            //Get the pre calculated preg verts for this mesh
            var renderKey = GetMeshKey(clothSmr);        
            md.TryGetValue(renderKey, out MeshData _md);            

            //Check for existing offset values, init if none found
            var clothingOffsetsHasValue = md[renderKey].HasClothingOffsets;
            var clothOffsets = new float[0];
            if (!clothingOffsetsHasValue) 
            {
                md[renderKey].clothingOffsets = new float[clothSmr.sharedMesh.vertexCount];
                clothOffsets = md[renderKey].clothingOffsets;
            }
            //If we have already computed these for this mesh, just return the existing values
            else if (!needsRecomputeOffsets && clothingOffsetsHasValue)
            {
                return md[renderKey].clothingOffsets;
            }

            var origVerts = md[renderKey].originalVertices;
            var bellyVerticieIndexes = md[renderKey].bellyVerticieIndexes;

            //Lerp the final offset based on the inflation size.  Since clothes will be most flatteded at the largest size (40), and no change needed at default belly size
            var rayCastDist = bellyInfo.OriginalSphereRadius/2;            
            var minOffset = bellyInfo.WaistWidth/200;                

            //Get the mesh collider we will raycast to (The body mesh)
            var meshCollider = GetMeshCollider();
            if (meshCollider == null) 
            {
                if (PregnancyPlusPlugin.DebugLog.Value) PregnancyPlusPlugin.Logger.LogWarning($" MeshCollider is null when it shouldn't be");
                return md[renderKey].clothingOffsets;
            }
  
            //Get the 4 or 5 points inside the body we want to raycast to
            GetRayCastTargetPositions(sphereCenter);

            if (PregnancyPlusPlugin.DebugCalcs.Value) PregnancyPlusPlugin.Logger.LogInfo($" Pre-calculating clothing offset {clothSmr.name}");

            // the last index (+1) is the sphere center position.  The rest of the indexes are a list of bone positions
            var raycastTargetCount = rayCastTargetPositions.Length + 1;
            var rayCastHits = ProcessRayCastCommands(clothSmr, origVerts, bellyVerticieIndexes, sphereCenter, rayCastDist);                

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
        internal bool NeedsClothMeasurement(SkinnedMeshRenderer clothSmr, SkinnedMeshRenderer bodySmr, Vector3 sphereCenter, bool isClothingMesh)
        {  
            if (!isClothingMesh) return false;
            if (!bodySmr)
            {
                if (PregnancyPlusPlugin.DebugLog.Value) PregnancyPlusPlugin.Logger.LogWarning($" NeedsClothMeasurement No bodySmr, skipping for {clothSmr.name}");
                return false;
            }            

            //skip body meshes  (but this can be incorrect when a clothing mesh contains o_body_a or _cf in rare cases (Bad mesh makers! bad!))
            if (clothSmr.name.Contains(BodyMeshName)) 
            {
                if (PregnancyPlusPlugin.DebugLog.Value) PregnancyPlusPlugin.Logger.LogWarning($" NeedsClothMeasurement smr {clothSmr.name} contains {BodyMeshName} skpping");
                return false;    
            }   

            var renderKey = GetMeshKey(clothSmr); 
            return !md[renderKey].HasClothingOffsets;
        }


        /// <summary>
        /// Get the positions of a few bones that we will raycast to
        /// </summary>
        public void GetRayCastTargetPositions(Vector3 sphereCenter)
        {
            //Get the t-pose positions of these bones that we want to raycast to
            var bodySmr = GetBodyMeshRenderer();
            if (bodySmr == null) return;            

            // if (PregnancyPlusPlugin.DebugCalcs.Value) DebugTools.DrawSphere(0.05f, sphereCenter, color: Color.white);

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

                var bindPoseOffset = MeshSkinning.GetBindPoseOffset(ChaControl, bindPoseList, bodySmr, bodySmr.sharedMesh.bindposes[j], bodySmr.bones[j]) ?? Matrix4x4.identity;

                //Compute the bind pose bone position
                MeshSkinning.GetBindPoseBoneTransform(bodySmr, bodySmr.sharedMesh.bindposes[j], bindPoseOffset, out var position, out var rotation, bindPoseList, bodySmr.bones[j]);
                rayCastTargetPositions[i] = position;

                // if (PregnancyPlusPlugin.DebugCalcs.Value) DebugTools.DrawSphere(0.05f, position, color: Color.white); 
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
            //V2 is just a simple offset based on slider value, since DoClothMeasurement takes care of making sure any cloth bypasses the cloth flattening issue.
            //This method remains as a way for the user to further offset clothing items if they need to
            return GetClothesFixOffsetV2(infConfigClone, sphereCenterWs, sphereRadius, waistWidth, origVertWS, meshName, offset);
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

#endregion Slider Controlled Offset

    }
}


