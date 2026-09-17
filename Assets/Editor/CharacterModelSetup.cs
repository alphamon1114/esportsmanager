#if UNITY_EDITOR
using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEditor.SceneManagement;
public static class CharacterModelSetup
{
    [MenuItem("FPS Manager/Rebuild Character Prefabs")]
    public static void Build()
    {
        Directory.CreateDirectory("Assets/Resources/Characters");AssetDatabase.Refresh();
        if(File.Exists("Assets/Art/LowPoly/Characters/char_ct_operator.fbx")) { LowPolyModelSetup.Build(); return; }
        foreach(string side in new[]{"CT","T"})
        {
            string folder="Assets/Art/Characters/"+side;
            foreach(string texture in new[]{"base_color","normal"})
            {
                string path=folder+"/model-material_0-"+texture+".png";
                var importer=(TextureImporter)AssetImporter.GetAtPath(path);
                importer.textureType=texture=="normal"?TextureImporterType.NormalMap:TextureImporterType.Default;
                importer.maxTextureSize=1024;importer.mipmapEnabled=true;
                importer.textureCompression=TextureImporterCompression.Compressed;importer.SaveAndReimport();
            }
            var modelImporter=(ModelImporter)AssetImporter.GetAtPath(folder+"/model.obj");
            modelImporter.importAnimation=false;modelImporter.addCollider=false;
            modelImporter.materialImportMode=ModelImporterMaterialImportMode.None;
            modelImporter.meshCompression=ModelImporterMeshCompression.Medium;
            modelImporter.isReadable=false;modelImporter.SaveAndReimport();
            string materialPath=folder+"/Soldier.mat";
            var material=AssetDatabase.LoadAssetAtPath<Material>(materialPath);
            if(material==null){material=new Material(Shader.Find("Standard"));AssetDatabase.CreateAsset(material,materialPath);}
            material.mainTexture=AssetDatabase.LoadAssetAtPath<Texture2D>(folder+"/model-material_0-base_color.png");
            material.SetTexture("_BumpMap",AssetDatabase.LoadAssetAtPath<Texture2D>(folder+"/model-material_0-normal.png"));
            material.EnableKeyword("_NORMALMAP");material.SetFloat("_BumpScale",.55f);material.SetFloat("_Glossiness",.15f);material.SetFloat("_Metallic",0);
            EditorUtility.SetDirty(material);
            var root=new GameObject(side);var mesh=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(folder+"/model.obj"));
            mesh.transform.SetParent(root.transform,false);
            mesh.transform.localRotation=Quaternion.identity;
            var renderers=mesh.GetComponentsInChildren<Renderer>();Bounds bounds=renderers[0].bounds;
            foreach(var renderer in renderers){bounds.Encapsulate(renderer.bounds);renderer.sharedMaterial=material;}
            float scale=1.8f/bounds.size.y;mesh.transform.localScale=Vector3.one*scale;
            mesh.transform.localPosition=new Vector3(-bounds.center.x*scale,-bounds.min.y*scale,-bounds.center.z*scale);
            PrefabUtility.SaveAsPrefabAsset(root,"Assets/Resources/Characters/"+side+".prefab");UnityEngine.Object.DestroyImmediate(root);
        }
        AssetDatabase.SaveAssets();AssetDatabase.Refresh();Debug.Log("CHARACTER_BUILD_OK");
    }
    public static void Verify()
    {
        Build();CheckNewWeapons();EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
        string output=Environment.GetEnvironmentVariable("CHARACTER_QA_OUTPUT")??Path.GetTempPath();Directory.CreateDirectory(output);
        var sun=new GameObject("QA light").AddComponent<Light>();sun.type=LightType.Directional;sun.intensity=1.5f;sun.transform.rotation=Quaternion.Euler(35,-30,0);
        RenderSettings.ambientLight=new Color(.45f,.45f,.45f);
        var camera=new GameObject("QA camera").AddComponent<Camera>();camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.12f,.15f,.19f);camera.fieldOfView=40;
        foreach(string side in new[]{"CT","T"})
        {
            var model=UnityEngine.Object.Instantiate(Resources.Load<GameObject>("Characters/"+side));
            var renderers=model.GetComponentsInChildren<Renderer>();Bounds bounds=renderers[0].bounds;
            int triangles=0;foreach(var r in renderers){bounds.Encapsulate(r.bounds);if(r.sharedMaterial==null)throw new Exception("Missing material");}
            foreach(var mesh in model.GetComponentsInChildren<MeshFilter>())triangles+=mesh.sharedMesh.triangles.Length/3;
            foreach(var mesh in model.GetComponentsInChildren<SkinnedMeshRenderer>())triangles+=mesh.sharedMesh.triangles.Length/3;
            var motion=model.GetComponent<FpsManager.CharacterAnimation>();if(motion!=null){motion.SetState(true,side=="CT"?"m4a1_s":"ak_47",side=="CT");CheckAnimation(model,camera,output,side);}
            if(Mathf.Abs(bounds.size.y-1.8f)>.03f||Mathf.Abs(bounds.min.y)>.03f)throw new Exception("Model normalization failed");
            for(int view=0;view<2;view++)
            {
                camera.transform.position=new Vector3(view==0?0:2.7f,1.1f,view==0?3.8f:2.7f);camera.transform.LookAt(new Vector3(0,.9f,0));
                Capture(camera,Path.Combine(output,side+"-"+view+".png"),700,800);
            }
            Debug.Log("CHARACTER_MODEL_OK "+side+" height="+bounds.size.y+" triangles="+triangles);
            UnityEngine.Object.DestroyImmediate(model);
        }
        UnityEngine.Object.DestroyImmediate(camera.gameObject);UnityEngine.Object.DestroyImmediate(sun.gameObject);
        var game=new GameObject("Integration").AddComponent<FpsManager.Prototype>();game.Initialize();
        CheckSides(game);game.SwapPreviewSides();CheckSides(game);
        game.Select(0);
        foreach(var animator in game.GetComponentsInChildren<Animator>())animator.Update(.2f);
        var flags=System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance;
        var eye=(Camera)typeof(FpsManager.Prototype).GetField("eyeCamera",flags).GetValue(game);
        Capture(eye,Path.Combine(output,"game-pov.png"),1000,600);
        var map=(Camera)typeof(FpsManager.Prototype).GetField("mapCamera",flags).GetValue(game);
        if((map.cullingMask&((1<<10)|(1<<11)|(1<<12)))!=0)throw new Exception("Map markers/model mask failed");
        if((eye.cullingMask&((1<<10)|(1<<12)))!=0)throw new Exception("First-person self/capsule mask failed");
        Capture(map,Path.Combine(output,"game-map.png"),800,800);
        // Dead artwork lies down; the logical capsule and position remain authoritative.
        var fire=typeof(FpsManager.CombatSystem).GetMethod("Fire",flags);
        game.Combat.Equip(0,new FpsManager.WeaponProfile{damage=10000,baseSpreadDegrees=0,recoilPerShot=0});
        fire.Invoke(game.Combat,new object[]{0,5,0f,10f,100});game.Select(0);
        var visuals=(GameObject[])typeof(FpsManager.Prototype).GetField("characterVisuals",flags).GetValue(game);
        if(Quaternion.Angle(visuals[5].transform.localRotation,Quaternion.Euler(90,0,0))>.1f)throw new Exception("Death visual not updated");
        game.PrepareNextRound();
        if(Quaternion.Angle(visuals[5].transform.localRotation,Quaternion.identity)>.1f)throw new Exception("Reset did not restore pose");
        CheckEquipment(game,eye,output);
        Debug.Log("CHARACTER_INTEGRATION_OK sides, hierarchy layers, selected body hidden; model and equipment rendering");
    }
    static void CheckNewWeapons()
    {
        foreach(string id in new[]{"galil_ar","tec_9","five_seven","dual_berettas","awp","famas"})
        {
            var prefab=Resources.Load<GameObject>("Weapons/weapon_"+id);
            if(prefab==null)throw new Exception("Missing model "+id);
            var character=UnityEngine.Object.Instantiate(Resources.Load<GameObject>("Characters/CT"));
            character.GetComponent<FpsManager.CharacterAnimation>().SetState(true,id,true);
            character.GetComponentInChildren<Animator>().Update(.2f);
            var model=Bone(character,"Socket_RightHand").GetChild(0).gameObject;
            var renderers=model.GetComponentsInChildren<Renderer>();
            if(renderers.Length==0)throw new Exception("Empty weapon "+id);
            Bounds bounds=renderers[0].bounds;
            foreach(var r in renderers){bounds.Encapsulate(r.bounds);foreach(var m in r.sharedMaterials)if(m==null)throw new Exception("Missing weapon material "+id);}
            float size=Mathf.Max(bounds.size.x,Mathf.Max(bounds.size.y,bounds.size.z));
            if(size<.15f||size>1.4f)throw new Exception("Weapon scale "+id+": "+size);
            if(Vector3.Dot((Bone(model,"Muzzle").position-Bone(model,"Grip").position).normalized,Vector3.forward)<.8f)throw new Exception("Weapon direction "+id);
            UnityEngine.Object.DestroyImmediate(character);
        }
        Debug.Log("NEW_WEAPONS_OK six prefabs, geometry size, materials, muzzle direction");
    }
    static void CheckEquipment(FpsManager.Prototype game,Camera eye,string output)
    {
        var flags=System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance;
        var field=typeof(FpsManager.Prototype).GetField("equipmentCamera",flags);
        foreach(string id in new[]{"ak_47","m4a1_s","glock_18","usp_s","desert_eagle","galil_ar","tec_9","five_seven","dual_berettas","awp","famas"})
        {
            game.Combat.Equip(0,FpsManager.WeaponCatalog.Find(id));game.Select(0);
            var overlay=(Camera)field.GetValue(game);
            if(!overlay.enabled||overlay.cullingMask!=(1<<13)||(eye.cullingMask&(1<<13))!=0)throw new Exception("Equipment camera mask");
            var arms=(GameObject)typeof(FpsManager.Prototype).GetField("equipmentModel",flags).GetValue(game);
            arms.GetComponentInChildren<Animator>().Update(.2f);
            var dual=arms.GetComponent<FpsManager.DualWieldVisual>();if(dual!=null)dual.ApplyPose();
            if(id=="dual_berettas")
            {
                var left=Bone(arms,"Left Beretta");var right=Bone(arms,"Socket_RightHand");
                if(Vector3.Distance(left.position,right.position)<.20f||Vector3.Distance(left.position,right.position)>.40f)throw new Exception("Dual pistol spacing");
                if(Mathf.Abs(left.lossyScale.x/right.lossyScale.x-1)>.01f)throw new Exception("Dual pistol scale");
            }
            if(arms.GetComponent<FpsManager.CharacterAnimation>().VisualWeapon!=FpsManager.CharacterAnimation.ModelFor(id,game.TeamIndexOf(0)==game.CounterTerroristTeam))throw new Exception("Equipment weapon mismatch");
            Capture(eye,Path.Combine(output,"pov-"+id+".png"),1000,600);
        }
        var wall=GameObject.CreatePrimitive(PrimitiveType.Cube);
        wall.transform.position=eye.transform.position+eye.transform.forward*1f;wall.transform.localScale=new Vector3(4,4,.1f);wall.transform.rotation=eye.transform.rotation;
        var wallMaterial=new Material(Shader.Find("Standard"));wallMaterial.color=Color.gray;wall.GetComponent<Renderer>().sharedMaterial=wallMaterial;
        Capture(eye,Path.Combine(output,"pov-near-wall.png"),1000,600);UnityEngine.Object.DestroyImmediate(wall);UnityEngine.Object.DestroyImmediate(wallMaterial);
        game.Combat.Equip(1,new FpsManager.WeaponProfile{damage=10000,baseSpreadDegrees=0,recoilPerShot=0});
        typeof(FpsManager.CombatSystem).GetMethod("Fire",flags).Invoke(game.Combat,new object[]{1,0,0f,10f,100});
        game.Select(0);if(((Camera)field.GetValue(game)).enabled)throw new Exception("Dead player equipment visible");
        game.Select(1);if(!((Camera)field.GetValue(game)).enabled)throw new Exception("Selection did not restore equipment");
        game.PrepareNextRound();game.Select(0);if(!((Camera)field.GetValue(game)).enabled)throw new Exception("Reset did not restore equipment");
        Debug.Log("FIRST_PERSON_EQUIPMENT_OK weapon selection, masks, death, spectator change, reset");
    }
    static Transform Bone(GameObject model,string name)
    {
        foreach(var t in model.GetComponentsInChildren<Transform>())if(t.name==name)return t;
        throw new Exception("Missing bone "+name);
    }
    static void CheckAnimation(GameObject model,Camera camera,string output,string side)
    {
        var a=model.GetComponentInChildren<Animator>();
        var arm=Bone(model,"mixamorig:RightArm");
        var leg=Bone(model,"mixamorig:LeftUpLeg");
        Quaternion bind=arm.localRotation;
        a.Update(.2f);
        if(Quaternion.Angle(bind,arm.localRotation)<10)throw new Exception("Upper pose stayed in bind position");
        var muzzle=Bone(model,"Muzzle");var grip=Bone(model,"Grip");
        if(Vector3.Dot((muzzle.position-grip.position).normalized,Vector3.forward)<.8f)throw new Exception("Weapon points away from aim");
        a.SetFloat("Speed",2.4f);a.Update(.15f);
        Quaternion first=leg.localRotation;a.Update(.3f);
        if(Quaternion.Angle(first,leg.localRotation)<5)throw new Exception("Walk legs not animating");
        camera.transform.position=new Vector3(2.7f,1.1f,2.7f);camera.transform.LookAt(new Vector3(0,.9f,0));
        Capture(camera,Path.Combine(output,side+"-walk.png"),700,800);
        a.SetFloat("Speed",5);a.Update(.15f);first=leg.localRotation;float runChange=0; for(int frame=0;frame<8;frame++){a.Update(.07f);runChange=Mathf.Max(runChange,Quaternion.Angle(first,leg.localRotation));}
        if(runChange<5)throw new Exception("Run legs not animating");
        a.SetFloat("Speed",0);a.Update(.1f);
        model.GetComponent<FpsManager.CharacterAnimation>().SetState(true,side=="CT"?"usp_s":"glock_18",side=="CT");a.Update(.2f);
        Capture(camera,Path.Combine(output,side+"-pistol.png"),700,800);
        model.GetComponent<FpsManager.CharacterAnimation>().SetState(true,side=="CT"?"m4a1_s":"ak_47",side=="CT");a.Update(.2f);
        Debug.Log("LOWPOLY_ANIMATION_OK "+side+" upper pose, walk, run, gun forward, pistol switch");
    }
    static void CheckSides(FpsManager.Prototype game)
    {
        var flags=System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance;
        var models=(GameObject[])typeof(FpsManager.Prototype).GetField("characterVisuals",flags).GetValue(game);
        for(int i=0;i<10;i++)
        {
            bool ct=game.TeamIndexOf(i)==game.CounterTerroristTeam;
            if(models[i]==null||models[i].name!=(ct?"CT soldier":"T desert soldier"))throw new Exception("Wrong side model");
            foreach(var tr in models[i].GetComponentsInChildren<Transform>())if(tr.gameObject.layer!=(i==0?12:11))throw new Exception("Wrong child layer");
        }
    }
    static void Capture(Camera camera,string path,int width,int height)
    {
        var previous=camera.targetTexture;var active=RenderTexture.active;var target=new RenderTexture(width,height,24);
        camera.targetTexture=target;camera.Render();
        foreach(var overlay in camera.GetComponentsInChildren<Camera>())if(overlay!=camera&&overlay.enabled){var saved=overlay.targetTexture;overlay.targetTexture=target;overlay.Render();overlay.targetTexture=saved;}
        RenderTexture.active=target;var image=new Texture2D(width,height,TextureFormat.RGB24,false);image.ReadPixels(new Rect(0,0,width,height),0,0);image.Apply();File.WriteAllBytes(path,image.EncodeToPNG());
        camera.targetTexture=previous;RenderTexture.active=active;UnityEngine.Object.DestroyImmediate(image);target.Release();UnityEngine.Object.DestroyImmediate(target);
    }
}
#endif


