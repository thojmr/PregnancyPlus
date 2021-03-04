using KKAPI;
using KKAPI.Chara;
using UnityEngine;
using System;
using System.Collections.Generic;
#if HS2 || AI
    using AIChara;
#endif

namespace KK_PregnancyPlus
{

    //This partial class contains the clothing offset calculation logic
    public partial class PregnancyPlusCharaController: CharaCustomFunctionController
    {           

        public Vector3 currentMeshSphereCenter = Vector3.zero;
        internal string[] rayCastTargetNames = new string[4] { "cf_J_Spine02", "cf_J_Kosi01", "cf_J_LegUp00_L", "cf_J_LegUp00_R" };
        internal Vector3[] rayCastTargetPositions = new Vector3[4];


        /// <summary>
        /// Create a new mesh collider on a skinned mesh renderer
        /// </summary>
        public Mesh CreateMeshCollider(SkinnedMeshRenderer bodySmr = null)
        {        
            //If not passed in, fetch it
            bodySmr = bodySmr != null ? bodySmr : GetBodyMeshRenderer();
            if (bodySmr == null) return null;

            //Skip when the collider already exists
            var colliderExists = bodySmr.transform.gameObject.GetComponent<MeshCollider>();
            if (colliderExists != null) return null;

            //Create the collider component
            var collider = bodySmr.transform.gameObject.AddComponent<MeshCollider>();
            //Copy the current base body mesh to use as the collider
            var meshCopy = (Mesh)UnityEngine.Object.Instantiate(bodySmr.sharedMesh); 

            collider.sharedMesh = meshCopy;

            return meshCopy;
        }


        /// <summary>
        /// Destroy an existing mesh collider
        /// </summary>
        public void RemoveMeshCollider()
        {
            var bodyMeshRenderer = GetBodyMeshRenderer();
            if (bodyMeshRenderer == null) return;

            //Get the collider component if it exists
            var collider = bodyMeshRenderer.transform.gameObject.GetComponent<MeshCollider>();
            if (collider != null) Destroy(collider);
        }


        /// <summary>
        /// Get the main body mesh renderer for a character
        /// </summary>
        public SkinnedMeshRenderer GetBodyMeshRenderer()
        {
            #if KK
                var meshName = "o_body_a";
            #elif HS2 || AI
                var meshName = "o_body_cf";
            #endif
            var bodyMeshRenderers = PregnancyPlusHelper.GetMeshRenderers(ChaControl.objBody, true);
            return bodyMeshRenderers.Find(x => x.name == meshName);
        }


        /// <summary>
        /// Raycast from the clothing vert to the sphere center and get the distance if it hits the mesh collider
        /// </summary>
        public float RayCastToCenter(Vector3 clothVertWs, Vector3 sphereCenter, float maxDistance)
        {
            //Get the direction of the raycast to move
            var direction = sphereCenter - clothVertWs;
            return RayCastToCenter(clothVertWs, maxDistance, direction);
        }


        /// <summary>
        /// Raycast from the clothing vert to the direction passed and get the distance if it hits the mesh collider
        /// </summary>
        public float RayCastToCenter(Vector3 clothVertWs, float maxDistance, Vector3 direction)
        {
            if (Physics.Raycast(clothVertWs, direction, out RaycastHit hit, maxDistance))
            {
                return hit.distance;
            }

            //If nothing hit, return the default maxDistance
            return maxDistance;
        }


