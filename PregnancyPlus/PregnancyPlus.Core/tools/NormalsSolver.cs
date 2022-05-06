/* 
 * The following code was taken from: https://schemingdeveloper.com
 *
 * Visit our game studio website: http://stopthegnomes.com
 *
 * License: You may use this code however you see fit, as long as you include this notice
 *          without any modifications.
 *
 *          You may not publish a paid asset on Unity store if its main function is based on
 *          the following code, but you may publish a paid asset that uses this code.
 *
 *          If you intend to use this in a Unity store asset or a commercial project, it would
 *          be appreciated, but not required, if you let me know with a link to the asset. If I
 *          don't get back to you just go ahead and use it anyway!
 */

using System;
using System.Collections.Generic;
using UnityEngine;

public static class NormalSolver
{

    public static bool debugShowBellyVertsOnly = false;//fun bug-turned-feature to see only the verts affected by belly bones

    /// <summary>
    ///     Recalculate the normals of a mesh based on an angle threshold. This takes
    ///     into account distinct vertices that have the same position.
    /// </summary>
    /// <param name="mesh"></param>
    /// <param name="angle">
    ///     The smoothing angle. Note that triangles that already share
    ///     the same vertex will be smooth regardless of the angle! 
    /// </param>
    /// <param name="indexedVerts">optional list of indexes to include.  False indexes will be skipped</param>
    /// <param name="rotationUndo">Used to undo any normals rotation due to mesh or bindpose rotations in localspace</param>
    public static void RecalculateNormals(this Mesh mesh, float angle, bool[] indexedVerts = null, Matrix4x4 rotationUndo = new Matrix4x4()) 
    {
        var cosineThreshold = Mathf.Cos(angle * Mathf.Deg2Rad);

        var vertices = mesh.vertices;
        var normals = debugShowBellyVertsOnly ? new Vector3[vertices.Length] : mesh.normals;
        var hasRotationUndo = rotationUndo != Matrix4x4.identity;

        // Holds the normal of each triangle in each sub mesh.
        var triNormals = new Vector3[mesh.subMeshCount][];
        var dictionary = new Dictionary<VertexKey, List<VertexEntry>>();

        for (var subMeshIndex = 0; subMeshIndex < mesh.subMeshCount; ++subMeshIndex) 
        {            
            var triangles = mesh.GetTriangles(subMeshIndex);
            triNormals[subMeshIndex] = new Vector3[triangles.Length / 3];

            for (var i = 0; i < triangles.Length; i += 3) 
            {
                int i1 = triangles[i];
                int i2 = triangles[i + 1];
                int i3 = triangles[i + 2];

                //Break early if all of these verts are not belly verts
                if (indexedVerts != null)                 
                    if (!indexedVerts[i1] && !indexedVerts[i2] && !indexedVerts[i3]) 
                        continue;            

                // Calculate the normal of the triangle
                Vector3 p1 = (vertices[i2] - vertices[i1]).normalized;
                Vector3 p2 = (vertices[i3] - vertices[i1]).normalized;
                Vector3 normal = Vector3.Cross(p1, p2).normalized;
                int triIndex = i / 3;
                triNormals[subMeshIndex][triIndex] = normal;

                List<VertexEntry> entry;
                VertexKey key;

                if (!dictionary.TryGetValue(key = new VertexKey(vertices[i1]), out entry)) 
                {
                    entry = new List<VertexEntry>(4);
                    dictionary.Add(key, entry);
                }
                entry.Add(new VertexEntry(subMeshIndex, triIndex, i1));

                if (!dictionary.TryGetValue(key = new VertexKey(vertices[i2]), out entry)) 
                {
                    entry = new List<VertexEntry>();
                    dictionary.Add(key, entry);
                }
                entry.Add(new VertexEntry(subMeshIndex, triIndex, i2));

                if (!dictionary.TryGetValue(key = new VertexKey(vertices[i3]), out entry)) 
                {
                    entry = new List<VertexEntry>();
                    dictionary.Add(key, entry);
                }
                entry.Add(new VertexEntry(subMeshIndex, triIndex, i3));
            }
        }

        // Each entry in the dictionary represents a unique vertex position.
        foreach (var vertList in dictionary.Values) 
        {
            for (var i = 0; i < vertList.Count; ++i) 
            {                                
                var lhsEntry = vertList[i];
                var sum = new Vector3();         

                for (var j = 0; j < vertList.Count; ++j) 
                {
                    var rhsEntry = vertList[j];
                    if (lhsEntry.VertexIndex == rhsEntry.VertexIndex) 
                    {
                        sum += triNormals[rhsEntry.MeshIndex][rhsEntry.TriangleIndex];
                    } 
                    else 
                    {
                        // The dot product is the cosine of the angle between the two triangles.
                        // A larger cosine means a smaller angle.
                        var dot = Vector3.Dot(
                            triNormals[lhsEntry.MeshIndex][lhsEntry.TriangleIndex],
                            triNormals[rhsEntry.MeshIndex][rhsEntry.TriangleIndex]);

                        if (dot >= cosineThreshold) 
                        {
                            sum += triNormals[rhsEntry.MeshIndex][rhsEntry.TriangleIndex];
                        }
                    }
                }
                
                normals[lhsEntry.VertexIndex] = sum.normalized;

                if (hasRotationUndo)
                    normals[lhsEntry.VertexIndex] = rotationUndo.MultiplyPoint3x4(normals[lhsEntry.VertexIndex]);
            }
        }

        mesh.normals = normals;
    }

    private struct VertexKey
    {
        private readonly long _x;
        private readonly long _y;
        private readonly long _z;

        // Change this if you require a different precision.
        private const int Tolerance = 100000;

        // Magic FNV values. Do not change these.
        private const long FNV32Init = 0x811c9dc5;
        private const long FNV32Prime = 0x01000193;

        public VertexKey(Vector3 position) 
        {
            _x = (long)(Mathf.Round(position.x * Tolerance));
            _y = (long)(Mathf.Round(position.y * Tolerance));
            _z = (long)(Mathf.Round(position.z * Tolerance));
        }

        public override bool Equals(object obj) 
        {
            var key = (VertexKey)obj;
            return _x == key._x && _y == key._y && _z == key._z;
        }

        public override int GetHashCode() 
        {
            long rv = FNV32Init;
            rv ^= _x;
            rv *= FNV32Prime;
            rv ^= _y;
            rv *= FNV32Prime;
            rv ^= _z;
            rv *= FNV32Prime;

            return rv.GetHashCode();
        }
    }

    private struct VertexEntry 
    {
        public int MeshIndex;
        public int TriangleIndex;
        public int VertexIndex;

        public VertexEntry(int meshIndex, int triIndex, int vertIndex) 
        {
            MeshIndex = meshIndex;
            TriangleIndex = triIndex;
            VertexIndex = vertIndex;
        }
    }
}