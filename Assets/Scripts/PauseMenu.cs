using UnityEngine;
namespace FpsManager {
 public partial class Prototype {
  // Separate from tactical timeouts and the legacy movement-test pause.
  int pausePage; // 0 closed, 1 pause, 2 options, 3 quit confirmation
  public bool PauseMenuOpen {get{return pausePage!=0;}}
  public void TogglePauseMenu(){if(pausePage>1)pausePage=1;else pausePage=pausePage==0?1:0;}
  public void ResumeFromPause(){pausePage=0;}
  public void OpenPauseOptions(){if(PauseMenuOpen)pausePage=2;}
  void CheckPauseInput(){
#if ENABLE_INPUT_SYSTEM && !ENABLE_LEGACY_INPUT_MANAGER
   if(UnityEngine.InputSystem.Keyboard.current!=null&&UnityEngine.InputSystem.Keyboard.current.escapeKey.wasPressedThisFrame)TogglePauseMenu();
#elif UNITY_5_3_OR_NEWER
   if(Input.GetKeyDown(KeyCode.Escape))TogglePauseMenu();
#endif
  }
  void QuitFromPause(){
   if(Tournament!=null)SaveTournament();
#if UNITY_EDITOR
   UnityEditor.EditorApplication.isPlaying=false;
#elif UNITY_WEBGL
   pausePage=0;OpenMainMenu();
#elif UNITY_5_3_OR_NEWER
   Application.Quit();
#endif
  }
  void DrawPauseMenu(){
   if(eyeTexture!=null)GUI.DrawTexture(new Rect(16,80,1316,740),eyeTexture,ScaleMode.StretchToFill);
   HudPanel(new Rect(0,0,1600,900),new Color(.015f,.025f,.04f,.85f));
   HudPanel(new Rect(510,195,580,510),new Color(.055f,.085f,.12f,.98f));
   HudText(new Rect(550,225,500,48),pausePage==2?"OPTIONS":pausePage==3?"QUIT GAME":"PAUSED",30,HudWhite,true);
   if(pausePage==1){
    if(HudButton(new Rect(550,310,500,64),"RESUME GAME"))ResumeFromPause();
    if(HudButton(new Rect(550,394,500,64),"OPTIONS"))OpenPauseOptions();
    if(HudButton(new Rect(550,478,500,64),"QUIT GAME"))pausePage=3;
    HudText(new Rect(550,602,500,28),"ESC: resume / back",17,HudMuted);
   }else if(pausePage==2){
    HudText(new Rect(550,307,500,35),"LANGUAGE",22,HudWhite,true);
    if(HudButton(new Rect(550,367,240,64),"한국어",true,korean))SetLanguage(true);
    if(HudButton(new Rect(810,367,240,64),"English",true,!korean))SetLanguage(false);
    DrawResolutionOptions(550,445,500);
    if(HudButton(new Rect(550,620,500,52),"BACK"))pausePage=1;
   }else{
    HudText(new Rect(550,310,500,32),"Quit the game?",22,HudWhite,true);
    HudText(new Rect(550,366,500,30),"Completed tournament maps are saved.",17,HudMuted);
    HudText(new Rect(550,405,500,30),"Progress in the current map is not saved.",17,HudGold);
    if(HudButton(new Rect(550,525,240,64),"BACK"))pausePage=1;
    if(HudButton(new Rect(810,525,240,64),"QUIT GAME"))QuitFromPause();
   }
  }
 }
}
