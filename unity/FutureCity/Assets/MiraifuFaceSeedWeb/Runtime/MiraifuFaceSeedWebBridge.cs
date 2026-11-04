using System;
using System.Linq;
using System.Collections;
using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.InputSystem;
using MiraifuAvatarPrototype;
using MiraifuAppearancePrototype;
namespace MiraifuFaceSeedWeb
{
    public sealed class MiraifuFaceSeedWebBridge : MonoBehaviour
    {
        [Serializable] public class CatalogModel {public string id;public MiraifuWebAvatarReceiver.WebChoice defaults;}
        [Serializable] public class Slot {public string key;public int count;}
        [Serializable] public class Catalog {public CatalogModel[] models;public Slot[] slots;public bool skinColorSupported=true;public string profile="face-seed-20261103";}
        [Serializable] public class State {public bool accepted,pairMatched,world;public string error,sourceJsonSha,bodyMesh,faceMesh;public float yaw;public Vector3 position;public MiraifuWebAvatarReceiver.WebChoice choice;}
        public bool world;public MiraifuRuntimeAvatarLab lab;public MiraifuWebAvatarReceiver receiver;public Camera viewCamera;public Catalog catalog;
        bool dragging;Vector2 pointer;int width,height;MiraifuWebAvatarReceiver.WebChoice last;
#if UNITY_WEBGL && !UNITY_EDITOR
        [DllImport("__Internal")] static extern void MiraifuFaceSeedCatalog(string json);
        [DllImport("__Internal")] static extern void MiraifuFaceSeedReady();
        [DllImport("__Internal")] static extern void MiraifuFaceSeedState(string json);
#endif
        IEnumerator Start()
        {
            if(!world&&Application.absoluteURL.Contains("mode=world")){SceneManager.LoadScene("FaceSeedWorld");yield break;}
            yield return null;string first=Application.absoluteURL.Contains("gender=female")?"young-woman":"young-man";var choice=catalog.models.Single(m=>m.id==first).defaults;ApplyJson(JsonUtility.ToJson(choice));
#if UNITY_WEBGL && !UNITY_EDITOR
            MiraifuFaceSeedCatalog(JsonUtility.ToJson(catalog));MiraifuFaceSeedReady();
#endif
        }
        public void ApplyJson(string json)
        {
            bool accepted=receiver.TryApplyWebJson(json,out var error);if(accepted){last=JsonUtility.FromJson<MiraifuWebAvatarReceiver.WebChoice>(json);Frame();}Publish(accepted,error);
        }
        void Frame()
        {
            if(world||lab.Actor==null)return;var renderers=lab.Actor.GetComponentsInChildren<SkinnedMeshRenderer>().Where(r=>r.enabled).ToArray();if(renderers.Length==0)return;var bounds=renderers[0].bounds;foreach(var renderer in renderers.Skip(1))bounds.Encapsulate(renderer.bounds);
            viewCamera.orthographic=true;viewCamera.transform.SetPositionAndRotation(new Vector3(bounds.center.x,bounds.center.y,-4),Quaternion.identity);viewCamera.orthographicSize=Mathf.Max(1.15f,Mathf.Max(bounds.extents.y*1.15f,bounds.extents.x/Mathf.Max(.2f,viewCamera.aspect)*1.2f));
        }
        void Update()
        {
            if(world)return;if(width!=Screen.width||height!=Screen.height){width=Screen.width;height=Screen.height;Frame();}
            var mouse=Mouse.current;if(mouse==null)return;var p=mouse.position.ReadValue();if(mouse.leftButton.wasPressedThisFrame){dragging=true;pointer=p;}if(mouse.leftButton.wasReleasedThisFrame)dragging=false;
            if(dragging){float delta=p.x-pointer.x;lab.anchor.Rotate(Vector3.up,-delta*360f/Mathf.Max(1,Screen.width),Space.World);pointer=p;if(Mathf.Abs(delta)>.1f)Publish(true,"");}
        }
        void Publish(bool accepted,string error)
        {
            if(lab.Actor==null)return;var value=lab.Actor.Current;var body=lab.Actor.PartRenderer("body");var face=lab.Actor.PartRenderer("face");var model=lab.Actor.model;
            var state=new State{accepted=accepted,error=error,world=world,choice=last,position=lab.transform.position,yaw=lab.anchor.eulerAngles.y,sourceJsonSha=model.sourceJsonSha,bodyMesh=body.sharedMesh.name,faceMesh=face.sharedMesh.name,pairMatched=body.sharedMesh==model.parts.Single(p=>p.key=="body").meshes[value.expression]&&face.sharedMesh==model.parts.Single(p=>p.key=="face").meshes[value.expression]};
#if UNITY_WEBGL && !UNITY_EDITOR
            MiraifuFaceSeedState(JsonUtility.ToJson(state));
#endif
        }
    }
}
