using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;
using InputTouchPhase = UnityEngine.InputSystem.TouchPhase;

namespace XFramework.AutoTest
{
    internal static class AutoTestInput
    {
        private const int DefaultSettleFrames = 3;
        private const string AutomationMouseLayout = "XFrameworkAutomationMouse";
        private const string AutomationKeyboardLayout = "XFrameworkAutomationKeyboard";
        private const string AutomationTouchscreenLayout = "XFrameworkAutomationTouchscreen";
        private static readonly HashSet<Key> s_HeldKeys = new HashSet<Key>();
        private static readonly HashSet<MouseButton> s_HeldMouseButtons = new HashSet<MouseButton>();
        private static readonly Dictionary<int, Vector2> s_TouchPositions = new Dictionary<int, Vector2>();
        private static Mouse s_Mouse;
        private static Keyboard s_Keyboard;
        private static Touchscreen s_Touchscreen;
        private static MouseState s_MouseState;
        private static Vector2 s_MousePosition;

        [Serializable]
        private sealed class InputStateResult
        {
            public string timestampUtc;
            public int frameCount;
            public bool initialized;
            public List<string> heldKeys = new List<string>();
            public MouseInputState mouse;
            public List<TouchInputState> touches = new List<TouchInputState>();
            public InputDeviceState keyboardDevice;
            public InputDeviceState mouseDevice;
            public InputDeviceState touchscreenDevice;
        }

        [Serializable]
        private sealed class MouseInputState
        {
            public Vector2 position;
            public List<string> heldButtons = new List<string>();
        }

        [Serializable]
        private sealed class TouchInputState
        {
            public int touchId;
            public Vector2 position;
        }

        [Serializable]
        private sealed class InputDeviceState
        {
            public int deviceId;
            public string displayName;
            public string layout;
            public bool added;
            public bool enabled;
            public bool isCurrent;
        }

        internal static void ResetState()
        {
            s_HeldKeys.Clear();
            s_HeldMouseButtons.Clear();
            s_TouchPositions.Clear();
            s_Mouse = null;
            s_Keyboard = null;
            s_Touchscreen = null;
            s_MouseState = new MouseState();
            s_MousePosition = Vector2.zero;
        }

        internal static void ResetInterruptedInput()
        {
            ResetInputImmediately();
        }

        internal static string GetState(bool compact)
        {
            var result = new InputStateResult {
                timestampUtc = DateTime.UtcNow.ToString("O"),
                frameCount = Time.frameCount,
                initialized = s_Mouse != null || s_Keyboard != null || s_Touchscreen != null,
                heldKeys = s_HeldKeys.Select(key => key.ToString()).OrderBy(key => key, StringComparer.Ordinal).ToList(),
                mouse = new MouseInputState {
                    position = s_MousePosition,
                    heldButtons = s_HeldMouseButtons.Select(button => button.ToString()).OrderBy(button => button, StringComparer.Ordinal).ToList(),
                },
                touches = s_TouchPositions.OrderBy(pair => pair.Key).Select(pair => new TouchInputState { touchId = pair.Key, position = pair.Value }).ToList(),
                keyboardDevice = CreateDeviceState(s_Keyboard, Keyboard.current),
                mouseDevice = CreateDeviceState(s_Mouse, Mouse.current),
                touchscreenDevice = CreateDeviceState(s_Touchscreen, Touchscreen.current),
            };
            return JsonUtility.ToJson(result, !compact);
        }

        internal static IEnumerator Run(AutoTestInputSequence sequence, AutoTestOperationContext context)
        {
            InputSettings settings = InputSystem.settings;
            InputSettings.BackgroundBehavior previousBackgroundBehavior = settings.backgroundBehavior;
#if UNITY_EDITOR
            InputSettings.EditorInputBehaviorInPlayMode previousEditorInputBehavior = settings.editorInputBehaviorInPlayMode;
#endif
            bool resetCompleted = false;
            try
            {
                settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
#if UNITY_EDITOR
                settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
#endif
                EnsureDevices();
                for (int i = 0; i < sequence.steps.Count; i++)
                {
                    if (context.IsCancellationRequested)
                        yield break;
                    yield return ExecuteStep(sequence.steps[i]);
                }

                if (sequence.resetAfter)
                {
                    yield return ResetInput();
                    resetCompleted = true;
                }
                context.SetOutput("OK");
            }
            finally
            {
                if (sequence.resetAfter && !resetCompleted)
                    ResetInputImmediately();
#if UNITY_EDITOR
                settings.editorInputBehaviorInPlayMode = previousEditorInputBehavior;
#endif
                settings.backgroundBehavior = previousBackgroundBehavior;
            }
        }

