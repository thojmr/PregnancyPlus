using UnityEngine;
using BepInEx.Logging;

public static class DebugTools
{     
    #if DEBUG
        internal static bool debugLog = true;
    #else
        internal static bool debugLog = false;
    #endif

    //Set the logger on init to use it
    public static ManualLogSource logger = null;

    public static string lineRendererGOName = "Preg+DebugLineRenderer";
    public static string sphereGOName = "Preg+DebugSphere";


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
    /// Draw a debug shpere  (Cant see these in KK studio...)
    /// </summary>
    public static GameObject DrawSphere(float radius = 0.05f, Vector3 position = new Vector3(), Color color = default(Color))
    {
        var sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere).transform;        
            
        #if HS2 || AI
            radius = radius * 10;
        #endif

        if (color == null || color == default(Color)) color = Color.grey;

        sphere.name = sphereGOName;
        sphere.position = position;
        sphere.localScale = new Vector3(radius, radius, radius);
        var sphereRenderer = sphere.GetComponent<Renderer>();
        sphereRenderer.material.color = color;
        sphereRenderer.enabled = true;
        #if KKS
            //Makes sphere more visible in KK Maker instead of black
            //I dont think color works with this sprite
            sphereRenderer.material = new Material(Shader.Find("Sprites/Default"));
        #endif

        return sphere.gameObject;
    }

    
    /// <summary>
    /// Draw shphere and attach to a parent transform (optional offset)
    /// </summary>
    public static void DrawSphereAndAttach(Transform parent, float radius = 1, Vector3 localPosition = new Vector3(), bool removeExisting = true, 
                                           bool worldPositionStays = false, Color color = default(Color))
    {
        var sphere = DrawSphere(radius, color: color);

        //If parent has debug spheres delete it
        if (removeExisting)        
            ClearSpheres(parent.gameObject);        

        //Attach and move to parent position
        sphere.transform.SetParent(parent, worldPositionStays);
        sphere.transform.localPosition = localPosition;
    }


    /// <summary>
    /// Draw a line renderer between two Vectors
    /// </summary>
    public static GameObject DrawLine(Vector3 fromVector = new Vector3(), Vector3 toVector = new Vector3(), float width = 0f, 
                                            bool useWorldSpace = false, Color startColor = default(Color))
    {
        //Draw forward by default
        if (toVector == Vector3.zero) toVector = new Vector3(0, 0, 1);

        var lineRendGO = new GameObject(lineRendererGOName);
        var lineRenderer = lineRendGO.AddComponent<LineRenderer>();

        if (width == 0f)
        {
            #if KKS
                var minWidth = 0.005f;
            #elif HS2 || AI
                var minWidth = 0.005f * 10;
            #endif

            //Make the width larger for longer lines, and set a minimumWidth too
            width = Mathf.Max(Vector3.Distance(fromVector, toVector)/30, minWidth);
        } else {
            #if HS2 || AI
                width = width * 10;
            #endif
        }
        
        if (startColor == null || startColor == default(Color)) startColor = Color.blue;

        lineRenderer.useWorldSpace = useWorldSpace;
        lineRenderer.startColor = startColor;
        lineRenderer.endColor = Color.red;
        lineRenderer.material = new Material(Shader.Find("Sprites/Default"));
        lineRenderer.startWidth = width;
        lineRenderer.endWidth = width/5;
        lineRenderer.SetPosition(0, fromVector);
        lineRenderer.SetPosition(1, toVector);
        lineRenderer.enabled = true; // show it

        return lineRendGO;
    }


    /// <summary>
    /// Overload for DrawLine when you just want to set a forward line length from a single Vector
    /// </summary>
    public static GameObject DrawLine(float length, float width = 0f)
    {        
        return DrawLine(Vector3.zero, new Vector3(0, 0, 1)* length, width);
    }


    /// <summary>
    /// Draw line and attach to a parent transform (optional offset)
    /// </summary>
    public static void DrawLineAndAttach(Transform parent, Vector3 fromVector = new Vector3(), Vector3 toVector = new Vector3(), Vector3 localPosition = new Vector3(), 
                                         bool removeExisting = true, bool worldPositionStays = false, float width = 0.001f)
    {
        var line = DrawLine(fromVector, toVector, width);

        //If parent has a debug sphere delete it
        if (removeExisting)
            ClearLinesRenderers(parent.gameObject);

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
    public static void DebugMeshVerts(GameObject go, Vector3[] verticies, Vector3 visualOffset = new Vector3(), bool removeExisting = true, 
                                      bool worldPositionStays = false, bool[] filterVerts = null, Color color = default(Color), float size = 0.01f) {
        if (verticies == null || verticies.Length <= 0) return;

        //Clear old spheres from previous runs
        if (removeExisting) 
            DrawSphereAndAttach(go.transform, size, Vector3.zero, removeExisting: true, color: color);

        for (int i = 0; i < verticies.Length; i++)
        {
            //Filterr out certain verts
            if (filterVerts == null || filterVerts[i])
                //Place spheres on each vert to debug the mesh calculated position relative to other meshes
                DrawSphereAndAttach(go.transform, size, verticies[i] - visualOffset, removeExisting: false, worldPositionStays, color: color);  
        } 
    }


    /// <summary>
    /// Overload for DebugMeshVerts when you just want worldspace positions (unattached)
    /// </summary>
    public static void DebugMeshVerts(Vector3[] verticies, Vector3 visualOffset = new Vector3(), bool[] filterVerts = null, Color color = default(Color), float size = 0.01f) {
        if (verticies == null || verticies.Length <= 0) return;

        for (int i = 0; i < verticies.Length; i++)
        {
            //Filter out certain verts
            if (filterVerts == null || filterVerts[i])
            {
                //Place spheres on each vert to debug the mesh calculated position relative to other meshes
                DrawSphere(size, verticies[i] - visualOffset, color: color);  
            }                
        } 
    }


    /// <summary>
    /// Add debug lines and spheres to raycast hits and misses
    /// </summary>
    public static void ShowRayCast(Vector3 origin, Vector3 direction, RaycastHit hit) {
        //Draw the raycast line        
        if (hit.collider) DrawLine(origin, origin + (hit.point - origin), width: 0.001f); 
        else DrawLine(origin, origin + direction, width: 0.0005f, startColor: Color.yellow); 
        //Mark hit point, if it hit
        if (hit.collider) DrawSphere(0.001f, hit.point); 
    }


    /// <summary>
    /// Remove all debug line renderers
    /// </summary>
    public static void ClearLinesRenderers(GameObject parent = null)
    {
        var children = (parent == null) ? GameObject.FindObjectsOfType<LineRenderer>() : parent.GetComponents<LineRenderer>();
        foreach (var child in children )
        {
            if (child.name == lineRendererGOName)
                GameObject.Destroy(child.gameObject); 
        }
    }

    /// <summary>
    /// Remove all debug spheres
    /// </summary>
    public static void ClearSpheres(GameObject parent = null)
    {
        var children = (parent == null) ? GameObject.FindObjectsOfType<MeshFilter>() : parent.GetComponents<MeshFilter>();
        foreach (var child in children )
        {
            if (child.name == sphereGOName)
                GameObject.Destroy(child.gameObject); 
        }
    }


    /// <summary>
    /// Remove all debug spheres and lines from a character
    /// </summary>
    public static void ClearAllThingsFromCharacter(GameObject chaControlGo = null)
    {
        ClearLinesRenderers(chaControlGo);
        ClearSpheres(chaControlGo);
    }
}
