#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
public static class FirstPersonModelSetup
{
    public static void Build(GameObject source,string side)
    {
        System.IO.Directory.CreateDirectory("Assets/Resources/ViewModels");
        var root=UnityEngine.Object.Instantiate(source);
        int number=0,total=0;
        foreach(var renderer in root.GetComponentsInChildren<SkinnedMeshRenderer>())
        {
            Mesh original=renderer.sharedMesh;
            var weights=original.boneWeights;
            var bones=renderer.bones;
            var arm=new bool[bones.Length];
            for(int i=0;i<bones.Length;i++)
                arm[i]=bones[i].name.Contains("ForeArm")||bones[i].name.Contains("Hand");
            var included=new bool[weights.Length];
            for(int i=0;i<weights.Length;i++)
            {
                var w=weights[i];
                float sum=(arm[w.boneIndex0]?w.weight0:0)+(arm[w.boneIndex1]?w.weight1:0)
                    +(arm[w.boneIndex2]?w.weight2:0)+(arm[w.boneIndex3]?w.weight3:0);
                included[i]=sum>.5f;
            }
            var mesh=UnityEngine.Object.Instantiate(original);mesh.name=side+" first person arms";
            int count=0;
            for(int sub=0;sub<original.subMeshCount;sub++)
            {
                int[] triangles=original.GetTriangles(sub);var keep=new List<int>();
                for(int i=0;i<triangles.Length;i+=3)
                    if(included[triangles[i]]&&included[triangles[i+1]]&&included[triangles[i+2]])
                    {keep.Add(triangles[i]);keep.Add(triangles[i+1]);keep.Add(triangles[i+2]);}
                mesh.SetTriangles(keep,sub);count+=keep.Count/3;
            }
            total+=count;
            string path="Assets/Art/LowPoly/Characters/"+side+"-arms-"+number+++".asset";
            var existing=AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if(existing==null){AssetDatabase.CreateAsset(mesh,path);existing=mesh;}
            else {EditorUtility.CopySerialized(mesh,existing);UnityEngine.Object.DestroyImmediate(mesh);}
            renderer.sharedMesh=existing;renderer.enabled=count>0;
            renderer.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows=false;
        }
        if(total==0)throw new Exception("First person arms are empty");
        PrefabUtility.SaveAsPrefabAsset(root,"Assets/Resources/ViewModels/"+side+".prefab");
        UnityEngine.Object.DestroyImmediate(root);
        Debug.Log("VIEWMODEL_BUILD_OK "+side+" triangles="+total);
    }
}
#endif