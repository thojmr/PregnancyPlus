using UnityEngine;

namespace KK_PregnancyPlus
{

    //Typical matrix methods, but some of these do not exist in unity 5.2 (KK)
    public static class Matrix
    {   

        //Returns the position Vector of a Matrix4x4
        public static Vector3 GetPosition(Matrix4x4 matrix)
        {
            return matrix.GetColumn(3);
        }


        //Returns the rotation Quaternion of a Matrix4x4
        public static Quaternion GetRotation(Matrix4x4 matrix)
        {
            return Quaternion.LookRotation(matrix.GetColumn(2), matrix.GetColumn(1));
        }

    }

}