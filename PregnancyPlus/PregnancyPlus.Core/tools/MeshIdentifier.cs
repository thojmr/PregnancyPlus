namespace KK_PregnancyPlus
{
    //Stores the mesh values to uniquely identify a skinned mesh renderer.
    public class MeshIdentifier {            
        public string name;
        public int vertexCount;

        public string RenderKey {
            get { return $"{name}_{vertexCount}"; }
        }

        public MeshIdentifier(string _name, int _vertexCount) 
        {
            name = _name;
            vertexCount = _vertexCount;
        }
    }
}