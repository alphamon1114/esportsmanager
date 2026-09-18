using UnityEngine;
namespace FpsManager
{
 public partial class Prototype
 {
#if UNITY_5_3_OR_NEWER
  readonly float[] flashUntil=new float[10];
  readonly int[] visualShots=new int[10];
  readonly GameObject[] muzzleFlashes=new GameObject[10];
  Mesh flashMesh;Material flashMaterial;
  Vector3 viewKick;float viewPush;
  void EnsureFlash(int i)
  {
   if(flashMesh==null)
   {
    flashMesh=new Mesh{name="Low poly muzzle flame"};
    flashMesh.vertices=new[]{Vector3.zero,new Vector3(-.055f,0,.045f),new Vector3(0,.04f,.035f),new Vector3(.055f,0,.045f),new Vector3(0,-.04f,.035f),new Vector3(0,0,.19f)};
    flashMesh.triangles=new[]{0,2,1,0,3,2,0,4,3,0,1,4,5,1,2,5,2,3,5,3,4,5,4,1};flashMesh.RecalculateNormals();
    var template=Resources.Load<Material>("MapColor");
    flashMaterial=template!=null?new Material(template):new Material(Shader.Find("Unlit/Color"));flashMaterial.name="Muzzle flash amber";flashMaterial.color=new Color(1,.7f,.18f);
   }
   if(muzzleFlashes[i]!=null)return;
   var go=new GameObject("Muzzle flash "+i);go.transform.SetParent(transform,false);
   go.AddComponent<MeshFilter>().sharedMesh=flashMesh;go.AddComponent<MeshRenderer>().sharedMaterial=flashMaterial;
   go.GetComponent<Renderer>().shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;
   var glow=go.AddComponent<Light>();glow.type=LightType.Point;glow.color=new Color(1,.55f,.12f);glow.range=1.5f;glow.intensity=1.4f;glow.shadows=LightShadows.None;glow.cullingMask=(1<<11)|(1<<13);
   muzzleFlashes[i]=go;go.SetActive(false);
  }
#endif
  void ShotPresentation(int i,Vector2 kick)
  {
#if UNITY_5_3_OR_NEWER
   if(FastForwarding)return;
   flashUntil[i]=Time.time+.055f;visualShots[i]++;
   if(i!=selected)return;
   var gun=combat.WeaponFor(i);float impulse=gun.id=="awp"?2.2f:gun.id=="desert_eagle"?1.6f:.65f;
   viewKick.x=Mathf.Clamp(viewKick.x-impulse-Mathf.Abs(kick.y)*.35f,-7,0);
   viewKick.y=Mathf.Clamp(viewKick.y+kick.x*.45f,-2,2);
   viewKick.z=Mathf.Clamp(viewKick.z+(visualShots[i]%2==0?1:-1)*impulse*.22f,-1,1);
   viewPush=Mathf.Min(.065f,viewPush+.016f*impulse);
#endif
  }
  void ResetShotPresentation()
  {
   ResetBulletTraces();
#if UNITY_5_3_OR_NEWER
   viewKick=Vector3.zero;viewPush=0;
   for(int i=0;i<10;i++){flashUntil[i]=0;visualShots[i]=0;if(muzzleFlashes[i]!=null)muzzleFlashes[i].SetActive(false);}
#endif
  }
  void ClearViewKick()
  {
#if UNITY_5_3_OR_NEWER
   viewKick=Vector3.zero;viewPush=0;
#endif
  }
  void UpdateShotPresentation()
  {
   UpdateBulletTraces();
#if UNITY_5_3_OR_NEWER
   if(combat==null)return;
   if(!combat.Alive(selected)){viewKick=Vector3.zero;viewPush=0;}
   // Select restores the authoritative pose first. These offsets never touch actors/AI.
   eyeCamera.transform.rotation*=Quaternion.Euler(viewKick.x*.15f,viewKick.y*.12f,viewKick.z*.18f);
   if(equipmentModel!=null)
   {
    equipmentModel.transform.localRotation=Quaternion.Euler(viewKick);
    equipmentModel.transform.localPosition+=new Vector3(viewKick.y*.001f,0,-viewPush);
    var dual=equipmentModel.GetComponent<DualWieldVisual>();if(dual!=null&&dual.enabled)dual.ApplyPose();
   }
   for(int i=0;i<10;i++)
   {
    bool active=flashUntil[i]>Time.time&&combat.Alive(i);
    if(!active){if(muzzleFlashes[i]!=null)muzzleFlashes[i].SetActive(false);continue;}
    GameObject model=i==selected?equipmentModel:characterVisuals[i];if(model==null)continue;
    Transform muzzle=null,grip=null;bool left=combat.WeaponFor(i).id=="dual_berettas"&&visualShots[i]%2==0;
    foreach(var t in model.GetComponentsInChildren<Transform>())
    {
     bool isLeft=false;for(var p=t;p!=null&&p!=model.transform;p=p.parent)if(p.name=="Left Beretta")isLeft=true;
     if(isLeft!=left)continue;
     if(t.name=="Muzzle")muzzle=t;if(t.name=="Grip")grip=t;
    }
    if(muzzle==null||grip==null)continue;
    EnsureFlash(i);var flash=muzzleFlashes[i];flash.layer=i==selected?13:11;
    flash.transform.SetPositionAndRotation(muzzle.position,Quaternion.LookRotation((muzzle.position-grip.position).normalized));
    float size=combat.WeaponFor(i).id=="m4a1_s"||combat.WeaponFor(i).id=="usp_s"?.3f:1;
    flash.transform.localScale=Vector3.one*size;flash.SetActive(true);
   }
   float recovery=1-Mathf.Exp(-18*Time.deltaTime);viewKick=Vector3.Lerp(viewKick,Vector3.zero,recovery);viewPush=Mathf.Lerp(viewPush,0,recovery);
#endif
  }
  void DisposeShotPresentation()
  {
   DisposeBulletTraces();
#if UNITY_5_3_OR_NEWER
   if(flashMesh!=null)Destroy(flashMesh);if(flashMaterial!=null)Destroy(flashMaterial);
#endif
  }
 }
}


