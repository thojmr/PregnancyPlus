using System.Collections.Generic;
using BepInEx.Logging;


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
        if (!debugLog && ErrorCodeExists(charId, errorCode)) return;
        AppendErrorCode(charId, errorCode);
        logger.LogInfo($"{errorCode} {message}");        
    }
}


//Possible Preg+ errors we want to look for in output.logs
public enum ErrorCode
{
    PregPlus_MeshNotReadable,//When the mesh is marked as isReadable == false, we can't read or modify the mesh.
    PregPlus_IncorrectVertCount,//When the current mesh vert count does not match the stored mesh vert count.  The mesh was swaped out.
    PregPlus_BadMeasurement,//When a part of the character fails to take measurement needed for belly placement.
    PregPlus_HSPENotFound
}
