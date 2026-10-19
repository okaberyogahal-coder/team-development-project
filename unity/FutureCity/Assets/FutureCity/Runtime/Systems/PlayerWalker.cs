using UnityEngine;

namespace FutureCity
{
    /// <summary>
    /// 三人称で街を歩くプレイヤー。
    ///  - 視点：ふだんからマウスを動かすだけで見回せる（ボタン不要）。Esc でカーソルを出す（画面をクリックで戻る）。V で切り替え
    ///          カーソルを出しているときは、左右どちらかのボタンでドラッグしても見回せる
    ///          Q／E でも回せる ／ ホイールで近づく・離れる ／ R で視点をもとに戻す
    ///  - 移動：地面を左クリック（ドラッグせずに離す）でそこまで歩く ／ WASD・矢印キー（Shift で速く）
    /// カメラはなめらかに追従し、段差や木・街灯でガクッと動かないようにしている。
    /// 画面（パネル）を開いている間は移動しない（仕様 FR-202／315）。
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public class PlayerWalker : MonoBehaviour
    {
        public Camera cam;
        /// <summary>プレイヤーのアバター（見た目）</summary>
        public Transform body;

        [Header("移動")]
        public float walkSpeed = 3.2f;
        public float runSpeed = 6f;
        public float jumpHeight = 0.8f;
        public float gravity = -20f;

        [Header("視点（三人称）")]
        public float lookSensitivity = 1.2f;
        public float keyTurnSpeed = 70f;
        public float cameraDistance = 5f;
        public float minDistance = 2.5f, maxDistance = 9f;
        public float pivotHeight = 1.6f;
        public float minPitch = -10f, maxPitch = 60f;
        [Tooltip("視点の追従のなめらかさ（大きいほど素早い）")]
        public float lookSmoothing = 14f;

        /// <summary>カメラの障害物判定から外すレイヤー（デモ人物など。2 = Ignore Raycast）</summary>
        public const int PeopleLayer = 2;

        CharacterController cc;
        Vector3 spawnPos;
        float spawnYaw;
        float yaw, pitch = 15f, vy;           // 目標の向き
        float camYaw, camPitch = 15f;          // 実際のカメラの向き（なめらかに追従）
        float bodyYaw, stride, strideAmount, camDist, smoothDistance;
        Vector3 planarVel, smoothPivot;
        bool pivotInit;

        // ドラッグで見回す
        bool pressing, dragging;
        Vector2 pressPos;
        public static bool FreeLook { get; private set; }
        /// <summary>マウスで見回すモードにしたいか（既定：オン）。パネルを開いている間だけ自動でカーソルを出す</summary>
        public static bool LookWanted = true;
        bool suppressThisPress;
        /// <summary>直前のマウス操作がドラッグ（見回し）だった → クリック移動にしない</summary>
        public bool LastPressWasDrag { get; private set; }

        // クリック移動
        Vector3? autoTarget;
        float autoStuck;
        Vector3 lastPos;
        System.Action onArrive;

        public bool AutoMoving => autoTarget.HasValue;
        public float Yaw => yaw;

        void Awake()
        {
            cc = GetComponent<CharacterController>();
            cc.height = 1.8f;
            cc.radius = 0.35f;
            cc.center = new Vector3(0f, 0.9f, 0f);
            cc.stepOffset = 0.5f;
            cc.slopeLimit = 50f;
            cc.skinWidth = 0.05f;
            camDist = smoothDistance = cameraDistance;
        }

        public void Place(Vector3 pos, float yawDeg)
        {
            spawnPos = pos;
            spawnYaw = yawDeg;
            Teleport(pos, yawDeg);
        }

        public void Teleport(Vector3 pos, float yawDeg)
        {
            if (cc == null) cc = GetComponent<CharacterController>();
            cc.enabled = false;
            transform.position = pos;
            cc.enabled = true;
            yaw = camYaw = bodyYaw = yawDeg;
            pitch = camPitch = 15f;
            vy = 0f;
            planarVel = Vector3.zero;
            pivotInit = false;
            StopAuto();
        }

        /// <summary>指定した場所まで歩く。着いたら onArrived を呼ぶ。</summary>
        public void MoveTo(Vector3 target, System.Action onArrived = null)
        {
            autoTarget = target;
            onArrive = onArrived;
            autoStuck = 0f;
            lastPos = transform.position;
        }

        public void StopAuto()
        {
            autoTarget = null;
            onArrive = null;
        }

        /// <summary>視点をもとに戻す（自分の後ろから少し見下ろす）</summary>
        public void ResetView()
        {
            yaw = bodyYaw;
            pitch = 15f;
            cameraDistance = 5f;
        }

        public static void SetFreeLook(bool on)
        {
            FreeLook = on;
            Cursor.lockState = on ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !on;
        }

        void OnDisable()
        {
            if (FreeLook) SetFreeLook(false);
        }

        void Update()
        {
            // パネルを開いている間は止まる（文字入力と移動が混ざらない）。カーソルを出してボタンを押せるようにする
            if (MiraiWorld.UIOpen)
            {
                if (FreeLook) SetFreeLook(false);
                planarVel = Vector3.zero;
                strideAmount = 0f;
                pressing = dragging = false;
                return;
            }
            float dt = Time.deltaTime;

            // ---- 見回すモード：ふだんはオン。Esc でカーソルを出し、画面をクリックすると戻る。V で切り替え
            if (InputBridge.Down(FKey.Escape) && LookWanted) LookWanted = false;
            else if (InputBridge.Down(FKey.V)) LookWanted = !LookWanted;
            bool down = InputBridge.LeftMouseDown || InputBridge.RightMouseDown;
            if (down)
            {
                suppressThisPress = false;
                if (!LookWanted && InputBridge.LeftMouseDown && !MiraiWorld.PointerOverUI)
                {
                    LookWanted = true;            // このクリックは「見回しに戻る」だけに使う（歩かない）
                    suppressThisPress = true;
                }
            }
            if (FreeLook != LookWanted) SetFreeLook(LookWanted);

            Vector2 md = InputBridge.MouseDelta;
            if (down && (FreeLook || !MiraiWorld.PointerOverUI))
            {
                pressing = true;
                dragging = false;
                pressPos = InputBridge.MousePosition;
            }
            bool held = InputBridge.LeftMouse || InputBridge.RightMouse;
            if (FreeLook)
            {
                Look(md);   // ボタンを押さなくても、マウスを動かすだけで見回す
            }
            else if (pressing && held)
            {
                if (!dragging && (InputBridge.MousePosition - pressPos).sqrMagnitude > 36f) dragging = true; // 6px 以上動いたらドラッグ
                if (dragging || InputBridge.RightMouse) Look(md);
            }
            if (pressing && !held)
            {
                LastPressWasDrag = (!FreeLook && dragging) || suppressThisPress;
                pressing = dragging = false;
                suppressThisPress = false;
            }
            if (InputBridge.Held(FKey.Q)) yaw -= keyTurnSpeed * dt;
            if (InputBridge.Held(FKey.E) && !MiraiWorld.PromptVisible) yaw += keyTurnSpeed * dt;
            float sc = InputBridge.Scroll;
            if (sc != 0f && !MiraiWorld.PointerOverUI)
                cameraDistance = Mathf.Clamp(cameraDistance - sc * 0.7f, minDistance, maxDistance);
            if (InputBridge.Down(FKey.R)) ResetView();

            // ---- 移動：キー（カメラの向き基準）
            var flatRot = Quaternion.Euler(0f, camYaw, 0f);
            Vector3 fwd = flatRot * Vector3.forward, right = flatRot * Vector3.right;
            Vector3 input = Vector3.zero;
            if (InputBridge.Held(FKey.W) || InputBridge.Held(FKey.Up)) input += fwd;
            if (InputBridge.Held(FKey.S) || InputBridge.Held(FKey.Down)) input -= fwd;
            if (InputBridge.Held(FKey.D) || InputBridge.Held(FKey.Right)) input += right;
            if (InputBridge.Held(FKey.A) || InputBridge.Held(FKey.Left)) input -= right;

            if (input.sqrMagnitude > 0.01f) StopAuto();
            else if (autoTarget.HasValue)
            {
                Vector3 to = autoTarget.Value - transform.position;
                to.y = 0f;
                if (to.magnitude < 0.6f)
                {
                    var cb = onArrive;
                    StopAuto();
                    cb?.Invoke();
                }
                else
                {
                    input = to.normalized;
                    // 近づいたら減速して、ぴたっと止まる
                    if (to.magnitude < 1.5f) input *= Mathf.Clamp01(to.magnitude / 1.5f) + 0.25f;
                    if ((transform.position - lastPos).sqrMagnitude < 0.0004f) autoStuck += dt; else autoStuck = 0f;
                    lastPos = transform.position;
                    if (autoStuck > 1.5f) StopAuto();
                }
            }

            float speed = InputBridge.Held(FKey.Shift) ? runSpeed : walkSpeed;
            Vector3 target = input.sqrMagnitude > 0.0001f ? Vector3.ClampMagnitude(input, 1f) * speed : Vector3.zero;
            if (input.sqrMagnitude > 1.01f) target = input.normalized * speed;
            // 加速・減速をなめらかに（指数補間）
            float k = 1f - Mathf.Exp(-(cc.isGrounded ? 10f : 2.5f) * dt);
            planarVel = Vector3.Lerp(planarVel, target, k);

            if (cc.isGrounded)
            {
                if (vy < 0f) vy = -2f;
                if (InputBridge.Down(FKey.Space)) vy = Mathf.Sqrt(jumpHeight * -2f * gravity);
            }
            vy += gravity * dt;
            cc.Move((planarVel + Vector3.up * vy) * dt);

            // 体を進む方向へ（なめらかに回す）
            float sp = planarVel.magnitude;
            if (sp > 0.3f)
            {
                float want = Mathf.Atan2(planarVel.x, planarVel.z) * Mathf.Rad2Deg;
                bodyYaw = Mathf.LerpAngle(bodyYaw, want, 1f - Mathf.Exp(-10f * dt));
            }
            bool moving = cc.isGrounded && sp > 0.4f;
            strideAmount = Mathf.Lerp(strideAmount, moving ? 1f : 0f, 1f - Mathf.Exp(-6f * dt));
            if (moving) stride += dt * sp * 2.4f;

            if (transform.position.y < -20f) Teleport(spawnPos, spawnYaw);
        }

        void Look(Vector2 md)
        {
            yaw += md.x * lookSensitivity;
            pitch = Mathf.Clamp(pitch - md.y * lookSensitivity, minPitch, maxPitch);
        }

        void LateUpdate()
        {
            if (cam == null) return;
            float dt = Time.deltaTime;

            if (body != null)
            {
                float lean = Mathf.Clamp(planarVel.magnitude * 1.2f, 0f, 8f);
                body.localPosition = Vector3.up * (Mathf.Abs(Mathf.Sin(stride)) * 0.05f * strideAmount);
                body.rotation = Quaternion.Euler(lean, bodyYaw, Mathf.Sin(stride) * 2f * strideAmount);
            }

            // 向き：目標に向かってなめらかに追従（マウスの細かな揺れやフレームのばらつきを吸収）
            float lk = 1f - Mathf.Exp(-lookSmoothing * dt);
            camYaw = Mathf.LerpAngle(camYaw, yaw, lk);
            camPitch = Mathf.Lerp(camPitch, pitch, lk);
            smoothDistance = Mathf.Lerp(smoothDistance, cameraDistance, 1f - Mathf.Exp(-8f * dt));

            // 注視点：水平はぴったり、高さは段差でガクッとしないようになめらかに
            Vector3 rawPivot = transform.position + Vector3.up * pivotHeight;
            if (!pivotInit) { smoothPivot = rawPivot; pivotInit = true; }
            smoothPivot.x = rawPivot.x;
            smoothPivot.z = rawPivot.z;
            smoothPivot.y = Mathf.Lerp(smoothPivot.y, rawPivot.y, 1f - Mathf.Exp(-12f * dt));

            var rot = Quaternion.Euler(camPitch, camYaw, 0f);
            Vector3 dir = rot * Vector3.back;

            // 建物・壁があれば手前に寄る。デモ人物は無視し、寄る・離れるともになめらかに
            int mask = ~(1 << PeopleLayer);
            float allowed = smoothDistance;
            if (Physics.SphereCast(smoothPivot, 0.2f, dir, out RaycastHit hit, smoothDistance, mask, QueryTriggerInteraction.Ignore))
                allowed = Mathf.Max(0.6f, hit.distance - 0.1f);
            float rate = allowed < camDist ? 18f : 4f;
            camDist = Mathf.Lerp(camDist, allowed, 1f - Mathf.Exp(-rate * dt));
            if (camDist > allowed + 0.4f) camDist = allowed + 0.4f;   // 壁を大きく突き抜けない

            cam.transform.SetPositionAndRotation(smoothPivot + dir * camDist, rot);
            if (body != null) body.gameObject.SetActive(camDist > 0.9f);
        }
    }
}
