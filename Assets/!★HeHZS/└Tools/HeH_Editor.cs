#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEditor;
using UnityEngine.Animations;
using VRC.SDK3.Avatars.ScriptableObjects;
using UnityEditor.SceneManagement;
using System.IO;
using System;

//version:1.1
//Creator:HeHZS

public class HeH_Editor : EditorWindow
{
    [MenuItem("HeH_Tools/HeH_Editor")]
    public static void OpenWindow(){
      HeH_Editor window = (HeH_Editor)GetWindow(typeof(HeH_Editor));

      // window.minSize = new Vector2(300,300);
      window.Show();
    }
    public Transform TargetAvatar;
    public bool OnlyToTarget;
    public AnimationClip TargetAnimation;
    public bool WithChild;
    public string FromSTR;
    public string ToSTR;

    private void OnGUI(){
      if (GUILayout.Button("GetComponentName",GUILayout.Height(20))){
        GetComponentName();
        return;
      }
      EditorGUILayout.HelpBox("Select a GameObject and click button.\nCheck components name of the GameObject.", MessageType.None);

      if (GUILayout.Button("GetPath",GUILayout.Height(20))){
        GetPath();
        return;
      }
      EditorGUILayout.HelpBox("Select a GameObject and click button.\nCheck path of the GameObject.", MessageType.None);

      if (GUILayout.Button("CreateMenuICON",GUILayout.Height(20))){
        CreateMenuICON();
        return;
      }
      EditorGUILayout.HelpBox("Select Menu files and click button.\nCreate menu ICONS.", MessageType.None);
      
      if (GUILayout.Button("Create PB Object",GUILayout.Height(20))){
        CreatePBObject();
        return;
      }
      EditorGUILayout.HelpBox("Select GameObject and click button.", MessageType.None);


      if (GUILayout.Button("AnimationDefault",GUILayout.Height(20))){
        AnimationDefault();
        return;
      }
      TargetAvatar = (Transform)EditorGUILayout.ObjectField(new GUIContent("RootGameObject"),TargetAvatar,typeof(Transform), true);
      OnlyToTarget = EditorGUILayout.Toggle(new GUIContent("OnlyToTarget"), OnlyToTarget);
      TargetAnimation = (AnimationClip)EditorGUILayout.ObjectField(new GUIContent("TargetAnimation"),TargetAnimation,typeof(AnimationClip), true);
      EditorGUILayout.HelpBox("Select Animation files and click button.\nCreate default values for all used channels.", MessageType.None);

      if (GUILayout.Button("String Replace",GUILayout.Height(20))){
        ReplaceSTR();
        return;
      }
      WithChild = EditorGUILayout.Toggle(new GUIContent("WithChild"), WithChild);
      FromSTR = EditorGUILayout.TextField(new GUIContent("From"), FromSTR);
      ToSTR = EditorGUILayout.TextField(new GUIContent("To"), ToSTR);
      EditorGUILayout.HelpBox("Select GameObject and click button.\nReplaces the String in the object name.", MessageType.None);
    }

    public void GetComponentName(){
      if(Selection.activeGameObject != null) {
        Component[] components = Selection.activeGameObject.GetComponents<Component>() as Component[];
        if(components.Length > 0) {
          foreach(Component component in components)
              Debug.Log (component);
          // Selection.objects = (Object[])components;
        }
      }
    }

    public void GetPath(){
      Transform[] SelectionObject = Selection.transforms;
      for(int x=0;x<SelectionObject.Length;x++){ 
        Debug.Log (GetFullPath(SelectionObject[x]));
      }
    }

    public string GetFullPath(Transform transform)
    {
        string path = transform.name;
        while (transform.parent != null)
        {
            transform = transform.parent;
            path = transform.name + "/" + path;
        }
        return path;
    }

    public void CreatePBObject(){
      Transform[] SelectionObject = Selection.transforms;
      for(int x=0;x<SelectionObject.Length;x++){
        string tempName = SelectionObject[x].name.Replace("1","");
        GameObject emptyObject = new GameObject(tempName);
        VRC.SDK3.Dynamics.PhysBone.Components.VRCPhysBone tempPB = emptyObject.AddComponent<VRC.SDK3.Dynamics.PhysBone.Components.VRCPhysBone>();
        tempPB.rootTransform = SelectionObject[x];
        tempPB.pull=0.2f;
        tempPB.spring=0.7f;
        tempPB.immobile=0.9f;
        tempPB.maxStretch=0.45f;
      }
    }


