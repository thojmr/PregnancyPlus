using UnityEngine;
using System;
using System.Collections.Generic;

namespace KK_PregnancyPlus
{
	//Just the actual window interface builder code
    public partial class PregnancyPlusBlendShapeGui
    {

		/// <summary>
        /// The main blendshape GUI
        /// </summary>
		internal void WindowFunc(int id)
		{
			var hasBlendShapes = guiSkinnedMeshRenderers != null && guiSkinnedMeshRenderers.Count > 0;

			//Exit when no smrs
			if (!blendShapeWindowShow || guiSkinnedMeshRenderers == null && guiSkinnedMeshRenderers.Count <= 0) 
			{
				CloseBlendShapeGui();
				return;
			}	

			GUILayout.Box("", new GUILayoutOption[]
			{
				GUILayout.Width(450f),
				GUILayout.Height(GetGuiHeight(hasBlendShapes))
			});

			//Set the size of the interactable area
			Rect screenRect = new Rect(10f, 25f, 450f, GetGuiHeight(hasBlendShapes));
			GUILayout.BeginArea(screenRect);

			var clearBtnCLicked = false;
			var createBtnCLicked = false;
			var removeBtnCLicked = false;

			//Show sliders if we have blendshapes set already
			if (hasBlendShapes)
			{				
				anyMeshEmpty = IsAnyMeshEmpty(guiSkinnedMeshRenderers);
				//When a mesh becomes empty, reset sliders
				if (anyMeshEmpty && !lastAnyMeshEmpty)
				{					
					//Log empty meshes
					if (PregnancyPlusPlugin.DebugLog.Value)  
					{
						foreach (var smrIdentifier in guiSkinnedMeshRenderers)
						{
							var smr = PregnancyPlusHelper.GetMeshRendererByName(_charaInstance.ChaControl, smrIdentifier.name, smrIdentifier.vertexCount);
							var name = smr != null ? smr.name : "<NUll smr>";
							
							if (smr == null || smr.sharedMesh == null || smr.sharedMesh.blendShapeCount == 0) 
								PregnancyPlusPlugin.Logger.LogInfo($" IsAnyMeshEmpty > {name} is empty ");
						}						
					}
					ResetHspeBlendShapes(guiSkinnedMeshRenderers);
				}
				lastAnyMeshEmpty = anyMeshEmpty;

				//For each SMR we want a slider for
				for (int i = 0; i < guiSkinnedMeshRenderers.Count; i++)
				{				
					//Create and watch each blendshape sliders
					GuiSliderControlls(i);
				}	

				GUILayout.Label("The blendshape sliders above can be adjusted and then saved to Timeline (Ctrl+T) or VNGE > Clip Manager.  The blendshapes will persist to the character card after the scene is saved.", _labelTextStyle, new GUILayoutOption[0]);
				GUILayout.Label("You can 'Create New' blendshapes with the current P+ character sliders (Overwrites existing).", _labelTextStyle, new GUILayoutOption[0]);

				//Error messages for the user, when something goes wrong
				if (!HSPEExists) GUILayout.Label(HspeNotFoundMessage, _labelErrorTextStyle, new GUILayoutOption[0]);
				if (anyMeshEmpty) GUILayout.Label("One or more blendshapes no longer match their mesh and need to be recreated with 'Create New'.  Things like changing Uncensor, or clothing, can cause this.", _labelErrorTextStyle, new GUILayoutOption[0]);

				createBtnCLicked = GUILayout.Button("Create New", new GUILayoutOption[0]);
				clearBtnCLicked = GUILayout.Button("Reset BlendShape Sliders", new GUILayoutOption[0]);
				removeBtnCLicked = GUILayout.Button("Remove BlendShapes", new GUILayoutOption[0]);
			}			
			else 
			{
				//If no blendshapes, then all the user to set them with a create button
				GUILayout.Label("No P+ blendshapes found.  Use the 'Create' button to capture a snapshot of the current P+ slider values as blendshapes.  Make sure Inflation Size is not 0.", _labelTextStyle, new GUILayoutOption[0]);
				createBtnCLicked = GUILayout.Button("Create", new GUILayoutOption[0]);
			}

			var closeBtnCLicked = GUILayout.Button("Close", new GUILayoutOption[0]);
			GUILayout.EndArea();			
			GUI.DragWindow();
			
			//Button click events from above
			if (closeBtnCLicked) CloseWindow();
			if (clearBtnCLicked) ClearAllSliderValues();
			if (createBtnCLicked) _charaInstance.OnCreateBlendShapeSelected();
			if (removeBtnCLicked) OnRemoveAllGUIBlendShapes();
		}


