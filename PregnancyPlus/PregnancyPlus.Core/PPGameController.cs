#if KK
    using ActionGame;    
#endif
using KKAPI.MainGame;

namespace KK_PregnancyPlus
{
    public class PregnancyPlusGameController : GameCustomFunctionController
    {

        //When the day changes, check for end of pregnancies
        #if KK
        
            protected override void OnDayChange(Cycle.Week day)
            {
                foreach (var controller in FindObjectsOfType<PregnancyPlusCharaController>())
                    controller.StartCoroutine(controller.ReloadStoryInflation(0.1f, "OnDayChange", checkNewMesh: false));                             
            }

        #elif AI

            protected override void OnDayChange(int day)
            {
                foreach (var controller in FindObjectsOfType<PregnancyPlusCharaController>())
                    controller.StartCoroutine(controller.ReloadStoryInflation(0.1f, "OnDayChange", checkNewMesh: false));
            }

        #endif

    }
}