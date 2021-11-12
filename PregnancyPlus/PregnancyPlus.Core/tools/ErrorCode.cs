using System.Collections.Generic;
using BepInEx.Logging;

namespace KK_PregnancyPlus
{
    //Possible Preg+ errors we want to look for in output.logs
    public enum ErrorCode
    {
        PregPlus_MeshNotReadable,//(This is probably depreciated now) When the mesh is marked as isReadable == false, we can't read or modify the mesh.
        PregPlus_IncorrectVertCount,//When the current mesh vert count does not match the stored mesh vert count.  The mesh was swaped out.
        PregPlus_BadMeasurement,//When a part of the character fails to take measurement needed for belly placement.
        PregPlus_HSPENotFound,//When HSPE plugin is not found while using blendshapes (It's not a hard dependency, but still good to know when its not included)
        PregPlus_BodyMeshDisguisedAsCloth,//When a body mesh is detected that is nested under a cloth Game Object (like Squeeze Socks)
        PregPlus_BodyMeshVertexChanged,//When a saved blendshape tries to loadd, but the mesh is no longer the same and can't load
        PregPlus_BodyUncensorChanged,//When a saved blendshape tries to load, but the uncensor body no longer matches, so it can't
        PregPlus_NoMeshRootFound,//For some reason in RX11 KK, the mesh root bone name changed . '_low' was appended in free roam.  Catch these changes if they happen again
    }

    /// <summary>
    /// I needed better error tracking from user reports. Errors are thrown once per character when conditions are met. Search output_log.txt for the Enums above.
    /// </summary>
    public class ErrorCodeController
    {    
        // Tracks the existing thrown error codes for a given character, so we don't log the same error multuple times (No one likes spam!)
        public Dictionary<string, List<ErrorCode>> charErrorCodes = new Dictionary<string, List<ErrorCode>>();
        public ManualLogSource logger;
        public bool debugLog = false;//When true, always show error code log


        //Set the logging destination in the constructor
        public ErrorCodeController(ManualLogSource _logger, bool _debugLog)
        {
            logger = _logger;
            debugLog = _debugLog;
        }


        /// <summary>
        /// Allow changing the debug log state at runtime
        /// </summary>
        public void SetDebugLogState(bool isDebug)
        {
            debugLog = isDebug;
        }


        /// <summary>
        /// Check for existing error code for a character id
        /// </summary>
        public bool ErrorCodeExists(string charId, ErrorCode errorCode)
        {
            if (charId.Equals(null)) return false;

            var success = charErrorCodes.TryGetValue(charId, out List<ErrorCode> _errorCodes);
            if (!success || _errorCodes == null) return false;

            return _errorCodes.Contains(errorCode);
        }


        /// <summary>
        /// Append error code to the character id list
        /// </summary>
        public void AppendErrorCode(string charId, ErrorCode errorCode)
        {
            var exists = charErrorCodes.TryGetValue(charId, out List<ErrorCode> _errorCodes);

            //Make a new character id entry
            if (!exists) 
            {
                charErrorCodes.Add(charId, new List<ErrorCode>() {errorCode});
            }  
            //Append code to an existing character id
            else if (!_errorCodes.Contains(errorCode))
            {
                charErrorCodes[charId].Add(errorCode);
            }
        }

        
        /// <summary>
        /// Log an error code to the output.log for debugging
        ///     1 log allowed per error code type per character to avoid spamming log
        /// </summary>
        public void LogErrorCode(string charId, ErrorCode errorCode, string message)
        {
            //Always log Error Codes when debug is true
            if (!debugLog && ErrorCodeExists(charId, errorCode)) 
            {                
                if (PregnancyPlusPlugin.DebugLog.Value) logger.LogWarning($"{errorCode} > {message}");
                return;
            }
            
            AppendErrorCode(charId, errorCode);
            logger.LogWarning($"{errorCode} > {message}");       
        }
    }
}
