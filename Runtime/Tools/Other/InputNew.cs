using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;

namespace XFramework
{
    public static class InputNew
    {
        private const float MouseAxisSensitivity = 0.1f;
        private const float MouseScrollWheelDeltaPerNotch = 120f;

        private static readonly HashSet<KeyCode> s_UnsupportedKeys = new();
        private static readonly HashSet<int> s_UnsupportedMouseButtons = new();
        private static readonly HashSet<string> s_UnsupportedAxes = new();

        public static Vector3 mousePosition
        {
            get
            {
                if (Mouse.current == null)
                {
                    return Vector3.zero;
                }

                Vector2 position = Mouse.current.position.ReadValue();
                return new Vector3(position.x, position.y, 0f);
            }
        }

        public static bool GetKey(KeyCode key)
        {
            return TryGetKeyControl(key, out KeyControl control) && control.isPressed;
        }

        public static bool GetKeyDown(KeyCode key)
        {
            return TryGetKeyControl(key, out KeyControl control) && control.wasPressedThisFrame;
        }

        public static bool GetKeyUp(KeyCode key)
        {
            return TryGetKeyControl(key, out KeyControl control) && control.wasReleasedThisFrame;
        }

        public static bool GetMouseButton(int button)
        {
            return TryGetMouseButton(button, out ButtonControl control) && control.isPressed;
        }

        public static bool GetMouseButtonDown(int button)
        {
            return TryGetMouseButton(button, out ButtonControl control) && control.wasPressedThisFrame;
        }

        public static bool GetMouseButtonUp(int button)
        {
            return TryGetMouseButton(button, out ButtonControl control) && control.wasReleasedThisFrame;
        }

        public static float GetAxis(string axisName)
        {
            switch (axisName)
            {
                case "Horizontal":
                    return GetKeyboardAxis(KeyCode.LeftArrow, KeyCode.A, KeyCode.RightArrow, KeyCode.D);
                case "Vertical":
                    return GetKeyboardAxis(KeyCode.DownArrow, KeyCode.S, KeyCode.UpArrow, KeyCode.W);
                case "Mouse X":
                    return GetMouseDeltaAxis(0);
                case "Mouse Y":
                    return GetMouseDeltaAxis(1);
                case "Mouse ScrollWheel":
                    return GetMouseScrollWheelAxis();
                default:
                    WarnUnsupportedAxis(axisName);
                    return 0f;
            }
        }

        private static float GetKeyboardAxis(KeyCode negativeKey, KeyCode altNegativeKey, KeyCode positiveKey, KeyCode altPositiveKey)
        {
            bool negative = GetKey(negativeKey) || GetKey(altNegativeKey);
            bool positive = GetKey(positiveKey) || GetKey(altPositiveKey);

            if (negative == positive)
            {
                return 0f;
            }

            return positive ? 1f : -1f;
        }

        private static float GetMouseDeltaAxis(int axis)
        {
            Mouse mouse = Mouse.current;
            if (mouse == null)
            {
                return 0f;
            }

            Vector2 delta = mouse.delta.ReadValue();
            return (axis == 0 ? delta.x : delta.y) * MouseAxisSensitivity;
        }

        private static float GetMouseScrollWheelAxis()
        {
            Mouse mouse = Mouse.current;
            if (mouse == null)
            {
                return 0f;
            }

            return mouse.scroll.ReadValue().y / MouseScrollWheelDeltaPerNotch * MouseAxisSensitivity;
        }

        private static bool TryGetKeyControl(KeyCode keyCode, out KeyControl control)
        {
            control = null;
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null)
            {
                return false;
            }

            if (!TryMapKeyCode(keyCode, out Key key))
            {
                WarnUnsupportedKey(keyCode);
                return false;
            }

            control = keyboard[key];
            return control != null;
        }

        private static bool TryGetMouseButton(int button, out ButtonControl control)
        {
            control = null;
            Mouse mouse = Mouse.current;
            if (mouse == null)
            {
                return false;
            }

            control = button switch
            {
                0 => mouse.leftButton,
                1 => mouse.rightButton,
                2 => mouse.middleButton,
                _ => null
            };

            if (control == null)
            {
                WarnUnsupportedMouseButton(button);
                return false;
            }

            return true;
        }

