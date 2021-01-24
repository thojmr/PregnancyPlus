using UnityEngine;
using System;
using System.Reflection;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using HSPE;

namespace KK_PregnancyPlus
{
    public partial class PregnancyPlusBlendShapeGui
    {
		public List<SkinnedMeshRenderer> guiSkinnedMeshRenderers = new List<SkinnedMeshRenderer>();//All SMR's that have P+ blend shapes
		public const int guiWindowId = 7639;
		public Rect windowRect = new Rect((float)(Screen.width - 450), (float)(Screen.height / 2 - 50), 250f, 15f);
		public bool blendShapeWindowShow = false;//Shows/hides the GUI
		public Dictionary<string, float> _sliderValues = new Dictionary<string, float>();//Tracks user modified blendshape slider values
		public Dictionary<string, float> _sliderValuesHistory = new Dictionary<string, float>();//Tracks user modified blendshape slider values
		internal PregnancyPlusPlugin _pluginInstance = null;
		internal PregnancyPlusCharaController _charaInstance = null;
		internal bool HSPEExists = true;//Warn user when the HSPE plugin is not found
		internal int lastTouched = -1;//Last touched slider index, for coloring it green

		#if KK
			internal const string HspeNotFoundMessage = "KKPE was not found";
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
			if (_pluginInstance == null)
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
		internal void OpenBlendShapeGui(List<SkinnedMeshRenderer> smrs, PregnancyPlusCharaController charaInstance) 
		{
			if (PregnancyPlusPlugin.debugLog)  PregnancyPlusPlugin.Logger.LogInfo($" OpenBlendShapeGui ");		

			_charaInstance = charaInstance;				
			OnGuiInit(smrs);
			guiSkinnedMeshRenderers = smrs;
			blendShapeWindowShow = true;//Trigger gui to show			
		}


		/// <summary>
        /// When a new mesh is added and the blendshape created, update the list here
        /// </summary>
		internal void OnSkinnedMeshRendererBlendShapesCreated(List<SkinnedMeshRenderer> smrs)
		{
			OnGuiInit(smrs);
			guiSkinnedMeshRenderers = smrs;			
		}
		

