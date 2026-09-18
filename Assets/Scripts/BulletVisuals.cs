using UnityEngine;
namespace FpsManager {
 public partial class Prototype {
#if UNITY_5_3_OR_NEWER
  readonly LineRenderer[] bulletLines=new LineRenderer[24];readonly float[] bulletExpiry=new float[24];int bulletCursor;Material bulletMaterial;
#endif
  void PresentBullet(int shooter,Vector3 start,Vector3 end){
#if UNITY_5_3_OR_NEWER
   if(FastForwarding)return;int n=bulletCursor++%bulletLines.Length;
   if(bulletLines[n]==null){var go=new GameObject("Bullet trace");go.layer=11;go.transform.SetParent(transform,false);var line=go.AddComponent<LineRenderer>();line.useWorldSpace=true;line.positionCount=2;line.startWidth=.012f;line.endWidth=.006f;line.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;line.receiveShadows=false;
    if(bulletMaterial==null){bulletMaterial=new Material(Shader.Find("Sprites/Default"));bulletMaterial.color=new Color(1,.8f,.45f,.28f);}line.sharedMaterial=bulletMaterial;bulletLines[n]=line;}
   var direction=(end-start).normalized;float length=(end-start).magnitude;
   // Start away from the eye so the line never covers the crosshair or viewmodel.
   bulletLines[n].SetPosition(0,start+direction*Mathf.Min(.8f,length));bulletLines[n].SetPosition(1,end);bulletLines[n].enabled=true;bulletExpiry[n]=Time.time+.045f;
#endif
  }
  void UpdateBulletTraces(){
#if UNITY_5_3_OR_NEWER
   for(int i=0;i<bulletLines.Length;i++)if(bulletLines[i]!=null&&Time.time>=bulletExpiry[i])bulletLines[i].enabled=false;
#endif
  }
  void ResetBulletTraces(){
#if UNITY_5_3_OR_NEWER
   for(int i=0;i<bulletLines.Length;i++)if(bulletLines[i]!=null)bulletLines[i].enabled=false;
#endif
  }
  void DisposeBulletTraces(){
#if UNITY_5_3_OR_NEWER
   if(bulletMaterial!=null)Destroy(bulletMaterial);
#endif
  }
 }
}
