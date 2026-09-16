using System.Collections.Generic;
using UnityEngine;
namespace FpsManager
{
    public partial class Prototype
    {
        readonly List<GameObject> utilityObjects=new List<GameObject>();
        Material smokeVisual,flashVisual;
        Vector2 UtilityPosition(PlayerAutonomy.Utility effect)
        { float t=Mathf.Clamp01(1-effect.fuse); return effect.origin+(effect.position-effect.origin)*t; }
        bool UtilityKnown(PlayerAutonomy.Utility effect)
        {
            if(!fogOfWar||teamIndex[effect.owner]==AlliedTeamIndex) return true;
            Vector2 point=UtilityPosition(effect);
            for(int i=0;i<actors.Count;i++)
            {
                if(teamIndex[i]!=AlliedTeamIndex||!combat.Alive(i)||autonomy.Blinded[i]) continue;
                Vector2 delta=point-MapPosition(i);
                if(delta.magnitude>visionSettings.maxRange||!navigation.SightClear(MapPosition(i),point)) continue;
                if(delta.magnitude<=visionSettings.peripheralRange||Mathf.Abs(CombatSystem.SignedAngle(MapFacing(i),delta))<45) return true;
            }
            return false;
        }
        void SyncUtilityVisuals()
        {
            int count=autonomy==null?0:autonomy.UtilityCount;
            if(count>0&&smokeVisual==null)
            { smokeVisual=Material(new Color(.55f,.59f,.62f)); flashVisual=Material(new Color(1f,.95f,.35f)); }
            while(utilityObjects.Count<count)
            {
                var obj=GameObject.CreatePrimitive(PrimitiveType.Sphere);
                obj.name="Utility dummy"; obj.transform.SetParent(transform);
                obj.GetComponent<Collider>().enabled=false;
                utilityObjects.Add(obj);
            }
            for(int i=0;i<utilityObjects.Count;i++)
            {
                var obj=utilityObjects[i]; var renderer=obj.GetComponent<Renderer>();
                renderer.enabled=i<count; if(i>=count) continue;
                var effect=autonomy.UtilityAt(i); bool airborne=effect.fuse>0;
                float t=Mathf.Clamp01(1-effect.fuse);
                obj.transform.position=World(UtilityPosition(effect),airborne?1.5f+Mathf.Sin(t*Mathf.PI)*2:1.5f);
                obj.transform.localScale=airborne?new Vector3(.65f,.65f,.65f):effect.smoke?new Vector3(10,6,10):new Vector3(2,2,2);
                renderer.sharedMaterial=effect.smoke?smokeVisual:flashVisual;
                // Hidden enemy utility remains physically visible in POV, not omniscient on map.
                obj.layer=UtilityKnown(effect)?0:9;
            }
        }
        void DrawUtilityMap(Rect map)
        {
            if(autonomy==null) return;
            for(int i=0;i<autonomy.UtilityCount;i++)
            {
                var effect=autonomy.UtilityAt(i); if(!UtilityKnown(effect)) continue;
                var point=mapCamera.WorldToViewportPoint(World(UtilityPosition(effect)));
                string label=effect.smoke?"SMOKE":"FLASH";
                if(effect.fuse>0) label+=" >";
                else if(effect.smoke) label+=" "+Mathf.Max(0,effect.life).ToString("F1")+"s";
                GUI.Label(new Rect(map.x+point.x*map.width-36,map.y+(1-point.y)*map.height-12,95,24),label);
            }
        }
        void DrawUtilityPov(Rect viewport)
        {
            if(autonomy==null) return;
            Color old=GUI.color;
            bool inside=false;
            for(int i=0;i<autonomy.UtilityCount;i++)
            {
                var effect=autonomy.UtilityAt(i);
                if(effect.smoke&&effect.fuse<=0&&effect.life>0&&Vector2.Distance(MapPosition(selected),effect.position)<5) inside=true;
            }
            if(inside) { GUI.color=new Color(.55f,.59f,.62f,1); GUI.DrawTexture(viewport,Texture2D.whiteTexture,ScaleMode.StretchToFill); }
            float blind=autonomy.BlindRemaining(selected);
            if(blind>0) { GUI.color=new Color(1,1,1,Mathf.Clamp01(blind/.5f)); GUI.DrawTexture(viewport,Texture2D.whiteTexture,ScaleMode.StretchToFill); }
            GUI.color=old;
        }
    }
}