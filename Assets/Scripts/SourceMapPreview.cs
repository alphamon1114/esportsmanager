#if UNITY_5_3_OR_NEWER
using UnityEngine;using System.IO;using System.Collections.Generic;
namespace FpsManager {
 public sealed class SourceMapPreview:MonoBehaviour {
  public string sourceMap;Dictionary<int,SourceMapData.Area> areas;
  void OnDrawGizmosSelected(){if(areas==null){string path=Path.Combine(Application.dataPath,"MapSources/2000908/"+sourceMap+".nav");if(!File.Exists(path))return;areas=SourceMapData.ReadNav(path);}Gizmos.color=new Color(.2f,.85f,1,.65f);foreach(var a in areas.Values){if(a.hull!=0)continue;for(int i=0;i<a.corners.Length;i++)Gizmos.DrawLine(a.corners[i]+Vector3.up*.04f,a.corners[(i+1)%a.corners.Length]+Vector3.up*.04f);}}
 }
}
#endif
