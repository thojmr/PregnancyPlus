using UnityEngine;

namespace KK_PregnancyPlus
{

    //Typical matrix methods, but some of these do not exist in unity 5.2 (KK)
    public static class Matrix
    {   

        /// <summary>
        /// Returns the position Vector3 of a Matrix4x4
        /// </summary> 
        public static Vector3 GetPosition(Matrix4x4 matrix)
        {
            return matrix.GetColumn(3);
        }


        /// <summary>
        /// Returns the rotation Quaternion of a Matrix4x4
        /// </summary> 
        public static Quaternion GetRotation(Matrix4x4 matrix)
        {
            return Quaternion.LookRotation(matrix.GetColumn(2), matrix.GetColumn(1));
        }


        /// <summary>
        /// Returns the scale Vector3 of a Matrix4x4
        /// </summary> 
        public static Vector3 GetScale(Matrix4x4 matrix)
        {
            var scaleMatrix = GetScaleMatrix(matrix);
            return new Vector3(scaleMatrix.m00, scaleMatrix.m11, scaleMatrix.m22);
        }


        /// <summary>
        /// Returns the scale of a Matrix4x4, excluding position and rotation
        /// </summary> 
        public static Matrix4x4 GetScaleMatrix(Matrix4x4 matrix)
        {
            return GetPositionAndRotationMatrrix(matrix).inverse * matrix;
        }

        
        /// <summary>
        /// Get position and rotation, excluding scale of a Matrix4x4
        /// </summary> 
        public static Matrix4x4 GetPositionAndRotationMatrrix(Matrix4x4 matrix)
        {
            return Matrix4x4.TRS(GetPosition(matrix), GetRotation(matrix), Vector3.one);
        }

    }

}