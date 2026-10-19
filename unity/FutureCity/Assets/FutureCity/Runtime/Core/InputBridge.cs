using UnityEngine;
#if FUTURECITY_INPUTSYSTEM && ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace FutureCity
{
    public enum FKey { W, A, S, D, Q, E, Shift, Space, H, T, One, Two, Three, Four, Five, Six, N, P, Enter, Up, Down, Left, Right, R, V, LeftBracket, RightBracket, Escape }

    /// <summary>新 Input System / 旧 Input Manager のどちらの設定でも動く入力ラッパー。</summary>
    public static class InputBridge
    {
#if FUTURECITY_INPUTSYSTEM && ENABLE_INPUT_SYSTEM
        static Key Map(FKey k)
        {
            switch (k)
            {
                case FKey.W: return Key.W;
                case FKey.A: return Key.A;
                case FKey.S: return Key.S;
                case FKey.D: return Key.D;
                case FKey.Q: return Key.Q;
                case FKey.E: return Key.E;
                case FKey.Shift: return Key.LeftShift;
                case FKey.Space: return Key.Space;
                case FKey.H: return Key.H;
                case FKey.T: return Key.T;
                case FKey.One: return Key.Digit1;
                case FKey.Two: return Key.Digit2;
                case FKey.Three: return Key.Digit3;
                case FKey.Four: return Key.Digit4;
                case FKey.Five: return Key.Digit5;
                case FKey.Six: return Key.Digit6;
                case FKey.N: return Key.N;
                case FKey.P: return Key.P;
                case FKey.Enter: return Key.Enter;
                case FKey.Up: return Key.UpArrow;
                case FKey.Down: return Key.DownArrow;
                case FKey.Left: return Key.LeftArrow;
                case FKey.Right: return Key.RightArrow;
                case FKey.R: return Key.R;
                case FKey.V: return Key.V;
                case FKey.LeftBracket: return Key.LeftBracket;
                case FKey.RightBracket: return Key.RightBracket;
                default: return Key.Escape;
            }
        }

        public static bool Held(FKey k)
        {
            var kb = Keyboard.current;
            return kb != null && kb[Map(k)].isPressed;
        }

        public static bool Down(FKey k)
        {
            var kb = Keyboard.current;
            return kb != null && kb[Map(k)].wasPressedThisFrame;
        }

        public static Vector2 MouseDelta => Mouse.current != null ? Mouse.current.delta.ReadValue() * 0.1f : Vector2.zero;
        public static bool RightMouse => Mouse.current != null && Mouse.current.rightButton.isPressed;
        public static bool LeftMouseDown => Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame;
        public static bool LeftMouse => Mouse.current != null && Mouse.current.leftButton.isPressed;
        public static bool LeftMouseUp => Mouse.current != null && Mouse.current.leftButton.wasReleasedThisFrame;
        public static bool RightMouseDown => Mouse.current != null && Mouse.current.rightButton.wasPressedThisFrame;
        /// <summary>マウス位置（画面ピクセル、左下が原点）</summary>
        public static Vector2 MousePosition => Mouse.current != null ? Mouse.current.position.ReadValue() : Vector2.zero;

        public static float Scroll
        {
            get
            {
                if (Mouse.current == null) return 0f;
                float y = Mouse.current.scroll.ReadValue().y;
                return Mathf.Abs(y) > 0.01f ? Mathf.Sign(y) : 0f;
            }
        }
#else
        static KeyCode Map(FKey k)
        {
            switch (k)
            {
                case FKey.W: return KeyCode.W;
                case FKey.A: return KeyCode.A;
                case FKey.S: return KeyCode.S;
                case FKey.D: return KeyCode.D;
                case FKey.Q: return KeyCode.Q;
                case FKey.E: return KeyCode.E;
                case FKey.Shift: return KeyCode.LeftShift;
                case FKey.Space: return KeyCode.Space;
                case FKey.H: return KeyCode.H;
                case FKey.T: return KeyCode.T;
                case FKey.One: return KeyCode.Alpha1;
                case FKey.Two: return KeyCode.Alpha2;
                case FKey.Three: return KeyCode.Alpha3;
                case FKey.Four: return KeyCode.Alpha4;
                case FKey.Five: return KeyCode.Alpha5;
                case FKey.Six: return KeyCode.Alpha6;
                case FKey.N: return KeyCode.N;
                case FKey.P: return KeyCode.P;
                case FKey.Enter: return KeyCode.Return;
                case FKey.Up: return KeyCode.UpArrow;
                case FKey.Down: return KeyCode.DownArrow;
                case FKey.Left: return KeyCode.LeftArrow;
                case FKey.Right: return KeyCode.RightArrow;
                case FKey.R: return KeyCode.R;
                case FKey.V: return KeyCode.V;
                case FKey.LeftBracket: return KeyCode.LeftBracket;
                case FKey.RightBracket: return KeyCode.RightBracket;
                default: return KeyCode.Escape;
            }
        }

        public static bool Held(FKey k) => Input.GetKey(Map(k));
        public static bool Down(FKey k) => Input.GetKeyDown(Map(k));
        public static Vector2 MouseDelta => new Vector2(Input.GetAxis("Mouse X"), Input.GetAxis("Mouse Y"));
        public static bool RightMouse => Input.GetMouseButton(1);
        public static bool LeftMouseDown => Input.GetMouseButtonDown(0);
        public static bool LeftMouse => Input.GetMouseButton(0);
        public static bool LeftMouseUp => Input.GetMouseButtonUp(0);
        public static bool RightMouseDown => Input.GetMouseButtonDown(1);
        public static Vector2 MousePosition => Input.mousePosition;

        public static float Scroll
        {
            get
            {
                float y = Input.mouseScrollDelta.y;
                return Mathf.Abs(y) > 0.01f ? Mathf.Sign(y) : 0f;
            }
        }
#endif
    }
}