		/// <summary>
        /// Removed all blendshape sliders, and blendshapes from the character
        /// </summary>
		internal void OnRemoveAllBlendShapes()
		{
			try 
			{
				ResetHspeBlendShape(guiSkinnedMeshRenderers);
			}	
			catch (Exception e)
			{
				if (PregnancyPlusPlugin.debugLog)  PregnancyPlusPlugin.Logger.LogInfo($" ResetHspeBlendShape > HSPE not found {e.Message} ");
			}	
			_charaInstance.OnRemoveAllBlendShapes();
			guiSkinnedMeshRenderers = new List<SkinnedMeshRenderer>();
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
        /// The main blendshape GUI
        /// </summary>
		internal void WindowFunc(int id)
		{
			var hasBlendShapes = guiSkinnedMeshRenderers != null && guiSkinnedMeshRenderers.Count > 0 && guiSkinnedMeshRenderers[0] != null;

			//Exit when mesh becomes null (probably deleted character)
			if (!blendShapeWindowShow || (guiSkinnedMeshRenderers != null && guiSkinnedMeshRenderers.Count > 0 && guiSkinnedMeshRenderers[0] == null)) 
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
				//For each SMR we want a slider for
				for (int i = 0; i < guiSkinnedMeshRenderers.Count; i++)
				{				
					//Create and watch each blendshape sliders
					GuiSliderControlls(i);
				}	

				GUILayout.Label("The blendshape sliders above can be adjusted and then saved to Timeline (Ctrl+T) or VNGE > Clip Manager.  The blendshapes will persist to the character card after the scene is saved.", _labelTextStyle, new GUILayoutOption[0]);
				GUILayout.Label("You can 'Create New' blendshapes with the current P+ character sliders (Overwrites existing).", _labelTextStyle, new GUILayoutOption[0]);

				if (!HSPEExists) GUILayout.Label(HspeNotFoundMessage, _labelErrorTextStyle, new GUILayoutOption[0]);

				createBtnCLicked = GUILayout.Button("Create New", new GUILayoutOption[0]);
				clearBtnCLicked = GUILayout.Button("Reset Sliders", new GUILayoutOption[0]);
				removeBtnCLicked = GUILayout.Button("Remove P+ BlendShapes", new GUILayoutOption[0]);
			}
			//If no blendshapes, then all the user to set them with a create button
			else 
			{
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
			if (removeBtnCLicked) OnRemoveAllBlendShapes();
		}


        /// <summary>
        /// Define each blendshape slider, and watch for changes
        /// </summary>
		internal void GuiSliderControlls(int i)
		{
			var smrName = guiSkinnedMeshRenderers[i].name;
			//Find the index of the Preg+ blendshape
			var kkBsIndex = GetBlendShapeIndexFromName(guiSkinnedMeshRenderers[i].sharedMesh);
			if (kkBsIndex < 0) return;

			//Create a slider for the matching Preg+ blendshape
			GUILayout.BeginHorizontal(new GUILayoutOption[0]);
			GUILayout.Label(guiSkinnedMeshRenderers[i].sharedMesh.GetBlendShapeName(kkBsIndex), lastTouched == i ? _labelTitleActiveStyle : _labelTitleStyle, new GUILayoutOption[0]);
			_sliderValues[smrName] = GUILayout.HorizontalSlider(_sliderValues[smrName], 0f, 100f, new GUILayoutOption[0]);
			GUILayout.Label(_sliderValues[smrName].ToString("#0"), _labelValueStyle, new GUILayoutOption[0]);
			GUILayout.EndHorizontal();

			//Only update slider on changes
			if (_sliderValues[smrName] != _sliderValuesHistory[smrName]) 
			{					
				lastTouched = i;
				try 
				{					
					HSPEExists = SetHspeBlendShapeWeight(guiSkinnedMeshRenderers[i], kkBsIndex, _sliderValues[smrName]);
				}	
				catch (Exception e)
				{
					//If KKPE does not exists catch the error
					if (PregnancyPlusPlugin.debugLog)  PregnancyPlusPlugin.Logger.LogInfo($" SetHspeBlendShapeWeight > HSPE not found {e.Message} ");
					HSPEExists = false;
				}

				if (!HSPEExists) 
				{
					//If not found adjust the weight the normal way, that won't integrate with VNGE, or Timeline
					guiSkinnedMeshRenderers[i].SetBlendShapeWeight(kkBsIndex, _sliderValues[smrName]);
				}				
			}
			_sliderValuesHistory[smrName] = _sliderValues[smrName];
		}


		/// <summary>
        /// Closes the window and resets HSPE blendshapes
        /// </summary>
		internal void CloseWindow()
		{
			blendShapeWindowShow = false;
			try 
			{
				ResetHspeBlendShape(guiSkinnedMeshRenderers);
			}	
			catch (Exception e)
			{
				if (PregnancyPlusPlugin.debugLog)  PregnancyPlusPlugin.Logger.LogInfo($" ResetHspeBlendShape > HSPE not found {e.Message} ");
			}
			guiSkinnedMeshRenderers = new List<SkinnedMeshRenderer>();
			lastTouched = -1;
		}


        /// <summary>
        /// Initilize sliders on first window open, or smrs appended
        /// </summary>
		public void OnGuiInit(List<SkinnedMeshRenderer> smrs) 
		{
			_sliderValues = BuildSliderListValues(smrs, _sliderValues);
			_sliderValuesHistory = BuildSliderListValues(smrs, _sliderValuesHistory);
		}


		public void ClearAllSliderValues() 
		{
			_sliderValues = BuildSliderListValues(guiSkinnedMeshRenderers, _sliderValues);
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
		internal Dictionary<string, float> BuildSliderListValues(List<SkinnedMeshRenderer> smrs, Dictionary<string, float> sliderValues) 
		{
			if (smrs == null || smrs.Count <= 0 ) return new Dictionary<string, float>();
			if (sliderValues == null) sliderValues = new Dictionary<string, float>();

			//For each smr get the smr key and the starting slider value
			foreach (var smr in smrs)
			{
				sliderValues[smr.name] = 0;
			}

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
				if (name.Contains(searchName)) return i;
			}

			return -1;
		}


        /// <summary>
        /// Have to manually update the blendshape slider in the HSPE window in order for Timeline, or VNGE to detect the change
		/// They don't automagically watch for mesh.blendshape changes
        /// </summary>
		/// <returns>Will return True if HSPE was found</returns>
		internal bool SetHspeBlendShapeWeight(SkinnedMeshRenderer smr, int index, float weight) 
        {
			var bsModule = GetHspeBlenShapeModule();
			if (bsModule == null) return false;

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

			// ResetHspeBlendShape(bsModule, smr, index);

			// var dynMethod = bsModule.GetType().GetMethod("SetBlendShapeWeight", BindingFlags.NonPublic | BindingFlags.Instance);
			// dynMethod.Invoke(this, new object[] { smr , index, weight });
        }

		/// <summary>
        /// Reset HSPE blendshape when character changes
        /// </summary>
		/// <returns>Will return True if HSPE was found</returns>
		internal bool ResetHspeBlendShape(List<SkinnedMeshRenderer> smrs) 
        {
			if (smrs == null || smrs.Count <= 0) return true;

			var bsModule = GetHspeBlenShapeModule();
			if (bsModule == null) return false;

			//Set the following values as if the HSPE blendshape tab was clicked
			Traverse.Create(bsModule).Field("_lastEditedBlendShape").SetValue(-1);

			//Set the blend shape weight in HSPE for a specific smr, (Finally working............)
			var SetMeshRendererNotDirty = bsModule.GetType().GetMethod("SetMeshRendererNotDirty", BindingFlags.NonPublic | BindingFlags.Instance);
			if (SetMeshRendererNotDirty == null) return false;

			//reset all active smrs in HSPE
			foreach(var smr in smrs)
			{	
				if (smr == null) continue;
				SetMeshRendererNotDirty.Invoke(bsModule, new object[] { smr } );	
			}

			return true;
        }

		/// <summary>
        /// Get the active HSPE blend shape module, that we want to make alterations to
        /// </summary>
		internal HSPE.AMModules.BlendShapesEditor GetHspeBlenShapeModule()
		{
			//Get main HSPE window reference
			var hspeMainWindow = _pluginInstance.gameObject.GetComponent<HSPE.MainWindow>();
			if (hspeMainWindow == null) return null;

			//Pose target contains the character main window buttons
			var poseCtrl = Traverse.Create(hspeMainWindow).Field("_poseTarget").GetValue<HSPE.PoseController>();
			if (poseCtrl == null) return null;

			//The modules are indivisual popups originating from the pose target window
			var advModules = Traverse.Create(poseCtrl).Field("_modules").GetValue<List<HSPE.AMModules.AdvancedModeModule>>();
			if (advModules == null || advModules.Count <= 0) return null;

			//Get the blendShape module  (4 == blendshape.type, or just use the string name)
			var bsModule = (HSPE.AMModules.BlendShapesEditor)advModules.FirstOrDefault(x => x.displayName.Contains("Blend Shape"));
			if (bsModule == null) return null;

			return bsModule;
		
		}

    }

}