using UnityEngine;
namespace FpsManager
{
    // Hand-authored prototype curves, NOT measured CS2 recoil coordinates.
    // Each row is horizontal motion at fixed stages of a full magazine.
    public static class SprayPatterns
    {
        static readonly float[][] lateral={
            new[]{0f,.1f,-.12f,.15f,-.08f,.1f}, // Glock
            new[]{0f,-.08f,.1f,-.12f,.08f,-.06f}, // USP
            new[]{0f,.3f,-.35f,.4f,-.3f,.2f}, // Duals
            new[]{0f,.15f,.4f,-.3f,-.45f,.2f}, // Tec9
            new[]{0f,-.12f,-.3f,.25f,.35f,-.1f}, // FiveSeven
            new[]{0f,.12f,-.18f,.2f,-.15f,.1f}, // Deagle
            new[]{0f,-.2f,-.5f,.6f,.25f,-.55f}, // MAC10
            new[]{0f,.18f,.48f,-.5f,-.15f,.4f}, // MP9
            new[]{0f,.08f,-.3f,.4f,.1f,-.3f}, // MP7
            new[]{0f,-.15f,.5f,.2f,-.55f,.35f}, // Bizon
            new[]{0f,.12f,.4f,-.3f,-.45f,.25f}, // UMP
            new[]{0f,-.15f,-.6f,.35f,.65f,-.25f}, // Galil
            new[]{0f,.08f,.4f,-.5f,-.2f,.35f}, // Famas
            new[]{0f,.05f,-.55f,-.85f,.7f,-.45f}, // AK
            new[]{0f,-.03f,.28f,.5f,-.42f,.2f}, // M4
            new[]{0f,.05f,-.35f,.45f,.2f,-.35f}, // AUG
            new[]{0f,-.08f,-.5f,.65f,-.2f,-.45f}, // SG
            new[]{0f,.03f,-.05f,.04f,-.04f,0f} // AWP
        };
        static readonly float[] climb={0,1.6f,3.1f,4.1f,4.6f,4.8f};
        public static Vector2 Raw(WeaponProfile gun,int shot)
        {
            if(shot<=0||gun.recoilPerShot<=0) return Vector2.zero;
            int index=System.Array.IndexOf(WeaponCatalog.Ids,gun.id); if(index<0) index=13;
            // Pattern timing belongs to the gun, not to the capacity of its magazine.
            float t=Mathf.Clamp01(shot/29f)*5;
            int segment=System.Math.Min(4,(int)t); float part=t-segment;
            float x=lateral[index][segment]+(lateral[index][segment+1]-lateral[index][segment])*part;
            float y=climb[segment]+(climb[segment+1]-climb[segment])*part;
            return new Vector2(x*2f,y)*(gun.recoilPerShot/.18f);
        }
        public static Vector2 Controlled(Vector2 pattern,int aim,DeterministicRandom random)
        {
            float skill=Mathf.Clamp01(aim/100f);
            float error=(.06f+.18f*(1-skill))*Mathf.Min(1,pattern.magnitude);
            return pattern*(1-.8f*skill)+new Vector2(random.NextSigned(),random.NextSigned())*error;
        }
    }
}