        internal static IEnumerator RunPointerAction(string action, float x, float y, string button, float scrollX, float scrollY)
        {
            var sequence = new AutoTestInputSequence {
                steps = new List<AutoTestInputStep> {
                    new AutoTestInputStep {
                        type = "mouse",
                        action = action,
                        button = button,
                        x = x,
                        y = y,
                        scrollX = scrollX,
                        scrollY = scrollY,
                        frames = 2,
                    },
                },
            };
            var context = new AutoTestOperationContext(new AutoTestOperationInfo(0, "pointer", _ => null));
            return Run(sequence, context);
        }

        private static IEnumerator ExecuteStep(AutoTestInputStep step)
        {
            if (step == null || string.IsNullOrWhiteSpace(step.type))
                throw new ArgumentException("每个输入 step 都必须指定 type。");

            switch (step.type.ToLowerInvariant())
            {
                case "mouse":
                    yield return ExecuteMouseStep(step);
                    break;
                case "keyboard":
                case "key":
                    yield return ExecuteKeyboardStep(step);
                    break;
                case "touch":
                    ExecuteTouchStep(step);
                    yield return WaitFrames(GetFrameCount(step));
                    break;
                case "wait":
                    yield return WaitFrames(GetFrameCount(step));
                    break;
                case "reset":
                    yield return ResetInput();
                    break;
                default:
                    throw new ArgumentException($"未知输入 step 类型：{step.type}");
            }
        }

        private static IEnumerator ExecuteMouseStep(AutoTestInputStep step)
        {
            string action = string.IsNullOrEmpty(step.action) ? "move" : step.action.ToLowerInvariant();
            Vector2 position = new Vector2(step.x, step.y);
            Vector2 delta = new Vector2(step.deltaX, step.deltaY);
            if (delta == Vector2.zero)
                delta = position - s_MousePosition;
            s_MousePosition = position;
            s_MouseState.position = position;
            s_MouseState.delta = delta;
            s_MouseState.scroll = new Vector2(step.scrollX, step.scrollY);

            if (action == "move" || action == "scroll")
            {
                QueueMouseState();
                yield return WaitFrames(GetFrameCount(step));
                yield break;
            }

            MouseButton button = ParseMouseButton(step.button);
            QueueMouseState();
            yield return WaitFrames(1);
            switch (action)
            {
                case "down":
                    s_HeldMouseButtons.Add(button);
                    s_MouseState = s_MouseState.WithButton(button, true);
                    QueueMouseState();
                    yield return WaitFrames(GetFrameCount(step));
                    break;
                case "up":
                    s_HeldMouseButtons.Remove(button);
                    s_MouseState = s_MouseState.WithButton(button, false);
                    QueueMouseState();
                    yield return WaitFrames(GetFrameCount(step));
                    break;
                case "click":
                    s_HeldMouseButtons.Add(button);
                    s_MouseState = s_MouseState.WithButton(button, true);
                    QueueMouseState();
                    yield return WaitFrames(1);
                    s_MouseState.delta = Vector2.zero;
                    s_MouseState.scroll = Vector2.zero;
                    s_HeldMouseButtons.Remove(button);
                    s_MouseState = s_MouseState.WithButton(button, false);
                    QueueMouseState();
                    yield return WaitFrames(GetFrameCount(step));
                    break;
                default:
                    throw new ArgumentException($"未知鼠标 action：{step.action}");
            }
        }

