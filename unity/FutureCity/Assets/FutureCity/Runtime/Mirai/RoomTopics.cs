using System;
using System.Collections.Generic;
using UnityEngine;

namespace FutureCity
{
    [Serializable] public sealed class RoomTopic
    {
        public string id,title,prompt;
        public string[] hints;
    }
    [Serializable] public sealed class RoomTopicGroup
    {
        public string roomId;
        public RoomTopic[] topics;
    }
    [Serializable] public sealed class RoomTopicCatalog
    {
        public RoomTopicGroup[] rooms;
        public static RoomTopicCatalog Load()
        {
            var asset=Resources.Load<TextAsset>("RoomTopics");
            return asset==null?new RoomTopicCatalog():JsonUtility.FromJson<RoomTopicCatalog>(asset.text);
        }
        public RoomTopic[] ForRoom(string roomId)
        {
            if(rooms!=null)foreach(var room in rooms)
                if(room.roomId==roomId)return room.topics??Array.Empty<RoomTopic>();
            return Array.Empty<RoomTopic>();
        }
    }
    // Conversation selections are kept per room for this visit, never as account data.
    public sealed class RoomTopicPicker
    {
        readonly RoomTopicCatalog catalog;
        readonly Dictionary<string,int> selected=new Dictionary<string,int>();
        public RoomTopicPicker(RoomTopicCatalog catalog){this.catalog=catalog;}
        public RoomTopic[] ForRoom(string roomId)=>catalog.ForRoom(roomId);
        public int Index(string roomId)=>roomId!=null&&selected.TryGetValue(roomId,out var index)?index:0;
        public RoomTopic Current(string roomId)
        {
            var topics=ForRoom(roomId);
            return topics.Length==0?null:topics[Index(roomId)];
        }
        public bool Select(string roomId,int index)
        {
            if(roomId==null||index<0||index>=ForRoom(roomId).Length)return false;
            selected[roomId]=index;return true;
        }
        public bool Next(string roomId)
        {
            int count=ForRoom(roomId).Length;
            return count>0&&Select(roomId,(Index(roomId)+1)%count);
        }
    }
}
