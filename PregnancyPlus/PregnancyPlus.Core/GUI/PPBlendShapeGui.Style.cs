using UnityEngine;

namespace KK_PregnancyPlus
{
    public partial class PregnancyPlusBlendShapeGui
    {

        internal GUIStyle _labelTitleStyle = new GUIStyle
        {
            fixedWidth = 275f,
            alignment = TextAnchor.MiddleRight,
			padding = new RectOffset(0, 0, 3, 3),
            normal = new GUIStyleState
            {
                textColor = Color.white
            }
        };


        internal GUIStyle _labelTitleActiveStyle = new GUIStyle
        {
            fixedWidth = 275f,
            alignment = TextAnchor.MiddleRight,
			padding = new RectOffset(0, 0, 3, 3),
            normal = new GUIStyleState
            {
                textColor = Color.green
            }
        };


		internal GUIStyle _labelTextStyle = new GUIStyle
        {
            alignment = TextAnchor.MiddleLeft,
			padding = new RectOffset(5, 5, 20, 20),
			wordWrap = true,
            normal = new GUIStyleState
            {
                textColor = Color.white
            }
        };


		internal GUIStyle _labelErrorTextStyle = new GUIStyle
        {
            alignment = TextAnchor.MiddleLeft,
			padding = new RectOffset(5, 5, 5, 5),
			wordWrap = true,
            normal = new GUIStyleState
            {
                textColor = new Color(1, 0.2f, 0.2f, 1)
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
            padding = new RectOffset(0, 5, 0, 0),
            normal = new GUIStyleState
            {
                textColor = Color.white
            }
        };


		internal GUIStyle _btnValueStyle = new GUIStyle
        {
			margin=new RectOffset(10,100,20,50)
        };


		public float GetGuiHeight(bool hasBlendShapes)
		{			
			//When blendshapes are set, include sliders height
			if (hasBlendShapes)
			{
				var sliderTotals = ((15 + (_labelTitleStyle.padding.bottom * 2)) * guiSkinnedMeshRenderers.Count);
				var textsTotals = (_labelTextStyle.padding.bottom * 2) + (30 * 4);
				var btnTotals = (40 * 3);
                var errorTotals = HSPEExists ? 0 : 30;
                errorTotals = anyMeshEmpty ? errorTotals + (30 * 3) : errorTotals;
                
				return (sliderTotals +  textsTotals + btnTotals + errorTotals);
			}
			//Otherwise, its just text and buttons
			else 
			{
				var textsTotals = (_labelTextStyle.padding.bottom * 2) + (30 * 2);
				var btnTotals = (40 * 2);
				return (textsTotals + btnTotals); 
			}
		}

    }
}