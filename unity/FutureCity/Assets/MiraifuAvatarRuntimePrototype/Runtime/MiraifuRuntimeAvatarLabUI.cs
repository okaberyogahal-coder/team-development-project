using System;
using UnityEngine;
namespace MiraifuAvatarPrototype
{
    public sealed class MiraifuRuntimeAvatarLabUI : MonoBehaviour
    {
        public MiraifuRuntimeAvatarLab lab;public Camera viewCamera;public bool movementControlled;Vector2 scroll;string error="";float yaw;int framing=2;
        static readonly string[] names={"若者男性","若者女性","大人男性","大人女性","シニア男性","シニア女性"};
        void OnGUI()
        {
            GUILayout.BeginArea(new Rect(12,12,300,Screen.height-24),GUI.skin.box);scroll=GUILayout.BeginScrollView(scroll);
            GUILayout.Label("ミライフ：独立ランタイム試作");GUILayout.Label("本番登録・通信・保存は行いません");
            if(lab.Actor==null){GUILayout.Label("アバターを読み込み中");GUILayout.EndScrollView();GUILayout.EndArea();return;}
            for(int i=0;i<lab.models.Length;i++){int index=i;if(GUILayout.Button(names.Length>i?names[i]:lab.models[i].modelId))Run(()=>lab.SwitchModel(lab.models[index].modelId));}
            var state=lab.Actor.Current.Copy();bool changed=false;
            changed|=Select("表情",ref state.expression,new[]{"0 怒り","1 笑顔","2 おだやか"},0);
            changed|=Select("髪型",ref state.hair,new[]{"0","1","2"},0);
            changed|=Select("上着",ref state.top,new[]{"0","1","2"},0);changed|=Select("ズボン",ref state.pants,new[]{"0","1","2"},0);changed|=Select("靴",ref state.shoes,new[]{"0","1","2"},0);
            changed|=Select("衣装",ref state.costume,new[]{"通常服","ピエロ","ネズミ"},-1);
            if(changed)Run(()=>lab.ApplySettings(state));
            if(movementControlled)GUILayout.Label("WASD: 移動 / Space: ジャンプ / Q・E: 視点\nR: 視点リセット / ホイール: 拡大\nアニメーションは移動・接地から自動切替");
            else{GUILayout.Label("アニメーション");GUILayout.BeginHorizontal();foreach(AvatarMotion motion in Enum.GetValues(typeof(AvatarMotion)))if(GUILayout.Button(motion==AvatarMotion.Idle?"待機":motion==AvatarMotion.Walk?"歩行":"ジャンプ"))Run(()=>lab.SetMotion(motion));GUILayout.EndHorizontal();}
            GUILayout.Label("色：生成候補の色を保持 / 個体だけ上書き");if(GUILayout.Button("候補の肌色・髪色へ戻す")){state=lab.Actor.Current.Copy();state.skinColor="";state.hairColor="";Run(()=>lab.ApplySettings(state));}
            GUILayout.BeginHorizontal();if(GUILayout.Button("肌ライト")){state.skinColor="#F3D5BF";Run(()=>lab.ApplySettings(state));}if(GUILayout.Button("肌ブラウン")){state.skinColor="#9A6747";Run(()=>lab.ApplySettings(state));}GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();if(GUILayout.Button("髪ブラック")){state.hairColor="#1C1917";Run(()=>lab.ApplySettings(state));}if(GUILayout.Button("髪シルバー")){state.hairColor="#9CA3AF";Run(()=>lab.ApplySettings(state));}GUILayout.EndHorizontal();
            if(!movementControlled){GUILayout.BeginHorizontal();if(GUILayout.Button("正面"))yaw=0;if(GUILayout.Button("35°"))yaw=35;if(GUILayout.Button("90°"))yaw=90;GUILayout.EndHorizontal();framing=GUILayout.SelectionGrid(framing,new[]{"顔","上半身","全身"},3);}
            GUILayout.TextArea(JsonUtility.ToJson(lab.Actor.Current,true));GUILayout.Label("再生: "+lab.Actor.Motion);if(!string.IsNullOrEmpty(error))GUILayout.Label(error);
            GUILayout.EndScrollView();GUILayout.EndArea();
            if(viewCamera!=null&&!movementControlled){var center=new Vector3(.5f,framing==0?1.62f:framing==1?1.3f:1,0);viewCamera.transform.position=center+Quaternion.Euler(0,yaw,0)*Vector3.back*4;viewCamera.transform.LookAt(center);viewCamera.orthographicSize=framing==0?.36f:framing==1?.7f:1.25f;}
        }
        static bool Select(string title,ref int value,string[] options,int offset){GUILayout.Label(title);int selected=GUILayout.SelectionGrid(value-offset,options,3);if(selected==value-offset)return false;value=selected+offset;return true;}
        void Run(Action action){try{action();error="";}catch(Exception e){error=e.Message;}}
    }
}
