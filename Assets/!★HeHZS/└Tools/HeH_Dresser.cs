#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.Animations;
using VRC.SDK3.Avatars.ScriptableObjects;
using UnityEditor.SceneManagement;
using System.Globalization;

//version:1.1
//Creator:HeHZS

[CustomEditor(typeof(HeH_Dresser))]
[System.Serializable]
public class HEHDresser : Editor
{
  SerializedProperty CostumeHips;
  SerializedProperty TargetAvatar;
  SerializedProperty password;
  SerializedProperty Info;
  SerializedProperty PBInfo;
  SerializedProperty Zero;
  private bool Fold;
  void OnEnable()
  {
      TargetAvatar = serializedObject.FindProperty("TargetAvatar");
      password = serializedObject.FindProperty("password");
      Info = serializedObject.FindProperty("Info");
      PBInfo = serializedObject.FindProperty("PBInfo");
      Zero = serializedObject.FindProperty("Zero");
  }
  public override void OnInspectorGUI()
  {
      HeH_Dresser myTarget = (HeH_Dresser)target;
      GUIContent GUIC_Password = new GUIContent("Naming");
      GUIContent GUIC_Info = new GUIContent("Info");
      GUIContent GUIC_PBInfo = new GUIContent("PBInfo");
      GUIContent GUIC_Zero = new GUIContent("Zero transform");

      GUIContent GUIC_TargetAvatar = new GUIContent("TargetAvatar");
      myTarget.TargetAvatar = (Transform)EditorGUILayout.ObjectField(GUIC_TargetAvatar,myTarget.TargetAvatar,typeof(Transform), true);
      if(myTarget.TargetAvatar == null && myTarget.Info != ""){
        GameObject[] objects = GameObject.FindObjectsOfType<GameObject>();
        foreach (GameObject obj in objects){
          VRC.SDK3.Avatars.Components.VRCAvatarDescriptor component = obj.GetComponent<VRC.SDK3.Avatars.Components.VRCAvatarDescriptor>();
          if(component != null && obj.name.Contains(myTarget.Info) && obj.activeSelf){
            myTarget.TargetAvatar = obj.transform;
          }
        }
      }
      base.OnInspectorGUI();
      serializedObject.ApplyModifiedProperties();
      EditorGUILayout.HelpBox("Drag your avatar in here.　Click button and it will automatically put on clothes for your avatar.\n指定アバターをここに入れてください。ボタンをクリックし、自動的に服を着せます。", MessageType.None);
      if (GUILayout.Button("★★★Dress!★★★",GUILayout.Height(30)))myTarget.GoDress();

      Fold = EditorGUILayout.Foldout (Fold,"Other");
      if(Fold){
        myTarget.password = EditorGUILayout.TextField(GUIC_Password,myTarget.password);
        EditorGUILayout.HelpBox("Add prefix to all child objects.\n全てのサブオブジェクトにプレフィックスを追加する。", MessageType.None);
        if (GUILayout.Button("★★★Naming!★★★",GUILayout.Height(20)))myTarget.GoNaming();
        myTarget.Zero = EditorGUILayout.Toggle(GUIC_Zero, myTarget.Zero);
        EditorGUILayout.HelpBox("After bones are matched, set transform to zero.\n対応Bone見つけたら、transformを0に設置。", MessageType.None);
        if (GUILayout.Button("Delete!",GUILayout.Height(20)))myTarget.GoDestroy();
        EditorGUILayout.HelpBox("All Gameobjects containing the [Naming] string will be deleted from the target avatar.\n「Naming」文字を含むGameobjectは全てTargetAvatarから削除されます。", MessageType.None);
        myTarget.Info = EditorGUILayout.TextField(GUIC_Info,myTarget.Info);
        myTarget.PBInfo = EditorGUILayout.TextField(GUIC_PBInfo,myTarget.PBInfo);

      }
  }
}

