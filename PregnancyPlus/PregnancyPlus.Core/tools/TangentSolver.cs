using UnityEngine;

public static class TangentSolver
{
    //After we do tangent recalculation, replace old tangents where the belly does not touch (to prevent nipples and others from chaning appearance)
    //  I did this because the Unity recalculate tangents code is super fast, so its easier/quicker to work backward from there
    public static Vector4[] UnRecalculateTangents(Vector4[] newTangents, Vector4[] oldTangents, bool[] indexedVerts)
    {
        for (var i = 0; i < oldTangents.Length; i++)
        {
            //If the vert is not a belly vert, set it back to its old tangent value
            if (!indexedVerts[i])
            {
                newTangents[i] = oldTangents[i];
            }
        }

        return newTangents;
    }
}