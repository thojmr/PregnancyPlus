using UnityEngine;


//Typical matrix methods, but some of these do not exist in unity 5.2 (KK)
public static class Matrix
{   
    /// <summary>
    /// Returns the position in Vector3 of a Matrix4x4
    /// </summary> 
    public static Vector3 GetPosition(Matrix4x4 matrix)
    {
        return matrix.GetColumn(3);
    }


    /// <summary>
    /// Returns the rotation as Quaternion of a Matrix4x4
    /// </summary> 
    public static Quaternion GetRotation(Matrix4x4 matrix)
    {
        return Quaternion.LookRotation(matrix.GetColumn(2), matrix.GetColumn(1));
    }


    /// <summary>
    /// Returns the scale as Vector3 of a scale only Matrix4x4
    /// </summary> 
    public static Vector3 GetScale(Matrix4x4 matrix)
    {
        var scaleMatrix = GetScaleOnlyMatrix(matrix);
        return new Vector3(scaleMatrix.m00, scaleMatrix.m11, scaleMatrix.m22);
    }


    /// <summary>
    /// Returns the scale of a Matrix4x4, excluding position and rotation
    /// </summary> 
    public static Matrix4x4 GetScaleOnlyMatrix(Matrix4x4 matrix)
    {
        return GetPositionAndRotationMatrix(matrix).inverse * matrix;
    }

    
    /// <summary>
    /// Get position and rotation, excluding scale of a Matrix4x4
    /// </summary> 
    public static Matrix4x4 GetPositionAndRotationMatrix(Matrix4x4 matrix)
    {
        return Matrix4x4.TRS(GetPosition(matrix), GetRotation(matrix), Vector3.one);
    }

}