[ExecuteInEditMode]
public class HeH_Dresser : MonoBehaviour
{
  [HideInInspector][SerializeField]
  public Transform TargetAvatar;
  [HideInInspector][SerializeField]
  public string password="Cos_";
  [HideInInspector][SerializeField]
  public string Info="";
  [HideInInspector][SerializeField]
  public string PBInfo="";
  [HideInInspector][SerializeField]
  public bool Zero=true;

  public void GoNaming()
  {
    Naming(this.transform,password);
  }

  public void GoDress()
  {
    if (PrefabUtility.GetPrefabInstanceStatus(this.gameObject) != PrefabInstanceStatus.NotAPrefab)
    {
        PrefabUtility.UnpackPrefabInstance(this.gameObject, PrefabUnpackMode.Completely, InteractionMode.UserAction);
    }
    Transform Hips = getJoint(this.transform,"Hips");
    Transform TargetHips = getJoint(TargetAvatar,"Hips");
    Transform TargetBody = getJoint(TargetAvatar,"Body");
    Transform Armature = Hips.parent;
    if(Hips != null && TargetHips !=null){
      if(TargetBody != null){
        RendererSet(this.transform,TargetBody.GetComponent<SkinnedMeshRenderer>().rootBone,TargetBody.GetComponent<SkinnedMeshRenderer>().localBounds,TargetBody.GetComponent<SkinnedMeshRenderer>().probeAnchor);
      }
      string Prefix = Hips.name.Substring(0,Hips.name.Length-4);
      dodresser(Hips,TargetHips,Prefix);
      DestroyImmediate(Armature.gameObject);
      int count=this.transform.childCount;
      GameObject emptyObject = new GameObject(Prefix+"Group");
      emptyObject.transform.parent=TargetAvatar.root;
      emptyObject.transform.localPosition = Vector3.zero;
      emptyObject.transform.localRotation = new Quaternion(0,0,0,0);
      emptyObject.transform.localScale = Vector3.one;
      for(int xz=0; xz<count;xz++){
        Transform temp = this.transform.GetChild(0);
        temp.parent=emptyObject.transform;
        temp.localPosition = Vector3.zero;
        temp.localRotation = new Quaternion(0,0,0,0);
        temp.localScale = Vector3.one;
      }
      if(PBInfo != ""){
        string[] PBsp = PBInfo.Split('|');
        foreach (string word in PBsp){
          string[] PBspB = word.Split('#');
          if(PBspB.Length >1){
            Transform findOBJ= GameObject.Find(PBspB[0]).transform;
            if(PBspB[1] == "ChirdType"){
              VRC.SDK3.Dynamics.PhysBone.Components.VRCPhysBone component = findOBJ.GetComponent<VRC.SDK3.Dynamics.PhysBone.Components.VRCPhysBone>();
              if(findOBJ != null && component != null){
                component.multiChildType = VRC.SDK3.Dynamics.PhysBone.Components.VRCPhysBone.MultiChildType.First;
                EditorUtility.SetDirty(component);
              }
            }
          }
        }
      }
      Selection.activeGameObject = TargetAvatar.gameObject;
      DestroyImmediate(gameObject);
    }else{
      EditorUtility.DisplayDialog("Error","Can't find Hips.", "OK");
    }
  }
  void RendererSet(Transform Target,Transform root,Bounds bound,Transform Anchor){
    int count = Target.childCount;
    if(count>0){
      for(int x=0; x<count;x++){
        RendererSet(Target.GetChild(x),root,bound,Anchor);
      }
    }
    if(Target.GetComponent(typeof(SkinnedMeshRenderer))){
      SkinnedMeshRenderer temprenderer=(SkinnedMeshRenderer)Target.GetComponent(typeof(SkinnedMeshRenderer));
      if(temprenderer.rootBone != null)
      {
        temprenderer.localBounds=bound;
        temprenderer.rootBone = root;
      }
      temprenderer.probeAnchor = Anchor;
    }
    if(Target.GetComponent(typeof(MeshRenderer))){
      MeshRenderer temprenderer=(MeshRenderer)Target.GetComponent(typeof(MeshRenderer));
      temprenderer.probeAnchor = Anchor;
    }

  }
  string DestroyInfo="";
  int DestroyCount=0;
  int DestroyCountR=0;
  public void GoDestroy()
  {
    DestroyInfo="GameObject Deleted:\n";
    DestroyCount=0;
    DestroyCountR=0;
    dodelete(TargetAvatar,password);
    DestroyInfo=DestroyInfo+"Total: "+DestroyCountR+" GameObject.";
    EditorUtility.DisplayDialog("GameObject Deleted",DestroyInfo, "OK");
  }


