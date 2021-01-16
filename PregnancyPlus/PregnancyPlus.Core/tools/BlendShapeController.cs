using UnityEngine;

namespace KK_PregnancyPlus
{
    public class BlendShapeController
    {
        public BlendShape blendShape = new BlendShape();

        public class BlendShape 
        {
            public string name;
            private float _weight;
            public float weight 
            {
                set { _weight = Mathf.Clamp(value, 0, 100); }
                get { return _weight; }
            }
            public Vector3[] verticies;
            public Vector3[] normals;
            public Vector3[] tangents;
            public bool isInitilized {
                get { return name != null; }
            }
        }

        /// <summary>
        /// Constructor that takes in a target skinned mesh renderer with verts and creates a blend shape object from it.  This blend shape will then be assigned to the mesh
        /// </summary>
        /// <param name="originalSmr">Target skinned mesh renderer</param>
        /// <param name="blendShapeName">Desired name of the blend shape, should be unique</param>
        /// <param name="newSmr">The smr containing the new mesh vert positions</param>
        public BlendShapeController(Mesh originalSmrMesh, SkinnedMeshRenderer newSmr, string blendShapeName) 
        {
            if (!blendShape.isInitilized) 
            {
                var maxShapeSize = 100f;

                //Create blend shape object to be reused on other meshes (maybe)
                blendShape = new BlendShape();
                blendShape.name = blendShapeName;
                blendShape.weight = maxShapeSize;

                //Get delta diffs of the two meshes for the blend shape
                blendShape.verticies = GetV3Deltas(originalSmrMesh.vertices, newSmr.sharedMesh.vertices);
                blendShape.normals = GetV3Deltas(originalSmrMesh.normals, newSmr.sharedMesh.normals);
                blendShape.tangents = GetV3Deltas(ConvertV4ToV3(originalSmrMesh.tangents), ConvertV4ToV3(newSmr.sharedMesh.tangents));                            
            }

            AddBlendShapeToMesh(newSmr);
        }


        /// <summary>
        /// This will apply the previously created BlendShape object to an existing skinned mesh renderer
        /// </summary>
        /// <param name="smr">Target skinned mesh renderer to attche the blend shape</param>
        public void AddBlendShapeToMesh(SkinnedMeshRenderer smr) 
        {
            if (!blendShape.isInitilized) return;
            var shapeIndex = smr.sharedMesh.GetBlendShapeIndex(blendShape.name);
            if (shapeIndex >= 0) 
            {
                //Blend shape already exists //TODO overwright it?
                if (PregnancyPlusPlugin.debugLog) PregnancyPlusPlugin.Logger.LogInfo($" AddBlendShape > blend shape exists {shapeIndex}");
                return;
            }

            smr.sharedMesh.AddBlendShapeFrame(blendShape.name, blendShape.weight, blendShape.verticies, blendShape.normals, blendShape.tangents);    
            //Fix for some shared mesh properties not updating after AddBlendShapeFrame
            smr.sharedMesh = smr.sharedMesh;    

            if (PregnancyPlusPlugin.debugLog) PregnancyPlusPlugin.Logger.LogInfo($" AddBlendShape > {blendShape.name}");
        }


        /// <summary>
        /// This will change the weight (apperance) of an existing BlendShape attached to a skinned mesh renderer. Weight 0 will reset to the default shape
        /// </summary>
        /// <param name="smr">Target skinned mesh renderer to attche the blend shape</param>
        /// <param name="weight">Float value from 0-100 that will increase the blend to the target shape as the number grows</param>
        /// <returns>boolean true if the blend shape exists</returns>
        public bool ApplyBlendShapeWeight(SkinnedMeshRenderer smr, float weight) 
        {
            if (!blendShape.isInitilized || weight < 0) return false;

            //Belly size goes from 0-40, but blendShapes have to be 0-100
            var lerpWeight = Mathf.Lerp(0, 100, weight/40);
            var shapeIndex = smr.sharedMesh.GetBlendShapeIndex(blendShape.name);
            //If the blendshape is not found, return
            if (shapeIndex < 0) return false;

            var shapeWeight = smr.GetBlendShapeWeight(shapeIndex);            
            var shapeFrameCount = smr.sharedMesh.GetBlendShapeFrameCount(shapeIndex);
            var shapeName = smr.sharedMesh.GetBlendShapeName(shapeIndex);
            var shapeCount = smr.sharedMesh.blendShapeCount;

            if (PregnancyPlusPlugin.debugLog) PregnancyPlusPlugin.Logger.LogInfo($" ApplyBlendShapeWeight > shapeIndex {shapeIndex} shapeWeight {shapeWeight} shapeCount {shapeCount} shapeFrameCount {shapeFrameCount} lerpWeight {lerpWeight}");            
            smr.SetBlendShapeWeight(shapeIndex, lerpWeight);

            return true;
        }

        public void ClearBlendShapes(SkinnedMeshRenderer smr) 
        {
            smr.sharedMesh.ClearBlendShapes();
            blendShape = new BlendShape();
        }

        //Just subtract the vectors to get deltas
        internal Vector3[] GetV3Deltas(Vector3[] origins, Vector3[] targets) 
        {
            var deltas = new Vector3[origins.Length];

            for (var i = 0; i < origins.Length; i++) 
            {
                deltas[i] = targets[i] - origins[i];
            }

            return deltas;
        }

        internal Mesh CopyMesh(Mesh mesh)
        {
            Mesh newmesh = new Mesh();
            newmesh.vertices = mesh.vertices;
            newmesh.triangles = mesh.triangles;
            newmesh.uv = mesh.uv;
            newmesh.normals = mesh.normals;
            newmesh.colors = mesh.colors;
            newmesh.tangents = mesh.tangents;

            return newmesh;
        }

        internal Vector3[] ConvertV4ToV3(Vector4[] v4s) 
        {
            var v3s = new Vector3[v4s.Length];
            var i = 0;//I know, I know....

            foreach(var v4 in v4s) 
            {
                v3s[i] = new Vector3(v4.x, v4.y, v4.z);
                i++;
            }

            return v3s;
        }
    }
}
