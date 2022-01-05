using System.Collections.Generic;
using UnityEngine;

namespace KK_PregnancyPlus
{
    //Contains the BindPose skinning logic, used to align all meshes to a T-pose
    public static class MeshSkinning
    {  

        /// <summary>
        /// Get the bone matricies of the characters T-pose position
        ///     This extracts the T-pose bone positions from the BindPose, so it doesn't matter what animation the character is currently in
        /// </summary>     
        public static Matrix4x4[] GetBoneMatrices(SkinnedMeshRenderer smr) 
        {
            Transform[] skinnedBones = smr.bones;
            Matrix4x4[] boneMatrices = new Matrix4x4[smr.bones.Length];
            Matrix4x4[] bindposes = smr.sharedMesh.bindposes;                         
            Matrix4x4 smrMatrix = smr.transform.localToWorldMatrix;

            for (int j = 0; j < boneMatrices.Length; j++)
            {
                if (skinnedBones[j] != null && bindposes[j] != null)
                {                                                            
                    //Create dummy transform to store a BindPose position
                    var synthTransform = new GameObject().transform;

                    //Compute bind pose for a bone
                    GetBoneBindPose(smrMatrix, smr.sharedMesh.bindposes[j], smr.bones[j], out var position, out var rotation);
                    synthTransform.position = position;
                    synthTransform.rotation = rotation;

                    //Compute bone matrix used to skin the T-pose bone
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
        /// Get a single Bind pose from a bone and an smr (apply offset or rotation if needed)
        /// </summary>  
        /// <param name="position">The T-pose position of this bone, localspace</param>   
        /// <param name="rotation">The T-pose rotation of this bone, localspace</param>   
        public static void GetBoneBindPose(Matrix4x4 smrMatrix, Matrix4x4 bindPose, Transform bone, out Vector3 position, out Quaternion rotation) 
        {
            //Compute the initial Bindpose for this bone
            GetBindPoseBoneTransform(smrMatrix, bindPose, bone, out position, out rotation);

            //make bindpose points localspace oriented (The devault value of rotaion here carries the characters origin rotation, and we dont want that)
            // rotation = Quaternion.LookRotation(Vector3.forward, Vector3.up);
        }


        /// <summary>
        /// Convert an unskinned mesh vert into the default T-pose mesh vert using the bindpose bone positions
        /// </summary>
        public static Vector3 UnskinnedToSKinnedVertex(Vector3 unskinnedVert, Matrix4x4 RendererLocalToWorldMatrix, Matrix4x4[] boneMatrices, BoneWeight boneWeight)
        {
            if (boneWeight == null) return Vector3.zero;
            if (boneMatrices == null) return Vector3.zero;
            
            Matrix4x4 skinningMatrix = GetSkinningMatrix(boneMatrices, boneWeight);
            return RendererLocalToWorldMatrix.MultiplyPoint3x4(skinningMatrix.MultiplyPoint3x4(unskinnedVert));
        }


        /// <summary>
        /// Compute the original bone BindPose position and rotation (T-Pose)
        /// </summary>  
        public static void GetBindPoseBoneTransform(Matrix4x4 smrMatrix, Matrix4x4 bindPose, Transform bone, out Vector3 position, out Quaternion rotation)
        {
            // Get global matrix for bone
            Matrix4x4 bindPoseMatrixGlobal = smrMatrix * bindPose.inverse;

            // Get local X, Y, Z, and position of matrix
            Vector3 mX = new Vector3(bindPoseMatrixGlobal.m00, bindPoseMatrixGlobal.m10, bindPoseMatrixGlobal.m20);
            Vector3 mY = new Vector3(bindPoseMatrixGlobal.m01, bindPoseMatrixGlobal.m11, bindPoseMatrixGlobal.m21);
            Vector3 mZ = new Vector3(bindPoseMatrixGlobal.m02, bindPoseMatrixGlobal.m12, bindPoseMatrixGlobal.m22);
            Vector3 mP = new Vector3(bindPoseMatrixGlobal.m03, bindPoseMatrixGlobal.m13, bindPoseMatrixGlobal.m23);

            // Set position
            // Adjust scale of matrix to compensate for difference in binding scale and model scale
            float bindScale = mZ.magnitude;
            position = mP * bindScale;

            // Set rotation
            // Check if scaling is negative and handle accordingly
            if (Vector3.Dot(Vector3.Cross(mX, mY), mZ) >= 0)
                rotation = Quaternion.LookRotation(mZ, mY);
            else
                rotation = Quaternion.LookRotation(-mZ, -mY);
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

        
        /// <summary>
        /// Add debug lines at each bindPose point to see the bindPose in worldspace
        /// </summary> 
        /// <param name="parent">optional: Attach lines to this parent transform</param>   
        public static void ShowBindPose(SkinnedMeshRenderer smr, Transform parent = null)
        {
            #if KK 
                var lineLen = 0.03f;
            #elif AI || HS2
                var lineLen = 0.3f;
            #endif

            Matrix4x4 smrMatrix = smr.transform.localToWorldMatrix;
            for (int i = 0; i < smr.bones.Length; i++)
            {
                //Get a bone's bindPose position/rotation
                GetBoneBindPose(smrMatrix, smr.sharedMesh.bindposes[i], smr.bones[i], out var position, out var rotation);        

                if (parent == null) 
                {
                    DebugTools.DrawLine(position, position + rotation * Vector3.right * lineLen, width: 0.005f);
                    DebugTools.DrawLine(position, position + rotation * Vector3.up * lineLen, width: 0.005f);
                    DebugTools.DrawLine(position, position + rotation * Vector3.forward * lineLen, width: 0.005f);
                } 
                else
                {
                    DebugTools.DrawLineAndAttach(parent, position, position + rotation * Vector3.right * lineLen, width: 0.005f, removeExisting: false);
                    DebugTools.DrawLineAndAttach(parent, position, position + rotation * Vector3.up * lineLen, width: 0.005f, removeExisting: false);
                    DebugTools.DrawLineAndAttach(parent, position, position + rotation * Vector3.forward * lineLen, width: 0.005f, removeExisting: false);
                }
            }
        }
    }
}