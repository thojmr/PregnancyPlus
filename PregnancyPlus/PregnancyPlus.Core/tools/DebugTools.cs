using UnityEngine;
using BepInEx.Logging;

public static class DebugTools
{     
    #if DEBUG
        internal static bool debugLog = true;
    #else
        internal static bool debugLog = false;
    #endif

    public static ManualLogSource logger = null;


    /// <summary>
    /// Visualize the GameObject tree and components under each (debug only)
    /// </summary>
    internal static void LogChildrenComponents(GameObject parent, bool recursive = true, int level = 0)
    {            
        var children = parent?.GetComponents<Component>();
        if (children == null) return;

        if (level == 0) 
        {
            if (debugLog) logger.LogInfo($" "); 
            if (debugLog) logger.LogInfo($" [LogChildrenComponents]");
        }

        //Add spaces to each log level to see what the structure looks like
        var spaces = " "; 
        for (var s = 0; s < level; s++)
        {
            spaces += "  ";
        }

        if (debugLog) logger.LogInfo($"{spaces}{parent.name} {parent.activeSelf}"); 

        //Log all child components
        foreach(var child in children)
        {
            var isStatic = child.transform.gameObject.isStatic ? " [isStatic]" : "";
            if (debugLog) logger.LogInfo($" {spaces}{child.name}: {child.GetType().Name}{isStatic} p{Pos(child.transform.position)} lp{Pos(child.transform.position)} r{Rot(child.transform.rotation)} lr{Rot(child.transform.localRotation)} {child.transform.childCount}:");
        }

        if (!recursive) return;
        level++;

        //Loop through each child game object
        for (var i = 0; i <  parent.transform.childCount; i++)
        {
            LogChildrenComponents(parent.transform.GetChild(i).gameObject, recursive, level);
        }
    }


    //Get a position or return 0
    internal static string Pos(Vector3 position)
    {
        if (position == Vector3.zero) return "(0)";
        if (position == Vector3.one) return "(1)";
        return position.ToString().Replace("0.0,", "0,").Replace("1.0,", "1,").Replace("0.0)", "0)").Replace("1.0)", "1)");
    }

    //Get a rotation or return 0
    internal static string Rot(Quaternion rotation)
    {
        if (rotation.eulerAngles == Vector3.zero) return "(0)";
        if (rotation.eulerAngles == Vector3.one) return "(1)";
        return rotation.eulerAngles.ToString().Replace("0.0,", "0,").Replace("1.0,", "1,").Replace("0.0)", "0)").Replace("1.0)", "1)");
    }


    /// <summary>
    /// Visualize the GameObject tree and components under each (debug only)
    /// </summary>
    internal static void LogParents(GameObject currentGo, int maxLevel = 1, int currentLevel = 0)
    {         
        if (currentLevel == 0) 
        {
            if (debugLog) logger.LogInfo($" "); 
            if (debugLog) logger.LogInfo($" [LogParents]");
        }

        //Add spaces to each log level to see what the structure looks like
        var spaces = " "; 
        for (var s = 0; s < currentLevel; s++)
        {
            spaces += "  ";
        }

        if (debugLog) logger.LogInfo($"{spaces}{currentGo.name} {currentGo.activeSelf}"); 

        var children = currentGo?.GetComponents<Component>();

        if (children != null)
        {
            //Log all child components
            foreach(var child in children)
            {
                var isStatic = child.transform.gameObject.isStatic ? " [isStatic]" : "";
                if (debugLog) logger.LogInfo($" {spaces}{child.name}: {child.GetType().Name}{isStatic} p{Pos(child.transform.position)} lp{Pos(child.transform.position)} r{Rot(child.transform.rotation)} lr{Rot(child.transform.localRotation)} {child.transform.childCount}:");                
            }
        }

        //End when max level hit
        if (currentLevel >= maxLevel) return;        

        //Get next parent
        if (currentGo.transform.parent == null) return;
        var parent = currentGo.transform.parent.gameObject;
        if (parent == null) return;    

        currentLevel++;
        //Check for next parent
        LogParents(parent, maxLevel, currentLevel);
        
    }

    
    /// <summary>
    /// Draw a debug shpere
    /// </summary>
    public static GameObject DrawSphere(float radius = 0.05f, Vector3 position = new Vector3())
    {
        var sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere).transform;
        // sphere.GetComponent<Renderer>().material = new Material(Shader.Find("Transparent/Diffuse")); // assign the selected material to it
            
        #if HS2 || AI
            radius = radius * 10;
        #endif

        sphere.name = "DebugSphere";
        sphere.position = position;
        sphere.localScale = new Vector3(radius, radius, radius);
        sphere.GetComponent<Renderer>().material.color = Color.white;
        sphere.GetComponent<Renderer>().enabled = true; // show it

