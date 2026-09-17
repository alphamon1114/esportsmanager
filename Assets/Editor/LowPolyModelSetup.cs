#if UNITY_EDITOR
using System;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
public static class LowPolyModelSetup
{
    const string Art="Assets/Art/LowPoly";
    public static void Build()
    {
        System.IO.Directory.CreateDirectory("Assets/Resources/Weapons");
        AssetDatabase.Refresh();
        foreach(string file in System.IO.Directory.GetFiles(Art+"/Weapons","*.fbx"))
        {
            string path=file.Replace('\\','/');
            var importer=(ModelImporter)AssetImporter.GetAtPath(path);
            importer.importAnimation=false;importer.addCollider=false;
            importer.materialImportMode=ModelImporterMaterialImportMode.ImportStandard;
            importer.SaveAndReimport();
            var model=AssetDatabase.LoadAssetAtPath<GameObject>(path);
            var root=(GameObject)PrefabUtility.InstantiatePrefab(model);
            PrefabUtility.SaveAsPrefabAsset(root,"Assets/Resources/Weapons/"+System.IO.Path.GetFileNameWithoutExtension(path)+".prefab");
            UnityEngine.Object.DestroyImmediate(root);
        }
        foreach(string side in new[]{"CT","T"})
        {
            string path=Art+"/Characters/char_"+side.ToLower()+"_operator.fbx";
            var importer=(ModelImporter)AssetImporter.GetAtPath(path);
            importer.animationType=ModelImporterAnimationType.Generic;
            importer.importAnimation=true;importer.addCollider=false;importer.optimizeGameObjects=false;
            importer.materialImportMode=ModelImporterMaterialImportMode.ImportStandard;
            importer.meshCompression=ModelImporterMeshCompression.Off;
            importer.SaveAndReimport();
            var clips=importer.defaultClipAnimations;
            foreach(var clip in clips) { clip.loopTime=true; clip.loopPose=true; clip.lockRootRotation=true;clip.lockRootHeightY=true;clip.lockRootPositionXZ=true; }
            importer.clipAnimations=clips;importer.SaveAndReimport();
            var model=AssetDatabase.LoadAssetAtPath<GameObject>(path);
            var animations=AssetDatabase.LoadAllAssetsAtPath(path).OfType<AnimationClip>().Where(c=>!c.name.StartsWith("__")).ToArray();
            foreach(var clip in animations) Debug.Log("LOWPOLY_CLIP "+side+" "+clip.name+" "+clip.length);
            Func<string,AnimationClip> find=name=>animations.Single(c=>c.name.EndsWith(name,StringComparison.Ordinal));
            var root=new GameObject(side);
            var mesh=(GameObject)PrefabUtility.InstantiatePrefab(model);mesh.transform.SetParent(root.transform,false);
            var renderers=mesh.GetComponentsInChildren<Renderer>();Bounds bounds=renderers[0].bounds;
            foreach(var r in renderers) bounds.Encapsulate(r.bounds);
            float scale=1.8f/bounds.size.y;mesh.transform.localScale=Vector3.one*scale;
            mesh.transform.localPosition=new Vector3(-bounds.center.x*scale,-bounds.min.y*scale,-bounds.center.z*scale);
            string controllerPath=Art+"/Characters/"+side+".controller";
            var controller=AssetDatabase.LoadAssetAtPath<AnimatorController>(controllerPath);
            if(controller==null) controller=AnimatorController.CreateAnimatorControllerAtPath(controllerPath);
            // Clear generated subassets on rebuild, preserving the controller's GUID.
            foreach(var asset in AssetDatabase.LoadAllAssetsAtPath(controllerPath))
                if(asset!=controller) UnityEngine.Object.DestroyImmediate(asset,true);
            controller.parameters=new AnimatorControllerParameter[0];
            controller.layers=new AnimatorControllerLayer[0];
            controller.AddParameter("Speed",AnimatorControllerParameterType.Float);
            var lower=new AnimatorStateMachine{name="Lower"};AssetDatabase.AddObjectToAsset(lower,controller);
            var lowerLayer=new AnimatorControllerLayer{name="Lower",stateMachine=lower,defaultWeight=1};
            controller.AddLayer(lowerLayer);
            var tree=new BlendTree{name="Locomotion",blendParameter="Speed",blendType=BlendTreeType.Simple1D,useAutomaticThresholds=false};
            AssetDatabase.AddObjectToAsset(tree,controller);
            tree.AddChild(find("lower_idle"),0);tree.AddChild(find("lower_walk"),2.4f);tree.AddChild(find("lower_run"),5);
            var move=lower.AddState("Move");move.motion=tree;lower.defaultState=move;
            var mask=new AvatarMask{name="UpperMask"};
            mask.AddTransformPath(mesh.transform,true);
            for(int i=0;i<mask.transformCount;i++)
            {
                string bone=mask.GetTransformPath(i);
                bone=bone==mesh.name?"":bone.Substring(mesh.name.Length+1);mask.SetTransformPath(i,bone);
                mask.SetTransformActive(i,bone.Contains("Spine"));
            }
            AssetDatabase.AddObjectToAsset(mask,controller);
            var upper=new AnimatorStateMachine{name="Upper"};AssetDatabase.AddObjectToAsset(upper,controller);
            controller.AddLayer(new AnimatorControllerLayer{name="Upper",stateMachine=upper,defaultWeight=1,avatarMask=mask,blendingMode=AnimatorLayerBlendingMode.Override});
            foreach(string pose in new[]{"rifle","pistol","knife"})
            {
                var state=upper.AddState(pose);state.motion=find("upper_"+pose+"_idle");
                if(pose=="rifle") upper.defaultState=state;
            }
            var animator=mesh.GetComponent<Animator>();if(animator==null)animator=mesh.AddComponent<Animator>();
            animator.runtimeAnimatorController=controller;animator.applyRootMotion=false;animator.cullingMode=AnimatorCullingMode.AlwaysAnimate;
            root.AddComponent<FpsManager.CharacterAnimation>();
            EditorUtility.SetDirty(controller);
            PrefabUtility.SaveAsPrefabAsset(root,"Assets/Resources/Characters/"+side+".prefab");
            FirstPersonModelSetup.Build(root,side);
            UnityEngine.Object.DestroyImmediate(root);
        }
        AssetDatabase.SaveAssets();AssetDatabase.Refresh();Debug.Log("LOWPOLY_BUILD_OK");
    }
}
#endif