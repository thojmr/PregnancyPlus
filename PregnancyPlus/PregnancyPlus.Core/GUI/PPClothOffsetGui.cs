using UnityEngine;
using System;
using System.Reflection;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;

namespace KK_PregnancyPlus
{
    public partial class PregnancyPlusClothOffsetGui
    {
		public List<SkinnedMeshRenderer> guiSkinnedMeshRenderers = new List<SkinnedMeshRenderer>();//All SMR's that are clothing
		public const int guiWindowId = 7669;
		public Rect windowRect = new Rect((float)(Screen.width - 450), (float)(Screen.height / 2 - 50), 250f, 15f);
		public bool windowShow = false;//Shows/hides the GUI
		public Dictionary<string, float> _sliderValues = new Dictionary<string, float>();//Tracks user modified blendshape slider values
		public Dictionary<string, float> _sliderValuesHistory = new Dictionary<string, float>();//Tracks user modified blendshape slider values
		internal PregnancyPlusPlugin _pluginInstance = null;
		internal PregnancyPlusCharaController _charaInstance = null;
		internal int lastTouched = -1;//Last touched slider index, for coloring it green



		/// <summary>
        /// Triggered each tick by PPPlugin.OnGUI, will show the gui when windowShow = true
        /// </summary>
		internal void OnGUI(PregnancyPlusPlugin instance)
		{
			if (_pluginInstance == null && instance != null)
			{
				_pluginInstance = instance;
			}

			if (windowShow)
			{
				//Show GUI when true
				GUI.backgroundColor = Color.black;
				windowRect = GUILayout.Window(guiWindowId, windowRect, new GUI.WindowFunction(WindowFunc), "Pregnancy+ Cloth Offsets", new GUILayoutOption[0]);

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
		internal void OpenClothOffsetGui(List<SkinnedMeshRenderer> clothSmrs, PregnancyPlusCharaController charaInstance) 
		{
			if (PregnancyPlusPlugin.DebugLog.Value)  PregnancyPlusPlugin.Logger.LogInfo($" OpenClothOffsetGui ");		

			_charaInstance = charaInstance;				
			OnGuiInit(clothSmrs);
			guiSkinnedMeshRenderers = clothSmrs;
			windowShow = true;//Trigger gui to show			
		}


		/// <summary>
        /// Close and reset any necessary GUI properties
        /// </summary>
		internal void CloseGui() 
		{
			CloseWindow();
		}


		/// <summary>
        /// Closes the window and resets HSPE blendshapes
        /// </summary>
		internal void CloseWindow()
		{
			windowShow = false;
			guiSkinnedMeshRenderers = new List<SkinnedMeshRenderer>();
			lastTouched = -1;
		}


        /// <summary>
        /// Initilize sliders on first window open, or smrs appended
        /// </summary>
		public void OnGuiInit(List<SkinnedMeshRenderer> clothSmrs) 
		{
			_sliderValues = BuildSliderListValues(clothSmrs, _sliderValues);
			_sliderValuesHistory = BuildSliderListValues(clothSmrs, _sliderValuesHistory);
		}


        /// <summary>
        /// When character clothes change, update the slider list
        /// </summary>
		public void OnClothingChanged(List<SkinnedMeshRenderer> clothSmrs) 
		{
			if (!windowShow) return;
			OnGuiInit(clothSmrs);
			guiSkinnedMeshRenderers = clothSmrs;
		}


		public void ClearAllSliderValues() 
		{
			_sliderValues = BuildSliderListValues(guiSkinnedMeshRenderers, _sliderValues, clearAll: true);
		}


        /// <summary>
        /// If any slider value is not equal to another they have differing values, so one changed
        /// </summary>
		internal bool SliderValuesChanged(Dictionary<string, float> sliderValues) 
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
		internal Dictionary<string, float> BuildSliderListValues(List<SkinnedMeshRenderer> clothSmrs, Dictionary<string, float> sliderValues, bool clearAll = false) 
		{
			if (clothSmrs == null || clothSmrs.Count <= 0 ) return new Dictionary<string, float>();
			if (sliderValues == null) sliderValues = new Dictionary<string, float>();
			
			var offsets = _charaInstance.infConfig.IndividualClothingOffsets;
			var hasAnyValues = offsets != null;

			//For each smr get the smr key and the starting slider value
			foreach (var smr in clothSmrs)
			{
				var savedValue = 0f;
				var meshKey = _charaInstance.GetMeshKey(smr);
				
				var hasSavedvalue = hasAnyValues ? offsets.Any(o => o.Key == meshKey) : false;
				if (hasSavedvalue)
				{
					savedValue = offsets.FirstOrDefault(o => o.Key == meshKey).Value;
				}
				sliderValues[meshKey] = clearAll ? 0 : savedValue;
			}

			sliderValues["dummy"] = 0;

			return sliderValues;
		}



    }

}