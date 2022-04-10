using UnityEngine;
using System;
using System.Linq;
using System.Collections.Generic;

namespace KK_PregnancyPlus
{
	//Just the actual window interface builder code
    public partial class PregnancyPlusClothOffsetGui
    {

		/// <summary>
        /// The main blendshape GUI
        /// </summary>
		internal void WindowFunc(int id)
		{
			var isWearingClothing = guiSkinnedMeshRenderers != null && guiSkinnedMeshRenderers.Count > 0;

			//Exit when no smrs
			if (!windowShow || guiSkinnedMeshRenderers == null && guiSkinnedMeshRenderers.Count <= 0) 
			{
				CloseGui();
				return;
			}	

			GUILayout.Box("", new GUILayoutOption[]
			{
				GUILayout.Width(450f),
				GUILayout.Height(GetGuiHeight(isWearingClothing))
			});

			//Set the size of the interactable area
			Rect screenRect = new Rect(10f, 25f, 450f, GetGuiHeight(isWearingClothing));
			GUILayout.BeginArea(screenRect);

			var clearBtnCLicked = false;

			//Show sliders if we have blendshapes set already
			if (isWearingClothing)
			{				
				//For each SMR we want a slider for
				for (int i = 0; i < guiSkinnedMeshRenderers.Count; i++)
				{				
					//Create and watch each blendshape sliders
					GuiSliderControlls(i);
				}	

				GUILayout.Label("The sliders above control the offset amount for each clothing item.\nSlider offsets will save with the character/scene.", _labelTextStyle, new GUILayoutOption[0]);

				clearBtnCLicked = GUILayout.Button("Reset Sliders", new GUILayoutOption[0]);
			}			
			else 
			{
				//If no clothing found
				GUILayout.Label("No clothing found", _labelTextStyle, new GUILayoutOption[0]);
			}

			var closeBtnCLicked = GUILayout.Button("Close", new GUILayoutOption[0]);
			GUILayout.EndArea();			
			GUI.DragWindow();
			
			//Button click events from above
			if (closeBtnCLicked) CloseWindow();
			if (clearBtnCLicked) ClearAllSliderValues();
		}


        /// <summary>
        /// Define each offset slider, and update mesh on change
        /// </summary>
		internal void GuiSliderControlls(int i)
		{
			string smrName = null;
			string sliderLabel = "<clothing changed, refresh the window>";

			var guiSmr = guiSkinnedMeshRenderers[i];
			var smrIsEmpty = guiSmr == null || guiSmr.sharedMesh == null;

			//Get slider details
			if (!smrIsEmpty) 
			{
				smrName = _charaInstance.GetMeshKey(guiSmr);
				sliderLabel = guiSmr.name;	
			}

			//Check for empty clothing
			if (sliderLabel == null && smrName != null)
			{							
				smrIsEmpty = true;
				//When blendshape becomes invalid but mesh still exists, set the value to 0, so it will be reset in HSPE too if needed
				_sliderValues[smrName] = 0;
				_sliderValuesHistory[smrName] = -2f;//To trigger clear val				
			}
			else if (sliderLabel == null)
			{
				//If the blendshape and smr dissapeared for some reason
				smrIsEmpty = true;
			}

			//Disable a single slider when the mesh is empty
			GUI.enabled = !smrIsEmpty;

			//Create a slider for the matching Preg+ blendshape
			GUILayout.BeginHorizontal(new GUILayoutOption[0]);			
			if (smrName != null)
			{
				GUILayout.Label(sliderLabel, lastTouched == i ? _labelTitleActiveStyle : _labelTitleStyle, new GUILayoutOption[0]);
				_sliderValues[smrName] = GUILayout.HorizontalSlider(_sliderValues[smrName], -1f, 1f, new GUILayoutOption[0]);
				GUILayout.Label(_sliderValues[smrName].ToString("#0"), _labelValueStyle, new GUILayoutOption[0]);
			}
			else
			{
				//Dummy disabled slider
				GUILayout.Label(sliderLabel, lastTouched == i ? _labelTitleActiveStyle : _labelTitleStyle, new GUILayoutOption[0]);
				GUILayout.HorizontalSlider(0, -1f, 1f, new GUILayoutOption[0]);
				GUILayout.Label(0.ToString("#0"), _labelValueStyle, new GUILayoutOption[0]);
			}
			GUILayout.EndHorizontal();

			GUI.enabled = true;

			//Don't need to check for slider changes when null (The Gui will close soon anyway)
			if (smrName == null) return;

			//When user changed slider value
			if (_sliderValues[smrName] != _sliderValuesHistory[smrName]) 
			{					
				lastTouched = i;
				_sliderValuesHistory[smrName] = _sliderValues[smrName];		

				//Set the value to character data, and trigger mesh recalculation
				updateOffsetValue(smrName, _sliderValues[smrName]);				
			}
			
			_sliderValuesHistory[smrName] = _sliderValues[smrName];
		}


		//Update the pluginData value when slider changes, then trigger mesh inflate
		private void updateOffsetValue(string smrMeshKey, float sliderValue) 
		{
			var offsets = _charaInstance.infConfig.IndividualClothingOffsets;

			//Initialize when needed
			if (offsets == null) 
			{
				offsets = new List<KeyValuePair<string, float>>();
			}

			//Check if a saved value exists
			var hasKey = offsets.Any(o => o.Key == smrMeshKey);
			if (!hasKey)
			{
				offsets.Add(new KeyValuePair<string, float>(smrMeshKey, sliderValue));
			}
			else
			{
				var index = offsets.FindIndex(o => o.Key == smrMeshKey);
				offsets.Insert(index, new KeyValuePair<string, float>(smrMeshKey, sliderValue));
			}

			_charaInstance.infConfig.IndividualClothingOffsets = offsets;

			// if (PregnancyPlusPlugin.DebugLog.Value) PregnancyPlusPlugin.Logger.LogInfo($" Setting slider value {smrMeshKey} to {sliderValue}");

			//Trigger re calculation of the cloth shape
			_charaInstance.MeshInflate(new MeshInflateFlags(_charaInstance), "ClothOffsetGui");
		}

    }
}