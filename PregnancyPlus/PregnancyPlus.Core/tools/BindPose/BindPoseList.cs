using System.Collections.Generic;
using UnityEngine;

#if HS2 || AI
    using AIChara;
#endif

namespace KK_PregnancyPlus
{
    //Computes and contains the BindPose list of bone positions in T-pose
    public class BindPoseList
    {  
        //Contains the list of body bones' bindPose positions (T-pose)
        public Dictionary<string, Vector3> bindPoses = new Dictionary<string, Vector3>();
        public const string UncensorCOMName = "com.deathweasel.bepinex.uncensorselector";
        //Used as key for ErrorCode
        public string charaFileName;


        //constructor
        public BindPoseList(string _charaFileName)
        {
            charaFileName = _charaFileName;
        }


        /// <summary>
        /// Get a bone's bind pose position from cached list
        /// </summary> 
        public Vector3 Get(string boneName)
        {
            if (bindPoses.Count <= 0) {
                if (PregnancyPlusPlugin.DebugCalcs.Value) PregnancyPlusPlugin.Logger.LogWarning($" The bindPoses list has not been set yet");
                return Vector3.zero;
            }
            
            if (!bindPoses.ContainsKey(boneName)) 
                return Vector3.zero;

            return bindPoses[boneName];
        }


        /// <summary>
        /// Get the bindpose positions from a list of bones in the body SMR.
        /// </summary> 
        public void ComputeBindPose(ChaControl chaCtrl, SkinnedMeshRenderer smr, bool force = false)
        {
            //When we alrready have the bind poses, we don't need to recompute them
            if (bindPoses.Count > 0 && !force) return;
            //Clear bindposelist
            if (force) bindPoses = new Dictionary<string, Vector3>();
            Matrix4x4 optionalOffsetMatrix = Matrix4x4.identity;

            //Fix bad body bindpose positions in KK
            #if KK                
                var meshOffsetType = MeshOffSet.GetMeshOffsetType(smr);

                //If not default kk body mesh fix with offset
                if (meshOffsetType != MeshOffSetType.DefaultMesh) 
                {
                    var offset = MeshOffSet.GetBindposeOffsetFix(chaCtrl, smr);
                    optionalOffsetMatrix = Matrix4x4.TRS(offset, Quaternion.identity, Vector3.one);                
                }
            #endif

            bindPoses = SetBindPosePositions(smr, chaCtrl, optionalOffsetMatrix);            
        }


        /// <summary>
        /// If a valid bind pose mesh is found get its bone positions, and add them to the bindPoses Dictionary
        /// </summary> 
        internal Dictionary<string, Vector3> SetBindPosePositions(SkinnedMeshRenderer smr, ChaControl chaCtrl, Matrix4x4 bindPoseOffset = new Matrix4x4())
        {
            var _bindPoses = new Dictionary<string, Vector3>();

            //Make sure bones match bindposes
            if (smr.bones.Length <= 0 || smr.sharedMesh.bindposes.Length <= 0 || smr.bones.Length < smr.sharedMesh.bindposes.Length)
            {                        
                PregnancyPlusPlugin.errorCodeCtrl.LogErrorCode(charaFileName, ErrorCode.PregPlus_BoneBindPoseMismatch, 
                    $"SetBindPosePositions > Bindposes and Bones must have a length, and bones must be >= bindposes.  smr {smr.name} bindposes:{smr.sharedMesh.bindposes.Length} bones:{smr.bones.Length}");         
                return new Dictionary<string, Vector3>();
            }

            for (int i = 0; i < smr.bones.Length; i++)
            {
                //Sometimes body has more bones than bindPoses, so skip these extra bones
                if (i > smr.sharedMesh.bindposes.Length -1) continue;

                MeshSkinning.GetBindPoseBoneTransform(smr, smr.sharedMesh.bindposes[i], bindPoseOffset, out var position, out var rotation);

                //subtract chaCtrl position to ignore characters worldspace position/movement
                _bindPoses.Add(smr.bones[i].name, position);
            }

            return _bindPoses;
        }


    }

}