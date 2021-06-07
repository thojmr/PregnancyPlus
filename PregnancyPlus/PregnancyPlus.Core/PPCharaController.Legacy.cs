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
        /// If the meshBlendShape.UncensorGUID is null, but the current character mesh is a match, insert this UncensorGUID
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

                //If its not a body mesh
                if (!meshBlendShape.BlendShape.name.Contains("o_body_")) continue;

                //Find renderer with matching name
                var bodySmr = bodyRenderers.Find(smr => smr.name == meshBlendShape.MeshName);                
                if (bodySmr == null) continue;
                
                //Compare vert counts
                if (bodySmr.sharedMesh.vertexCount != meshBlendShape.VertCount) continue;
                hasMatchingMesh = true;                                    
            }

            //When all blendshape uncensorGUID's are empty set the current GUID to the card data
            if (guidsAllNull && hasMatchingMesh) 
            {
                if (PregnancyPlusPlugin.DebugLog.Value) PregnancyPlusPlugin.Logger.LogInfo(
                    $" meshBlendShape.UncensorGUID is null but the mesh matches.  setting {uncensorGUID} to card ");

                //Get current card state, incase we made some changes that have not been saved
                var tempInfConfig = GetCardData();
                AddUncensorGUID(meshBlendShapes, uncensorGUID);

                //Re-pack the blendshapes that have the new uncensorGUID
                AddBlendShapesToData(tempInfConfig, meshBlendShapes);  
                //Set it to the current state too, so character swaps will detect it              
                AddBlendShapesToData(infConfig, meshBlendShapes);                

                //Update card with the new guid data.  HotSwap makes it like a quick save, instead of a full one
                SetExtendedData(tempInfConfig.Save(hotSwap: true));
            }
        }


        /// <summary>
        /// < v3.6
        /// When saving card, If the meshBlendShape.UncensorGUID is null, but the current character mesh has a blendshape, update all guids with current uncensorGUID
        /// </summary>
        internal void Legacy_CheckNullUncensorGuid(List<MeshBlendShape> meshBlendShapes, MeshBlendShape meshBlendShape, SkinnedMeshRenderer smr, string uncensorGUID)
        {
            //For old blendshape data (when null), if blendshape matches the mesh, then save the current uncensorGUID     
            if (meshBlendShape.UncensorGUID == null && meshBlendShape.VertCount == smr.sharedMesh.vertexCount && meshBlendShape.MeshName.Contains("o_body_")) 
            {
                if (PregnancyPlusPlugin.DebugLog.Value)  
                    PregnancyPlusPlugin.Logger.LogInfo($" CaptureNewBlendshapeWeights > appending uncensorGUID {uncensorGUID} to all saved blendshapes since there was not one already"); 

                AddUncensorGUID(meshBlendShapes, uncensorGUID);
            }
        }
    }
}