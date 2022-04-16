using UnityEngine;
using System;
using System.Reflection;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;

namespace KK_PregnancyPlus
{
    public partial class PregnancyPlusBlendShapeGui
    {
		public List<MeshIdentifier> guiSkinnedMeshRenderers = new List<MeshIdentifier>();//All SMR's that have P+ blend shapes
		public const int guiWindowId = 7639;
		public Rect windowRect = new Rect((float)(Screen.width - 450), (float)(Screen.height / 2 - 50), 250f, 15f);
		public bool blendShapeWindowShow = false;//Shows/hides the GUI
		public Dictionary<string, float> _sliderValues = new Dictionary<string, float>();//Tracks user modified blendshape slider values
		public Dictionary<string, float> _sliderValuesHistory = new Dictionary<string, float>();//Tracks user modified blendshape slider values
		internal PregnancyPlusPlugin _pluginInstance = null;
		internal PregnancyPlusCharaController _charaInstance = null;
		internal bool HSPEExists = true;//Warn user when the HSPE plugin is not found
		internal int lastTouched = -1;//Last touched slider index, for coloring it green
		internal bool anyMeshEmpty {get; set;} = false;
		internal bool lastAnyMeshEmpty {get; set;} = false;

		#if KKS
			internal const string HspeNotFoundMessage = "KKSPE was not found";
		#elif HS2
			internal const string HspeNotFoundMessage = "HS2PE was not found";
		#elif AI
			internal const string HspeNotFoundMessage = "AIPE was not found";
		#endif



		/// <summary>
        /// Triggered each tick by PPPlugin.OnGUI, will show the gui when blendShapeWindowShow = true
        /// </summary>
		internal void OnGUI(PregnancyPlusPlugin instance)
		{
			if (_pluginInstance == null && instance != null)
			{
				_pluginInstance = instance;
			}

			if (blendShapeWindowShow)
			{
				//Show GUI when true
				GUI.backgroundColor = Color.black;
				windowRect = GUILayout.Window(guiWindowId, windowRect, new GUI.WindowFunction(WindowFunc), "Pregnancy+ Blendshapes", new GUILayoutOption[0]);

				// Prevent clicks from going through
            	if (windowRect.Contains(new Vector2(Input.mousePosition.x, Screen.height - Input.mousePosition.y)))
				{
                	Input.ResetInputAxes();
				}
			}
		}


		/// <summary>
        /// Open the GUI and set the default init state
        /// </summary>
		internal void OpenBlendShapeGui(List<MeshIdentifier> smrIdentifiers, PregnancyPlusCharaController charaInstance) 
		{
			if (PregnancyPlusPlugin.DebugLog.Value)  PregnancyPlusPlugin.Logger.LogInfo($" OpenBlendShapeGui ");		

			_charaInstance = charaInstance;				
			OnGuiInit(smrIdentifiers);
			guiSkinnedMeshRenderers = smrIdentifiers;
			anyMeshEmpty = IsAnyMeshEmpty(guiSkinnedMeshRenderers);
			blendShapeWindowShow = true;//Trigger gui to show			
		}


		/// <summary>
        /// When a new mesh is added and the blendshape created add them all here
        /// </summary>
		internal void OnSkinnedMeshRendererBlendShapesCreated(List<MeshIdentifier> smrIdentifiers)
		{
			OnGuiInit(smrIdentifiers);
			guiSkinnedMeshRenderers = smrIdentifiers;			
			anyMeshEmpty = IsAnyMeshEmpty(guiSkinnedMeshRenderers);
		}
		

		/// <summary>
        /// Removed all blendshape sliders, and blendshapes from the character
        /// </summary>
		internal void OnRemoveAllGUIBlendShapes()
		{
			if (PregnancyPlusPlugin.DebugLog.Value)  PregnancyPlusPlugin.Logger.LogInfo($" OnRemoveAllGUIBlendShapes ");
			PregnancyPlusPlugin.Hooks_HSPE.ResetHspeBlendShapes(guiSkinnedMeshRenderers, _charaInstance.ChaControl);

			_charaInstance.OnRemoveAllGUIBlendShapes();
			guiSkinnedMeshRenderers = new List<MeshIdentifier>();
			lastTouched = -1;
		}


