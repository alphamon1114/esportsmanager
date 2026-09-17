using UnityEngine;
namespace FpsManager
{
    public partial class Prototype
    {
#if UNITY_5_3_OR_NEWER
        Camera equipmentCamera;
        GameObject equipmentModel;
        int equipmentSide=-1;
#endif
        void SyncFirstPersonEquipment()
        {
#if UNITY_5_3_OR_NEWER
            if(eyeCamera==null||combat==null)return;
            if(equipmentCamera==null)
            {
                var go=new GameObject("First person equipment camera");go.transform.SetParent(eyeCamera.transform,false);
                equipmentCamera=go.AddComponent<Camera>();
                equipmentCamera.clearFlags=CameraClearFlags.Depth;
                equipmentCamera.cullingMask=1<<13;equipmentCamera.depth=eyeCamera.depth+1;
                equipmentCamera.nearClipPlane=.01f;equipmentCamera.farClipPlane=5;
                equipmentCamera.fieldOfView=65;
            }
            equipmentCamera.targetTexture=eyeTexture;
            bool living=combat.Alive(selected);
            string weapon=combat.WeaponFor(selected).id;
            equipmentCamera.enabled=living&&weapon!="unarmed";
            int side=teamIndex[selected]==ctTeam?0:1;
            if(equipmentModel==null||equipmentSide!=side)
            {
                if(equipmentModel!=null){equipmentModel.SetActive(false);if(Application.isPlaying)Destroy(equipmentModel);else DestroyImmediate(equipmentModel);}
                var prefab=Resources.Load<GameObject>("ViewModels/"+(side==0?"CT":"T"));
                if(prefab==null){equipmentCamera.enabled=false;return;}
                equipmentModel=Instantiate(prefab,eyeCamera.transform,false);
                equipmentModel.name="First person arms and weapon";
                equipmentModel.transform.localPosition=new Vector3(.16f,-1.53f,.02f);
                equipmentSide=side;
                var animator=equipmentModel.GetComponentInChildren<Animator>();
                if(animator!=null){animator.SetFloat("Speed",0);animator.applyRootMotion=false;}
                // Camera-follow displacement is not walking input for a first-person rig.
                equipmentModel.GetComponent<CharacterAnimation>().enabled=false;
            }
            equipmentModel.transform.localPosition=(weapon=="awp"||weapon=="famas")?new Vector3(.10f,-1.59f,.26f):new Vector3(.16f,-1.53f,.02f);
            equipmentModel.transform.localRotation=Quaternion.identity;
            equipmentModel.SetActive(equipmentCamera.enabled);
            if(!equipmentCamera.enabled)return;
            equipmentModel.GetComponent<CharacterAnimation>().SetState(true,weapon,side==0);
            foreach(var child in equipmentModel.GetComponentsInChildren<Transform>(true))child.gameObject.layer=13;
#endif
        }
    }
}
