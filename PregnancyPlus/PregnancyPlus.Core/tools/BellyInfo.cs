using UnityEngine;

namespace KK_PregnancyPlus
{
       
    //Used to determine belly scale direction
    public enum BellyDir
    {
        x,y,z
    }

    //Stores information about the characters belly measurements that we will need later
    public class BellyInfo 
    {
        public float WaistWidth;
        public float WaistHeight;

        //In 6.0+ we use a static distance, since we have a static virtual mesh to work with
        #if KKS
            public float WaistThick = 0.045f;
        #else
            public float WaistThick = 0.45f;
        #endif 

        //Well, with the BindPose changes, I've almost completely removed the need for these scalers. yey
        public Vector3 CharacterScale;//ChaControl.transform scale (set by the Axis scale control)
        public Vector3 BodyTopScale;//BodyTop bone scale
        public Vector3 NHeightScale;//n_height bone scale
        public Vector3 TotalCharScale
        {
            //Multiply x*x, y*y etc to get the toal character scale
            get { return new Vector3(BodyTopScale.x * CharacterScale.x, BodyTopScale.y * CharacterScale.y, BodyTopScale.z * CharacterScale.z); }
        }

        public float SphereRadius;
        public float OriginalSphereRadius;
        public float CurrentMultiplier;

        //From char z=0 position
        public float ZLimit
        {
            //Get the distance from center -> spine, where the belly is allowed to wrap around to
            get { return WaistThick; }
        }

        //In 6.0+ we use a static distance
        #if KKS
            public float BellyToBreastDist = 0.182f;
        #else
            public float BellyToBreastDist = 1.82f;
        #endif          

        //From char belly button to breast distance
        public float YLimit
        {
            //Get the distance from center -> ribs
            get { return BellyToBreastDist; }
        }

        public float YLimitOffset
        {
            //Get the offset below the YLimit where we want to start lerping the mesh
            get { return YLimit/2; }
        }

        public float BellyButtonHeight;//Foot to belly button height

        
        public bool IsInitialized 
        {
            get { return WaistWidth > 0 && WaistHeight > 0; }
        }

        internal BellyInfo(float waistWidth, float waistHeight, float sphereRadius, float originalSphereRadius, 
                            Vector3 bodyTopScale, float currentMultiplier, Vector3 nHeightScale,
                            Vector3 characterScale) 
        {
            WaistWidth = waistWidth;
            WaistHeight = waistHeight;
            SphereRadius = sphereRadius;
            OriginalSphereRadius = originalSphereRadius;
            BodyTopScale = bodyTopScale;
            CurrentMultiplier = currentMultiplier;
            NHeightScale = nHeightScale;
            CharacterScale = characterScale;
        }

        //Determine if we need to recalculate the sphere radius (hopefully to avoid change in hip bones causing belly size to sudenly change)
        internal bool NeedsSphereRecalc(PregnancyPlusData data, float newMultiplier) 
        {
            if (!IsInitialized) return true;
            if (CurrentMultiplier != newMultiplier) return true;

            return false;
        }


        //Determine if we need to recalculate the bone distances (typically when character scale changes)
        internal bool NeedsBoneDistanceRecalc(Vector3 bodyTopScale, Vector3 nHeightScale, Vector3 charScale) 
        {
            if (!IsInitialized) return true;
            if (BodyTopScale != bodyTopScale) return true;
            if (NHeightScale != nHeightScale) return true;
            if (CharacterScale != charScale) return true;

            return false;
        }

        //Allows cloning, to avoid pass by ref issues when keeping history
        public object Clone()
        {
            return this.MemberwiseClone();
        }

        public string Log()
        {
            return $@" BellyInfo:    
    BodyTopScale {BodyTopScale} NHeightScale {NHeightScale} CharacterScale {CharacterScale} TotalCharScale {TotalCharScale}
    WaistWidth   {WaistWidth  } WaistHeight  {WaistHeight}
    SphereRadius {SphereRadius} OriginalSphereRadius {OriginalSphereRadius}
            ";
        }

    }

}