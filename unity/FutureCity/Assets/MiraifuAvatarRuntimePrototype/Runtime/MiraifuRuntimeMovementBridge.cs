using UnityEngine;
using FutureCity;
namespace MiraifuAvatarPrototype
{
    // Reuses the production motor without modifying it. Only the visual child is replaced.
    [DefaultExecutionOrder(500)]
    public sealed class MiraifuRuntimeMovementBridge : MonoBehaviour
    {
        public MiraifuRuntimeAvatarLab lab;
        public PlayerWalker walker;
        CharacterController controller;
        MiraifuRuntimeAvatar orientedActor;
        Vector3 previous;
        void Awake()
        {
            controller=walker.GetComponent<CharacterController>();
            // A stable, disabled Animator keeps PlayerWalker's animated-body path active
            // across model swaps. It never animates or alters the child's skeleton.
            var marker=lab.anchor.gameObject.AddComponent<Animator>();
            marker.runtimeAnimatorController=lab.models[0].prefab.GetComponentInChildren<Animator>().runtimeAnimatorController;
            marker.enabled=false;
            walker.body=lab.anchor;
            walker.Place(transform.position,0);
            previous=transform.position;
            PlayerWalker.LookWanted=false;
            lab.Changed+=OrientVisual;
        }
        void OrientVisual(AvatarSettings state)
        {
            if(lab.Actor==null||lab.Actor==orientedActor)return;
            // Candidate previews face their front camera (180 degrees). The native
            // rig faces +Z for movement; change only this transient visual instance.
            lab.Actor.transform.localRotation=Quaternion.identity;orientedActor=lab.Actor;
        }
        void OnDestroy(){if(lab!=null)lab.Changed-=OrientVisual;}
        void LateUpdate()
        {
            Vector3 delta=transform.position-previous;previous=transform.position;delta.y=0;
            var motion=!controller.isGrounded?AvatarMotion.Jump:delta.magnitude/Mathf.Max(Time.deltaTime,.0001f)>.16f?AvatarMotion.Walk:AvatarMotion.Idle;
            if(lab.Actor!=null&&lab.Actor.Motion!=motion)lab.SetMotion(motion);
            // IMGUI is deliberately mouse-accessible; Q/E and wheel retain the existing camera controls.
            PlayerWalker.LookWanted=false;PlayerWalker.SetFreeLook(false);
        }
    }
}
