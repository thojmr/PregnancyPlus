using UnityEngine;


//Contains methods to extract bindpose data from a mesh
public static class BindPose
{  

    /// <summary>
    /// Get a bindpose matrix of a single SMR bone
    /// </summary>
    public static Matrix4x4 GetBindPose(Matrix4x4 smrMatrix, Matrix4x4 bindPose)
    {
        return smrMatrix * bindPose.inverse;
    }

    //Overload for the above using a SMR directly
    public static Matrix4x4 GetBindPose(SkinnedMeshRenderer smr, Matrix4x4 bindPose)
    {
        return GetBindPose(smr.transform.localToWorldMatrix, bindPose);
    }


    /// <summary>
    /// Get the scale from the first bindpose matrix
    /// </summary>  
    public static Matrix4x4 GetScale(SkinnedMeshRenderer smr)
    {            
        if (smr == null) 
            return Matrix4x4.identity;

        //For a bindpose check for scale (just grab the first if any exists)
        var bindposes = smr.sharedMesh.bindposes;
        if (bindposes.Length <= 0)       
            return Matrix4x4.identity;

        //Note: This assumes the scale is the same for all bindposes. It's worked so far ...
        return Matrix.GetScaleOnlyMatrix(bindposes[0]);
    }


    /// <summary>
    /// Get the worldspace rotation of a bindpose
    /// </summary>  
    public static Quaternion GetRotation(SkinnedMeshRenderer smr, Matrix4x4 bindpose)
    {   
        return Matrix.GetRotation(smr.transform.localToWorldMatrix * bindpose.inverse);
    }


    /// <summary>
    /// Get the average rotation of all the bindposes in localspace
    /// </summary>  
    public static Quaternion GetAverageRotation(SkinnedMeshRenderer smr)
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
            var bindposeRotation = GetRotation(smr, bindposes[i]);
            //We want to ignore character rotation, so convert to local rotation
            var localRotation = Quaternion.Inverse(smr.transform.rotation) * bindposeRotation;
            
            //Round them to the nearest 90 degree axis since most offset rotations are at 90 degree intervals
            var currentRotation = Rotation.AxisRound(localRotation);                
            totalX+=currentRotation.x;
            totalY+=currentRotation.y;
            totalZ+=currentRotation.z;
            totalW+=currentRotation.w;                
        }                        

        //Compute the average rotation
        var averageRotation = new Quaternion(
                x: totalX/bindposes.Length, 
                y: totalY/bindposes.Length, 
                z: totalZ/bindposes.Length, 
                w: totalW/bindposes.Length
            );

        //Round the final rotation to the nearest 90 degree axis
        var roundedRotation = Rotation.AxisRound(averageRotation);
        
        return roundedRotation;            
    }
}