        /// <summary>
        /// Define each blendshape slider, and update HSPE sliders on change
        /// </summary>
		internal void GuiSliderControlls(int i)
		{
			string smrName = null;
			int kkBsIndex = -1;
			string sliderLabel = "<Empty Mesh>";

			var guiSmr = PregnancyPlusHelper.GetMeshRendererByName(_charaInstance.ChaControl, guiSkinnedMeshRenderers[i].name, guiSkinnedMeshRenderers[i].vertexCount);
			var smrIsEmpty = guiSmr == null || guiSmr.sharedMesh == null || guiSmr.sharedMesh.blendShapeCount == 0;

			//Get slider details
			if (!smrIsEmpty) 
			{
				smrName = guiSmr.name;			
				//Find the index of the Preg+ blendshape
				kkBsIndex = GetBlendShapeIndexFromName(guiSmr.sharedMesh);
				if (kkBsIndex >= 0)
				{
					sliderLabel = guiSmr.sharedMesh.GetBlendShapeName(kkBsIndex);
				}
			}

			//Check for empty blendshapes
			if ((kkBsIndex < 0 || sliderLabel == null) && smrName != null)
			{							
				smrIsEmpty = true;
				//When blendshape becomes invalid but mesh still exists, set the value to 0, so it will be reset in HSPE too if needed
				_sliderValues[smrName] = 0;
				_sliderValuesHistory[smrName] = -1;//To trigger clear val				
			}
			else if (kkBsIndex < 0 || sliderLabel == null)
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
				_sliderValues[smrName] = GUILayout.HorizontalSlider(_sliderValues[smrName], 0f, 100f, new GUILayoutOption[0]);
				GUILayout.Label(_sliderValues[smrName].ToString("#0"), _labelValueStyle, new GUILayoutOption[0]);
			}
			else
			{
				//Dummy disabled slider
				GUILayout.Label(sliderLabel, lastTouched == i ? _labelTitleActiveStyle : _labelTitleStyle, new GUILayoutOption[0]);
				GUILayout.HorizontalSlider(0, 0f, 100f, new GUILayoutOption[0]);
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
				// if (PregnancyPlusPlugin.DebugLog.Value)  PregnancyPlusPlugin.Logger.LogInfo($" BlendShapeSlider changed {smrName} > kkBsIndex {kkBsIndex}  val {_sliderValues[smrName]}");

				//If there are no blendshapes for this mesh anymore just reset it in HSPE
				if (kkBsIndex < 0) 
				{
					ResetHspeBlendShapes(new List<MeshIdentifier> { guiSkinnedMeshRenderers[i] });
					_sliderValuesHistory[smrName] = _sliderValues[smrName];
					return;
				}

				try 
				{					
					//We want to use HSPE when we can because changes to it will integrate with Timeline and VNGE
					HSPEExists = SetHspeBlendShapeWeight(guiSmr, kkBsIndex, _sliderValues[smrName]);
				}	
				catch (Exception e)
				{
					//If KKPE does not exists catch the error, and warn the user
					PregnancyPlusPlugin.errorCodeCtrl.LogErrorCode("-1", ErrorCode.PregPlus_HSPENotFound, 
                    	$"SetHspeBlendShapeWeight > HSPE not found {e.Message} ");
					HSPEExists = false;
				}

				if (!HSPEExists) 
				{
					//If HSPE not found adjust the weight the normal way, that won't integrate with VNGE, or Timeline, but is still visible to the user
					guiSmr.SetBlendShapeWeight(kkBsIndex, _sliderValues[smrName]);
				}				
			}
			//If the user changed the blendshape weight in KKPE (or elsewhere), update the GUI to match
			else if (_sliderValues[smrName] != guiSmr.GetBlendShapeWeight(kkBsIndex)) 
			{
				_sliderValues[smrName] = _sliderValuesHistory[smrName] = guiSmr.GetBlendShapeWeight(kkBsIndex);
			}
			
			_sliderValuesHistory[smrName] = _sliderValues[smrName];
		}

    }
}