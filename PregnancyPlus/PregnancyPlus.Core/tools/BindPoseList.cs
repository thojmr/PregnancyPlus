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
        public Dictionary<string, Vector3> bindPoses = new Dictionary<string, Vector3>();
        public const string UncensorCOMName = "com.deathweasel.bepinex.uncensorselector";


        /// <summary>
        /// Get a bone's bind pose position
        /// </summary> 
        public Vector3 Get(string boneName)
        {
            if (bindPoses.Count <= 0) {
                if (PregnancyPlusPlugin.DebugCalcs.Value) PregnancyPlusPlugin.Logger.LogWarning($" The bindPoses list has not been set yet");
                return Vector3.zero;
            }
            if (!bindPoses.ContainsKey(boneName))
            {
                if (PregnancyPlusPlugin.DebugCalcs.Value) PregnancyPlusPlugin.Logger.LogWarning($" The bindPose bone could not be found: {boneName}");
                return Vector3.zero;
            }

            return bindPoses[boneName];
        }


        /// <summary>
        /// Get the bindpose from a list of bones.
        ///     We need to make sure we use a valid SMR as the source, since some are not in the correct bindpose position
        ///     Check each body smr for one with valid bounds and use that as the real bindpose
        /// </summary> 
        public void ComputeValidBindPose(ChaControl chaCtrl, SkinnedMeshRenderer smr, bool force = false)
        {
            //When we alrready have the bind poses, we don't need to recompute them
            if (bindPoses.Count > 0 && !force) return;
            //Clear bindposelist
            if (force) bindPoses = new Dictionary<string, Vector3>();
            Matrix4x4 optionalOffset = Matrix4x4.identity;

            //Fix bad body bindposes in KK
            #if KK
                var isValidBindPose = IsValidBindPoseSmr(chaCtrl, smr);
                //If default KK body, fix with an offset
                if (!isValidBindPose) 
                {
                    var offset = chaCtrl.transform.InverseTransformPoint(smr.transform.position);
                    if (PregnancyPlusPlugin.DebugCalcs.Value) PregnancyPlusPlugin.Logger.LogWarning($" {smr.name} offset {offset}");
                    optionalOffset = Matrix4x4.TRS(offset, Quaternion.identity, Vector3.one);
                }
            #endif

            bindPoses = SetBindPosePositions(smr, chaCtrl, optionalOffset);            

            if (bindPoses.Count <= 0)
            {
                if (PregnancyPlusPlugin.DebugCalcs.Value) PregnancyPlusPlugin.Logger.LogWarning($" Failed to find valid bind poses for this character"); 
            }
        }


        /// <summary>
        /// If a valid bind pose mesh is found get its bone positions
        /// </summary> 
        internal Dictionary<string, Vector3> SetBindPosePositions(SkinnedMeshRenderer smr, ChaControl chaCtrl, Matrix4x4 bindPoseOffset = new Matrix4x4())
        {
            var bindPoses = new Dictionary<string, Vector3>();

            for (int i = 0; i < smr.bones.Length; i++)
            {
                MeshSkinning.GetBindPoseBoneTransform(smr, smr.sharedMesh.bindposes[i], smr.bones[i], bindPoseOffset, out var position, out var rotation);

                //subtract chaCtrl position to ignore characters worldspace position/movement
                bindPoses.Add(smr.bones[i].name, position);
            }

            return bindPoses;
        }


        /// <summary>
        /// Take the bindPose list for a mesh, and compare it against the existing master list, merge in new bindPoses bones (Not used any more)
        /// </summary> 
        public void MergeWithExisting(Dictionary<string, Vector3> newbindPoses)
        {
            //Check to see if this bindPose bone exists
            foreach (var key in newbindPoses.Keys)
            {
                //If not, add it
                if (!bindPoses.ContainsKey(key))
                {
                    bindPoses.Add(key, newbindPoses[key]);
                    continue;
                }

                //If it does, make sure the position matches
                if (Vector3.Distance(bindPoses[key], newbindPoses[key]) > 0.001f)
                {
                    if (PregnancyPlusPlugin.DebugCalcs.Value) PregnancyPlusPlugin.Logger.LogWarning($" MisMatch bindpose positions expected {bindPoses[key]}  got  {newbindPoses[key]} from `{key}`"); 
                }

            }
        }


        /// <summary>
        /// Whether an SMR has valid (correctly positioned) bind poses.  Based on the local bounds of the verts (Usullly only happens in KK/KKS)
        /// </summary> 
        public bool IsValidBindPoseSmr(ChaControl chaCtrl, SkinnedMeshRenderer smr)
        {
            var isDefaultBody = !PregnancyPlusPlugin.Hooks_Uncensor.IsUncensorBody(chaCtrl, UncensorCOMName);
            //When the mesh shares similar local vertex positions as the default body use Bounds to determine if the mesh is not aligned
            //  Bounds are the only way I could come up with to detect an offset mesh...
            var isLikeDefaultBody = smr.localBounds.center.y < 0 && smr.sharedMesh.bounds.center.y < 0;

            return !isDefaultBody && !isLikeDefaultBody;
        }
    }

}