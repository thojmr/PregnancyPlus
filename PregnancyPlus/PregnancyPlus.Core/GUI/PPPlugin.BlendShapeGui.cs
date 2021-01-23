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
    public partial class PregnancyPlusPlugin
    {
		public static List<SkinnedMeshRenderer> guiSkinnedMeshRenderers;
		public const int guiWindowId = 7639;
		public Rect windowRect = new Rect((float)(Screen.width - 450), (float)(Screen.height / 2 - 50), 250f, 15f);
		public static bool blendShapeWindowShow = false;
		public static bool guiInit = true;
		public Dictionary<string, float> _sliderValues = new Dictionary<string, float>();//Tracks user modified blendshape slider values
		public Dictionary<string, float> _sliderValuesHistory = new Dictionary<string, float>();//Tracks user modified blendshape slider values
		//When not -1, sets the value of all the sliders
		public float allBsSliderValue = -1f;


		internal void OnGUI()
		{
			bool flag = blendShapeWindowShow;
			if (flag)
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


		//On constructor call, show the blendshape GUI
		internal static void OpenBlendShapeGui(List<SkinnedMeshRenderer> smrs) 
		{
			guiSkinnedMeshRenderers = smrs;
			guiInit = true;
			blendShapeWindowShow = !blendShapeWindowShow;//Trigger gui to show
			if (PregnancyPlusPlugin.debugLog)  PregnancyPlusPlugin.Logger.LogInfo($" blendShapeWindowShow {blendShapeWindowShow}");
		}


		internal static void CloseBlendShapeGui() 
		{
			blendShapeWindowShow = false;
		}


        internal GUIStyle _labelTitleStyle = new GUIStyle
        {
            fixedWidth = 275f,
            alignment = TextAnchor.MiddleRight,
            normal = new GUIStyleState
            {
                textColor = Color.white
            }
        };


		internal GUIStyle _labelAllTitleStyle = new GUIStyle
        {
            fixedWidth = 275f,
            alignment = TextAnchor.MiddleRight,
            normal = new GUIStyleState
            {
                textColor = Color.green
            }
        };


        internal GUIStyle _labelValueStyle = new GUIStyle
        {
            fixedWidth = 25f,
            alignment = TextAnchor.MiddleRight,
            normal = new GUIStyleState
            {
                textColor = Color.white
            }
        };

		internal GUIStyle _btnValueStyle = new GUIStyle
        {
			margin=new RectOffset(10,100,20,50)
        };


		internal void WindowFunc(int id)
		{
			//Exit when mesh becomes null (probably deleted character)
			if (guiSkinnedMeshRenderers == null || guiSkinnedMeshRenderers[0] == null) 
			{
				blendShapeWindowShow = false;
				return;
			}

			//Initilize slider values
			if (guiInit)
			{				
				OnGuiInit();
			}		

			var lastAllBsSliderValue = allBsSliderValue;

			GUILayout.Box("", new GUILayoutOption[]
			{
				GUILayout.Width(450f),
				GUILayout.Height((float)(15 * (guiSkinnedMeshRenderers.Count + 5)))
			});

			//Set the size of the interactable area
			Rect screenRect = new Rect(10f, 25f, 450f, (float)(15 * (guiSkinnedMeshRenderers.Count + 5)));
			GUILayout.BeginArea(screenRect);

			//For each SMR we want a slider for
			for (int i = 0; i < guiSkinnedMeshRenderers.Count; i++)
			{				
				var smrName = guiSkinnedMeshRenderers[i].name;
				//Find the index of the Preg+ blendshape
				var kkBsIndex = GetBlendShapeIndexFromName(guiSkinnedMeshRenderers[i].sharedMesh);
				if (kkBsIndex < 0) continue;

				//Create a slider for the matching Preg+ blendshape
				GUILayout.BeginHorizontal(new GUILayoutOption[0]);
				GUILayout.Label(guiSkinnedMeshRenderers[i].sharedMesh.GetBlendShapeName(kkBsIndex), _labelTitleStyle, new GUILayoutOption[0]);
				_sliderValues[smrName] = GUILayout.HorizontalSlider(_sliderValues[smrName], 0f, 100f, new GUILayoutOption[0]);
				GUILayout.Label(_sliderValues[smrName].ToString("#0"), _labelValueStyle, new GUILayoutOption[0]);
				GUILayout.EndHorizontal();

				//Only update slider on changes
				if (_sliderValues[smrName] != _sliderValuesHistory[smrName]) 
				{					
					// guiSkinnedMeshRenderers[i].SetBlendShapeWeight(kkBsIndex, _sliderValues[smrName]);
					SetHspeBlendShapeWeight(guiSkinnedMeshRenderers[i], kkBsIndex, _sliderValues[smrName]);					
				}
				_sliderValuesHistory[smrName] = _sliderValues[smrName];
			}	

			// //Reset back to normal, when any single slider changes value
			// if (BlendShapeSliderValuesChanged(_sliderValues)) allBsSliderValue = -1;

			// //Set the All sliders slider
			// GUILayout.BeginHorizontal(new GUILayoutOption[0]);
			// GUILayout.Label("All Pregnancy+ BlendShapes", _labelAllTitleStyle, new GUILayoutOption[0]);
			// allBsSliderValue = GUILayout.HorizontalSlider(allBsSliderValue, 0f, 100f, new GUILayoutOption[0]);
			// GUILayout.Label(allBsSliderValue.ToString("#0"), _labelValueStyle, new GUILayoutOption[0]);
			// GUILayout.EndHorizontal();		

			// //When selected update all sliders to the same value
			// if (allBsSliderValue >= 0 && lastAllBsSliderValue != allBsSliderValue) 
			// {
			// 	for (int i = 0; i < guiSkinnedMeshRenderers.Count; i++)
			// 	{
			// 		var smrName = guiSkinnedMeshRenderers[i].name;
			// 		//Find the index of the Preg+ blendshape
			// 		var kkBsIndex = GetBlendShapeIndexFromName(guiSkinnedMeshRenderers[i].sharedMesh);
			// 		if (kkBsIndex < 0) continue;
					
			// 		// guiSkinnedMeshRenderers[i].SetBlendShapeWeight(kkBsIndex, allBsSliderValue);
			// 		SetHspeBlendShapeWeight(guiSkinnedMeshRenderers[i], kkBsIndex, _sliderValues[smrName]);					

			// 		//Update indivisual values to the same number
			// 		_sliderValues[smrName] = allBsSliderValue;
			// 		_sliderValuesHistory[smrName] = allBsSliderValue;
			// 	}
			// }


			var clearBtnCLicked = GUILayout.Button("Clear", new GUILayoutOption[0]);
			var closeBtnCLicked = GUILayout.Button("Close", new GUILayoutOption[0]);
			GUILayout.EndArea();			
			GUI.DragWindow();
			
			if (closeBtnCLicked) blendShapeWindowShow = false;
			if (clearBtnCLicked) ClearAllSliderValues();
		}


        /// <summary>
        /// Initilize sliders on first window open
        /// </summary>
		public void OnGuiInit() 
		{
			_sliderValues = BuildSliderListValues(guiSkinnedMeshRenderers, _sliderValues);
			_sliderValuesHistory = BuildSliderListValues(guiSkinnedMeshRenderers, _sliderValuesHistory);
			allBsSliderValue = -1;
			guiInit = false;
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


		// public void OpenHspeBlendShapeGui()
		// {
		// 	var hspeMainWindow = this.gameObject.GetComponent<MainWindow>();
		// 	if (hspeMainWindow == null) return;

		// 	var poseCtrl = Traverse.Create(hspeMainWindow).Field("_poseTarget").GetValue<PoseController>();
		// 	if (poseCtrl == null) return;

		// 	var advModules = Traverse.Create(poseCtrl).Field("_modules").GetValue<List<HSPE.AMModules.AdvancedModeModule>>();
		// 	if (advModules == null || advModules.Count <= 0) return;

		// 	//Get the blendShape module  (4 == blendshape)
		// 	var bsModule = (HSPE.AMModules.BlendShapesEditor)advModules.FirstOrDefault(x => x.displayName.Contains("Blend Shape"));
		// 	if (bsModule != null) return;
		// }


        /// <summary>
        /// Have to manually update the blendshape slider in the HSPE window in order for Timeline, or VNGE to detect the change
		/// They don't automagically watch for mesh.blendshape changes
        /// </summary>
		internal void SetHspeBlendShapeWeight(SkinnedMeshRenderer smr, int index, float weight) 
        {
			//Get main HSPE window reference
			var hspeMainWindow = this.gameObject.GetComponent<MainWindow>();
			if (hspeMainWindow == null) return;

			//Pose target contains the character main window buttons
			var poseCtrl = Traverse.Create(hspeMainWindow).Field("_poseTarget").GetValue<PoseController>();
			if (poseCtrl == null) return;

			//The modules are indivisual popups originating from the pose target window
			var advModules = Traverse.Create(poseCtrl).Field("_modules").GetValue<List<HSPE.AMModules.AdvancedModeModule>>();
			if (advModules == null || advModules.Count <= 0) return;

			//Get the blendShape module  (4 == blendshape.type, or just use the string name)
			var bsModule = (HSPE.AMModules.BlendShapesEditor)advModules.FirstOrDefault(x => x.displayName.Contains("Blend Shape"));
			if (bsModule == null) return;

			//Set the following values as if the HSPE blendshape tab was clicked
			Traverse.Create(bsModule).Field("_skinnedMeshTarget").SetValue(smr);
			Traverse.Create(bsModule).Field("_lastEditedBlendShape").SetValue(index);

			//Set the blend shape weight in HSPE for a specific smr, (Finally working............)
			var SetBlendShapeWeight = bsModule.GetType().GetMethod("SetBlendShapeWeight", BindingFlags.NonPublic | BindingFlags.Instance);
			if (SetBlendShapeWeight == null) return;
        	SetBlendShapeWeight.Invoke(bsModule, new object[] { smr, index, weight} );

			//Set last changed smr slider to be visibly active in HSPE
			var SetMeshRendererDirty = bsModule.GetType().GetMethod("SetMeshRendererDirty", BindingFlags.NonPublic | BindingFlags.Instance);
			if (SetMeshRendererDirty == null) return;
        	SetMeshRendererDirty.Invoke(bsModule, new object[] { smr } );

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

    }

}