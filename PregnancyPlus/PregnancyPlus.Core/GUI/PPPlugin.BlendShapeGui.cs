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
		public static bool windowShow = false;
		internal static bool guiInit = true;
		internal Dictionary<string, float> _sliderValues = new Dictionary<string, float>();//Tracks user modified blendshape slider values


		internal void OnGUI()
		{
			bool flag = windowShow;
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
			windowShow = !windowShow;//Trigger gui to show
			if (PregnancyPlusPlugin.debugLog)  PregnancyPlusPlugin.Logger.LogInfo($" windowShow {windowShow}");
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
			if (guiInit)
			{
				//Initilize slider values
				_sliderValues = BuildSliderListValues(bodySkinnedMeshRenderers);
				guiInit = false;
			}
			GUILayout.Box("", new GUILayoutOption[]
			{
				GUILayout.Width(450f),
				GUILayout.Height((float)(15 * (bodySkinnedMeshRenderers.Count + 3)))
			});

			//Set the size of the interactable area
			Rect screenRect = new Rect(10f, 25f, 450f, (float)(15 * (bodySkinnedMeshRenderers.Count + 3)));
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

			var btnCLicked = GUILayout.Button("Close", new GUILayoutOption[0]);
			GUILayout.EndArea();			
			GUI.DragWindow();

			if (btnCLicked) windowShow = false;
		}

		internal Dictionary<string, float> BuildSliderListValues(List<SkinnedMeshRenderer> smrs) 
		{
			//For each smr get the smr key and the starting slider value
			foreach (var smr in smrs)
			{
				_sliderValues[smr.name] = 0;
			}

			return _sliderValues;
		}

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