    public void CreateMenuICON(){
      UnityEngine.Object[] SelectionObject = Selection.objects;
      VRCExpressionsMenu menu;
      Texture2D tempicon;
      Texture2D  texture;
  		// texture.SetPixels(new Rect(0, 0, 256, 256), 0, 0);
      int ID;
      if(SelectionObject.Length >0) {
        for(int x=0; x<SelectionObject.Length;x++){
        ID=0;
        texture = new Texture2D(256, 256, TextureFormat.ARGB32, false);
        Color fillColor = Color.clear;
        for(int zx=0;zx<256;zx++){
          for(int zy=0;zy<256;zy++){
            texture.SetPixel(zx,zy,fillColor);
          }
        }
    		texture.Apply();

          if(SelectionObject[x] is VRCExpressionsMenu){
            menu = (VRCExpressionsMenu)SelectionObject[x];
            for(int y=0;y<menu.controls.Count;y++){
              if(menu.controls[y] != null){
                if(menu.controls[y].icon != null){
                  tempicon = menu.controls[y].icon;
                  if(menu.controls.Count<=4){
                    PaintIcon4X(texture,tempicon,ID);
                  }else{
                    PaintIcon8X(texture,tempicon,ID);
                  }
                  ID++;
                }
              }
            }
            string path = AssetDatabase.GetAssetPath(SelectionObject[x]).Replace(".asset",".png");
            File.WriteAllBytes(path,texture.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(texture);

         }

        }
        // FileUtil.CopyFileOrDirectory(texture, "Assets/MyMaterial.png");
        // AssetDatabase.Refresh();
        AssetDatabase.Refresh();
        for(int x=0; x<SelectionObject.Length;x++){
          if(SelectionObject[x] is VRCExpressionsMenu){
            string path = AssetDatabase.GetAssetPath(SelectionObject[x]).Replace(".asset",".png");
            TextureImporter importer = AssetImporter.GetAtPath(path)as TextureImporter;
            importer.textureType=TextureImporterType.Sprite;
            importer.maxTextureSize=256;
            importer.crunchedCompression=true;
            AssetDatabase.WriteImportSettingsIfDirty(path);

          }
        }
        AssetDatabase.Refresh();
      }
    }



    public void AnimationDefault(){
      // List<StringList> Clips = new List<StringList>();
      List<string> Clips = new List<string>();
      UnityEngine.Object[] SelectionObject = Selection.objects;
      for(int x=0; x<SelectionObject.Length;x++){
        if(SelectionObject[x] is AnimationClip){
          foreach (var binding in AnimationUtility.GetCurveBindings((AnimationClip)SelectionObject[x]))
            {
                var type = binding.propertyName;
                var key = CurveKey(binding.path, binding.type, binding.propertyName);
                var curve =0;
                Debug.Log(key);
                if(binding.propertyName != "localEulerAnglesRaw.y"
                && binding.propertyName != "localEulerAnglesRaw.z"
                && binding.propertyName != "m_LocalPosition.y"
                && binding.propertyName != "m_LocalPosition.z"
                && binding.propertyName != "m_LocalScale.y"
                && binding.propertyName != "m_LocalScale.z")
                Clips.Add((string)key);
            }
        }
      }

      // for(int y=0; y<Clips.Count;y++){
      //   Debug.Log(Clips[y]);
      // }
      if(OnlyToTarget && TargetAnimation!=null){
        SelectionObject = new UnityEngine.Object[]{TargetAnimation};
      }
      for(int x=0; x<SelectionObject.Length;x++){
        if(SelectionObject[x] is AnimationClip){
          for(int y=0; y<Clips.Count;y++){
            string[] strarray = Clips[y].Split(':');
            bool have=false;
            foreach (var binding in AnimationUtility.GetCurveBindings((AnimationClip)SelectionObject[x])){
              var key = CurveKey(binding.path, binding.type, binding.propertyName);
              if(key == strarray[0]+":"+strarray[1]+":"+strarray[2]){
                have=true;
                break;
              }
            }
            if(!have){
              Transform tempTarget = TargetAvatar.Find(strarray[0]);
              if(strarray[1] == "UnityEngine.SkinnedMeshRenderer"){
                ((AnimationClip)SelectionObject[x]).SetCurve(strarray[0],typeof(SkinnedMeshRenderer) , strarray[2], new AnimationCurve(new Keyframe(0, GetBlendShapeWeightByName(tempTarget.gameObject,strarray[2].Replace("blendShape.","")), 0, 0)));
              }
              if(strarray[2] == "localEulerAnglesRaw.x"){
                Vector3 tempV3 = UnityEditor.TransformUtils.GetInspectorRotation(tempTarget);
                ((AnimationClip)SelectionObject[x]).SetCurve(strarray[0],typeof(Transform) , "localEulerAnglesRaw.x", new AnimationCurve(new Keyframe(0, tempV3.x)));
                ((AnimationClip)SelectionObject[x]).SetCurve(strarray[0],typeof(Transform) , "localEulerAnglesRaw.y", new AnimationCurve(new Keyframe(0, tempV3.y)));
                ((AnimationClip)SelectionObject[x]).SetCurve(strarray[0],typeof(Transform) , "localEulerAnglesRaw.z", new AnimationCurve(new Keyframe(0, tempV3.z)));
              }
              if(strarray[2] == "m_LocalPosition.x"){
                Vector3 tempV3 = tempTarget.localPosition;
                ((AnimationClip)SelectionObject[x]).SetCurve(strarray[0],typeof(Transform) , "m_LocalPosition.x", new AnimationCurve(new Keyframe(0, tempV3.x)));
                ((AnimationClip)SelectionObject[x]).SetCurve(strarray[0],typeof(Transform) , "m_LocalPosition.y", new AnimationCurve(new Keyframe(0, tempV3.y)));
                ((AnimationClip)SelectionObject[x]).SetCurve(strarray[0],typeof(Transform) , "m_LocalPosition.z", new AnimationCurve(new Keyframe(0, tempV3.z)));
              }
              if(strarray[2] == "m_LocalScale.x"){
                Vector3 tempV3 = tempTarget.localScale;
                ((AnimationClip)SelectionObject[x]).SetCurve(strarray[0],typeof(Transform) , "m_LocalScale.x", new AnimationCurve(new Keyframe(0, tempV3.x)));
                ((AnimationClip)SelectionObject[x]).SetCurve(strarray[0],typeof(Transform) , "m_LocalScale.y", new AnimationCurve(new Keyframe(0, tempV3.y)));
                ((AnimationClip)SelectionObject[x]).SetCurve(strarray[0],typeof(Transform) , "m_LocalScale.z", new AnimationCurve(new Keyframe(0, tempV3.z)));
              }
              if(strarray[2] == "m_IsActive"){
                float tempflo=0;
                if(tempTarget.gameObject.active)tempflo=1;
                ((AnimationClip)SelectionObject[x]).SetCurve(strarray[0],typeof(GameObject) , "m_IsActive", new AnimationCurve(new Keyframe(0,tempflo )));
              }
            }
          }
        }
      }
    }

    public void ReplaceSTR(){
      Transform[] SelectionObject = Selection.transforms;
      for(int x=0;x<SelectionObject.Length;x++){
        DoReplace(SelectionObject[x]);
      }
    }

    void DoReplace(Transform Target){
      if(WithChild){
        int count = Target.childCount;
        if(count>0){
          for(int x=0; x<count;x++){
            DoReplace(Target.GetChild(x));
          }
        }
      }
      Target.name=Target.name.Replace(FromSTR,ToSTR);
    }

    public float GetBlendShapeWeightByName(GameObject Target,string ShapekeyName){
      SkinnedMeshRenderer m = (SkinnedMeshRenderer)Target.GetComponent(typeof(SkinnedMeshRenderer));
      string[] shapelist = new string [m.sharedMesh.blendShapeCount];
      for(int x=0;x<m.sharedMesh.blendShapeCount;x++){
        if(ShapekeyName == m.sharedMesh.GetBlendShapeName(x)){
          return m.GetBlendShapeWeight(x);
        }
      }
      return 0;
    }

    private string CurveKey(string pathToObject, Type type, string propertyName)
    {
        return $"{pathToObject}:{type.FullName}:{propertyName}";
    }

    public void PaintIcon8X(Texture2D Target, Texture2D Tempicon, int ID){
      float x = ID%3;
      float y = ID/3;
      Texture2D temptex = Resize(Tempicon,85,85);
      for(int zx=0;zx<85;zx++){
        for(int zy=0;zy<85;zy++){
          Target.SetPixel(Mathf.FloorToInt(85*x+zx),Mathf.FloorToInt(85*(2-y)+zy),temptex.GetPixel(zx,zy));
        }
      }
      UnityEngine.Object.DestroyImmediate(temptex);
    }

    public void PaintIcon4X(Texture2D Target, Texture2D Tempicon, int ID){
      float x = ID%2;
      float y = ID/2;
      Texture2D temptex = Resize(Tempicon,128,128);
      for(int zx=0;zx<127;zx++){
        for(int zy=0;zy<127;zy++){
          Target.SetPixel(Mathf.FloorToInt(128*x+zx),Mathf.FloorToInt(128*(1-y)+zy),temptex.GetPixel(zx,zy));
        }
      }
      UnityEngine.Object.DestroyImmediate(temptex);
    }

    Texture2D Resize(Texture2D texture2D,int targetX,int targetY)
    {
        RenderTexture rt=new RenderTexture(targetX, targetY,24);
        RenderTexture.active = rt;
        Graphics.Blit(texture2D,rt);
        Texture2D result=new Texture2D(targetX,targetY);
        result.ReadPixels(new Rect(0,0,targetX,targetY),0,0);
        result.Apply();
        return result;
    }

}
#endif