        private static bool TryMapKeyCode(KeyCode keyCode, out Key key)
        {
            key = keyCode switch
            {
                KeyCode.A => Key.A,
                KeyCode.B => Key.B,
                KeyCode.C => Key.C,
                KeyCode.D => Key.D,
                KeyCode.E => Key.E,
                KeyCode.F => Key.F,
                KeyCode.G => Key.G,
                KeyCode.H => Key.H,
                KeyCode.I => Key.I,
                KeyCode.J => Key.J,
                KeyCode.K => Key.K,
                KeyCode.L => Key.L,
                KeyCode.M => Key.M,
                KeyCode.N => Key.N,
                KeyCode.O => Key.O,
                KeyCode.P => Key.P,
                KeyCode.Q => Key.Q,
                KeyCode.R => Key.R,
                KeyCode.S => Key.S,
                KeyCode.T => Key.T,
                KeyCode.U => Key.U,
                KeyCode.V => Key.V,
                KeyCode.W => Key.W,
                KeyCode.X => Key.X,
                KeyCode.Y => Key.Y,
                KeyCode.Z => Key.Z,
                KeyCode.Alpha0 => Key.Digit0,
                KeyCode.Alpha1 => Key.Digit1,
                KeyCode.Alpha2 => Key.Digit2,
                KeyCode.Alpha3 => Key.Digit3,
                KeyCode.Alpha4 => Key.Digit4,
                KeyCode.Alpha5 => Key.Digit5,
                KeyCode.Alpha6 => Key.Digit6,
                KeyCode.Alpha7 => Key.Digit7,
                KeyCode.Alpha8 => Key.Digit8,
                KeyCode.Alpha9 => Key.Digit9,
                KeyCode.Keypad0 => Key.Numpad0,
                KeyCode.Keypad1 => Key.Numpad1,
                KeyCode.Keypad2 => Key.Numpad2,
                KeyCode.Keypad3 => Key.Numpad3,
                KeyCode.Keypad4 => Key.Numpad4,
                KeyCode.Keypad5 => Key.Numpad5,
                KeyCode.Keypad6 => Key.Numpad6,
                KeyCode.Keypad7 => Key.Numpad7,
                KeyCode.Keypad8 => Key.Numpad8,
                KeyCode.Keypad9 => Key.Numpad9,
                KeyCode.F1 => Key.F1,
                KeyCode.F2 => Key.F2,
                KeyCode.F3 => Key.F3,
                KeyCode.F4 => Key.F4,
                KeyCode.F5 => Key.F5,
                KeyCode.F6 => Key.F6,
                KeyCode.F7 => Key.F7,
                KeyCode.F8 => Key.F8,
                KeyCode.F9 => Key.F9,
                KeyCode.F10 => Key.F10,
                KeyCode.F11 => Key.F11,
                KeyCode.F12 => Key.F12,
                KeyCode.Escape => Key.Escape,
                KeyCode.Space => Key.Space,
                KeyCode.Return => Key.Enter,
                KeyCode.KeypadEnter => Key.NumpadEnter,
                KeyCode.Tab => Key.Tab,
                KeyCode.Backspace => Key.Backspace,
                KeyCode.Delete => Key.Delete,
                KeyCode.UpArrow => Key.UpArrow,
                KeyCode.DownArrow => Key.DownArrow,
                KeyCode.LeftArrow => Key.LeftArrow,
                KeyCode.RightArrow => Key.RightArrow,
                KeyCode.LeftShift => Key.LeftShift,
                KeyCode.RightShift => Key.RightShift,
                KeyCode.LeftControl => Key.LeftCtrl,
                KeyCode.RightControl => Key.RightCtrl,
                KeyCode.LeftAlt => Key.LeftAlt,
                KeyCode.RightAlt => Key.RightAlt,
                KeyCode.LeftCommand => Key.LeftMeta,
                KeyCode.RightCommand => Key.RightMeta,
                KeyCode.Minus => Key.Minus,
                KeyCode.Equals => Key.Equals,
                KeyCode.LeftBracket => Key.LeftBracket,
                KeyCode.RightBracket => Key.RightBracket,
                KeyCode.Semicolon => Key.Semicolon,
                KeyCode.Quote => Key.Quote,
                KeyCode.Comma => Key.Comma,
                KeyCode.Period => Key.Period,
                KeyCode.Slash => Key.Slash,
                KeyCode.Backslash => Key.Backslash,
                KeyCode.BackQuote => Key.Backquote,
                _ => Key.None
            };

            return key != Key.None;
        }

        private static void WarnUnsupportedKey(KeyCode keyCode)
        {
            if (s_UnsupportedKeys.Add(keyCode))
            {
                Debug.LogWarning($"[InputNew] Unsupported KeyCode: {keyCode}.");
            }
        }

        private static void WarnUnsupportedMouseButton(int button)
        {
            if (s_UnsupportedMouseButtons.Add(button))
            {
                Debug.LogWarning($"[InputNew] Unsupported mouse button: {button}.");
            }
        }

        private static void WarnUnsupportedAxis(string axisName)
        {
            if (s_UnsupportedAxes.Add(axisName))
            {
                Debug.LogWarning($"[InputNew] Unsupported axis: {axisName ?? "<null>"}.");
            }
        }
    }
}