        /// <summary>
        /// Compute the clothVert offset for each clothing vert from the distance it is away from the skin mesh
        ///  BodySmr must have mesh collider attached at this point
        /// </summary>
        internal void DoClothMeasurement(SkinnedMeshRenderer clothSmr, SkinnedMeshRenderer bodySmr, bool needsRecomputeOffsets = false)
        {     
            if (!bodySmr) return;    
            if (clothSmr.name.Contains("o_body_cf") || clothSmr.name.Contains("o_body_a")) return;//skip body meshes     

            //Get the pre calculated preg verts for this mesh
            var renderKey = GetMeshKey(clothSmr);        
            inflatedVertices.TryGetValue(renderKey, out Vector3[] inflatedVerts);            
            if (inflatedVerts == null || inflatedVerts.Length <= 0) return;

            var inflatedVertOffsets = inflatedVerticesOffsets[renderKey];
            var origVerts = originalVertices[renderKey];
            var alteredVertIndexes = alteredVerticieIndexes[renderKey];

            //Check for existing offset values, init if none found
            var clothingOffsetsHasValue = clothingOffsets.TryGetValue(renderKey, out float[] clothOffsets);
            if (!clothingOffsetsHasValue) 
            {
                clothingOffsets[renderKey] = new float[origVerts.Length];
                clothOffsets = clothingOffsets[renderKey];
            }

            //Lerp the final offset based on the inflation size.  Since clothes will be most flatteded at the largest size (40), and no change needed at default belly size
            var clothOffsetLerp = infConfig.inflationSize/40;
            var rayCastDist = bellyInfo.OriginalSphereRadius/2;            

            #if KK
                var centerBoneName = "cf_j_waist01";
            #elif HS2 || AI
                var centerBoneName = "cf_J_Kosi01";
            #endif            
            //Center point used to get offset direction and raycast to point
            var center = currentMeshSphereCenter == Vector3.zero ? PregnancyPlusHelper.GetBone(ChaControl, centerBoneName).position : currentMeshSphereCenter;

            //When we already have the offsets, just reuse them instead of recalculating
            if (clothingOffsetsHasValue && !needsRecomputeOffsets)
            {
                for (var i = 0; i < inflatedVerts.Length; i++)
                {
                    if (!alteredVertIndexes[i]) 
                    {
                        clothOffsets[i] = 0;
                        inflatedVertOffsets[i] = inflatedVerts[i];
                        continue;
                    }

                    var inflatedVertWs = clothSmr.transform.TransformPoint(inflatedVerts[i]);
                    //Re compute the offset distance from the stored clothOffset value for this vert
                    inflatedVertOffsets[i] = clothSmr.transform.InverseTransformPoint(VertOffsetWs(inflatedVertWs, center, clothOffsets[i], clothOffsetLerp));
                }

                currentMeshSphereCenter = Vector3.zero;
                return;
            }

            //Create mesh collider to make clothing measurements from skin
            CreateMeshCollider(bodySmr);   
            GetRayCastTargetPositions();
            if (PregnancyPlusPlugin.DebugLog.Value) PregnancyPlusPlugin.Logger.LogInfo($" Pre-calculating clothing offset values");

            //When we need to initially caluculate the offsets (or rebuild).  For each vert raycast to center and see if it hits
            for (var i = 0; i < inflatedVerts.Length; i++)
            {
                //Skip untouched verts
                if (!alteredVertIndexes[i]) 
                {
                    clothOffsets[i] = 0;
                    inflatedVertOffsets[i] = inflatedVerts[i];
                    continue;
                }

                //Convert to worldspace since thats where the mesh collider lives
                var origVertWs = clothSmr.transform.TransformPoint(origVerts[i]);
                var inflatedVertWs = clothSmr.transform.TransformPoint(inflatedVerts[i]);                
                
                //Get raycast hit distance to the mesh collider on the skin
                var dist = GetClosestRayCast(origVertWs, center, rayCastDist);

                // DebugTools.DrawLine(origVertWs, center, 0.1f);

                //Ignore any distance that didnt hit the mesh collider
                if (dist >= rayCastDist) 
                {
                    clothOffsets[i] = 0;
                    inflatedVertOffsets[i] = inflatedVerts[i];
                    continue;
                }
                clothOffsets[i] = dist;
                
                //Offset the Inflated vert by the raycast hit distance, and away from center              
                inflatedVertOffsets[i] = clothSmr.transform.InverseTransformPoint(VertOffsetWs(inflatedVertWs, center, dist, clothOffsetLerp));
                // if (PregnancyPlusPlugin.DebugLog.Value) PregnancyPlusPlugin.Logger.LogInfo($" MeshCollider dist {dist} lerp {clothOffsetLerp} max {rayCastDists}");
            }

            currentMeshSphereCenter = Vector3.zero;
        }


        /// <summary>
        /// Get the lowest distance of the cloth mesh to skin mesh based on a number of different raycast
        /// </summary>
        public float GetClosestRayCast(Vector3 clothVertWs, Vector3 sphereCenter, float maxDistance)
        {
            var lowestDist = maxDistance;

            //For each bone we want to raycast to
            foreach(var bonePosition in rayCastTargetPositions)
            {
                var _currentDist = RayCastToCenter(clothVertWs, bonePosition, maxDistance);
                if (_currentDist < lowestDist) lowestDist = _currentDist;
            }

            //Also check raycast to the current sphereCenter
            var currentDist = RayCastToCenter(clothVertWs, currentMeshSphereCenter, maxDistance);
            if (currentDist < lowestDist) lowestDist = currentDist;
            
            
            return lowestDist;
        }


        /// <summary>
        /// Calculate new position of the cloth vert after applying the original skin distance to it
        /// </summary>
        public Vector3 VertOffsetWs(Vector3 inflatedVertWs, Vector3 center, float distance, float lerp)
        {
            //Original inflatedvert + a direction + an offset
            return inflatedVertWs + (inflatedVertWs - center).normalized * (distance * lerp);
        }


        /// <summary>
        /// Get the positions of a few bones that we will raycast to
        /// </summary>
        public void GetRayCastTargetPositions()
        {
            for (int i = 0; i < rayCastTargetNames.Length; i++)
            {
                rayCastTargetPositions[i] = PregnancyPlusHelper.GetBone(ChaControl, rayCastTargetNames[i]).position;
            }            
        }

    }
}


