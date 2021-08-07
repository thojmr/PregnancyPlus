using KKAPI.Chara;
using UnityEngine;
using System;

namespace KK_PregnancyPlus
{

    //This partial class contains the animation curves used in mesh transformations, much better curvature than a simple linear mathf.lerp
    public partial class PregnancyPlusCharaController: CharaCustomFunctionController
    {           

        //Careful that none of these are threadsafe for mesh calculations, need to make copies first        
        public AnimationCurve FastLerpAC = FastLerpAnimCurve();
        public AnimationCurve SlowLerpAC = SlowLerpAnimCurve();
        public AnimationCurve BellyTopAC = BellyTopAnimCurve();
        public AnimationCurve BellyEdgeAC = BellyEdgeAnimCurve();
        public AnimationCurve BellySidesAC = BellySidesAnimCurve();
        public AnimationCurve MediumLerpAC = MediumLerpAnimCurve();


        //Curves quickly towards top
        public static AnimationCurve FastLerpAnimCurve()
        {            
            var curve = new AnimationCurve();

            //Fast curve
            curve.AddKey(0f, 0f);
            curve.AddKey(0.25f, 0.5f);
            curve.AddKey(0.5f, 0.75f);
            curve.AddKey(0.75f, 0.9f);
            curve.AddKey(1f, 1f);

            return curve;
        }

        //Curves slowly towards top
        public static AnimationCurve SlowLerpAnimCurve()
        {            
            var curve = new AnimationCurve();

            curve.AddKey(0f, 0f);
            curve.AddKey(0.25f, 0.1f);
            curve.AddKey(0.5f, 0.25f);
            curve.AddKey(0.75f, 0.5f);
            curve.AddKey(1f, 1f);

            return curve;
        }

        public static AnimationCurve MediumLerpAnimCurve()
        {            
            var curve = new AnimationCurve();

            curve.AddKey(0f, 0f);
            curve.AddKey(0.25f, 0.35f);
            curve.AddKey(0.5f, 0.5f);
            curve.AddKey(0.75f, 0.75f);
            curve.AddKey(1f, 1f);

            return curve;
        }

        //Custom curve for skin boundary at top of belly
        public static AnimationCurve BellyTopAnimCurve()
        {            
            var curve = new AnimationCurve();

            curve.AddKey(0f, 0f);
            curve.AddKey(0.25f, 0.1f);
            curve.AddKey(0.5f, 0.35f);
            curve.AddKey(0.75f, 0.9f);
            curve.AddKey(1f, 1f);

            return curve;
        }


        //Custom curve for roundness near the skin edge of the belly 
        public static AnimationCurve BellyEdgeAnimCurve()
        {            
            var curve = new AnimationCurve();

            curve.AddKey(0f, 0f);
            curve.AddKey(0.25f, 0.001f);
            curve.AddKey(0.5f, 0.001f);
            curve.AddKey(0.75f, 0.2f);        
            curve.AddKey(0.9f, 0.7f);
            curve.AddKey(1f, 1f);

            return curve;
        }

        //Custom curve for smoothing both sides of the belly from the back around to the sides (Reduces edges along back back at largest sizes)
        public static AnimationCurve BellySidesAnimCurve()
        {            
            var curve = new AnimationCurve();

            curve.AddKey(0f, 0f);
            curve.AddKey(0.25f, 0.15f);
            curve.AddKey(0.5f, 0.35f);
            curve.AddKey(0.7f, 0.7f);
            curve.AddKey(0.9f, 0.9f);
            curve.AddKey(1f, 1f);

            return curve;
        }
                      
    }
}


