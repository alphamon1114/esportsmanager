using System;
using System.Collections.Generic;
using FpsManager;
using UnityEngine;
public static class AwperChecks
{
 static void Check(bool value,string message){if(!value)throw new Exception(message);}
 public static void Run()
 {
  var nav=new DeploymentNavigation(new List<Rect>{new Rect(42,49,2,2)});
  var order=new PlayerObjective{valid=true,task=PlayerTask.HoldSite,destination=new Vector2(40,47)};
  var position=new Vector2(40,47);var aim=new Vector2(1,0);Vector2 target,watch;
  // A visible firing position with nearby cover below it.
  var awp=new AwperMovement(7,nav);awp.Shot(position,aim,1.45f);
  bool hidden=false,held=false,released=false;
  for(int n=0;n<100;n++)
  {
   bool controlled=awp.Step(.05f,position,order,true,out target,out watch);
   if(!controlled){released=true;break;}
   Check(nav.Clear(position,target),"AWP retreat crosses wall");
   Check(Vector2.Distance(position+watch,new Vector2(60,47))<.01f,"AWP follows movement instead of fixed firing angle");
   var next=Vector2.MoveTowards(position,target,.175f);
   held|=Vector2.Distance(position,next)<.001f;
   position=next;hidden|=!nav.SightClear(position,new Vector2(60,47));
  }
  Check(awp.Retreats==1&&hidden&&held&&released,"AWP did not retreat, wait under cover, then release");
  awp.Shot(position,aim,1.45f);order.task=PlayerTask.Defuse;
  Check(!awp.Step(.1f,position,order,true,out target,out watch)&&!awp.Active,"AWP delayed urgent defuse");
  order.task=PlayerTask.HoldSite;awp.Shot(position,aim,1.45f);
  Check(!awp.Step(.1f,position,order,false,out target,out watch),"AWP movement survived weapon switch");
  var open=new AwperMovement(7,new DeploymentNavigation(new List<Rect>()));open.Shot(position,aim,1.45f);
  Check(!open.Step(.1f,position,order,true,out target,out watch),"AWP made arbitrary open-ground dodge");
  Check(!new AwperMovement(7,nav).Active,"round reset retained AWP retreat");
  int[] holdFrames=new int[2];
  for(int sniper=0;sniper<2;sniper++)
  {
   var peek=new PeekMovement(31,nav,70,true,80,false){SniperMode=sniper==1};
   var at=new Vector2(40,50);var probe=new Vector2(60,50);
   order=new PlayerObjective{valid=true,task=PlayerTask.HoldSite,destination=at};
   for(int frame=0;frame<160;frame++)
   {
    if(peek.Step(.05f,at,order,probe,false,out target,out watch))
    {
     var next=Vector2.MoveTowards(at,target,.175f);
     if(nav.SightClear(at,probe)&&Vector2.Distance(at,next)<.001f)holdFrames[sniper]++;
     at=next;
    }
    if(peek.Completed>0)break;
   }
  }
  Check(holdFrames[1]>holdFrames[0],"AWP angle hold remained rifle jiggle timing");
  UnityEditor.SceneManagement.EditorSceneManager.NewScene(UnityEditor.SceneManagement.NewSceneSetup.EmptyScene,UnityEditor.SceneManagement.NewSceneMode.Single);
  var game=new GameObject("AWP event integration").AddComponent<Prototype>();game.Initialize();game.StartMatch();game.AdvanceFrame(Prototype.BuySeconds+.01f);
  var field=typeof(Prototype).GetField("awperMovement",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic);
  var controllers=(AwperMovement[])field.GetValue(game);
  game.Combat.Equip(0,WeaponCatalog.Find("awp"));game.Combat.ShotFired(0);
  Check(controllers[0].Active,"actual shot event did not arm AWP retreat");
  controllers[0].Cancel();game.Combat.Equip(0,WeaponCatalog.Find("ak_47"));game.Combat.ShotFired(0);
  Check(!controllers[0].Active,"rifle shot armed AWP retreat");
  var flags=System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic;
  var update=typeof(Prototype).GetMethod("UpdateAwperWeapon",flags);
  var sight=new VisionSystem(new DeploymentNavigation(new List<Rect>()),new VisionSettings(),10);
  typeof(Prototype).GetField("vision",flags).SetValue(game,sight);
  var locations=new Vector2[10];var facings=new Vector2[10];var teams=new int[10];
  for(int i=0;i<10;i++){locations[i]=new Vector2(90,90);facings[i]=new Vector2(1,0);teams[i]=game.TeamIndexOf(i);}
  locations[0]=game.MapPosition(0);locations[5]=locations[0]+new Vector2(8,0);
  game.MatchState(0).equipment=new[]{"awp","usp_s"};
  game.Combat.EquipSaved(0,"awp",new WeaponAmmo{rounds=3,spares=1});game.Combat.ShotFired(0);
  // Sound alone cannot reveal an exact close target and trigger this visible-contact rule.
  sight.ReportSound(teams[0],5,0,locations[5]);update.Invoke(game,new object[]{0,.05f});
  Check(game.Combat.WeaponFor(0).id=="awp","sound-only contact triggered exact pistol response");
  sight.Tick(.5f,locations,facings,teams);Check(sight.Sees(0,5),"sidearm fixture has no visible target");
  update.Invoke(game,new object[]{0,.05f});
  Check(game.Combat.WeaponFor(0).id=="usp_s"&&!controllers[0].Active,"close target did not switch AWP to pistol");
  Check(!game.Combat.KnifeOut(0),"pistol response left knife drawn");
  game.Combat.EquipSaved(0,"usp_s",new WeaponAmmo{rounds=4,spares=1});
  update.Invoke(game,new object[]{0,2f});Check(game.Combat.WeaponFor(0).id=="usp_s","close duel interrupted when AWP bolt ready");
  sight.Reset();update.Invoke(game,new object[]{0,.1f});
  Check(game.Combat.WeaponFor(0).id=="awp"&&game.Combat.Magazine(0)==3&&game.Combat.SpareMagazines(0)==1,"AWP return lost or refilled ammunition");
  game.Combat.ShotFired(0);sight.Tick(.5f,locations,facings,teams);update.Invoke(game,new object[]{0,.05f});
  Check(game.Combat.WeaponFor(0).id=="usp_s"&&game.Combat.Magazine(0)==4&&game.Combat.SpareMagazines(0)==1,"repeat pistol swap refilled ammunition");
  sight.Reset();update.Invoke(game,new object[]{0,2f});
  game.MatchState(0).equipment=new[]{"awp"};game.Combat.ShotFired(0);sight.Tick(.5f,locations,facings,teams);update.Invoke(game,new object[]{0,.05f});
  Check(game.Combat.WeaponFor(0).id=="awp","missing pistol created free equipment");
  Debug.Log("AWPER_ALL_OK shot-cover, stable world aim, concealed wait, bounded release, urgent task, weapon switch, open-ground fallback, reset");
 }
}