        private static IEnumerator ExecuteKeyboardStep(AutoTestInputStep step)
        {
            string action = string.IsNullOrEmpty(step.action) ? "press" : step.action.ToLowerInvariant();
            if (action == "text")
            {
                string text = step.text ?? string.Empty;
                for (int i = 0; i < text.Length; i++)
                {
                    InputSystem.QueueTextEvent(s_Keyboard, text[i]);
                    ProcessSelectedInputField(new UnityEngine.Event { type = EventType.KeyDown, character = text[i] });
                }
                yield return WaitFrames(GetFrameCount(step));
                yield break;
            }

            if (!Enum.TryParse(step.key, true, out Key key) || key == Key.None || !Enum.IsDefined(typeof(Key), key))
                throw new ArgumentException($"未知键盘按键：{step.key}");

            switch (action)
            {
                case "down":
                    s_HeldKeys.Add(key);
                    QueueKeyboardState();
                    ProcessSelectedInputField(CreateKeyEvent(key));
                    yield return WaitFrames(GetFrameCount(step));
                    break;
                case "up":
                    s_HeldKeys.Remove(key);
                    QueueKeyboardState();
                    yield return WaitFrames(GetFrameCount(step));
                    break;
                case "press":
                    s_HeldKeys.Add(key);
                    QueueKeyboardState();
                    ProcessSelectedInputField(CreateKeyEvent(key));
                    yield return WaitFrames(1);
                    s_HeldKeys.Remove(key);
                    QueueKeyboardState();
                    yield return WaitFrames(GetFrameCount(step));
                    break;
                default:
                    throw new ArgumentException($"未知键盘 action：{step.action}");
            }
        }

        private static void ExecuteTouchStep(AutoTestInputStep step)
        {
            if (step.touches != null && step.touches.Count > 0)
            {
                for (int i = 0; i < step.touches.Count; i++)
                    QueueTouch(step.touches[i]);
                return;
            }

            QueueTouch(step.touchId, ParseTouchPhase(step.action), new Vector2(step.x, step.y), new Vector2(step.deltaX, step.deltaY), step.pressure <= 0f ? 1f : step.pressure, Vector2.zero, step.tapCount);
        }

        private static void QueueTouch(AutoTestTouchPoint touch)
        {
            if (touch == null)
                throw new ArgumentException("touches 数组不能包含 null。");
            QueueTouch(touch.id, ParseTouchPhase(touch.phase), new Vector2(touch.x, touch.y), new Vector2(touch.deltaX, touch.deltaY), touch.pressure <= 0f ? 1f : touch.pressure, new Vector2(touch.radiusX, touch.radiusY), touch.tapCount);
        }

        private static void QueueTouch(int touchId, InputTouchPhase phase, Vector2 position, Vector2 delta, float pressure, Vector2 radius, int tapCount)
        {
            if (touchId <= 0)
                throw new ArgumentOutOfRangeException(nameof(touchId), "touch id 必须大于 0。");
            if (delta == Vector2.zero && s_TouchPositions.TryGetValue(touchId, out Vector2 previousPosition))
                delta = position - previousPosition;
            s_Touchscreen.MakeCurrent();
            InputSystem.QueueStateEvent(s_Touchscreen, new TouchState {
                touchId = touchId,
                phase = phase,
                position = position,
                delta = delta,
                pressure = pressure,
                radius = radius,
                tapCount = (byte)Mathf.Clamp(tapCount, 0, byte.MaxValue),
            });
            if (phase == InputTouchPhase.Ended || phase == InputTouchPhase.Canceled)
                s_TouchPositions.Remove(touchId);
            else
                s_TouchPositions[touchId] = position;
        }

        private static IEnumerator ResetInput()
        {
            ResetInputImmediately();
            yield return WaitFrames(DefaultSettleFrames);
        }

        private static void ResetInputImmediately()
        {
            EnsureDevices();
            s_HeldKeys.Clear();
            s_HeldMouseButtons.Clear();
            s_Keyboard.MakeCurrent();
            InputSystem.QueueStateEvent(s_Keyboard, new KeyboardState());
            s_MouseState = new MouseState { position = s_MousePosition };
            QueueMouseState();
            foreach (KeyValuePair<int, Vector2> touch in s_TouchPositions.ToArray())
                QueueTouch(touch.Key, InputTouchPhase.Canceled, touch.Value, Vector2.zero, 0f, Vector2.zero, 0);
            s_TouchPositions.Clear();
        }

        private static InputDeviceState CreateDeviceState(InputDevice device, InputDevice current)
        {
            if (device == null)
                return null;
            return new InputDeviceState {
                deviceId = device.deviceId,
                displayName = device.displayName,
                layout = device.layout,
                added = device.added,
                enabled = device.enabled,
                isCurrent = device == current,
            };
        }

