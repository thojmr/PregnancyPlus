using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using ExtensibleSaveFormat;
using HarmonyLib;
#if KK
using KKAPI.MainGame;
#endif
using Manager;
using UnityEngine;

namespace KK_PregnancyPlus
{
    public partial class PregnancyPlusPlugin
    {
        private static class Hooks
        {
            public static void InitHooks(Harmony harmonyInstance)
            {
                harmonyInstance.PatchAll(typeof(Hooks));
            }
        }
    }
}
