using UnityEngine;
using System.Linq;


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
    /// <param name="boneFilters">When included, only bindposes with matching bone names will be considered</param> 
    public static Quaternion GetAverageRotation(SkinnedMeshRenderer smr, string[] boneFilters)
    {            
        //For a bindpose check for any non 0 rotation repeated more than a few times
        var bindposes = smr.sharedMesh.bindposes;
        var totalX = 0f;
        var totalY = 0f;
        var totalZ = 0f;
        var totalW = 0f;

        var hasBoneFilters = boneFilters != null && boneFilters.Length > 0;
        var numAdded = 0;
        var bonesCount = smr.bones.Length;

        if ((hasBoneFilters && bonesCount == 0) || bindposes.Length == 0)
            return Quaternion.identity;

        //Add up all the rotations for each bindpose
        for (int i = 0; i < bindposes.Length; i++)
        {   
            //The current bone must match the filter if a filter exists
            //  We need this because a handfull of meshes come with extra bones that have totally arbitrary rotations
            if (hasBoneFilters)
            {
                //If there are more bindposes than bones, skip (is this even possible?)
                if (i >= bonesCount)
                    continue;

                //Check the bone name matches filter name
                var bone = smr.bones[i];
                if (bone == null)
                    continue;

                if (!boneFilters.Contains(bone.name))
                    continue;                    
            }

            var bindposeRotation = GetRotation(smr, bindposes[i]);
            //We want to ignore character rotation, so convert to local rotation
            var localRotation = Quaternion.Inverse(smr.transform.rotation) * bindposeRotation;
            
            //Round them to the nearest 90 degree axis since most offset rotations are at 90 degree intervals
            var currentRotation = Rotation.AxisRound(localRotation);                
            totalX+=currentRotation.x;
            totalY+=currentRotation.y;
            totalZ+=currentRotation.z;
            totalW+=currentRotation.w;        

            numAdded++;
        }                        

        //Compute the average rotation
        var averageRotation = new Quaternion(
                x: totalX/numAdded, 
                y: totalY/numAdded, 
                z: totalZ/numAdded, 
                w: totalW/numAdded
            );

        //Round the final rotation to the nearest 90 degree axis
        var roundedRotation = Rotation.AxisRound(averageRotation);
        
        return roundedRotation;            
    }
}
