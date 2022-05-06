using System.Collections.Generic;
using System.Linq;
using UnityEngine;

#if HS2 || AI
    using AIChara;
#endif

namespace KK_PregnancyPlus
{
    //Well this got complicated fast...
    /// <summary>
    /// Contains the BindPose skinning logic, used to align all meshes to a T-pose
    ///     Some additonal Notes: Offset Bindpose positions mostly happen in KK/KKS, but could possibly occur elsewhere
    ///                           SMR local rotations can happen on any mesh,  they seem to be incorrcetly imported meshes
    ///     The Offset issue is something that Unity just magically knows how to undo, resulting in a correctly alighed and skinned mesh.  
    ///         I on the other hand, struggle to find the missing transform that automatically corrects this problem.  And that's why most of this code exists.
    /// </summary>    
    public static class MeshSkinning
    {  

        /// <summary>
        /// Get the bone matricies of the characters T-pose position used in skinning the mesh
        ///     This extracts the T-pose bone positions from the BindPoseList, so it doesn't matter what animation the character is currently in we always get an aligned mesh
        /// </summary>     
        public static Matrix4x4[] GetBoneMatrices(ChaControl chaControl, SkinnedMeshRenderer smr, BindPoseList bindPoseList) 
        {
            Transform[] skinnedBones = smr.bones;
            Matrix4x4[] boneMatrices = new Matrix4x4[smr.bones.Length];
            Matrix4x4[] bindposes = smr.sharedMesh.bindposes;               

            var hasLocalRotation = MeshHasLocalRotation(smr);                                
            // if (PregnancyPlusPlugin.DebugLog.Value && hasLocalRotation) PregnancyPlusPlugin.Logger.LogWarning($" hasLocalRotation {smr.name} ");

            //Get a default offset to use when a mesh has extra bones not in the skeleton (and is a bad bindpose mesh to begin with)
            var firstNon0Offset = GetFirstBindPoseOffset(chaControl, bindPoseList, smr, smr.sharedMesh.bindposes, smr.bones);

            //For each bone, compute its bindpose position, and get the bone matrix
            for (int j = 0; j < boneMatrices.Length; j++)
            {
                //Prevent out of index errors when clothing has extra bones
                if (j > skinnedBones.Length -1 || j > bindposes.Length -1) {
                    boneMatrices[j] = firstNon0Offset;
                    // if (PregnancyPlusPlugin.DebugLog.Value) PregnancyPlusPlugin.Logger.LogWarning($" boneMatrix {j} does not match bone {skinnedBones.Length -1}, or bindpose {bindposes.Length -1}");
                    continue;
                }

                if (skinnedBones[j] != null && bindposes[j] != null)
                {                                                            
                    //Create dummy transform to store this bone's BindPose position
                    var synthTransform = new GameObject().transform;
                    //Any single bone can be incorrectly offset from the rest of the bones (Who knows why...). So compute each individually.
                    //  If the bone is not found, use the firstNon0Offset instead
                    var bindPoseOffset = GetBindPoseOffset(chaControl, bindPoseList, smr, smr.sharedMesh.bindposes[j], skinnedBones[j]) ?? firstNon0Offset;

                    //Get the bind pose position (and correct any bad bindpose offsets/rotations)
                    GetBindPoseBoneTransform(smr, smr.sharedMesh.bindposes[j], bindPoseOffset, out var position, out var rotation, bindPoseList, skinnedBones[j]);
                    synthTransform.position = position;
                    synthTransform.rotation = rotation;

                    //Store the bone matrix of the T-posed bone for skinning later
                    boneMatrices[j] = synthTransform.localToWorldMatrix * bindposes[j];

                    GameObject.Destroy(synthTransform.gameObject);
                }
                else
                {
                    boneMatrices[j] = firstNon0Offset;
                }
            }

            return boneMatrices;
        }