        return sphere.gameObject;
    }


    
    /// <summary>
    /// Draw shphere and attach to a parent transform (optional offset)
    /// </summary>
    public static void DrawSphereAndAttach(Transform parent, float radius = 1, Vector3 localPosition = new Vector3(), bool removeExisting = true, bool worldPositionStays = false)
    {
        var sphere = DrawSphere(radius);

        //If parent has debug spheres delete it
        if (removeExisting)
        {
            GameObject[] children = parent.GetComponents<GameObject>();
            foreach (var child in children )
            {
                if(child.name == "DebugSphere")
                    GameObject.DestroyImmediate(child); 
            }
        }

        //Attach and move to parent position
        sphere.transform.SetParent(parent, worldPositionStays);

        sphere.transform.localPosition = localPosition;
    }


    /// <summary>
    /// Draw a debug line renderer
    /// </summary>
    public static GameObject DrawLine(Vector3 fromVector = new Vector3(), Vector3 toVector = new Vector3(), float width = 0.002f)
    {
        //Draw forward by default
        if (toVector == Vector3.zero) toVector = new Vector3(0, 0, 1);

        var lineRendGO = new GameObject("DebugLineRenderer");
        var lineRenderer = lineRendGO.AddComponent<LineRenderer>();

        #if HS2 || AI
            width = width * 7;
        #endif
        
        lineRenderer.useWorldSpace = false;
        lineRenderer.startColor = Color.blue;
        lineRenderer.endColor = Color.red;
        // lineRenderer.material = new Material(CommonLib.LoadAsset<Shader>("chara/goo.unity3d", "goo.shader"));
        lineRenderer.startWidth = width;
        lineRenderer.endWidth = width;
        lineRenderer.SetPosition(0, fromVector);
        lineRenderer.SetPosition(1, toVector);
        lineRenderer.enabled = true; // show it

        return lineRendGO;
    }


    /// <summary>
    /// Overload for DrawLine when you just want to set a forward line length
    /// </summary>
    public static GameObject DrawLine(float length, float width = 0.5f)
    {        
        return DrawLine(Vector3.zero, new Vector3(0, 0, 1)* length, width);
    }


    /// <summary>
    /// Draw line and attach to a parent transform (optional offset)
    /// </summary>
    public static void DrawLineAndAttach(Transform parent, Vector3 fromVector = new Vector3(), Vector3 toVector = new Vector3(), Vector3 localPosition = new Vector3(), 
                                         bool removeExisting = true, bool worldPositionStays = false)
    {
        var line = DrawLine(fromVector, toVector);

        //If parent has a debug sphere delete it
        var existingLine = parent.Find("DebugLineRenderer");
        if (existingLine != null && removeExisting)
        {
            GameObject.DestroyImmediate(existingLine.gameObject);
        }

        //Attach and move to parent position
        line.transform.SetParent(parent, worldPositionStays);

        line.transform.localPosition = localPosition;
    }


    /// <summary>
    /// Overload for DrawLineAndAttach when you just want to set a forward line length
    /// </summary>
    public static void DrawLineAndAttach(Transform parent, float length, Vector3 localPosition = new Vector3(), bool removeExisting = true, bool worldPositionStays = false)
    {
        DrawLineAndAttach(parent, Vector3.zero, new Vector3(0, 0, 1)* length, localPosition, removeExisting, worldPositionStays);
    }

    
    /// <summary>
    /// This will create a sphere on every vert in the given mesh so you can visually see changes in a computed mesh
    ///     (In Koikatsu this only works in character maker, not studio)
    /// </summary>
    public static void DebugMeshVerts(GameObject go, Vector3[] verticies, Vector3 visualOffset = new Vector3(), bool removeExisting = true, bool worldPositionStays = false) {
        if (verticies == null || verticies.Length <= 0) return;

        //Clear old spheres from previous runs
        if (removeExisting) DebugTools.DrawSphereAndAttach(go.transform, 0.01f, Vector3.zero, removeExisting: true);

        for (int i = 0; i < verticies.Length; i++)
        {
            //Place spheres on each vert to debug the mesh calculated position relative to other meshes
            DebugTools.DrawSphereAndAttach(go.transform, 0.02f, verticies[i] - visualOffset, removeExisting: false, worldPositionStays);  
        } 
    }

    /// <summary>
    /// Overload for DebugMeshVerts when you just want worldspace positions
    /// </summary>
    public static void DebugMeshVerts(Vector3[] verticies, Vector3 visualOffset = new Vector3()) {
        if (verticies == null || verticies.Length <= 0) return;

        for (int i = 0; i < verticies.Length; i++)
        {
            //Place spheres on each vert to debug the mesh calculated position relative to other meshes
            DebugTools.DrawSphere(0.02f, verticies[i] - visualOffset);  
        } 
    }
}
