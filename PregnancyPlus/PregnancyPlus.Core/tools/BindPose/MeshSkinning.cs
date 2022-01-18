using System.Collections.Generic;
using UnityEngine;

#if HS2 || AI
    using AIChara;
#endif

namespace KK_PregnancyPlus
{
    //Contains the BindPose skinning logic, used to align all meshes to a T-pose
    public static class MeshSkinning
    {  

        /// <summary>
        /// Get the bone matricies of the characters T-pose position used in skinning the mesh
        ///     This extracts the T-pose bone positions from the BindPoseList, so it doesn't matter what animation the character is currently in
        /// </summary>     
        public static Matrix4x4[] GetBoneMatrices(ChaControl chaControl, SkinnedMeshRenderer smr, BindPoseList bindPoseList) 
        {
            Transform[] skinnedBones = smr.bones;
            Matrix4x4[] boneMatrices = new Matrix4x4[smr.bones.Length];
            Matrix4x4[] bindposes = smr.sharedMesh.bindposes;                                               

            //For each bone, compute its bindpose position, and get the bone matrix
            for (int j = 0; j < boneMatrices.Length; j++)
            {
                if (skinnedBones[j] != null && bindposes[j] != null)
                {                                                            
                    //Create dummy transform to store this bone's BindPose position
                    var synthTransform = new GameObject().transform;
                    var bindPoseOffset = GetBindPoseOffset(chaControl, bindPoseList, smr, smr.sharedMesh.bindposes[j], skinnedBones[j]);

                    //Get the bind pose position (and correct any bad offsets/rotations)
                    GetBindPoseBoneTransform(smr, smr.sharedMesh.bindposes[j], bindPoseOffset, out var position, out var rotation);
                    synthTransform.position = position;
                    synthTransform.rotation = rotation;

                    //Store the bone matrix of the T-posed bone
                    boneMatrices[j] = synthTransform.localToWorldMatrix * bindposes[j];

                    GameObject.Destroy(synthTransform.gameObject);
                }
                else
                {
                    boneMatrices[j] = Matrix4x4.identity;
                }
            }

            return boneMatrices;
        }


        /// <summary>
        /// Compute the bone BindPoses position and rotation
        /// </summary>  
        public static void GetBindPoseBoneTransform(SkinnedMeshRenderer smr, Matrix4x4 bindPose, Matrix4x4 bindPoseOffset, out Vector3 position, out Quaternion rotation)
        {
            var invBindPoseMatrix = GetInverseBindPoseBoneMatrix(smr, bindPose, bindPoseOffset);

            //The inverse bindpose of 0,0,0 gives us the T-pose position of the bone (Except Blender's FBX imported meshes that we have to correct first with an offset)
            position = invBindPoseMatrix.MultiplyPoint(Vector3.zero); 
            rotation = Matrix.GetRotation(invBindPoseMatrix);//This should always be Quaternion.identity
        }


        /// <summary>
        /// Compute the matrix that gives you a localspace bind pose bone position given a worldspace Vector3.zero
        /// </summary>  
        /// <param name="bindPoseOffset">Any offset required to line up an incorrect bindPose with the other correct ones (derived from bindPoseList.bindPoses)</param> 
        public static Matrix4x4 GetInverseBindPoseBoneMatrix(SkinnedMeshRenderer smr, Matrix4x4 bindPose, Matrix4x4 bindPoseOffset)
        {
            var smrMatrix = Matrix4x4.identity;

            //When an smr has rotated local position (incorrectly imported?) correct it with rotation matrix (Squeeze socks as example)
            if (smr.transform.localRotation != Quaternion.identity)
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
            //Remove position from the smr transform leaving only rotation
            return Matrix4x4.TRS(Matrix.GetPosition(smrMatrix), Quaternion.identity, Vector3.one).inverse * smrMatrix;
        }


        /// <summary>
        /// Get the offset that a smr bindpose bone needs in order to line up with the body's bindpose
        /// </summary>  
        public static Matrix4x4 GetBindPoseOffset(ChaControl chaControl, BindPoseList bindPoseList, SkinnedMeshRenderer smr, Matrix4x4 bindPose, Transform bone)
        {
            if (smr == null) return Matrix4x4.identity;

            //Some other plugins addd/remove bones at runtime
            if (bone == null) return Matrix4x4.identity;

            //If the bone name is not in the list, try the next bone
            if (!bindPoseList.bindPoses.ContainsKey(bone.name)) return Matrix4x4.identity;

            //Compare the real body bindpose position with this smr.bone's bindpose position
            var realBonePosePosition = bindPoseList.Get(bone.name);
            GetBindPoseBoneTransform(smr, bindPose, Matrix4x4.identity, out var questionablePosition, out var rotation);
            var offset = realBonePosePosition - questionablePosition;
            
            //Return the offset found, so all bones will have the same position
            return Matrix4x4.TRS(offset, Quaternion.identity, Vector3.one);
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

            #if KK 
                var lineLen = 0.03f;
            #elif AI || HS2
                var lineLen = 0.3f;
            #endif            
            
            for (int i = 0; i < smr.bones.Length; i++)
            {            
                //Sometimes body has more bones than bindPoses, so skip these extra bones
                if (i > smr.sharedMesh.bindposes.Length -1) continue;
                
                var bindPoseOffset = GetBindPoseOffset(chaControl, bindPoseList, smr, smr.sharedMesh.bindposes[i], smr.bones[i]);
                //Get a bone's bindPose position/rotation
                GetBindPoseBoneTransform(smr, smr.sharedMesh.bindposes[i], bindPoseOffset, out var position, out var rotation);

                if (parent == null) 
                {
                    DebugTools.DrawLine(position + Vector3.right * lineLen, position);
                    DebugTools.DrawLine(position + Vector3.up * lineLen, position);
                    DebugTools.DrawLine(position + Vector3.forward * lineLen, position);
                } 
                else
                {
                    DebugTools.DrawLineAndAttach(parent, position + Vector3.right * lineLen, position, removeExisting: false);
                    DebugTools.DrawLineAndAttach(parent, position + Vector3.up * lineLen, position, removeExisting: false);
                    DebugTools.DrawLineAndAttach(parent, position + Vector3.forward * lineLen, position, removeExisting: false);
                }
            }
        }

    }
}