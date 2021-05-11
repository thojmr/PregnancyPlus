namespace KK_PregnancyPlus
{
    //These are the flags needed to determine whether we need to compute a mesh shape, and or how to do it
    public class MeshInflateFlags 
    {
        //Check for any newly added meshes
        public bool checkForNewMesh = false;
        //Start from scratch, recalculating all meshes
        public bool freshStart = false;
        //When plugin sliders change, need to recalculate shape
        public bool pluginConfigSliderChanged = false;
        //When a character becomes visible in maian game mode, re-apply shape
        public bool visibilityUpdate = false;
        //Allows calling mesh inflate with 0 inflationSize.  Used to pre compute the current shape now, for faster performance later
        public bool bypassWhen0 = false;        
        public bool reMeasure = false;
        internal PregnancyPlusData infConfig = null;
        internal PregnancyPlusData infConfigHistory = null;


        //** Computed props below**


        //When any slider values have actually changed, we need to recompute the belly shape
        public bool SliderHaveChanged 
        {
            get 
            {
                if (pluginConfigSliderChanged) return true;            
                //See if any slider value differences 
                return infConfig.Equals(infConfigHistory);
            }
        }

        //When only the InflationSize slider has changed.  We don't need to recompute the shape, just alter the blendhsape weight
        public bool OnlyInflationSizeChanged 
        {
            get 
            { 
                if (pluginConfigSliderChanged) return true;
                return infConfig.InflationSizeOnlyChange(infConfigHistory);   
            }
        }
        
        //Whether we need to overwrite the mesh blendshape with a new shape
        public bool OverWriteMesh 
        {            
            get { return (!OnlyInflationSizeChanged && SliderHaveChanged) || freshStart; }
        }

        // Determine if we need to even start MeshInflate process     
        public bool NeedsToRun
        {
            get 
            {   
                if (!SliderHaveChanged && !visibilityUpdate && !bypassWhen0) 
                {
                    //Only stop here, if no recalculation needed
                    if (!freshStart && !checkForNewMesh)  return false; 
                }
                return true;
            }
        }




        /// <summary>
        /// Pass infConfig values to constructor to be used later in slider value comparisons
        /// </summary>
        public MeshInflateFlags(PregnancyPlusCharaController ppcc, bool _checkForNewMesh = false, bool _freshStart = false, bool _pluginConfigSliderChanged = false, 
                                bool _visibilityUpdate = false, bool _bypassWhen0 = false, bool _reMeasure = false) 
        {
            infConfig = ppcc.infConfig;
            infConfigHistory = ppcc.infConfigHistory;

            checkForNewMesh = _checkForNewMesh;
            freshStart = _freshStart;
            pluginConfigSliderChanged = _pluginConfigSliderChanged;
            visibilityUpdate = _visibilityUpdate;
            bypassWhen0 = _bypassWhen0;
            reMeasure = _reMeasure;
        }


        public void Log() 
        {
            //When a flag is true, log it
            var fieldsLogString = "";
            if (checkForNewMesh) fieldsLogString += $" checkForNewMesh T,";
            if (freshStart) fieldsLogString += $" freshStart T,";
            if (pluginConfigSliderChanged) fieldsLogString += $" pluginConfigSliderChanged T,";
            if (visibilityUpdate) fieldsLogString += $" visibilityUpdate T,";
            if (bypassWhen0) fieldsLogString += $" bypassWhen0 T,";
            if (reMeasure) fieldsLogString += $" reMeasure T,";

            var propsLogString = "";
            if (SliderHaveChanged) propsLogString += $" SliderHaveChanged T,";
            if (OnlyInflationSizeChanged) propsLogString += $" OnlyInflationSizeChanged T,";
            if (OverWriteMesh) propsLogString += $" OverWriteMesh T,";

            if (PregnancyPlusPlugin.DebugLog.Value && fieldsLogString != "")  PregnancyPlusPlugin.Logger.LogInfo($" MeshInflateFlags >{fieldsLogString}");
            if (PregnancyPlusPlugin.DebugLog.Value && propsLogString != "")  PregnancyPlusPlugin.Logger.LogInfo($" MeshInflateFlags computed >{propsLogString}");
        }

    }
}