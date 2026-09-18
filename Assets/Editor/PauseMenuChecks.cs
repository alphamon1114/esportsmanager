using System;using FpsManager;using UnityEngine;
public static class PauseMenuChecks {
 static void Check(bool value,string reason){if(!value)throw new Exception(reason);}
 public static void Run(){
  var g=new GameObject("pause checks").AddComponent<Prototype>();g.Initialize();g.StartMatch();float buy=g.StageSeconds;int timeouts=g.TimeoutsRemaining;
  g.TogglePauseMenu();g.AdvanceFrame(20);Check(g.PauseMenuOpen&&g.StageSeconds==buy&&g.TimeoutsRemaining==timeouts,"pause consumed buy/timeouts");
  g.OpenPauseOptions();g.SetLanguage(true);Check(g.Ui("QUIT GAME")=="게임 종료","quit translation");g.TogglePauseMenu();Check(g.PauseMenuOpen,"options ESC resumed instead of back");g.TogglePauseMenu();Check(!g.PauseMenuOpen,"ESC did not resume");
  g.AdvanceFrame(Prototype.BuySeconds+.01f);float clock=g.Director.Clock;int shots=g.Combat.Shots;g.TogglePauseMenu();g.AdvanceFrame(10);Check(g.Director.Clock==clock&&g.Combat.Shots==shots,"live simulation advanced");g.ResumeFromPause();g.AdvanceFrame(.1f);Check(g.Director.Clock>clock,"resume did not advance");
  g.OpenMainMenu();g.TogglePauseMenu();g.ResumeFromPause();clock=g.Director.Clock;g.AdvanceFrame(10);Check(g.Director.Clock==clock,"pause changed underlying front menu");Debug.Log("PAUSE_MENU_ALL_OK buy/live freeze, timeout preservation, submenu ESC, language, resume, underlying menu");
 }
}