  public Transform getJoint(Transform Target,string Key){
    int count = Target.childCount;
    if(count>0){
      for(int xz=0; xz<count;xz++){
        CompareInfo Compare=CultureInfo.InvariantCulture.CompareInfo;
        string tempA=Target.GetChild(xz).name;
        if(Compare.IndexOf(tempA,Key,CompareOptions.IgnoreCase ) != -1){
          return(Target.GetChild(xz));
        }else{
          Transform temp = getJoint(Target.GetChild(xz),Key);
          if(temp!= null){
            return(temp);
          }
        }
      }
    }
    return(null);
  }

  void Naming(Transform Target,string Nam){
      int count = Target.childCount;
      if(count>0){
        for(int x=0; x<count;x++){
          Naming(Target.GetChild(x),Nam);
        }
      }
      if(Target.name.Length > Nam.Length){
        if(Target.name.Substring(0,Nam.Length-1) != Nam.Substring(0,Nam.Length-1)){
            Target.name=Nam+Target.name;
        }
      }else{
        Target.name=Nam+Target.name;
      }

  }

  void dodresser(Transform Target,Transform TargetAvatar,string CKey){
    int count = Target.childCount;
    Transform targetjoint;
    if(count>0){
      for(int x=0; x<count;x++){
        if(Target.GetChild(count-x-1).name.Length > CKey.Length){
          targetjoint= TargetAvatar.Find(Target.GetChild(count-x-1).name.Substring(CKey.Length,Target.GetChild(count-x-1).name.Length-CKey.Length));
          if(targetjoint != null){
            dodresser(Target.GetChild(count-x-1),targetjoint,CKey);
          }

        }
      }
    }
    Target.parent = TargetAvatar;
    if(Zero){
      Target.localPosition = Vector3.zero;
      Target.localRotation = new Quaternion(0,0,0,0);
      Target.localScale = Vector3.one;
    }

  }
  void dodelete(Transform TargetA,string CKey){
    // Debug.Log(TargetA.name);

    if(TargetA.name.Length > CKey.Length){
      if(TargetA.name.Substring(0,CKey.Length-1) == CKey.Substring(0,CKey.Length-1)){
        if (PrefabUtility.GetPrefabParent(TargetA.gameObject) == null)
        {
          if(DestroyCount<25){
            DestroyInfo=DestroyInfo+TargetA.gameObject.name+"\n";
          }
          if(DestroyCount==25){
            DestroyInfo=DestroyInfo+TargetA.gameObject.name+"\n...\n";
          }
          DestroyImmediate(TargetA.gameObject);
          DestroyCountR++;
          DestroyCount++;
          return;
        }else{
          if(DestroyCount<25){
            DestroyInfo=DestroyInfo+TargetA.gameObject.name+" [SKIP Because is prefab]\n";
          }
          if(DestroyCount==25){
            DestroyInfo=DestroyInfo+TargetA.gameObject.name+" [SKIP Because is prefab]\n...\n";
          }
          DestroyCount++;

        }
      }
    }
    int count = TargetA.childCount;
    if(count>0){
      for(int x=0; x<count;x++){
        dodelete(TargetA.GetChild(count-x-1),CKey);
      }
    }

  }
}

#endif
