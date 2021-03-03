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


        /// <summary>
        /// Create a new mesh collider on a skinned mesh renderer
        /// </summary>
        public void CreateMeshCollider(SkinnedMeshRenderer smr)
        {
            var collider = smr.transform.gameObject.AddComponent<MeshCollider>();
            var meshCopy = (Mesh)UnityEngine.Object.Instantiate(smr.sharedMesh); 

            collider.sharedMesh = meshCopy;
            // collider.isTrigger = true;
        }


        /// <summary>
        /// Destroy an existing mesh collider
        /// </summary>
        public void RemoveMeshCollider(SkinnedMeshRenderer smr)
        {
            var collider = smr.transform.gameObject.GetComponent<MeshCollider>();
            Destroy(collider);
        }


        /// <summary>
        /// Raycast from the clothing vert to the sphere center and get the distance if it hits the mesh collider
        /// </summary>
        public float RayCastToCenter(Vector3 clothVertWs, Vector3 sphereCenter, float maxDistance)
        {
            var direction = sphereCenter - clothVertWs;

            if (Physics.Raycast(clothVertWs, direction, out RaycastHit hit, maxDistance))
            {
                return hit.distance;
            }

            return maxDistance;
        }


        /// <summary>
        /// Compute the clothVert offset for each clothing vert from the distance it is away from the skin mesh
        ///  BodySmr must have mesh collider attached at this point
        /// </summary>
        internal void DoClothMeasurement(SkinnedMeshRenderer clothSmr, SkinnedMeshRenderer bodySmr)
        {     
            if (!bodySmr) return;    
            if (clothSmr.name.Contains("o_body_cf") || clothSmr.name.Contains("o_body_a")) return;//skip body meshes        

            //Get the pre calculated preg verts for this mesh
            var renderKey = GetMeshKey(clothSmr);        
            var inflatedVerts = inflatedVertices[renderKey];            
            if (inflatedVerts == null || inflatedVerts.Length <= 0) return;

            var origVerts = originalVertices[renderKey];
            var alteredVertIndexes = alteredVerticieIndexes[renderKey];
            //Lerp the final offset based on the inflation size.  Since clothes will be most flatteded at the largest size (40), and no change needed at default belly size
            var clothOffsetLerp = infConfig.inflationSize/40;

            #if KK
                var centerBoneName = "cf_j_waist01";
            #elif HS2 || AI
                var centerBoneName = "cf_J_Kosi01";
            #endif            
            //Center point used to get offset direction and raycast to point
            var center = PregnancyPlusHelper.GetBone(ChaControl, centerBoneName).position;

            var colliderCount = 0;

            //For each vert raycast to center and see if it hits
            for (var i = 0; i < inflatedVerts.Length; i++)
            {
                //Skip untouched verts
                if (!alteredVertIndexes[i]) continue;

                //Convert to worldspace since thats where the mesh collider lives
                var origVertWs = clothSmr.transform.TransformPoint(origVerts[i]);
                var inflatedVertWs = clothSmr.transform.TransformPoint(inflatedVerts[i]);
                
                //Get raycast hit distance to the mesh collider on the skin
                var dist = RayCastToCenter(origVertWs, center, bellyInfo.OriginalSphereRadius);

                // DebugTools.DrawLine(origVertWs, center, 0.1f);

                //Ignore any distance that didnt hit the mesh collider
                if (dist >= bellyInfo.OriginalSphereRadius) continue;

                //Offset the Inflated vert by the raycast hit distance, and away from center              
                inflatedVerts[i] = clothSmr.transform.InverseTransformPoint(inflatedVertWs + (inflatedVertWs - center).normalized * (dist * clothOffsetLerp));
                // if (PregnancyPlusPlugin.DebugLog.Value) PregnancyPlusPlugin.Logger.LogInfo($" MeshCollider dist {dist} lerp {clothOffsetLerp} max {bellyInfo.OriginalSphereRadius}");
                colliderCount++;
            }

            if (PregnancyPlusPlugin.DebugLog.Value) PregnancyPlusPlugin.Logger.LogInfo($" MeshCollider collisions {colliderCount}");
        }
    }
}