		/// <summary>
        /// Close and reset any necessary GUI properties
        /// </summary>
		internal void CloseBlendShapeGui() 
		{
			CloseWindow();
		}


		/// <summary>
        /// Check to make sure at least one mesh is not null  (things like chaning uncensor can cause mesh to change and become null in this context)
		/// When any are null we probably want to warn the user
        /// </summary>
		internal bool IsAnyMeshEmpty(List<MeshIdentifier> smrIdentifiers)
		{
			foreach(var smrIdentifier in smrIdentifiers)
			{
				var smr = PregnancyPlusHelper.GetMeshRendererByName(_charaInstance.ChaControl, smrIdentifier.name, smrIdentifier.vertexCount);
				if (smr == null || smr.sharedMesh == null || smr.sharedMesh.blendShapeCount == 0) return true;			
			}
			
			return false;
		}


		/// <summary>
        /// Closes the window and resets HSPE blendshapes
        /// </summary>
		internal void CloseWindow()
		{
			blendShapeWindowShow = false;
			//We need to reset the HSPE sliders to avoid potential console errors
			// ResetHspeBlendShapes(guiSkinnedMeshRenderers);
			guiSkinnedMeshRenderers = new List<MeshIdentifier>();
			lastTouched = -1;
		}


        /// <summary>
        /// Initilize sliders on first window open, or smrs appended
        /// </summary>
		public void OnGuiInit(List<MeshIdentifier> smrIdentifiers) 
		{
			_sliderValues = BuildSliderListValues(smrIdentifiers, _sliderValues);
			_sliderValuesHistory = BuildSliderListValues(smrIdentifiers, _sliderValuesHistory);
		}


		public void ClearAllSliderValues() 
		{
			_sliderValues = BuildSliderListValues(guiSkinnedMeshRenderers, _sliderValues, clearAll: true);
		}


        /// <summary>
        /// If any slider value is not equal to another they have differing values, so one changed
        /// </summary>
		internal bool BlendShapeSliderValuesChanged(Dictionary<string, float> sliderValues) 
		{
			float lastSliderValue = -1;
			var keyList = new List<string>(sliderValues.Keys);

			//For each slider value see if it is the same as the previous value
			foreach (var key in keyList)
			{
				if (lastSliderValue == -1) lastSliderValue = sliderValues[key];
				if (sliderValues[key] != lastSliderValue) return true;
			}

			return false;
		}


        /// <summary>
        /// set the default sliderValue dictionary values
        /// </summary>
		internal Dictionary<string, float> BuildSliderListValues(List<MeshIdentifier> smrIdentifiers, Dictionary<string, float> sliderValues, bool clearAll = false) 
		{
			if (smrIdentifiers == null || smrIdentifiers.Count <= 0 ) return new Dictionary<string, float>();
			if (sliderValues == null) sliderValues = new Dictionary<string, float>();

			//For each smr get the smr key and the starting slider value
			foreach (var smrIdentifier in smrIdentifiers)
			{
				var smr = PregnancyPlusHelper.GetMeshRendererByName(_charaInstance.ChaControl, smrIdentifier.name, smrIdentifier.vertexCount);
				if (smr == null) continue;
				float existingWeight = 0;

				//Get existing weight value if one exists
				var blendShapeIndex = GetBlendShapeIndexFromName(smr.sharedMesh);
				if (blendShapeIndex >= 0)
				{
					existingWeight = smr.GetBlendShapeWeight(blendShapeIndex);
				}

				sliderValues[smr.name] = clearAll ? 0 : existingWeight;
			}

			sliderValues["dummy"] = 0;

			return sliderValues;
		}


        /// <summary>
        /// Find a blendshape index by partial matching blendshape name
        /// </summary>
        /// <param name="searchName">The string in the blendshape name to match to</param>
		internal int GetBlendShapeIndexFromName(Mesh sharedMesh, string searchName = "KK_PregnancyPlus") 
		{
			var count = sharedMesh.blendShapeCount;
			for (int i = 0; i < count; i++)
			{
				var name = sharedMesh.GetBlendShapeName(i);
				if (name.EndsWith(searchName)) return i;
			}

			return -1;
		}

    }

}