        /// <summary>
        /// Compute a smr bone BindPose position and rotation
        /// </summary>  
        /// <param name="bindPoseList">When we have an existing bindPoseList used the cached bone position/rotation</param> 
        public static void GetBindPoseBoneTransform(SkinnedMeshRenderer smr, Matrix4x4 bindPose, Matrix4x4 bindPoseOffset, 
                                                    out Vector3 position, out Quaternion rotation, BindPoseList bindPoseList = null, Transform bone = null)
        {
            //If the bindPose list is already populated, try and search it for a bindpose bone
            //  If no match found, then it's probably an extra bone added by the SMR, so we will need to compute its' offset manually below
            if (bindPoseList != null && bone != null && bindPoseList.bindPoses.ContainsKey(bone.name))
            {
                var tf = bindPoseList.Get(bone.name);
                position = tf.position;
                rotation = tf.rotation;
                return;
            }            

            //Compute the bindpose position from the existing smr.bindPose (including any offset)
            var invBindPoseMatrix = GetInverseBindPoseBoneMatrix(smr, bindPose, bindPoseOffset);

            //The inverse bindpose of Vector3.zero gives us the T-pose position of the bone (Except Blender's FBX imported meshes that we have corrected first with an offset)
            position = invBindPoseMatrix.MultiplyPoint(Vector3.zero); 

            //When SMR has local rotation we need to inverse it to get the true bindpose bone rotation
            //  It may just be Quaternion.inverse in the end, but better safe than sorry
            var rotationInverse = Matrix4x4.TRS(Vector3.zero, smr.transform.localRotation, Vector3.one).inverse;
            rotation = MeshHasLocalRotation(smr) ? Matrix.GetRotation(rotationInverse * invBindPoseMatrix) : Matrix.GetRotation(invBindPoseMatrix);
        }


        /// <summary>
        /// Compute the matrix that gives you a localspace bind pose bone position given a worldspace Vector3.zero
        /// </summary>  
        /// <param name="bindPoseOffset">Any offset required to line up an incorrect bindPose with the other correct ones (derived from bindPoseList.bindPoses)</param> 
        public static Matrix4x4 GetInverseBindPoseBoneMatrix(SkinnedMeshRenderer smr, Matrix4x4 bindPose, Matrix4x4 bindPoseOffset)
        {
            var smrMatrix = Matrix4x4.identity;

            //When an smr has rotated local position (incorrectly imported?) correct it with rotation matrix (Squeeze socks as example)
            if (MeshHasLocalRotation(smr))
            {
                smrMatrix = OffsetSmrRotation(smr.transform.localToWorldMatrix);                
            }

            //Apply any offset to the bindpose when needed (for FBX -> unity KK meshes, and maybe others)
            if (bindPoseOffset != null && bindPoseOffset != Matrix4x4.identity)
            {                
                smrMatrix = bindPoseOffset * smrMatrix;
            }

            return smrMatrix * bindPose.inverse;
        }


        /// <summary>
        /// When an SMR transform has localRotation, we want to remove it so the skinned mesh aligns correctly (Like SqueezeSocks in HS2)
        /// </summary>  
        public static Matrix4x4 OffsetSmrRotation(Matrix4x4 smrMatrix)
        {
            //Remove position from the TRS matrix, so when we multuply the inverse by the smr it removes the rotation
            return Matrix4x4.TRS(Matrix.GetPosition(smrMatrix), Quaternion.identity, Vector3.one).inverse * smrMatrix;
        }


        /// <summary>
        /// When an SMR bindpose has any scale, return it.  We need to apply it to vert deltas before making blendshape, 
        ///     because the unskinned mesh has a unique scale that needs to be matched
        /// </summary>  
        public static Matrix4x4 GetBindPoseScale(SkinnedMeshRenderer smr)
        {            
            var scale = Matrix4x4.identity;

            //For a bindpose check for scale (just grab the first if any exists)
            var bindposes = smr.sharedMesh.bindposes;
            for (int i = 0; i < bindposes.Length; i++)
            {        
                //Note: This assumes the scale is the same for all bindposes. Would not be suprised if that's not the case

                var currentScale = Matrix.GetScaleMatrix(bindposes[i]);
                //If at least 2 bones had the same scale, use that scale
                if (currentScale != scale)   
                    scale = currentScale;

                //Otherwise there is (probably) no scale
                break;
            }            

            return scale;
        }

