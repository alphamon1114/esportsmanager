using UnityEngine;
namespace FpsManager
{
    public partial class Prototype
    {
#if UNITY_5_3_OR_NEWER
        readonly GameObject[] characterVisuals=new GameObject[10];
        readonly int[] characterSides=new int[10];
        GameObject ctCharacter,tCharacter;
        bool characterAssetsLoaded;
        Light characterLight;
#endif
        void SyncCharacterVisuals()
        {
#if UNITY_5_3_OR_NEWER
            if(!characterAssetsLoaded)
            {
                ctCharacter=Resources.Load<GameObject>("Characters/CT");
                tCharacter=Resources.Load<GameObject>("Characters/T");
                characterAssetsLoaded=true;
            }
            if(ctCharacter==null&&tCharacter==null) return; // Keep the prototype usable without art assets.
            mapCamera.cullingMask=sourceArena!=null?1<<14:~((1<<9)|(1<<10)|(1<<11)|(1<<12)|(1<<13)|(1<<14));
            eyeCamera.cullingMask=~((1<<8)|(1<<10)|(1<<12)|(1<<13)|(1<<14));
            if(characterLight==null)
            {
                var lamp=new GameObject("Character lighting");lamp.transform.SetParent(transform,false);
                lamp.transform.rotation=Quaternion.Euler(45,-30,0);
                characterLight=lamp.AddComponent<Light>();characterLight.type=LightType.Directional;
                characterLight.intensity=1.2f;characterLight.shadows=LightShadows.None;
                characterLight.cullingMask=(1<<11)|(1<<12)|(1<<13);
            }
            for(int i=0;i<actors.Count;i++)
            {
                int side=teamIndex[i]==ctTeam?0:1;var prefab=side==0?ctCharacter:tCharacter;
                if(characterVisuals[i]!=null&&characterSides[i]!=side)
                { characterVisuals[i].SetActive(false);if(Application.isPlaying)Destroy(characterVisuals[i]);else DestroyImmediate(characterVisuals[i]);characterVisuals[i]=null; }
                if(prefab==null) continue;
                if(characterVisuals[i]==null)
                {
                    var model=Instantiate(prefab,actors[i].transform,false);model.name=side==0?"CT soldier":"T desert soldier";
                    // The logical capsule is wider; keep the artwork's original proportions.
                    Vector3 scale=actors[i].transform.localScale;
                    model.transform.localScale=new Vector3(1/scale.x,1/scale.y,1/scale.z);
                    foreach(var collider in model.GetComponentsInChildren<Collider>(true)) collider.enabled=false;
                    characterVisuals[i]=model;characterSides[i]=side;
                }
                actors[i].layer=10; // Tactical marker; the first-person camera excludes it.
                bool living=combat==null||combat.Alive(i);
                var root=characterVisuals[i].transform;
                root.localPosition=new Vector3(0,living?-1f-(IsCrouched(i)?.55f:0):-.72f,0);
                var actorScale=actors[i].transform.localScale;root.localScale=new Vector3(1/actorScale.x,1/actorScale.y,1/actorScale.z);
                root.localRotation=living?Quaternion.identity:Quaternion.Euler(90,0,0);
                var animation=characterVisuals[i].GetComponent<CharacterAnimation>();
                if(animation!=null){animation.SetState(living,HeldWeapon(i),side==0);animation.AimElevation=combat.AimElevation(i);animation.Crouched=IsCrouched(i);}
                foreach(var child in root.GetComponentsInChildren<Transform>(true)) child.gameObject.layer=i==selected?12:11;
            }
#endif
        }
    }
}