        private static IEnumerator WaitFrames(int frameCount)
        {
            for (int i = 0; i < frameCount; i++)
                yield return null;
        }

        private static int GetFrameCount(AutoTestInputStep step)
        {
            return step.frames > 0 ? step.frames : DefaultSettleFrames;
        }

        private static void QueueMouseState()
        {
            s_Mouse.MakeCurrent();
            InputSystem.QueueStateEvent(s_Mouse, s_MouseState);
            s_MouseState.delta = Vector2.zero;
            s_MouseState.scroll = Vector2.zero;
        }

        private static void QueueKeyboardState()
        {
            s_Keyboard.MakeCurrent();
            InputSystem.QueueStateEvent(s_Keyboard, new KeyboardState(s_HeldKeys.ToArray()));
        }

        private static void EnsureDevices()
        {
            EnsureLayouts();
            s_Mouse = FindUGUIBoundDevice<Mouse>() ?? (s_Mouse != null && s_Mouse.added ? s_Mouse : InputSystem.AddDevice(AutomationMouseLayout) as Mouse);
            s_Keyboard = FindUGUIBoundDevice<Keyboard>() ?? (s_Keyboard != null && s_Keyboard.added ? s_Keyboard : InputSystem.AddDevice(AutomationKeyboardLayout) as Keyboard);
            s_Touchscreen = FindUGUIBoundDevice<Touchscreen>() ?? (s_Touchscreen != null && s_Touchscreen.added ? s_Touchscreen : InputSystem.AddDevice(AutomationTouchscreenLayout) as Touchscreen);
            if (s_Mouse == null || s_Keyboard == null || s_Touchscreen == null)
                throw new InvalidOperationException("无法创建 XFramework 自动化输入设备。");
            if (!s_Mouse.enabled)
                InputSystem.EnableDevice(s_Mouse);
            if (!s_Keyboard.enabled)
                InputSystem.EnableDevice(s_Keyboard);
            if (!s_Touchscreen.enabled)
                InputSystem.EnableDevice(s_Touchscreen);
            ConfigureUGUIInputActions();
        }

        private static T FindUGUIBoundDevice<T>() where T : InputDevice
        {
            foreach (InputSystemUIInputModule module in UnityEngine.Object.FindObjectsOfType<InputSystemUIInputModule>())
            {
                InputActionAsset asset = module.actionsAsset;
                if (asset == null)
                    continue;
                if (asset.devices.HasValue)
                {
                    T device = asset.devices.Value.OfType<T>().FirstOrDefault(IsProjectInputDevice);
                    if (device != null)
                        return device;
                }
                foreach (InputAction action in asset)
                {
                    T device = action.controls.Select(control => control.device).OfType<T>().FirstOrDefault(IsProjectInputDevice);
                    if (device != null)
                        return device;
                }
            }
            return null;
        }

        private static bool IsProjectInputDevice(InputDevice device)
        {
            return device != null && device.added && device.enabled && !string.Equals(device.layout, AutomationMouseLayout, StringComparison.Ordinal) && !string.Equals(device.layout, AutomationKeyboardLayout, StringComparison.Ordinal) && !string.Equals(device.layout, AutomationTouchscreenLayout, StringComparison.Ordinal);
        }

        private static void EnsureLayouts()
        {
            var layouts = new HashSet<string>(InputSystem.ListLayouts());
            if (!layouts.Contains(AutomationMouseLayout))
                InputSystem.RegisterLayout($"{{\"name\":\"{AutomationMouseLayout}\",\"extend\":\"Mouse\",\"canRunInBackground\":true}}");
            if (!layouts.Contains(AutomationKeyboardLayout))
                InputSystem.RegisterLayout($"{{\"name\":\"{AutomationKeyboardLayout}\",\"extend\":\"Keyboard\",\"canRunInBackground\":true}}");
            if (!layouts.Contains(AutomationTouchscreenLayout))
                InputSystem.RegisterLayout($"{{\"name\":\"{AutomationTouchscreenLayout}\",\"extend\":\"Touchscreen\",\"canRunInBackground\":true}}");
        }