        /// <summary>
        /// When an SMR bindpose has a uniform rotation return it so we can correct it later
        /// </summary>  
        public static Quaternion GetBindPoseRotation(SkinnedMeshRenderer smr)
        {            
            //For a bindpose check for any non 0 rotation repeated more than a few times
            var bindposes = smr.sharedMesh.bindposes;
            var totalX = 0f;
            var totalY = 0f;
            var totalZ = 0f;
            var totalW = 0f;

            //Add up all the rotations for each bindpose
            for (int i = 0; i < bindposes.Length; i++)
            {      
                //Round them to the nearest 90 degree axis since most offset rotations are at 90 degree intervals
                var currentRotation = Rotation.AxisRound(Matrix.GetRotation(smr.transform.localToWorldMatrix * bindposes[i].inverse));                
                totalX+=currentRotation.x;
                totalY+=currentRotation.y;
                totalZ+=currentRotation.z;
                totalW+=currentRotation.w;                
            }                        

            //Compute the average rotation
            var averageRotation = new Quaternion(x: totalX/bindposes.Length, y: totalY/bindposes.Length, z: totalZ/bindposes.Length, w: totalW/bindposes.Length);

            //Round the final rotation to the nearest 90 degree axis
            var roundedRotation = Rotation.AxisRound(averageRotation);

            if (PregnancyPlusPlugin.DebugLog.Value && roundedRotation != Quaternion.identity) 
                PregnancyPlusPlugin.Logger.LogWarning($" GetBindPoseRotation {smr.name} has bindpose rotation {roundedRotation}");
            
            return roundedRotation;            
        }


        /// <summary>
        /// Get the offset that a smr bindpose bone needs in order to line up with the body's bindpose
        ///     Null when none found
        /// </summary>  
        public static Matrix4x4? GetBindPoseOffset(ChaControl chaControl, BindPoseList bindPoseList, SkinnedMeshRenderer smr, Matrix4x4 bindPose, Transform bone)
        {
            //Some other plugins add/remove bones at runtime
            if (smr == null || bone == null) return null;

            //If the bone name is not in the list, skip, we'll use the default offset instead
            if (!bindPoseList.bindPoses.ContainsKey(bone.name)) return null;

            //Compare the `real` body bindpose position with this smr.bone's bindpose position
            var realTransform = bindPoseList.Get(bone.name);
            GetBindPoseBoneTransform(smr, bindPose, Matrix4x4.identity, out var questionablePosition, out var rotation);
            var offset = realTransform.position - questionablePosition;
            
            //Return the offset found, so all bindpose bones will have the same alignment
            return Matrix4x4.TRS(offset, Quaternion.identity, Vector3.one);
        }


        /// <summary>
        /// Get the first non 0 offset from a SMR bindpose.  
        ///     Needed when a mesh has extra bones that dont match any existing bones
        /// </summary>  
        public static Matrix4x4 GetFirstBindPoseOffset(ChaControl chaControl, BindPoseList bindPoseList, SkinnedMeshRenderer smr, Matrix4x4[] bindPoses, Transform[] bones)
        {
            //We don't need to check the whole skeleton.  If we go through a few bones and there is no offset, then its probably safe to say there is none
            var limit = 10;

            //For each smr bone in our bindPoseList, compute it's offset            
            for (int i = 0; i < bones.Length; i++)
            {
                //Check for more bones than bindposes
                if (i > bindPoses.Length -1) continue;

                var offset = GetBindPoseOffset(chaControl, bindPoseList, smr, bindPoses[i], bones[i]) ?? Matrix4x4.identity;
                if (i > limit) break; 

                //Make sure the offset is sufficiently large enough to not be a floating point error
                if (offset == Matrix4x4.identity || Vector3.Distance(Vector3.zero, Matrix.GetPosition(offset)) <= 0.0001f) continue;

                return offset;                                           
            }

            //When no offset found
            return Matrix4x4.identity;
        }


        public static bool MeshHasLocalRotation(SkinnedMeshRenderer smr)
        {
            return smr.transform.localRotation != Quaternion.identity;
        }


#region Skinning

        /// <summary>
        /// Convert an unskinned mesh vert into the default T-pose mesh vert using the bindpose bone positions
        /// </summary>
        public static Vector3 UnskinnedToSkinnedVertex(Vector3 unskinnedVert, Matrix4x4 smrMatrix, Matrix4x4[] boneMatrices, BoneWeight boneWeight)
        {
            if (boneWeight == null) return Vector3.zero;
            if (boneMatrices == null) return Vector3.zero;
            
            Matrix4x4 skinningMatrix = GetSkinningMatrix(boneMatrices, boneWeight);
            return skinningMatrix.MultiplyPoint3x4(unskinnedVert);
        }


