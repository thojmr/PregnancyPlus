using UnityEngine;
using KKAPI.Studio;
using KKAPI.Studio.UI;
using KKAPI.Maker.UI;
using UnityEngine.UI;
using TMPro;

namespace KK_PregnancyPlus
{
    //This partial class contains all the button timer logic for incremening the text timer on a GUI button
    public static partial class PregnancyPlusGui
    {

        //Appends seonds count to blendshape smoothing text operation button.  Starts incrementing timer when >= 0
        internal static int secondsCount = -1;
        internal static float timePassed = 0f;


        //For updating UI text over time (place in MonoBehavior Update())
        public static void Update()
        {
            //When incrementing text count, add 1 every second
            if (secondsCount >= 0)
            {
                //Keep track of the seconds passed
                timePassed += Time.deltaTime;

                if (timePassed > 1f)
                {
                    //Update text on the smoothing button every second
                    UpdateTextComponentText();

                    secondsCount += 1;
                    timePassed = 0f;
                }
            }
        }


        /// <summary>
        /// Start the button text timer increment
        /// </summary>
        public static void StartTextCountIncrement()
        {
            if (PregnancyPlusPlugin.DebugLog.Value)  PregnancyPlusPlugin.Logger.LogInfo($" StartTextCountIncrement"); 

            if (secondsCount >= 0) return;//Already started

            timePassed = 0f;
            secondsCount = 0;

            //Sets text on the smoothing button
            UpdateTextComponentText();
        }


        /// <summary>
        /// Stop and Reset the button text timer
        /// </summary>
        public static void StopTextCountIncrement()
        {
            if (PregnancyPlusPlugin.DebugLog.Value)  PregnancyPlusPlugin.Logger.LogInfo($" StopTextCountIncrement"); 

            secondsCount = -1;
            timePassed = 0f;

            UpdateTextComponentText();            
        }


        /// <summary>
        /// Set or Reset the timer on the smoothing button
        /// </summary>
        internal static void UpdateTextComponentText()
        {
            //Set or Reset text on the smoothing button
            if (!StudioAPI.InsideStudio)
            {
                UpdateMakerTextComponentText(PregnancyPlusGui.smoothBtn, secondsCount);
            }
            else
            {
                UpdateStudioTextComponentText(cat, PregnancyPlusGui.smoothBellyMeshText, secondsCount);
            } 
        }


        /// <summary>
        /// When smoothing the mesh, update the text component with the current elapsed seconds (Studio)
        ///     We need extra params to uniquely identify a Studio button by its text, compared to Maker
        /// </summary>
        internal static void UpdateStudioTextComponentText(CurrentStateCategory cat, string name, int count)
        {             
            if (cat == null) return;

            //Get the unity text component by name
            var unityText = UnityUiTools.GetTextComponent(cat, name);
            if (unityText == null) return;
            
            var newText = IncrementText(unityText.text, count);
            if (newText != null) unityText.text = newText;
        }


        /// <summary>
        /// When smoothing the mesh, update the text component with the current elapsed seconds (Maker)
        /// </summary>
        internal static void UpdateMakerTextComponentText(MakerButton smoothBtn, int count)
        {             
            //Get the unity text component by name
            var unityTextMesh = smoothBtn?.ControlObject?.GetComponentInChildren<TextMeshProUGUI>();
            //If no TextMeshProUGUI component found, try and find a regular Text component
            var unityText = smoothBtn?.ControlObject?.GetComponentInChildren<Text>();

            if (unityText == null && unityTextMesh == null) return;
            
            //Modify the original text
            var originalText = unityTextMesh ? unityTextMesh.text : unityText.text;
            var newText = IncrementText(originalText, count);

            if (unityTextMesh != null && newText != null) unityTextMesh.text = newText;
            if (unityText != null && newText != null) unityText.text = newText;            
        }


        /// <summary>
        /// Increment the Text.text value to the current count, or reset it to default
        /// </summary>
        internal static string IncrementText(string currentText, int count)
        {
            var newText = "";
            //Inject string time counter when needed
            var timeIndex = currentText.IndexOf(" (");

            if (count < 0 && timeIndex > 0)
            {
                //Reset the text to default
                newText = currentText.Substring(0, timeIndex);
            }
            else if (count < 0)
            {
                //No changes needed since the counter has not been set
                return currentText;
            }
            else if (timeIndex > 0)
            {
                //Increment the text
                newText = currentText.Substring(0, timeIndex) + " (" + count.ToString() + "s)";
            }
            else 
            {
                //Start the text timer
                newText = currentText + " (0s)";
            } 

            return newText;
        }
    }
}
