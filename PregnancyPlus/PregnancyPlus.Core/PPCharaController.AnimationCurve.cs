using KKAPI.Chara;
using UnityEngine;
using System;

namespace KK_PregnancyPlus
{

    //This partial class contains the animation curves used in mesh transformations
    public partial class PregnancyPlusCharaController: CharaCustomFunctionController
    {           
        
        public AnimationCurve FastLerpAC = FastLerpAnimCurve();
        public AnimationCurve SlowLerpAC = SlowLerpAnimCurve();
        public AnimationCurve BellyTopAC = BellyTopAnimCurve();
        public AnimationCurve BellyEdgeAC = BellyEdgeAnimCurve();


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
                      
    }
}