        private static void ConfigureUGUIInputActions()
        {
            var automationDevices = new InputDevice[] { s_Mouse, s_Keyboard, s_Touchscreen };
            foreach (InputSystemUIInputModule module in UnityEngine.Object.FindObjectsOfType<InputSystemUIInputModule>())
            {
                InputActionAsset asset = module.actionsAsset;
                if (asset == null)
                    continue;
                if (asset.devices.HasValue)
                {
                    var devices = asset.devices.Value.ToList();
                    for (int i = 0; i < automationDevices.Length; i++)
                    {
                        if (!devices.Contains(automationDevices[i]))
                            devices.Add(automationDevices[i]);
                    }
                    asset.devices = devices.ToArray();
                }
                if (!asset.bindingMask.HasValue)
                    continue;
                var groups = new HashSet<string>((asset.bindingMask.Value.groups ?? string.Empty).Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries));
                foreach (InputAction action in asset)
                {
                    foreach (InputBinding binding in action.bindings)
                    {
                        foreach (string group in (binding.groups ?? string.Empty).Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries))
                            groups.Add(group);
                    }
                }
                asset.bindingMask = InputBinding.MaskByGroups(groups.ToArray());
            }
        }

        private static UnityEngine.Event CreateKeyEvent(Key key)
        {
            return new UnityEngine.Event { type = EventType.KeyDown, keyCode = ToLegacyKeyCode(key), modifiers = GetEventModifiers() };
        }

        private static void ProcessSelectedInputField(UnityEngine.Event inputEvent)
        {
            GameObject selected = EventSystem.current?.currentSelectedGameObject;
            if (selected == null)
                return;
            GameObject handler = ExecuteEvents.GetEventHandler<IUpdateSelectedHandler>(selected) ?? selected;
            InputField inputField = handler.GetComponent<InputField>();
            if (inputField != null && inputField.isFocused && inputField.IsInteractable())
            {
                inputField.ProcessEvent(inputEvent);
                inputField.ForceLabelUpdate();
                return;
            }
            TMP_InputField tmpInputField = handler.GetComponent<TMP_InputField>();
            if (tmpInputField != null && tmpInputField.isFocused && tmpInputField.IsInteractable())
            {
                tmpInputField.ProcessEvent(inputEvent);
                tmpInputField.ForceLabelUpdate();
            }
        }

        private static EventModifiers GetEventModifiers()
        {
            EventModifiers modifiers = EventModifiers.None;
            if (s_HeldKeys.Contains(Key.LeftCtrl) || s_HeldKeys.Contains(Key.RightCtrl))
                modifiers |= EventModifiers.Control;
            if (s_HeldKeys.Contains(Key.LeftShift) || s_HeldKeys.Contains(Key.RightShift))
                modifiers |= EventModifiers.Shift;
            if (s_HeldKeys.Contains(Key.LeftAlt) || s_HeldKeys.Contains(Key.RightAlt))
                modifiers |= EventModifiers.Alt;
            if (s_HeldKeys.Contains(Key.LeftMeta) || s_HeldKeys.Contains(Key.RightMeta))
                modifiers |= EventModifiers.Command;
            return modifiers;
        }

        private static KeyCode ToLegacyKeyCode(Key key)
        {
            if (key >= Key.Digit1 && key <= Key.Digit0)
            {
                int digit = key == Key.Digit0 ? 0 : (int)key - (int)Key.Digit1 + 1;
                return (KeyCode)((int)KeyCode.Alpha0 + digit);
            }
            return Enum.TryParse(key.ToString(), true, out KeyCode keyCode) ? keyCode : KeyCode.None;
        }

        private static MouseButton ParseMouseButton(string button)
        {
            if (string.IsNullOrEmpty(button))
                return MouseButton.Left;
            if (Enum.TryParse(button, true, out MouseButton result) && Enum.IsDefined(typeof(MouseButton), result))
                return result;
            throw new ArgumentException($"未知鼠标按键：{button}");
        }

        private static InputTouchPhase ParseTouchPhase(string phase)
        {
            if (string.IsNullOrEmpty(phase))
                return InputTouchPhase.Moved;
            if (Enum.TryParse(phase, true, out InputTouchPhase result) && Enum.IsDefined(typeof(InputTouchPhase), result))
                return result;
            throw new ArgumentException($"未知触控 phase：{phase}");
        }
    }
}
