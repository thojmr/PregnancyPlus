using UnityEngine;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace KK_PregnancyPlus
{
    public partial class PregnancyPlusPlugin
    {
		public static List<SkinnedMeshRenderer> bodySkinnedMeshRenderers;
		internal int windowId = 7639;
		internal Rect windowRect = new Rect((float)(Screen.width - 450), (float)(Screen.height / 2 - 50), 250f, 15f);
		public static bool blendShapeWindowShow = false;
		internal static bool guiInit = true;
		internal Dictionary<string, float> _sliderValues = new Dictionary<string, float>();//Tracks user modified blendshape slider values
		//When not -1, sets the value of all the sliders
		internal float allBsSliderValue = -1f;


		internal void OnGUI()
		{
			bool flag = blendShapeWindowShow;
			if (flag)
			{
				//Show GUI when true
				GUI.backgroundColor = Color.black;
				windowRect = GUILayout.Window(windowId, windowRect, new GUI.WindowFunction(WindowFunc), "Pregnancy+ Blendshapes", new GUILayoutOption[0]);
			}
		}


		//On constructor call, show the blendshape GUI
		internal static void OpenBlendShapeGui(List<SkinnedMeshRenderer> smrs) 
		{
			bodySkinnedMeshRenderers = smrs;
			guiInit = true;
			blendShapeWindowShow = !blendShapeWindowShow;//Trigger gui to show
			if (PregnancyPlusPlugin.debugLog)  PregnancyPlusPlugin.Logger.LogInfo($" blendShapeWindowShow {blendShapeWindowShow}");
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
			if (bodySkinnedMeshRenderers == null || bodySkinnedMeshRenderers[0] == null) 
			{
				blendShapeWindowShow = false;
				return;
			}

			if (guiInit)
			{
				//Initilize slider values
				_sliderValues = BuildSliderListValues(bodySkinnedMeshRenderers);
				allBsSliderValue = -1;
				guiInit = false;
			}						

			GUILayout.Box("", new GUILayoutOption[]
			{
				GUILayout.Width(450f),
				GUILayout.Height((float)(15 * (bodySkinnedMeshRenderers.Count + 4)))
			});

			//Set the size of the interactable area
			Rect screenRect = new Rect(10f, 25f, 450f, (float)(15 * (bodySkinnedMeshRenderers.Count + 4)));
			GUILayout.BeginArea(screenRect);

			//For each SMR we want to sear for Preg+ blendshapes
			for (int i = 0; i < bodySkinnedMeshRenderers.Count; i++)
			{				
				var smrName = bodySkinnedMeshRenderers[i].name;
				//Find the index of the Preg+ blendshape
				var kkBsIndex = GetBlendShapeIndexFromName(bodySkinnedMeshRenderers[i].sharedMesh);
				if (kkBsIndex < 0) continue;

				//Create a slider for the matching Preg+ blendshape
				GUILayout.BeginHorizontal(new GUILayoutOption[0]);
				GUILayout.Label(bodySkinnedMeshRenderers[i].sharedMesh.GetBlendShapeName(kkBsIndex), _labelTitleStyle, new GUILayoutOption[0]);
				_sliderValues[smrName] = GUILayout.HorizontalSlider(_sliderValues[smrName], 0f, 100f, new GUILayoutOption[0]);
				GUILayout.Label(_sliderValues[smrName].ToString("#0"), _labelValueStyle, new GUILayoutOption[0]);
				GUILayout.EndHorizontal();
				bodySkinnedMeshRenderers[i].SetBlendShapeWeight(kkBsIndex, _sliderValues[smrName]);
			}	

			//Reset back to normal, when any single slider changes value
			if (BlendShapeSliderValuesChanged(_sliderValues)) allBsSliderValue = -1;

			//Set the All sliders slider
			GUILayout.BeginHorizontal(new GUILayoutOption[0]);
			GUILayout.Label("All Pregnancy+ BlendShapes", _labelAllTitleStyle, new GUILayoutOption[0]);
			allBsSliderValue = GUILayout.HorizontalSlider(allBsSliderValue, 0f, 100f, new GUILayoutOption[0]);
			GUILayout.Label(allBsSliderValue.ToString("#0"), _labelValueStyle, new GUILayoutOption[0]);
			GUILayout.EndHorizontal();		

			//When selected update all sliders to the same value
			if (allBsSliderValue >= 0) 
			{
				for (int i = 0; i < bodySkinnedMeshRenderers.Count; i++)
				{
					var smrName = bodySkinnedMeshRenderers[i].name;
					//Find the index of the Preg+ blendshape
					var kkBsIndex = GetBlendShapeIndexFromName(bodySkinnedMeshRenderers[i].sharedMesh);
					if (kkBsIndex < 0) continue;
					bodySkinnedMeshRenderers[i].SetBlendShapeWeight(kkBsIndex, allBsSliderValue);
					//Update indivisual values to the same number
					_sliderValues[smrName] = allBsSliderValue;
				}
			}

			var btnCLicked = GUILayout.Button("Close", new GUILayoutOption[0]);
			GUILayout.EndArea();			
			GUI.DragWindow();
			
			if (btnCLicked) blendShapeWindowShow = false;
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
		internal Dictionary<string, float> BuildSliderListValues(List<SkinnedMeshRenderer> smrs) 
		{
			//For each smr get the smr key and the starting slider value
			foreach (var smr in smrs)
			{
				_sliderValues[smr.name] = 0;
			}

			return _sliderValues;
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

    }

}