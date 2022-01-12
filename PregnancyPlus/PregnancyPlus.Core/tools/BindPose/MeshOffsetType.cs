using UnityEngine;
#if HS2 || AI
    using AIChara;
#endif

namespace KK_PregnancyPlus
{

    //Types of meshs we find based on their bindpose positions (ones that align with the character, and ones that don't)
    public enum MeshOffsetType
    {  
        DefaultMesh, //This type of mesh is the default mesh provided by Illusion, and does not need to be offset (Some uncensors match this too, but that's fine)
        AlmostDefaultMesh, //This type of mesh is an uncensor that almost aligns in localspace with the verts of the DefaultMesh, but still needs an offset
        OtherMesh //This type of mesh is an uncensor which doesnt match either of the above.  Catch all for unknown types.  Ill add more if I find a pattern
    }


    //Contains the logic used to determine the offset type needed for some KK body meshes
    public static class MeshOffSet 
    {
        public static MeshOffsetType GetMeshOffsetType(SkinnedMeshRenderer smr) 
        {
            //  SMR Bounds are the only way I could come up with to reliably detect an offset mesh...
            var isLikeDefaultBody = smr.localBounds.center.y < 0 && smr.sharedMesh.bounds.center.y > 0;
            var isAlmostLikeDefaultBody = smr.localBounds.center.y < 0 && smr.sharedMesh.bounds.center.y >= -0.5f;

            if (isLikeDefaultBody) 
                return MeshOffsetType.DefaultMesh;

            if (isAlmostLikeDefaultBody)
                return MeshOffsetType.AlmostDefaultMesh;

            return MeshOffsetType.OtherMesh;
        }


        /// <summary>
        /// Whether an SMR has valid (correctly positioned) bind poses.  Based on the local bounds of the verts (Usullly only happens in KK/KKS)
        /// </summary> 
        public static Vector3 GetBindposeOffsetFix(ChaControl chaCtrl, SkinnedMeshRenderer smr)
        {
            var smrLocalPosition = chaCtrl.transform.InverseTransformPoint(smr.transform.position);
            var meshOffsetType = GetMeshOffsetType(smr);

            if (meshOffsetType == MeshOffsetType.DefaultMesh) 
                return Vector3.zero;

            if (meshOffsetType == MeshOffsetType.OtherMesh)
                return smrLocalPosition;
                    
            if (meshOffsetType == MeshOffsetType.AlmostDefaultMesh)
                return smrLocalPosition;
            
            if (PregnancyPlusPlugin.DebugCalcs.Value) PregnancyPlusPlugin.Logger.LogWarning($" Could not determine mesh offset type");
            return Vector3.zero;            
        }


    }
}