        /// <summary>
        /// Get the skin matrix used to convert unskinned verts into skinned verts
        /// </summary>
        public static Matrix4x4 GetSkinningMatrix(Matrix4x4[] boneMatrices, BoneWeight weight)
        {
            Matrix4x4 bm0;
            Matrix4x4 bm1;
            Matrix4x4 bm2;
            Matrix4x4 bm3;
            Matrix4x4 reverseSkinningMatrix = new Matrix4x4();
            //If you wanted to `reverse skin` from BakedMesh -> unskinned, you would add '.inverse' to these matricies, but we are `forward skinning` unskinned -> skinned so we don't need it here
            bm0 = boneMatrices[weight.boneIndex0];
            bm1 = boneMatrices[weight.boneIndex1];
            bm2 = boneMatrices[weight.boneIndex2];
            bm3 = boneMatrices[weight.boneIndex3];

            reverseSkinningMatrix.m00 = bm0.m00 * weight.weight0 + bm1.m00 * weight.weight1 + bm2.m00 * weight.weight2 + bm3.m00 * weight.weight3;
            reverseSkinningMatrix.m01 = bm0.m01 * weight.weight0 + bm1.m01 * weight.weight1 + bm2.m01 * weight.weight2 + bm3.m01 * weight.weight3;
            reverseSkinningMatrix.m02 = bm0.m02 * weight.weight0 + bm1.m02 * weight.weight1 + bm2.m02 * weight.weight2 + bm3.m02 * weight.weight3;
            reverseSkinningMatrix.m03 = bm0.m03 * weight.weight0 + bm1.m03 * weight.weight1 + bm2.m03 * weight.weight2 + bm3.m03 * weight.weight3;

            reverseSkinningMatrix.m10 = bm0.m10 * weight.weight0 + bm1.m10 * weight.weight1 + bm2.m10 * weight.weight2 + bm3.m10 * weight.weight3;
            reverseSkinningMatrix.m11 = bm0.m11 * weight.weight0 + bm1.m11 * weight.weight1 + bm2.m11 * weight.weight2 + bm3.m11 * weight.weight3;
            reverseSkinningMatrix.m12 = bm0.m12 * weight.weight0 + bm1.m12 * weight.weight1 + bm2.m12 * weight.weight2 + bm3.m12 * weight.weight3;
            reverseSkinningMatrix.m13 = bm0.m13 * weight.weight0 + bm1.m13 * weight.weight1 + bm2.m13 * weight.weight2 + bm3.m13 * weight.weight3;

            reverseSkinningMatrix.m20 = bm0.m20 * weight.weight0 + bm1.m20 * weight.weight1 + bm2.m20 * weight.weight2 + bm3.m20 * weight.weight3;
            reverseSkinningMatrix.m21 = bm0.m21 * weight.weight0 + bm1.m21 * weight.weight1 + bm2.m21 * weight.weight2 + bm3.m21 * weight.weight3;
            reverseSkinningMatrix.m22 = bm0.m22 * weight.weight0 + bm1.m22 * weight.weight1 + bm2.m22 * weight.weight2 + bm3.m22 * weight.weight3;
            reverseSkinningMatrix.m23 = bm0.m23 * weight.weight0 + bm1.m23 * weight.weight1 + bm2.m23 * weight.weight2 + bm3.m23 * weight.weight3;

            return reverseSkinningMatrix;
        }

#endregion Skinning
        
        
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
            var firstNon0Offset = GetFirstBindPoseOffset(chaControl, bindPoseList, smr, smr.sharedMesh.bindposes, smr.bones);

            for (int i = 0; i < smr.bones.Length; i++)
            {            
                //Sometimes smr has more bones than bindPoses, so skip these extra bones
                if (i > smr.sharedMesh.bindposes.Length -1) continue;
                
                var bindPoseOffset = GetBindPoseOffset(chaControl, bindPoseList, smr, smr.sharedMesh.bindposes[i], smr.bones[i]) ?? firstNon0Offset;
                //Get a bone's bindPose position/rotation
                GetBindPoseBoneTransform(smr, smr.sharedMesh.bindposes[i], bindPoseOffset, out var position, out var rotation, bindPoseList, smr.bones[i]);

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