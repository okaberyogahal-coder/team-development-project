using UnityEngine;

// Added only inside MiraifuCreativePreview, never to the FutureCity player.
// Rotates the visual root, leaving the camera, floor and lighting in place.
public sealed class MiraifuCreativePreviewOrbit : MonoBehaviour
{
    private Camera previewCamera;
    private Vector2 previousPointer;
    private bool dragging;

    public void FrameAvatar()
    {
        if (previewCamera == null) previewCamera = Camera.main;
        if (previewCamera == null) return;
        bool found = false;
        Bounds bounds = new Bounds(transform.position, Vector3.zero);
        foreach (var renderer in GetComponentsInChildren<SkinnedMeshRenderer>())
        {
            if (!renderer.enabled || renderer.sharedMesh == null) continue;
            if (!found) { bounds = renderer.bounds; found = true; }
            else bounds.Encapsulate(renderer.bounds);
        }
        if (!found || bounds.size.sqrMagnitude < 0.001f) return;
        // A sphere fits every yaw, so a broad model cannot be clipped on rotation.
        float radius = bounds.extents.magnitude * 1.12f;
        float vertical = previewCamera.fieldOfView * Mathf.Deg2Rad * 0.5f;
        float horizontal = Mathf.Atan(Mathf.Tan(vertical) * previewCamera.aspect);
        float distance = radius / Mathf.Sin(Mathf.Min(vertical, horizontal));
        Vector3 direction = (previewCamera.transform.position - bounds.center).normalized;
        if (direction.sqrMagnitude < 0.1f) direction = Vector3.back;
        previewCamera.transform.position = bounds.center + direction * distance;
        previewCamera.transform.LookAt(bounds.center);
        previewCamera.nearClipPlane = Mathf.Max(0.01f, distance - radius * 1.5f);
        previewCamera.farClipPlane = Mathf.Max(100f, distance + radius * 2f);
    }

    private float lastAspect;
    private void Update()
    {
        if (previewCamera != null && Mathf.Abs(lastAspect - previewCamera.aspect) > 0.01f)
        {
            lastAspect = previewCamera.aspect;
            FrameAvatar();
        }
        // Mouse and touch share the same angle calculation; no world movement.
        if (Input.touchCount > 0)
        {
            var touch = Input.GetTouch(0);
            if (touch.phase == TouchPhase.Moved) Rotate(touch.deltaPosition.x);
            dragging = false;
            return;
        }
        if (Input.GetMouseButtonDown(0)) { previousPointer = Input.mousePosition; dragging = true; }
        if (Input.GetMouseButtonUp(0)) dragging = false;
        if (!dragging) return;
        Vector2 pointer = Input.mousePosition;
        Rotate(pointer.x - previousPointer.x);
        previousPointer = pointer;
    }

    private void Rotate(float delta)
    {
        transform.Rotate(Vector3.up, -delta * 360f / Mathf.Max(Screen.width, 1), Space.World);
    }
}
