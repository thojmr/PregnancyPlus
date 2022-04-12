using UnityEngine;
using MessagePack;

namespace KK_PregnancyPlus
{
    //This format contains all the info a blendshape needs to be created (For a single blendshape frame).  It also server as the format we will save to a character card later
    [MessagePackObject(keyAsPropertyName: true)]
    public class BlendShape //This is technically a blendshape frame, but w/e.  Its already set in stone
    {
        public string name;
        private float _frameWeight = 100;//The range that _weight has to stay within
        public float frameWeight
        {
            set { _frameWeight = Mathf.Clamp(value, 0, 100); }
            get { return _frameWeight <= 0 ? 100 : _frameWeight; }//Fix for old cards not having frameWeight prop, set them to 100
        }
        private float _weight = 100;//The current weight
        public float weight 
        {
            set { _weight = value; }
            get { return _weight; }
        }
        public Vector3[] verticies;
        public Vector3[] normals;
        public Vector3[] tangents;

        [IgnoreMember]
        public bool isInitilized
        {
            get { return name != null; }
        }

        [IgnoreMember]
        public int vertexCount 
        {
            get { return verticies.Length; }
        }

        [IgnoreMember]
        public string log 
        {
            get { return $"name {name} weight {_weight} frameWeight {_frameWeight} vertexCount {vertexCount}"; }
        }
    }
}