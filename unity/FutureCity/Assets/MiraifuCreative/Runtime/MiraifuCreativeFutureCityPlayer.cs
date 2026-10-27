using System.Collections;
using UnityEngine;
using FutureCity;

/// <summary>
/// Swaps only the third-person player appearance; FutureCity controls the world,
/// camera, movement, profile and UI as before. Attach to a separate scene root.
/// Creative avatar receives ?avatar={JSON} via its own Start() in the WebGL page.
/// </summary>
[DefaultExecutionOrder(300)]
public sealed class MiraifuCreativeFutureCityPlayer : MonoBehaviour
{
    public GameObject creativeAvatarPrefab;
    [Tooltip("For world movement use 0 degrees: the character should face the walking direction. The separate preview uses 180 degrees to face its front camera.")]
    public float visualYawOffset = 0f;
    [Tooltip("Offset the character feet from the generated player's ground.")]
    public float feetOffsetY = 0f;

    private PlayerWalker walker;
    private Animator avatarAnimator;
    private Vector3 lastPlayerPosition;
    private bool wasMoving;
    private bool hasVertParameter;
    private int walkStateHash;
    private int idleStateHash;

    private IEnumerator Start()
    {
        // FutureCityBootstrap creates Player and the original avatar in Awake().
        yield return null;
#if UNITY_2023_1_OR_NEWER
        walker = FindFirstObjectByType<PlayerWalker>();
#else
        walker = FindObjectOfType<PlayerWalker>();
#endif
        if (walker == null || creativeAvatarPrefab == null)
        {
            Debug.LogError("Miraifu: no FutureCity PlayerWalker or Creative Avatar prefab; keep existing player unchanged.");
            yield break;
        }
        var original = walker.body;
        var pivot = new GameObject("MiraifuCreativeWalkingBody").transform;
        pivot.SetParent(walker.transform, false);
        pivot.localPosition = Vector3.zero;
        pivot.localRotation = Quaternion.identity;

        var appearance = Instantiate(creativeAvatarPrefab, pivot, false);
        appearance.name = "MiraifuCreativeAvatar";
        appearance.transform.localPosition = new Vector3(0f, feetOffsetY, 0f);
        appearance.transform.localRotation = Quaternion.Euler(0f, visualYawOffset, 0f);
        appearance.transform.localScale = Vector3.one;
        walker.body = pivot;

        // Only remove the default primitive avatar *after* the new avatar exists.
        if (original != null && original != pivot)
        {
            original.gameObject.SetActive(false);
            Destroy(original.gameObject);
        }
        avatarAnimator = appearance.GetComponentInChildren<Animator>(true);
        if (avatarAnimator != null)
        {
            avatarAnimator.applyRootMotion = false; // only PlayerWalker controls position
            foreach (var p in avatarAnimator.parameters)
                if (p.name == "Vert" && p.type == AnimatorControllerParameterType.Float)
                    hasVertParameter = true;
            walkStateHash = Animator.StringToHash("Base Layer.Walk_Forward");
            idleStateHash = Animator.StringToHash("Base Layer.Idle_Breathing");
        }
        lastPlayerPosition = walker.transform.position;
        Debug.Log("Miraifu: Creative Characters player is attached to FutureCity; website avatar query is supported.");
    }

    private void Update()
    {
        if (walker == null || avatarAnimator == null) return;
        // Position is already controlled by FutureCity.PlayerWalker. Animate locomotion
        // from actual displacement, including click-to-walk and keyboard movement.
        var position = walker.transform.position;
        var displacement = position - lastPlayerPosition;
        displacement.y = 0f;
        float velocity = Time.deltaTime > 0.0001f ? displacement.magnitude / Time.deltaTime : 0f;
        lastPlayerPosition = position;
        bool moving = velocity > 0.16f;
        if (hasVertParameter)
        {
            avatarAnimator.SetFloat("Vert", moving ? Mathf.Clamp01(velocity / 3.2f) : 0f, 0.12f, Time.deltaTime);
        }
        else if (moving != wasMoving)
        {
            int state = moving ? walkStateHash : idleStateHash;
            if (avatarAnimator.HasState(0, state))
                avatarAnimator.CrossFadeInFixedTime(state, 0.18f, 0);
        }
        wasMoving = moving;
    }
}
