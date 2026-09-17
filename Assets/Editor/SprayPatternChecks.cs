using System;
using FpsManager;
using UnityEngine;
public static class SprayPatternChecks
{
 public static void Run()
 {
  double low=0,mid=0,high=0; bool variable=false;
  foreach(string id in WeaponCatalog.Ids)
  {
   var gun=WeaponCatalog.Find(id);
   if(SprayPatterns.Raw(gun,0)!=Vector2.zero) throw new Exception("First shot has accumulated recoil: "+id);
   for(int shot=1;shot<gun.magazineSize;shot++)
   {
    var raw=SprayPatterns.Raw(gun,shot);
    if(raw!=SprayPatterns.Raw(gun,shot)) throw new Exception("Pattern depends on random state");
    var a=SprayPatterns.Controlled(raw,0,new DeterministicRandom(shot));
    var b=SprayPatterns.Controlled(raw,50,new DeterministicRandom(shot));
    var c=SprayPatterns.Controlled(raw,100,new DeterministicRandom(shot));
    low+=a.sqrMagnitude; mid+=b.sqrMagnitude; high+=c.sqrMagnitude;
    if(c!=SprayPatterns.Controlled(raw,100,new DeterministicRandom(shot))) throw new Exception("Control error is not replayable");
    variable|=c!=SprayPatterns.Controlled(raw,100,new DeterministicRandom(shot+100));
   }
  }
  if(!(low>mid&&mid>high&&high>0)||!variable) throw new Exception("Aim compensation is not graded or becomes a laser");
  var ak=WeaponCatalog.Find("ak_47"); var m4=WeaponCatalog.Find("m4a1_s");
  if(SprayPatterns.Raw(ak,10)==SprayPatterns.Raw(m4,10)) throw new Exception("Weapons share one pattern");
  if(SprayPatterns.Raw(ak,12).x>=0||SprayPatterns.Raw(ak,24).x<=0) throw new Exception("AK pattern lost directional stages");
  // Compact inspection plot: raw AK path and the compensated paths, without random spread.
  var svg=new System.Text.StringBuilder("<svg xmlns='http://www.w3.org/2000/svg' width='900' height='360' viewBox='0 0 900 360'><rect width='900' height='360' fill='#101820'/>");
  string[] names={"AK-47 raw pattern","Aim 50 + control error","Aim 100 + control error"};
  for(int panel=0;panel<3;panel++)
  {
   float center=150+panel*300; var rng=new DeterministicRandom(17);
   svg.Append("<text x='").Append(panel*300+20).Append("' y='26' fill='white' font-family='sans-serif' font-size='16'>").Append(names[panel]).Append("</text>");
   for(int shot=0;shot<30;shot++)
   {
    Vector2 p=SprayPatterns.Raw(ak,shot); if(panel>0) p=SprayPatterns.Controlled(p,panel==1?50:100,rng);
    string x=(center+p.x*65).ToString("F2",System.Globalization.CultureInfo.InvariantCulture), y=(310-p.y*50).ToString("F2",System.Globalization.CultureInfo.InvariantCulture);
    svg.Append("<circle cx='").Append(x).Append("' cy='").Append(y).Append("' r='3' fill='#65c9fa'/>");
   }
  }
  svg.Append("<text x='20' y='348' fill='#ccd6df' font-family='sans-serif' font-size='12'>Prototype curves, not measured CS2 data. Base weapon spread is additional in gameplay. Same scale in all panels.</text></svg>");
  System.IO.File.WriteAllText(System.IO.Path.Combine(System.IO.Path.GetTempPath(),"spray-pattern-preview.svg"),svg.ToString());
  Debug.Log("SPRAY_PATTERN_ALL_OK 18 patterns, first-shot zero, direction changes, seeded control error, aim ordering, nonzero residual");
 }
}