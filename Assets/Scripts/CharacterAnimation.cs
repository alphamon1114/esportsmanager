#if UNITY_5_3_OR_NEWER
using UnityEngine;
namespace FpsManager
{
    // Presentation only: samples actual actor displacement; never moves simulation actors.
    public sealed class CharacterAnimation : MonoBehaviour
    {
        Animator animator;
        Transform socket;
        Transform upperSpine; public float AimElevation; public bool Crouched;
        readonly Transform[] thighs=new Transform[2],shins=new Transform[2],feet=new Transform[2];
        GameObject held;
        DualWieldVisual dual;
        string weaponId;
        Vector3 previous;
        bool initialized,alive=true;
        public float VisualSpeed { get; private set; }
        public string VisualWeapon { get; private set; }
        void Setup()
        {
            if(initialized)return;
            initialized=true;previous=transform.position;
            animator=GetComponentInChildren<Animator>();
            foreach(var t in GetComponentsInChildren<Transform>(true)){if(t.name=="Socket_RightHand")socket=t;if(t.name=="mixamorig:Spine2")upperSpine=t;for(int leg=0;leg<2;leg++){string side=leg==0?"Left":"Right";if(t.name=="mixamorig:"+side+"UpLeg")thighs[leg]=t;if(t.name=="mixamorig:"+side+"Leg")shins[leg]=t;if(t.name=="mixamorig:"+side+"Foot")feet[leg]=t;}}
        }
        public void SetState(bool living,string id,bool ct)
        {
            Setup();
            if(alive!=living) { previous=transform.position;VisualSpeed=0; }
            alive=living;if(animator!=null)animator.enabled=living;
            if(dual!=null)dual.enabled=living;
            string model=ModelFor(id,ct);
            if(weaponId==id&&VisualWeapon==model)return;
            weaponId=id;VisualWeapon=model;
            if(dual!=null){dual.Clear();if(Application.isPlaying)Destroy(dual);else DestroyImmediate(dual);dual=null;}
            if(held!=null){held.SetActive(false);if(Application.isPlaying)Destroy(held);else DestroyImmediate(held);}
            if(socket!=null&&model!=null)
            {
                var prefab=Resources.Load<GameObject>("Weapons/"+model);
                if(prefab!=null)
                {
                    held=Instantiate(prefab,socket,false);
                    if(id=="dual_berettas"){dual=gameObject.AddComponent<DualWieldVisual>();dual.Initialize(prefab,socket);dual.enabled=living;}
                    held.transform.localPosition=Vector3.zero;held.transform.localRotation=Quaternion.identity;held.transform.localScale=Vector3.one;
                    foreach(var t in held.GetComponentsInChildren<Transform>(true))t.gameObject.layer=gameObject.layer;
                }
            }
            if(animator!=null&&living)
            {
                string pose=(id=="unarmed"||id=="knife")?"knife":IsPistol(id)?"pistol":"rifle";
                animator.Play("Upper."+pose,1,0);
            }
        }
        public static bool IsPistol(string id) { return System.Array.IndexOf(WeaponCatalog.Ids,id)>=0&&System.Array.IndexOf(WeaponCatalog.Ids,id)<6; }
        public static string ModelFor(string id,bool ct)
        {
            switch(id)
            {
                case "unarmed":return null;
                case "knife":return "weapon_knife";
                case "galil_ar":return "weapon_galil_ar";
                case "tec_9":return "weapon_tec_9";
                case "five_seven":return "weapon_five_seven";
                case "dual_berettas":return "weapon_dual_berettas";
                case "awp":return "weapon_awp";
                case "famas":return "weapon_famas";
                case "ak_47":return "weapon_ak47";
                case "m4a1_s":return "weapon_m4a1s";
                case "glock_18":return "weapon_glock18";
                case "usp_s":return "weapon_usp_s";
                case "desert_eagle":return "weapon_deagle";
                default:return IsPistol(id)?"weapon_pistol":ct?"weapon_m4a1s":"weapon_ak47";
            }
        }
        void LateUpdate()
        {
            Setup();
            float distance=Vector3.Distance(transform.position,previous);previous=transform.position;
            float speed=Time.deltaTime>0?distance/Time.deltaTime:0;
            // Teleports (spawning/reset) must not look like sprinting.
            if(distance>2||!alive)speed=0;
            VisualSpeed=Mathf.MoveTowards(VisualSpeed,Mathf.Min(speed,5),Time.deltaTime*30);
            if(animator!=null&&alive)animator.SetFloat("Speed",VisualSpeed);
            if(alive&&Crouched)for(int leg=0;leg<2;leg++){
                if(thighs[leg]!=null)thighs[leg].rotation=Quaternion.AngleAxis(-65,transform.right)*thighs[leg].rotation;
                if(shins[leg]!=null)shins[leg].rotation=Quaternion.AngleAxis(130,transform.right)*shins[leg].rotation;
                if(feet[leg]!=null)feet[leg].rotation=Quaternion.AngleAxis(-65,transform.right)*feet[leg].rotation;
            }
            if(alive&&upperSpine!=null&&Mathf.Abs(AimElevation)>.01f)upperSpine.rotation=Quaternion.AngleAxis(-AimElevation,transform.right)*upperSpine.rotation;
        }
    }
}
#endif