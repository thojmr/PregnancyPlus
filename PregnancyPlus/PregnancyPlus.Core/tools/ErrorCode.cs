using System.Collections.Generic;
using BepInEx.Logging;

namespace KK_PregnancyPlus
{
    //Possible Preg+ errors we want to look for in output.logs
    public enum ErrorCode
    {
        PregPlus_MeshNotReadable,//When the mesh is marked as isReadable == false, we can't read or modify the mesh.
        PregPlus_IncorrectVertCount,//When the current mesh vert count does not match the stored mesh vert count.  The mesh was swaped out.
        PregPlus_BadMeasurement,//When a part of the character fails to take measurement needed for belly placement.
        PregPlus_HSPENotFound,//When HSPE plugin is not found while using blendshapes (It's not a hard dependency, but still good to know when its not included)
        PregPlus_BodyMeshDisguisedAsCloth,//When a body mesh is detected that is nested under a cloth Game Object (like Squeeze Socks)
        PregPlus_BodyMeshVertexChanged,//When a saved blendshape tries to loadd, but the mesh is no longer the same and can't load
        PregPlus_BodyUncensorChanged,//When a saved blendshape tries to load, but the uncensor body no longer matches, so it can't
        PregPlus_NoMeshRootFound,//For some reason in RX11 KK, the mesh root bone name changed . '_low' was appended in free roam.  Catch these changes if they happen again
    }

    /// <summary>
    /// Needed better user log error tracking. Errors are thrown once per character when conditions are met. Search output.log for the Enums above to track down issues.
    /// </summary>
    public class ErrorCodeController
    {    
        // Tracks the existing thrown error codes for a given preg+ character
        public Dictionary<int, List<ErrorCode>> charErrorCodes = new Dictionary<int, List<ErrorCode>>();
        public ManualLogSource logger;
        public bool debugLog = false;//When true, always show error code log


        public ErrorCodeController(ManualLogSource _logger, bool _debugLog)
        {
            logger = _logger;
            debugLog = _debugLog;
        }

        public void SetDebugLogState(bool isDebug)
        {
            debugLog = isDebug;
        }


        /// <summary>
        /// Check for existing error code for this character id
        /// </summary>
        public bool ErrorCodeExists(int charId, ErrorCode errorCode)
        {
            if (charId.Equals(null)) return false;

            var success = charErrorCodes.TryGetValue(charId, out List<ErrorCode> _errorCodes);
            if (!success || _errorCodes == null) return false;

            return _errorCodes.Contains(errorCode);
        }


        /// <summary>
        /// Append error code to the users list
        /// </summary>
        public void AppendErrorCode(int charId, ErrorCode errorCode)
        {
            var exists = charErrorCodes.TryGetValue(charId, out List<ErrorCode> _errorCodes);

            //Make a new user entry
            if (!exists) 
            {
                charErrorCodes.Add(charId, new List<ErrorCode>() {errorCode});
            }  
            //Append code to an existing user
            else if (!_errorCodes.Contains(errorCode))
            {
                charErrorCodes[charId].Add(errorCode);
            }
        }

        
        /// <summary>
        /// Log an error code to the output.log for debugging
        ///     1 log allowed per error code type per character to avoid spamming log
        /// </summary>
        public void LogErrorCode(int charId, ErrorCode errorCode, string message)
        {
            if (!debugLog && ErrorCodeExists(charId, errorCode)) 
            {
                //Always log Error Codes when debug is true
                if (PregnancyPlusPlugin.DebugLog.Value) logger.LogWarning($"{errorCode} > {message}");
                return;
            }
            AppendErrorCode(charId, errorCode);
            logger.LogWarning($"{errorCode} > {message}");       
        }
    }
}
