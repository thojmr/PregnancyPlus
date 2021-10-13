using KKAPI.Studio.UI;
using UnityEngine.UI;
using TMPro;


namespace KK_PregnancyPlus
{
    // These tools are used to get and set Unity input buttons in Studio and Maker
    public static class UnityUiTools
    {

        /// <summary>
        /// Get a Unity Text component from the Studio category
        /// </summary>
        #if HS2 || AI

            public static TextMeshProUGUI GetTextComponent(CurrentStateCategory cat, string inputName)
            {
                if (cat == null) return null;

                //For each ui item in this category check for name match
                foreach (CurrentStateCategorySubItemBase subItem in cat.SubItems) 
                {
                    if (!subItem.Created) continue;
                    var itemGo = subItem.RootGameObject;                
                    var textComps = itemGo.GetComponentsInChildren<TextMeshProUGUI>();

                    //For each text component check name match
                    foreach(var textComp in textComps) 
                    {
                        if (textComp.name == "Text " + inputName) return textComp;
                    }

                }

                return null;
            }
            
        #elif KK

            public static Text GetTextComponent(CurrentStateCategory cat, string inputName)
            {
                if (cat == null) return null;

                //For each ui item in this category check for name match
                foreach (CurrentStateCategorySubItemBase subItem in cat.SubItems) 
                {
                    if (!subItem.Created) continue;
                    var itemGo = subItem.RootGameObject;                
                    var textComps = itemGo.GetComponentsInChildren<Text>();

                    //For each text component check name match
                    foreach(var textComp in textComps) 
                    {
                        if (textComp.name == "Text " + inputName) return textComp;
                    }

                }

                return null;
            }
        #endif


        /// <summary>
        /// Set a Unity Text component text from a studio category
        /// </summary>
        public static void SetTextComponentText(CurrentStateCategory cat, string name, string newText)
        {             
            if (cat == null) return;

            //Get the unity text component by name
            var unityTextComp = GetTextComponent(cat, name);
            if (unityTextComp == null) return;
            
            //Inject string time counter when needed
            unityTextComp.text = newText;       
        }


        /// <summary>
        /// Reset a single studio slider
        /// </summary>
        internal static void ResetSlider(CurrentStateCategory cat, string sliderName, float resetTo = 0) 
        {
            if (cat == null) return;

            //For each ui item check if its a slider
            foreach(CurrentStateCategorySubItemBase subItem in cat.SubItems) 
            {
                if (!subItem.Created) continue;
                var itemGo = subItem.RootGameObject;
                var sliders = itemGo.GetComponentsInChildren<Slider>();

                //For each slider component (should just be one per subItem) set to 0
                foreach(var slider in sliders) 
                {
                    if (slider.name == "Slider " + sliderName) slider.value = resetTo;
                }
            }
        }


        /// <summary>
        /// Reset all studio sliders to 0
        /// </summary>
        internal static void ResetAllSliders(CurrentStateCategory cat, float resetTo = 0) 
        {
            if (cat == null) return;

            //For each ui item check if its a slider
            foreach(CurrentStateCategorySubItemBase subItem in cat.SubItems) 
            {
                if (!subItem.Created) continue;
                var itemGo = subItem.RootGameObject;
                var sliders = itemGo.GetComponentsInChildren<Slider>();

                //For each slider component (should just be one per subItem) set to 0
                foreach(var slider in sliders) 
                {
                    slider.value = resetTo;
                }
            }
        }


        
        /// <summary>
        /// Reset a toggle (Breaks the game currently lol)
        /// </summary>
        internal static void ResetToggle(CurrentStateCategory cat, string toggleName, bool desiredState = false) 
        {
            if (cat == null) return;

            //For each ui item check if its a toggle
            foreach(CurrentStateCategorySubItemBase subItem in cat.SubItems) 
            {
                if (!subItem.Created || !subItem.Name.Contains(toggleName)) continue;
                
                var itemGo = subItem.RootGameObject;
                var sliders = itemGo.GetComponentsInChildren<Toggle>();

                //For each toggle item (should just be one per subItem), set the desited state
                foreach(var slider in sliders) 
                {
                    slider.isOn = desiredState;
                }
            }
        }


    }
}