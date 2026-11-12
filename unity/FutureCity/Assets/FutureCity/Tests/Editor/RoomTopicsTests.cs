using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace FutureCity.Tests
{
    public class RoomTopicsTests
    {
        [Test]
        public void TopicSelectionIsIndependentForEveryRoomAndCyclesBackToTheBeginning()
        {
            var picker=new RoomTopicPicker(RoomTopicCatalog.Load());
            string cafe=picker.Current("cafe").id;
            Assert.That(picker.Select("learn",3),Is.True);
            Assert.That(picker.Index("learn"),Is.EqualTo(3));
            Assert.That(picker.Current("cafe").id,Is.EqualTo(cafe));
            for(int i=0;i<5;i++)Assert.That(picker.Next("cafe"),Is.True);
            Assert.That(picker.Current("cafe").id,Is.EqualTo(cafe));
            Assert.That(picker.Index("learn"),Is.EqualTo(3));
        }
        [Test]
        public void InvalidSelectionsNeverReplaceTheCurrentTopic()
        {
            var picker=new RoomTopicPicker(RoomTopicCatalog.Load());
            picker.Select("hobby",2);string before=picker.Current("hobby").id;
            Assert.That(picker.Select("hobby",-1),Is.False);
            Assert.That(picker.Select("hobby",5),Is.False);
            Assert.That(picker.Current("hobby").id,Is.EqualTo(before));
            Assert.That(picker.Select("unknown",0),Is.False);
            Assert.That(picker.Next("unknown"),Is.False);
            Assert.That(picker.Current("unknown"),Is.Null);
        }
        [Serializable] class Seed { public Room[] rooms; }
        [Serializable] class Room { public string roomId; public Topic[] topics; }
        [Serializable] class Topic { public string id,title,prompt;public string[] hints; }

        [Test]
        public void EveryExistingRoomHasFiveCompleteUniqueConversationTopics()
        {
            var asset=Resources.Load<TextAsset>("RoomTopics");
            Assert.That(asset,Is.Not.Null,"Conversation topics must be included in the playable world");
            var seed=JsonUtility.FromJson<Seed>(asset.text);
            var rooms=new HashSet<string>();var ids=new HashSet<string>();
            Assert.That(seed.rooms.Length,Is.EqualTo(4));
            foreach(var room in seed.rooms)
            {
                Assert.That(rooms.Add(room.roomId),Is.True);
                Assert.That(room.topics.Length,Is.EqualTo(5),room.roomId);
                foreach(var topic in room.topics)
                {
                    Assert.That(ids.Add(topic.id),Is.True,topic.id);
                    Assert.That(topic.title,Is.Not.Null.And.Not.Empty);
                    Assert.That(topic.prompt,Is.Not.Null.And.Not.Empty);
                    Assert.That(topic.hints.Length,Is.EqualTo(3));
                    foreach(var hint in topic.hints)Assert.That(hint,Is.Not.Null.And.Not.Empty);
                }
            }
            CollectionAssert.AreEquivalent(new[]{"learn","story","cafe","hobby"},rooms);
        }
    }
}
