using System;
using UnityEngine;
namespace MiraifuAppearancePrototype
{
    public sealed class MiraifuAppearanceRuntimeUI : MonoBehaviour
    {
        public MiraifuAppearanceRuntime appearance;Vector2 scroll;int category;string error="";
        void OnGUI()
        {
            if(appearance.lab.Actor==null)return;
            GUILayout.BeginArea(new Rect(Mathf.Max(320,Screen.width-320),12,308,Screen.height-24),GUI.skin.box);scroll=GUILayout.BeginScrollView(scroll);
            GUILayout.Label("髪・装備：独立試作");GUILayout.Label("Play中の調整はファイル保存しません");var state=appearance.Current;var before=JsonUtility.ToJson(state);
            GUILayout.Label("メガネ");state.glasses=GUILayout.SelectionGrid(state.glasses+1,new[]{"なし","丸いメガネ","サングラス"},3)-1;
            GUILayout.Label("小物");state.accessory=GUILayout.SelectionGrid(state.accessory+1,new[]{"なし","ピエロ鼻","イヤホン","太いひげ","巻きひげ","おしゃぶり"},3)-1;
            GUILayout.Label("帽子（Web IDには追加しません）");state.hat=GUILayout.SelectionGrid(state.hat+1,new[]{"なし","Hat 010","008","013","016"},3)-1;
            state.hideHairWithHat=GUILayout.Toggle(state.hideHairWithHat,"帽子装着中は髪を隠す");GUILayout.Label("衣装中は独立帽子を一時非表示");
            category=GUILayout.SelectionGrid(category,new[]{"メガネ位置","小物位置","帽子位置"},3);int index=category==0?state.glasses:category==1?state.accessory:state.hat;
            if(index>=0)
            {
                var fit=category==0?state.glassesFits[index]:category==1?state.accessoryFits[index]:state.hatFits[index];
                fit.positionMm=Vector("位置 mm",fit.positionMm,-40,40);fit.rotation=Vector("回転 °",fit.rotation,-30,30);fit.scale=Vector("サイズ",fit.scale,.7f,1.3f);
                if(GUILayout.Button("この装備の位置をリセット")){fit.positionMm=Vector3.zero;fit.rotation=Vector3.zero;fit.scale=Vector3.one;}
            }
            if(JsonUtility.ToJson(state)!=before)try{appearance.Apply(state);error="";}catch(Exception e){error=e.Message;}
            if(!string.IsNullOrEmpty(error))GUILayout.Label(error);GUILayout.TextArea(JsonUtility.ToJson(appearance.Current,true));GUILayout.EndScrollView();GUILayout.EndArea();
        }
        static Vector3 Vector(string label,Vector3 v,float min,float max)
        {GUILayout.Label(label);v.x=Slider("X",v.x,min,max);v.y=Slider("Y",v.y,min,max);v.z=Slider("Z",v.z,min,max);return v;}
        static float Slider(string name,float value,float min,float max){GUILayout.BeginHorizontal();GUILayout.Label(name,GUILayout.Width(16));float v=GUILayout.HorizontalSlider(value,min,max);GUILayout.Label(v.ToString("F2"),GUILayout.Width(48));GUILayout.EndHorizontal();return v;}
    }
}
