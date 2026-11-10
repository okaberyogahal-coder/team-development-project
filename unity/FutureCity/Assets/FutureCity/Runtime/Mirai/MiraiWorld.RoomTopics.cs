using UnityEngine;

namespace FutureCity
{
    public partial class MiraiWorld
    {
        RoomTopicPicker roomTopics;
        RoomTopicPicker Topics=>roomTopics??(roomTopics=new RoomTopicPicker(RoomTopicCatalog.Load()));

        void DrawRoomTopics()
        {
            if(currentRoom==null){SetPanel(Panel.None);return;}
            var room=currentRoom;
            var body=Frame(room.name+"のお題",room.color*.8f,1080f,850f);
            var topics=Topics.ForRoom(room.id);var current=Topics.Current(room.id);
            if(current==null){GUI.Label(body,"この部屋のお題はまだありません。",sBody);return;}
            float width=body.width-28f;
            float promptHeight=sBody.CalcHeight(new GUIContent(current.prompt),width);
            float hintsHeight=0f;
            foreach(var hint in current.hints)hintsHeight+=sBody.CalcHeight(new GUIContent("・"+hint),width)+10f;
            float contentHeight=promptHeight+hintsHeight+350f+topics.Length*64f;
            scroll=GUI.BeginScrollView(body,scroll,new Rect(0,0,width,contentHeight));
            float y=0f;
            GUI.Label(new Rect(0,y,width,36f),$"<b>会話のお題 {Topics.Index(room.id)+1}/{topics.Length}</b>",sSmall);y+=42f;
            GUI.Label(new Rect(0,y,width,42f),"<b>"+Plain(current.title)+"</b>",sH2);y+=50f;
            GUI.Label(new Rect(0,y,width,promptHeight),Plain(current.prompt),sBody);y+=promptHeight+22f;
            GUI.Label(new Rect(0,y,width,36f),"<b>会話を広げるヒント</b>",sSmall);y+=40f;
            foreach(var hint in current.hints)
            {
                float h=sBody.CalcHeight(new GUIContent("・"+hint),width);
                GUI.Label(new Rect(0,y,width,h),"・"+Plain(hint),sBody);y+=h+10f;
            }
            if(Btn(new Rect(0,y,Mathf.Min(width,340f),56f),"次のお題へ",room.color*.8f))
            {Topics.Next(room.id);scroll=Vector2.zero;}
            y+=74f;
            GUI.Label(new Rect(0,y,width,36f),"<b>ほかのお題を選ぶ</b>",sSmall);y+=42f;
            for(int i=0;i<topics.Length;i++)
            {
                bool on=Topics.Index(room.id)==i;
                if(Btn(new Rect(0,y,width,56f),(on?"● ":"")+(i+1)+". "+Plain(topics[i].title),on?room.color*.8f:(Color?)null))
                {Topics.Select(room.id,i);scroll=Vector2.zero;}
                y+=64f;
            }
            GUI.Label(new Rect(0,y,width,60f),"順番に話してみましょう。答えにくいときはパスして大丈夫です。",sSmall);
            GUI.EndScrollView();
        }
    }
}
