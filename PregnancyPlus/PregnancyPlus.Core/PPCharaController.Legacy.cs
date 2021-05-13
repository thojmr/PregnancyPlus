using KKAPI;
using KKAPI.Chara;
using UnityEngine;
using System;
using System.Collections.Generic;
#if HS2 || AI
    using AIChara;
#endif

namespace KK_PregnancyPlus
{

    //This partial class contains all legacy code we use to fix older versions of card data, or sliders
    public partial class PregnancyPlusCharaController: CharaCustomFunctionController
    {           
        
        /// <summary>
        /// < v3.6
        /// If the meshBlendShape.UncensorGUID is null, but the current character mesh is a match, use this GUID instead
        /// </summary>
        internal void Legacy_CheckInitialUncensorGuid(List<MeshBlendShape> meshBlendShapes, string uncensorGUID) 
        {
            var hasMatchingMesh = false;
            var guidsAllNull = true;
            var bodyRenderers = PregnancyPlusHelper.GetMeshRenderers(ChaControl.objBody, true);
            if (bodyRenderers == null || bodyRenderers.Count <= 0) return;

            //For each saved blendshape see if the current body mesh is a match
            foreach(var meshBlendShape in meshBlendShapes)
            {
                if (meshBlendShape.UncensorGUID != null) guidsAllNull = false;

                //If the body uncensor does have a weight
                if (!meshBlendShape.BlendShape.name.Contains("_body_") || meshBlendShape.BlendShape.weight <= 0) continue;

                //Find renderer with matching naame
                var bodySmr = bodyRenderers.Find(smr => smr.name == meshBlendShape.MeshName);                
                if (bodySmr == null) continue;
                
                //Compare vert counts
                if (bodySmr.sharedMesh.vertexCount != meshBlendShape.VertCount) continue;
                hasMatchingMesh = true;                                    
            }

            //When all blendshape uncensorGUID's are empty set the initialUncensorGUID to be used in its place
            if (guidsAllNull && initialUncensorGUID == null && hasMatchingMesh) 
            {
                if (PregnancyPlusPlugin.DebugLog.Value) PregnancyPlusPlugin.Logger.LogInfo(
                    $" meshBlendShape.UncensorGUID is null but the mesh matches.  setting initialUncensorGUID to {uncensorGUID} ");
                initialUncensorGUID = uncensorGUID;
            }
        }


        /// <summary>
        /// < v3.6
        /// When saving card, If the meshBlendShape.UncensorGUID is null, but the current character mesh has a blendshape, update all guids with current uncensorGUID
        /// </summary>
        internal void Legacy_CheckNullUncensorGuid(List<MeshBlendShape> meshBlendShapes, MeshBlendShape meshBlendShape, SkinnedMeshRenderer smr, string uncensorGUID)
        {
            //For old blendshape data (when null), if blendshape matches the mesh, then save the current uncensorGUID     
            if (meshBlendShape.UncensorGUID == null && meshBlendShape.VertCount == smr.sharedMesh.vertexCount && meshBlendShape.MeshName.Contains("_body_")) 
            {
                if (PregnancyPlusPlugin.DebugLog.Value)  
                    PregnancyPlusPlugin.Logger.LogInfo($" CaptureNewBlendshapeWeights > appending uncensorGUID {uncensorGUID} to all saved blendshapes since there was not one already"); 

                AddUncensorGUID(meshBlendShapes, uncensorGUID);
            }
        }
    }
}