using UnityEngine;
namespace FpsManager {
 public partial class Prototype {
  public int ResolutionPreset {get;private set;}
  static readonly int[] resolutionWidths={1920,2560,3840},resolutionHeights={1080,1440,2160};
  public bool SetResolutionPreset(int preset){
   if(preset<0||preset>=resolutionWidths.Length)return false;ResolutionPreset=preset;
#if UNITY_5_3_OR_NEWER
   int width=resolutionWidths[preset],height=resolutionHeights[preset];
   // The observer used a fixed 800x450 texture; increasing the window alone would stay blurry.
   if(eyeCamera!=null&&(eyeTexture==null||eyeTexture.width!=width||eyeTexture.height!=height)){
    var previous=eyeTexture;eyeTexture=new RenderTexture(width,height,16){name="Observer resolution"};eyeTexture.Create();eyeCamera.targetTexture=eyeTexture;if(equipmentCamera!=null)equipmentCamera.targetTexture=eyeTexture;
    if(previous!=null){previous.Release();if(Application.isPlaying)Destroy(previous);else DestroyImmediate(previous);}
   }
#if !UNITY_EDITOR && !UNITY_WEBGL
   Screen.SetResolution(width,height,Screen.fullScreenMode);
#endif
   PlayerPrefs.SetInt("fps.resolution",preset);PlayerPrefs.Save();
#endif
   return true;
  }
  void LoadResolution(){
#if UNITY_5_3_OR_NEWER
   int saved=PlayerPrefs.GetInt("fps.resolution",0);SetResolutionPreset(saved>=0&&saved<3?saved:0);
#endif
  }
  void DrawResolutionOptions(float x,float y,float width){
   HudText(new Rect(x,y,width,30),"RESOLUTION",22,HudWhite,true);
   string[] names={"1K / FHD","2K / QHD","4K / UHD"};float cell=(width-20)/3;
   for(int i=0;i<3;i++){float left=x+i*(cell+10);if(HudButton(new Rect(left,y+40,cell,48),names[i],true,ResolutionPreset==i))SetResolutionPreset(i);HudText(new Rect(left+8,y+92,cell-8,25),resolutionWidths[i]+" x "+resolutionHeights[i],14,HudMuted);}
#if UNITY_EDITOR
   HudText(new Rect(x,y+122,width,25),"Editor: Game view size is set separately.",14,HudMuted);
#endif
  }
 }
}
