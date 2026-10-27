using UnityEngine;

namespace FutureCity
{
    /// <summary>Separate character and camera: wardrobe preview never moves the player's camera or body.</summary>
    public sealed class AvatarWardrobePreview : MonoBehaviour
    {
        const int PreviewLayer = 30;
        GameObject stage;
        ImportedAvatar avatar;
        Camera previewCamera, worldCamera;
        RenderTexture texture;
        string lastAppearance;
        float angle = 180f;
        bool worldIncludedPreview;
        int lastShownFrame = -10;
        public RenderTexture Texture => texture;

        public void Render(ImportedAvatar source, Camera camera, Color? background = null)
        {
            if (stage == null) Create(camera);
            previewCamera.backgroundColor = background ?? new Color(0.94f, 0.94f, 0.9f);
            string json = JsonUtility.ToJson(source.Appearance);
            if (json != lastAppearance)
            {
                avatar.ApplyAppearance(source.Appearance);
                var animator = avatar.GetComponentInChildren<Animator>();
                avatar.UpdateMotion(0f, false);
                animator.Play("Idle");
                animator.Update(0f);
                lastAppearance = json;
            }
            avatar.transform.localRotation = Quaternion.Euler(0f, angle, 0f);
            // OnGUI runs inside camera rendering. Let URP render this camera in its normal
            // camera loop instead of submitting a nested render request from OnGUI.
            lastShownFrame = Time.frameCount;
            previewCamera.enabled = true;
        }

        void LateUpdate()
        {
            if (previewCamera != null) previewCamera.enabled = Time.frameCount <= lastShownFrame + 1;
        }

        public void Turn(float degrees) => angle += degrees;

        void Create(Camera camera)
        {
            worldCamera = camera;
            worldIncludedPreview = (worldCamera.cullingMask & (1 << PreviewLayer)) != 0;
            worldCamera.cullingMask &= ~(1 << PreviewLayer);
            stage = new GameObject("WardrobePreview") { hideFlags = HideFlags.HideAndDontSave };
            stage.transform.position = new Vector3(0f, -20000f, 0f);
            var actor = ImportedAvatar.TryBuild(stage.transform, "WardrobePreviewAvatar", 1f, 40, false);
            avatar = actor.GetComponent<ImportedAvatar>();
            actor.GetComponent<PersonAnimator>().enabled = false;
            foreach (var child in actor.GetComponentsInChildren<Transform>(true)) child.gameObject.layer = PreviewLayer;
            var cameraObject = new GameObject("WardrobeCamera");
            cameraObject.transform.SetParent(stage.transform, false);
            previewCamera = cameraObject.AddComponent<Camera>();
            previewCamera.enabled = false;
            previewCamera.cullingMask = 1 << PreviewLayer;
            previewCamera.clearFlags = CameraClearFlags.SolidColor;
            previewCamera.backgroundColor = new Color(0.94f, 0.94f, 0.9f);
            // Leave room for the tallest hats and mascot ears, as well as the feet.
            previewCamera.fieldOfView = 36f;
            previewCamera.nearClipPlane = 0.1f;
            previewCamera.farClipPlane = 10f;
            cameraObject.transform.localPosition = new Vector3(0f, 1.05f, -3.6f);
            cameraObject.transform.localRotation = Quaternion.identity;
            texture = new RenderTexture(384, 512, 24) { name = "WardrobePreview", antiAliasing = 2 };
            texture.Create();
            previewCamera.targetTexture = texture;
        }

        void OnDestroy()
        {
            if (worldCamera != null && worldIncludedPreview) worldCamera.cullingMask |= 1 << PreviewLayer;
            if (texture != null) { texture.Release(); Destroy(texture); }
            if (stage != null) Destroy(stage);
        }
    }
}
