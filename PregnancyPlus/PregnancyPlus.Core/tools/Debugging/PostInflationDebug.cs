using System.Collections.Generic;
using UnityEngine;
#if HS2 || AI
    using AIChara;
#endif

namespace KK_PregnancyPlus
{
    //Methds used to debug verts and lines after Preg+ has computed all the mesh data
    public static class PostInflationDebug
    {
        /// <summary>
        /// Shoe debug spheres on screen when enabled in plugin config
        /// </summary>
        internal static void Start(ChaControl chaControl, Dictionary<string, MeshData> md, NativeDetourMesh nativeDetour)
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
            if (!PregnancyPlusPlugin.AnyDebugPrimitivesToggled())
                return;
            
            //Gather all SMR's
            var bodyRenderers = PregnancyPlusHelper.GetMeshRenderers(chaControl.objBody, findAll: true);                           
            var clothRenderers = PregnancyPlusHelper.GetMeshRenderers(chaControl.objClothes);         
            var accessoryRenderers = PregnancyPlusHelper.GetMeshRenderers(chaControl.objAccessory);     

            bodyRenderers.ForEach((SkinnedMeshRenderer smr) => DebugMesh(smr, md, nativeDetour));
            clothRenderers.ForEach((SkinnedMeshRenderer smr) => DebugMesh(smr, md, nativeDetour, isClothingMesh: true));
            accessoryRenderers.ForEach((SkinnedMeshRenderer smr) => DebugMesh(smr, md, nativeDetour, isClothingMesh: true));        
        }


        /// <summary>
        /// Depending on plugin config state, shows calculated verts on screen (Do not run inside a Task, lol)
        /// </summary>
        internal static void DebugMesh(SkinnedMeshRenderer smr, Dictionary<string, MeshData> md, NativeDetourMesh nativeDetour, bool isClothingMesh = false)
        {
            //If the mesh has been touched it has a key
            var hasKey = md.TryGetValue(PregnancyPlusHelper.GetMeshKey(smr), out var _md);
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
                //Undo any bindpose rotation in the deltas to make them line up visually for debugging
                var bindPoseRotation = BindPose.GetAverageRotation(smr);
                //When SMR has local rotation undo it in the deltas
                var rotationUndo = Matrix4x4.TRS(Vector3.zero, smr.transform.localRotation, Vector3.one).inverse * Matrix4x4.TRS(Vector3.zero, bindPoseRotation, Vector3.one);                           
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
                        DebugTools.DrawSphere((isClothingMesh ? 0.01f : 0.005f), _md.originalVertices[i], color: Color.white);   
                    else
                        DebugTools.DrawSphere((isClothingMesh ? 0.01f : 0.005f), _md.originalVertices[i], color: Color.grey);   
                } 
            }
        }


        /// <summary>
        /// Show the computed bindpose locations with debug lines
        /// </summary> 
        /// <param name="parent">optional: Attach lines to this parent transform</param>   
        public static void ShowBindPose(ChaControl chaControl, SkinnedMeshRenderer smr, BindPoseList bindPoseList, Transform parent = null)
        {
            if (!PregnancyPlusPlugin.DebugLog.Value && !PregnancyPlusPlugin.DebugCalcs.Value) return;

            #if KKS 
                var lineLen = 0.03f;
            #elif HS2 || AI
                var lineLen = 0.3f;
            #endif            
            
            //Get a default offset to use when a mesh has extra bones not in the skeleton
            var firstNon0Offset = MeshSkinning.GetFirstBindPoseOffset(chaControl, bindPoseList, smr, smr.sharedMesh.bindposes, smr.bones);

            for (int i = 0; i < smr.bones.Length; i++)
            {            
                //Sometimes smr has more bones than bindPoses, so skip these extra bones
                if (i > smr.sharedMesh.bindposes.Length -1) continue;
                
                var bindPoseOffset = MeshSkinning.GetBindPoseOffset(chaControl, bindPoseList, smr, smr.sharedMesh.bindposes[i], smr.bones[i]) ?? firstNon0Offset;
                //Get a bone's bindPose position/rotation
                MeshSkinning.GetBindPoseBoneTransform(smr, smr.sharedMesh.bindposes[i], bindPoseOffset, out var position, out var rotation, bindPoseList, smr.bones[i]);

                DebugTools.DrawAxis(position, lineLen, parent: parent);
            }
        }


        /// <summary>
        /// Show the raw bindpose locations of an SMR with debug lines
        /// </summary> 
        /// <param name="parent">optional: Attach lines to this parent transform</param>   
        public static void ShowRawBindPose(SkinnedMeshRenderer smr, Transform parent = null)
        {
            if (!PregnancyPlusPlugin.DebugLog.Value && !PregnancyPlusPlugin.DebugCalcs.Value) return;

            #if KKS 
                var lineLen = 0.03f;
            #elif HS2 || AI
                var lineLen = 0.3f;
            #endif            

            for (int i = 0; i < smr.bones.Length; i++)
            {            
                //Sometimes smr has more bones than bindPoses, so skip these extra bones
                if (i > smr.sharedMesh.bindposes.Length -1) continue;
                
                //Get a bone's bindPose position/rotation
                var position = Matrix.GetPosition(smr.transform.localToWorldMatrix * smr.sharedMesh.bindposes[i].inverse);

                DebugTools.DrawAxis(position, lineLen, parent: parent, startColor: Color.grey);
            }
        }
    }
}