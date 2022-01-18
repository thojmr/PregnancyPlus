using UnityEngine;

public static class SmoothMesh 
{

    /// <summary>
    ///     Smooth any jagged edges resulting from extreme belly slider positions.  Triggered by user button press since its quite compute heavy.
    /// </summary>
    /// <param name="sourceMesh"></param>
    /// <param name="indexedVerts">list of verticies that we care about, ignore all others to reduce compute time</param>
    public static Vector3[] Start(Mesh sourceMesh, bool[] indexedVerts) 
	{  
		// Apply Laplacian Smoothing Filter to Mesh
        // return SmoothFilter.laplacianFilter(workingMesh.vertices, workingMesh.triangles, indexedVerts);
        return SmoothFilter.hcFilter(sourceMesh.vertices, sourceMesh.triangles, 0.0f, 0.5f, indexedVerts);
	}
}