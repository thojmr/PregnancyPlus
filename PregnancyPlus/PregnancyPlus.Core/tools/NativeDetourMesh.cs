using MonoMod.RuntimeDetour;
using HarmonyLib;
using UnityEngine;

namespace KK_PregnancyPlus
{
    //This will create and manage the mesh NativeDetour, that forces a mesh to be readable at runtime
    public class NativeDetourMesh
    {
        public NativeDetour nativeDetour;


        //Create the detour when constructor called
        public NativeDetourMesh()
        {
            CreateDetour();
        }


        /// <summary>
        /// Create a method detour to allow an unreadable mesh to be readable.  Otherwise the clothing can not be read or altered 
        ///     call .Apply to activate it
        /// </summary>
        internal NativeDetour CreateDetour()
        {              
            if (nativeDetour != null) nativeDetour.Dispose();

            #if !KK || KKS
                nativeDetour = new NativeDetour(AccessTools.Property(typeof(Mesh), "canAccess").GetMethod, AccessTools.Method(typeof(NativeDetourMesh), "canAccess"));                                     
            #else
                //Typical...  We have to do it differently in KK
                nativeDetour = new NativeDetour(AccessTools.Property(typeof(Mesh), "canAccess").GetGetMethod(true), AccessTools.Method(typeof(NativeDetourMesh), "canAccess"));                                     
            #endif

            return nativeDetour;
        }


        //Detour override method
        public bool canAccess()
        {
            return true;
        }


        /// <summary>
        /// When active, a mesh will act as if is is readable, even when it is marked as isReadable = false by making canAccess() return true
        ///   Only use this while the mesh is being read/altered by Preg+.  Then call Undo() to set it back to normal to prevent potential plugin conflicts
        ///   No idea why this works, since unreadable meshes should not exists in CPU memory in the first place.  Does this edit on V-RAM directly?
        /// </summary>
        internal void Apply()
        {
            nativeDetour?.Apply();                                      
        }


        internal void Undo()
        {
            nativeDetour?.Undo();                                      
        }


        internal void Dispose()
        {
            nativeDetour?.Dispose();                                      
        }
    }

}