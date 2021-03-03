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


        /// <summary>
        /// Create a new mesh collider on a skinned mesh renderer
        /// </summary>
        public void CreateMeshCollider(SkinnedMeshRenderer bodySmr = null)
        {        
            //If not passed in, fetch it
            bodySmr = bodySmr != null ? bodySmr : GetBodyMeshRenderer();
            if (bodySmr == null) return;

            //Skip when the collider already exists
            var colliderExists = bodySmr.transform.gameObject.GetComponent<MeshCollider>();
            if (colliderExists != null) return;

            //Create the collider component
            var collider = bodySmr.transform.gameObject.AddComponent<MeshCollider>();
            //Copy the current base body mesh to use as the collider
            var meshCopy = (Mesh)UnityEngine.Object.Instantiate(bodySmr.sharedMesh); 

            collider.sharedMesh = meshCopy;
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
            var clothOffsets = clothingOffsets[renderKey];
            var clothingOffsetsHasValue = clothOffsets[0].Equals(null);
            //Lerp the final offset based on the inflation size.  Since clothes will be most flatteded at the largest size (40), and no change needed at default belly size
            var clothOffsetLerp = infConfig.inflationSize/40;

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
                if (PregnancyPlusPlugin.DebugLog.Value) PregnancyPlusPlugin.Logger.LogInfo($" Re-Using clothing offset values");
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
                var dist = RayCastToCenter(origVertWs, center, bellyInfo.OriginalSphereRadius);

                // DebugTools.DrawLine(origVertWs, center, 0.1f);

                //Ignore any distance that didnt hit the mesh collider
                if (dist >= bellyInfo.OriginalSphereRadius) 
                {
                    clothOffsets[i] = 0;
                    inflatedVertOffsets[i] = inflatedVerts[i];
                    continue;
                }
                clothOffsets[i] = dist;
                
                //Offset the Inflated vert by the raycast hit distance, and away from center              
                inflatedVertOffsets[i] = clothSmr.transform.InverseTransformPoint(VertOffsetWs(inflatedVertWs, center, dist, clothOffsetLerp));
                // if (PregnancyPlusPlugin.DebugLog.Value) PregnancyPlusPlugin.Logger.LogInfo($" MeshCollider dist {dist} lerp {clothOffsetLerp} max {bellyInfo.OriginalSphereRadius}");
            }

            currentMeshSphereCenter = Vector3.zero;
        }


        /// <summary>
        /// Calculate new position of the cloth vert after applying the original skin distance to it
        /// </summary>
        public Vector3 VertOffsetWs(Vector3 inflatedVertWs, Vector3 center, float distance, float lerp)
        {
            //Original inflatedvert + a direction + an offset
            return inflatedVertWs + (inflatedVertWs - center).normalized * (distance * lerp);
        }
    }
}


