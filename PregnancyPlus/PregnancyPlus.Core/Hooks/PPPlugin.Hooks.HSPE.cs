using System;
using System.Collections.Generic;
using KKAPI.Chara;
using UnityEngine;
using HarmonyLib;
using System.Reflection;
using System.Linq;
#if KK
    using KKAPI.MainGame;
#elif HS2
    using AIChara;
#elif AI
    using KKAPI.MainGame;
    using AIChara;
#endif

namespace KK_PregnancyPlus
{

    public partial class PregnancyPlusPlugin
    {
        //This class contains the logic for the HSPE plugin integration
        //Pregnancy+ BlendSpahe GUI needs to control some aspects of the HSPE blendshape window to keep slider values in sync
        public static class Hooks_HSPE
        {            
            internal static PregnancyPlusPlugin pluginInstance = null;
            public static bool blendShapeModuleExists = false;

            //Used to identifiy the assembly class methods during reflection
            #if KKS
                internal static string pluginName = "KKSPE";
            #elif AI
                internal static string pluginName = "AIPE";
            #elif HS2
                internal static string pluginName = "HSPE";
            #endif


            /// <summary>
            /// See if HSPE plugin exists, and grab a reference to it
            /// </summary>
            internal static void InitHooks(PregnancyPlusPlugin _pluginInstance) 
            {
                pluginInstance = _pluginInstance;

                //Get the blendshape editor module for later use
                var BlendShapesEditor = Type.GetType($"HSPE.AMModules.BlendShapesEditor, {pluginName}", false);
                if (BlendShapesEditor == null)
                {
                    PregnancyPlusPlugin.Logger.LogInfo(
                        $"Could not find HSPE.AMModules.BlendShapesEditor, This is not an issue, but you won't be able to use the BlendShape GUI integration with Timeline/VNGE");
                        return;
                }

                blendShapeModuleExists = BlendShapesEditor != null;
            }


            /// <summary>
            /// Have to manually update the blendshape slider in the HSPE window in order for Timeline, or VNGE to detect the change
            /// They don't automagically watch for mesh.blendshape changes
            /// </summary>
            /// <returns>Will return True if HSPE was found</returns>
            internal static bool SetHspeBlendShapeWeight(SkinnedMeshRenderer smr, int index, float weight) 
            {
                System.Object bsModule;
                try 
                {
                    bsModule = GetHspeBlenShapeModule();
                    if (bsModule == null) return false;
                }	
                catch (Exception e)
                {
                    PregnancyPlusPlugin.errorCodeCtrl.LogErrorCode("-1", ErrorCode.PregPlus_HSPENotFound, 
                            $" SetHspeBlendShapeWeight > HSPE not found: {e.Message} ");
                    return false;
                }	
                
                //Set the following values as if the HSPE blendshape tab was clicked
                Traverse.Create(bsModule).Field("_skinnedMeshTarget").SetValue(smr);
                Traverse.Create(bsModule).Field("_lastEditedBlendShape").SetValue(index);

                //Set the blend shape weight in HSPE for a specific smr, (Finally working............)
                var SetBlendShapeWeight = bsModule.GetType().GetMethod("SetBlendShapeWeight", BindingFlags.NonPublic | BindingFlags.Instance);
                if (SetBlendShapeWeight == null) return false;
                SetBlendShapeWeight.Invoke(bsModule, new object[] { smr, index, weight} );

                //Set last changed smr slider to be visibly active in HSPE
                var SetMeshRendererDirty = bsModule.GetType().GetMethod("SetMeshRendererDirty", BindingFlags.NonPublic | BindingFlags.Instance);
                if (SetMeshRendererDirty == null) return false;
                SetMeshRendererDirty.Invoke(bsModule, new object[] { smr } );

                return true;

                // (Leaviung behind the pain and misery below as a memorial of what not to do)
                // Traverse.Create(bsModule).Method("SetBlendShapeWeight", new object[] { smr, index, weight });
                // Traverse.Create(bsModule).Method("SetMeshRendererDirty", new object[] { smr });
                // Traverse.Create(bsModule).Method("SetBlendShapeDirty", new object[] { smr, index });
                // Traverse.Create(bsModule).Method("ApplyBlendShapeWeights", new object[] { });
                // Traverse.Create(bsModule).Method("Populate", new object[] { });

                // ResetHspeBlendShapes(bsModule, smr, index);

                // var dynMethod = bsModule.GetType().GetMethod("SetBlendShapeWeight", BindingFlags.NonPublic | BindingFlags.Instance);
                // dynMethod.Invoke(this, new object[] { smr , index, weight });
            }


            /// <summary>
            /// Reset HSPE blendshape when character changes
            /// </summary>
            /// <returns>Will return True if HSPE was found</returns>
            internal static bool ResetHspeBlendShapes(List<MeshIdentifier> smrIdentifiers, ChaControl chaControl) 
            {
                if (smrIdentifiers == null || smrIdentifiers.Count <= 0) return true;

                System.Object bsModule;
                try 
                {
                    bsModule = GetHspeBlenShapeModule();
                    if (bsModule == null) return false;
                }	
                catch (Exception e)
                {
                    PregnancyPlusPlugin.errorCodeCtrl.LogErrorCode("-1", ErrorCode.PregPlus_HSPENotFound, 
                            $" ResetHspeBlendShapes > HSPE not found: {e.Message} ");
                    return false;
                }

                //Set the following values as if the HSPE blendshape tab was clicked
                Traverse.Create(bsModule).Field("_lastEditedBlendShape").SetValue(-1);

                //Set the blend shape weight in HSPE for a specific smr, (Finally working............)
                var SetMeshRendererNotDirty = bsModule.GetType().GetMethod("SetMeshRendererNotDirty", BindingFlags.NonPublic | BindingFlags.Instance);
                if (SetMeshRendererNotDirty == null) return false;

                //reset all active smrs in HSPE
                foreach(var smrIdentifier in smrIdentifiers)
                {	
                    var smr = PregnancyPlusHelper.GetMeshRendererByName(chaControl, smrIdentifier.name, smrIdentifier.vertexCount);
                    if (smr == null) continue;
                    SetMeshRendererNotDirty.Invoke(bsModule, new object[] { smr } );	
                }

                return true;
            }


            /// <summary>
            /// Get the active HSPE blend shape module, that we want to make alterations to
            /// </summary>
            internal static System.Object GetHspeBlenShapeModule()
            {
                if (pluginInstance == null) 
                {
                    if (PregnancyPlusPlugin.DebugLog.Value)  PregnancyPlusPlugin.Logger.LogWarning($" GetHspeBlenShapeModule(): _pluginInstance must be defined first ");
                    return null;
                }

            	//Get main HSPE window reference
            	var hspeMainWindow = pluginInstance.gameObject.GetComponent<HSPE.MainWindow>();
            	if (hspeMainWindow == null) return null;

            	//Pose target contains the character main window buttons
            	var poseCtrl = Traverse.Create(hspeMainWindow).Field("_poseTarget").GetValue<HSPE.PoseController>();
            	if (poseCtrl == null) return null;

            	//The modules are indivisual popups originating from the pose target window
            	var advModules = Traverse.Create(poseCtrl).Field("_modules").GetValue<List<HSPE.AMModules.AdvancedModeModule>>();
            	if (advModules == null || advModules.Count <= 0) return null;

            	//Get the blendShape module  (4 == blendshape.type, or just use the string name)
            	var bsModule = advModules.FirstOrDefault(x => x != null && x.displayName.Contains("Blend Shape"));
            	return bsModule;            
            }
        }
    }
}