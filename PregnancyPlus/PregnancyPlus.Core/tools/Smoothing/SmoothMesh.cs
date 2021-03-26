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
		var workingMesh = CloneMesh(sourceMesh);
 
		// Apply Laplacian Smoothing Filter to Mesh
        // return SmoothFilter.laplacianFilter(workingMesh.vertices, workingMesh.triangles, indexedVerts);
        return SmoothFilter.hcFilter(sourceMesh.vertices, workingMesh.vertices, workingMesh.triangles, 0.0f, 0.5f, indexedVerts);
	}

    // Clone a mesh
    private static Mesh CloneMesh(Mesh mesh)
    {
        Mesh clone = new Mesh();
        clone.vertices = mesh.vertices;
        clone.normals = mesh.normals;
        clone.tangents = mesh.tangents;
        clone.triangles = mesh.triangles;
        clone.uv = mesh.uv;
        // clone.uv1 = mesh.uv1;
        clone.uv2 = mesh.uv2;
        clone.bindposes = mesh.bindposes;
        clone.boneWeights = mesh.boneWeights;
        clone.bounds = mesh.bounds;
        clone.colors = mesh.colors;
        clone.name = mesh.name;
        //TODO : Are we missing anything?
        return clone;
    }
}