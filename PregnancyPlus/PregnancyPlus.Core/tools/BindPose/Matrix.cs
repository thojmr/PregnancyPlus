using UnityEngine;

namespace KK_PregnancyPlus
{

    //Typical matrix methods, but some of these do not exist in unity 5.2 (KK)
    public static class Matrix
    {   

        /// <summary>
        /// Returns the position Vector of a Matrix4x4
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
        /// Only get position and rotation, excluding scale of a Matrix4x4
        /// </summary> 
        public static Matrix4x4 GetPositionAndRotation(Matrix4x4 matrix)
        {
            return Matrix4x4.TRS(GetPosition(matrix), GetRotation(matrix), Vector3.one);
        }

    }

}