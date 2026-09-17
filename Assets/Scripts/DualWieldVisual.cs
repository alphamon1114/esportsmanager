#if UNITY_5_3_OR_NEWER
using UnityEngine;
namespace FpsManager
{
    // Presentation-only two-bone arm adjustment after the generic Animator evaluates.
    // Both first-person arms and world characters use this component.
    [DefaultExecutionOrder(100)]
    public sealed class DualWieldVisual : MonoBehaviour
    {
        Transform upper,fore,hand,rightHand,rightSocket;
        GameObject second;
        public void Initialize(GameObject prefab,Transform socket)
        {
            rightSocket=socket;
            foreach(var t in GetComponentsInChildren<Transform>(true))
            {
                if(t.name=="mixamorig:LeftArm")upper=t;
                if(t.name=="mixamorig:LeftForeArm")fore=t;
                if(t.name=="mixamorig:LeftHand")hand=t;
                if(t.name=="mixamorig:RightHand")rightHand=t;
            }
            second=Instantiate(prefab,transform,false);second.name="Left Beretta";
            foreach(var t in second.GetComponentsInChildren<Transform>(true))t.gameObject.layer=gameObject.layer;
        }
        public void ApplyPose()
        {
            if(second==null||hand==null||rightHand==null||upper==null||fore==null)return;
            Vector3 target=rightHand.position-transform.right*.30f;
            float a=Vector3.Distance(upper.position,fore.position),b=Vector3.Distance(fore.position,hand.position);
            Vector3 delta=target-upper.position;
            float d=Mathf.Clamp(delta.magnitude,Mathf.Abs(a-b)+.001f,a+b-.001f);
            Vector3 direction=delta.normalized;
            Vector3 bend=Vector3.ProjectOnPlane(-transform.up-transform.right*.4f,direction).normalized;
            float along=(a*a-b*b+d*d)/(2*d);
            Vector3 elbow=upper.position+direction*along+bend*Mathf.Sqrt(Mathf.Max(0,a*a-along*along));
            upper.rotation=Quaternion.FromToRotation(fore.position-upper.position,elbow-upper.position)*upper.rotation;
            fore.rotation=Quaternion.FromToRotation(hand.position-fore.position,target-fore.position)*fore.rotation;
            hand.rotation=rightHand.rotation;
            second.transform.SetPositionAndRotation(rightSocket.position+hand.position-rightHand.position,rightSocket.rotation);
            Vector3 scale=rightSocket.lossyScale, parentScale=transform.lossyScale;
            second.transform.localScale=new Vector3(scale.x/parentScale.x,scale.y/parentScale.y,scale.z/parentScale.z);
        }
        void LateUpdate(){ApplyPose();}
        public void Clear()
        {
            if(second==null)return;
            second.SetActive(false);
            if(Application.isPlaying)Destroy(second);else DestroyImmediate(second);
            second=null;
        }
        void OnDestroy(){Clear();}
    }
}
#endif

