using System.Collections.Generic;
using System.Collections;
using System;
using UnityEngine;

namespace KK_PregnancyPlus
{
    //Contains the T-pose bone position logic (Not sure if ill even use this yet)
    public class Skeleton
    {  
        public Trans[] tBones;

        //Initially compute and set the T-pose bone positions that we will use later to skin the T-pose mesh internally
        public void SetTPoseBones(Transform chaControlTf)
        {          
            #if KK && !KKS 
                DisableAllAnimations(chaControlTf);  
            #endif

            var animatedBones = chaControlTf.GetComponentsInChildren<Transform>();   
            if (animatedBones == null) return;
            if (PregnancyPlusPlugin.DebugLog.Value) PregnancyPlusPlugin.Logger.LogWarning($" animatedBones {animatedBones.Length}");

            tBones = new Trans[animatedBones.Length];

            //For each T-pose bone get it's position/rotation
            for (int i = 0; i < animatedBones.Length; i++)
            {
                var aBone = animatedBones[i];

                //Copy the current transform
                var tBone = new Trans(aBone);
                
                //Alter the transform
                tBone.position = aBone.position;
                tBone.rotation = aBone.rotation;

                tBones[i] = tBone;
            }
        }

        #if KK && !KKS 

            //I dont think this works as is
            public void DisableAllAnimations(Transform chaControlTf)
            {            
                var anims = chaControlTf.GetComponentsInChildren<Animation>();
                foreach (var anim in anims)
                {
                    anim.enabled = false;
                }
            }
        #endif


        //Get a bone by name from the T-pose bone list
        public void GetTPoseBone(Transform bone, out Vector3 position, out Quaternion rotation)
        {
            var newTrans = Array.Find<Trans>(tBones, tBone => tBone.name == bone.name && tBone.parentName == bone.parent?.name);
            position = newTrans.position;
            rotation = newTrans.rotation;
        }


        //I don't think this works when an active pose/animation is applied :(
        public IEnumerator ClearAllCharBoneRotations(Transform chaTransform)
        {
            yield return new WaitForEndOfFrame();

            var bones = chaTransform.GetComponentsInChildren<Transform>();   
            if (bones == null) yield break;

            //Debug.Log ("Sampling skinning of SkinnedMeshRenderer "+skin.name);
            Vector3[] backupLocalPosition = new Vector3[bones.Length];

            // backup local position of bones. Only use rotation given by bind pose
            for (int i = 0; i < bones.Length; i++)
            {
                backupLocalPosition[i] = bones[i].localPosition;
            }

            // Set all parents to be null to be able to set global alignments of bones without affecting their children.
            Dictionary<Transform, Transform> parents = new Dictionary<Transform, Transform>();
            foreach (Transform bone in bones)
            {
                parents[bone] = bone.parent;
                bone.parent = null;
            }

            // Set global space position and rotation of each bone
            for (int i = 0; i < bones.Length; i++)
            {
                bones[i].rotation = Quaternion.identity;
            }

            // Reconnect bones in their original hierarchy
            foreach (Transform bone in bones)
                bone.parent = parents[bone];

            // put back local postion of bones
            for (int i = 0; i < bones.Length; i++)
            {
                bones[i].localPosition = backupLocalPosition[i];
            }
            
        }

    }



    //Allows us to keep a list of bone transforms
    public class Trans
    {
        public Vector3 position;
        public Quaternion rotation;
        public Vector3 localPosition;
        public Quaternion localRotation;
        public Vector3 localScale;
        public string name;
        public string parentName;
    
        public Trans (Vector3 newPosition, Quaternion newRotation, Vector3 newLocalPosition, Quaternion newLocalRotation, Vector3 newLocalScale, string newName, string newParentName)
        {
            position = newPosition;
            rotation = newRotation;
            localPosition = newLocalPosition;
            localRotation = newLocalRotation;
            localScale = newLocalScale;
            name = newName;
            parentName = newParentName;
        }
    
        public Trans ()
        {
            position = Vector3.zero;
            rotation = Quaternion.identity;
            localPosition = Vector3.zero;
            localRotation = Quaternion.identity;
            localScale = Vector3.one;
            name = null;
            parentName = null;
        }
    
        public Trans (Transform transform)
        {
            copyFrom (transform);
        }
    
        public void copyFrom (Transform transform)
        {
            position = transform.position;
            rotation = transform.rotation;
            localPosition = transform.localPosition;
            localRotation = transform.localRotation;
            localScale = transform.localScale;
            name = transform.name;
            parentName = transform.parent?.name;
        }
    
        public void copyTo (Transform transform)
        {
            transform.position = position;
            transform.rotation = rotation;
            transform.localPosition = localPosition;
            transform.localRotation = localRotation;
            transform.localScale = localScale;
            transform.name = name;
        }
    
    }

}