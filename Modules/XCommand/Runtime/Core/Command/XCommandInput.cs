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
using Object = UnityEngine.Object;
using UnityEvent = UnityEngine.Event;

namespace XFramework.Command
{
    [Serializable]
    public sealed class XCommandInputSequence
    {
        public List<XCommandInputStep> steps = new List<XCommandInputStep>();
        public bool resetAfter;
    }

    [Serializable]
    public sealed class XCommandInputStep
    {
        public string type;
        public string action;
        public string button;
        public string key;
        public string text;
        public float x;
        public float y;
        public float deltaX;
        public float deltaY;
        public float scrollX;
        public float scrollY;
        public int touchId;
        public float pressure;
        public int tapCount;
        public int frames;
        public List<XCommandTouchPoint> touches;
    }

    [Serializable]
    public sealed class XCommandTouchPoint
    {
        public int id;
        public string phase;
        public float x;
        public float y;
        public float deltaX;
        public float deltaY;
        public float pressure;
        public float radiusX;
        public float radiusY;
        public int tapCount;
    }

    /// <summary>XCommand 自动化输入状态与注入的唯一实例。</summary>
    internal sealed class XCommandInput
    {
        private const int DefaultSettleFrames = 3;
        private const string AutomationMouseLayout = "XFrameworkAutomationMouse";
        private const string AutomationKeyboardLayout = "XFrameworkAutomationKeyboard";
        private const string AutomationTouchscreenLayout = "XFrameworkAutomationTouchscreen";
        private readonly HashSet<Key> heldKeys = new HashSet<Key>();
        private readonly HashSet<MouseButton> heldMouseButtons = new HashSet<MouseButton>();
        private readonly Dictionary<int, Vector2> touchPositions = new Dictionary<int, Vector2>();
        private Mouse mouse;
        private Keyboard keyboard;
        private Touchscreen touchscreen;
        private MouseState mouseState;
        private Vector2 mousePosition;

        internal XCommandInput()
        {
        }

        internal void Start()
        {
            XCommand.Hub.CommandRuntimeResetRequested -= ResetState;
            XCommand.Hub.CommandRuntimeResetRequested += ResetState;
        }

        internal void Stop()
        {
            XCommand.Hub.CommandRuntimeResetRequested -= ResetState;
            ResetState();
        }

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

        internal void ResetState()
        {
            heldKeys.Clear();
            heldMouseButtons.Clear();
            touchPositions.Clear();
            mouse = null;
            keyboard = null;
            touchscreen = null;
            mouseState = new MouseState();
            mousePosition = Vector2.zero;
        }

        internal void ResetInterruptedInput()
        {
            ResetInputImmediately();
        }

        internal string GetState(bool compact)
        {
            var result = new InputStateResult {
                timestampUtc = DateTime.UtcNow.ToString("O"),
                frameCount = Time.frameCount,
                initialized = mouse != null || keyboard != null || touchscreen != null,
                heldKeys = heldKeys.Select(key => key.ToString()).OrderBy(key => key, StringComparer.Ordinal).ToList(),
                mouse = new MouseInputState {
                    position = mousePosition,
                    heldButtons = heldMouseButtons.Select(button => button.ToString()).OrderBy(button => button, StringComparer.Ordinal).ToList(),
                },
                touches = touchPositions.OrderBy(pair => pair.Key).Select(pair => new TouchInputState { touchId = pair.Key, position = pair.Value }).ToList(),
                keyboardDevice = CreateDeviceState(keyboard, Keyboard.current),
                mouseDevice = CreateDeviceState(mouse, Mouse.current),
                touchscreenDevice = CreateDeviceState(touchscreen, Touchscreen.current),
            };
            return JsonUtility.ToJson(result, !compact);
        }

        internal IEnumerator Run(XCommandInputSequence sequence, Func<bool> isCancellationRequested, Action<string> setOutput)
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
                    if (isCancellationRequested())
                        yield break;
                    yield return ExecuteStep(sequence.steps[i]);
                }

                if (sequence.resetAfter)
                {
                    yield return ResetInput();
                    resetCompleted = true;
                }
                setOutput("OK");
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


        private IEnumerator ExecuteStep(XCommandInputStep step)
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

        private IEnumerator ExecuteMouseStep(XCommandInputStep step)
        {
            string action = string.IsNullOrEmpty(step.action) ? "move" : step.action.ToLowerInvariant();
            Vector2 position = new Vector2(step.x, step.y);
            Vector2 delta = new Vector2(step.deltaX, step.deltaY);
            if (delta == Vector2.zero)
                delta = position - mousePosition;
            mousePosition = position;
            mouseState.position = position;
            mouseState.delta = delta;
            mouseState.scroll = new Vector2(step.scrollX, step.scrollY);

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
                    heldMouseButtons.Add(button);
                    mouseState = mouseState.WithButton(button, true);
                    QueueMouseState();
                    yield return WaitFrames(GetFrameCount(step));
                    break;
                case "up":
                    heldMouseButtons.Remove(button);
                    mouseState = mouseState.WithButton(button, false);
                    QueueMouseState();
                    yield return WaitFrames(GetFrameCount(step));
                    break;
                case "click":
                    heldMouseButtons.Add(button);
                    mouseState = mouseState.WithButton(button, true);
                    QueueMouseState();
                    yield return WaitFrames(1);
                    mouseState.delta = Vector2.zero;
                    mouseState.scroll = Vector2.zero;
                    heldMouseButtons.Remove(button);
                    mouseState = mouseState.WithButton(button, false);
                    QueueMouseState();
                    yield return WaitFrames(GetFrameCount(step));
                    break;
                default:
                    throw new ArgumentException($"未知鼠标 action：{step.action}");
            }
        }

        private IEnumerator ExecuteKeyboardStep(XCommandInputStep step)
        {
            string action = string.IsNullOrEmpty(step.action) ? "press" : step.action.ToLowerInvariant();
            if (action == "text")
            {
                string text = step.text ?? string.Empty;
                for (int i = 0; i < text.Length; i++)
                {
                    InputSystem.QueueTextEvent(keyboard, text[i]);
                    ProcessSelectedInputField(new UnityEvent { type = EventType.KeyDown, character = text[i] });
                }
                yield return WaitFrames(GetFrameCount(step));
                yield break;
            }

            if (!Enum.TryParse(step.key, true, out Key key) || key == Key.None || !Enum.IsDefined(typeof(Key), key))
                throw new ArgumentException($"未知键盘按键：{step.key}");

            switch (action)
            {
                case "down":
                    heldKeys.Add(key);
                    QueueKeyboardState();
                    ProcessSelectedInputField(CreateKeyEvent(key));
                    yield return WaitFrames(GetFrameCount(step));
                    break;
                case "up":
                    heldKeys.Remove(key);
                    QueueKeyboardState();
                    yield return WaitFrames(GetFrameCount(step));
                    break;
                case "press":
                    heldKeys.Add(key);
                    QueueKeyboardState();
                    ProcessSelectedInputField(CreateKeyEvent(key));
                    yield return WaitFrames(1);
                    heldKeys.Remove(key);
                    QueueKeyboardState();
                    yield return WaitFrames(GetFrameCount(step));
                    break;
                default:
                    throw new ArgumentException($"未知键盘 action：{step.action}");
            }
        }

        private void ExecuteTouchStep(XCommandInputStep step)
        {
            if (step.touches != null && step.touches.Count > 0)
            {
                for (int i = 0; i < step.touches.Count; i++)
                    QueueTouch(step.touches[i]);
                return;
            }

            QueueTouch(step.touchId, ParseTouchPhase(step.action), new Vector2(step.x, step.y), new Vector2(step.deltaX, step.deltaY), step.pressure <= 0f ? 1f : step.pressure, Vector2.zero, step.tapCount);
        }

        private void QueueTouch(XCommandTouchPoint touch)
        {
            if (touch == null)
                throw new ArgumentException("touches 数组不能包含 null。");
            QueueTouch(touch.id, ParseTouchPhase(touch.phase), new Vector2(touch.x, touch.y), new Vector2(touch.deltaX, touch.deltaY), touch.pressure <= 0f ? 1f : touch.pressure, new Vector2(touch.radiusX, touch.radiusY), touch.tapCount);
        }

        private void QueueTouch(int touchId, InputTouchPhase phase, Vector2 position, Vector2 delta, float pressure, Vector2 radius, int tapCount)
        {
            if (touchId <= 0)
                throw new ArgumentOutOfRangeException(nameof(touchId), "touch id 必须大于 0。");
            if (delta == Vector2.zero && touchPositions.TryGetValue(touchId, out Vector2 previousPosition))
                delta = position - previousPosition;
            touchscreen.MakeCurrent();
            InputSystem.QueueStateEvent(touchscreen, new TouchState {
                touchId = touchId,
                phase = phase,
                position = position,
                delta = delta,
                pressure = pressure,
                radius = radius,
                tapCount = (byte)Mathf.Clamp(tapCount, 0, byte.MaxValue),
            });
            if (phase == InputTouchPhase.Ended || phase == InputTouchPhase.Canceled)
                touchPositions.Remove(touchId);
            else
                touchPositions[touchId] = position;
        }

        private IEnumerator ResetInput()
        {
            ResetInputImmediately();
            yield return WaitFrames(DefaultSettleFrames);
        }

        private void ResetInputImmediately()
        {
            EnsureDevices();
            heldKeys.Clear();
            heldMouseButtons.Clear();
            keyboard.MakeCurrent();
            InputSystem.QueueStateEvent(keyboard, new KeyboardState());
            mouseState = new MouseState { position = mousePosition };
            QueueMouseState();
            foreach (KeyValuePair<int, Vector2> touch in touchPositions.ToArray())
                QueueTouch(touch.Key, InputTouchPhase.Canceled, touch.Value, Vector2.zero, 0f, Vector2.zero, 0);
            touchPositions.Clear();
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

        private static int GetFrameCount(XCommandInputStep step)
        {
            return step.frames > 0 ? step.frames : DefaultSettleFrames;
        }

        private void QueueMouseState()
        {
            mouse.MakeCurrent();
            InputSystem.QueueStateEvent(mouse, mouseState);
            mouseState.delta = Vector2.zero;
            mouseState.scroll = Vector2.zero;
        }

        private void QueueKeyboardState()
        {
            keyboard.MakeCurrent();
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(heldKeys.ToArray()));
        }

        private void EnsureDevices()
        {
            EnsureLayouts();
            mouse = FindUGUIBoundDevice<Mouse>() ?? (mouse != null && mouse.added ? mouse : InputSystem.AddDevice(AutomationMouseLayout) as Mouse);
            keyboard = FindUGUIBoundDevice<Keyboard>() ?? (keyboard != null && keyboard.added ? keyboard : InputSystem.AddDevice(AutomationKeyboardLayout) as Keyboard);
            touchscreen = FindUGUIBoundDevice<Touchscreen>() ?? (touchscreen != null && touchscreen.added ? touchscreen : InputSystem.AddDevice(AutomationTouchscreenLayout) as Touchscreen);
            if (mouse == null || keyboard == null || touchscreen == null)
                throw new InvalidOperationException("无法创建 XFramework 自动化输入设备。");
            if (!mouse.enabled)
                InputSystem.EnableDevice(mouse);
            if (!keyboard.enabled)
                InputSystem.EnableDevice(keyboard);
            if (!touchscreen.enabled)
                InputSystem.EnableDevice(touchscreen);
            ConfigureUGUIInputActions();
        }

        private static T FindUGUIBoundDevice<T>() where T : InputDevice
        {
            foreach (InputSystemUIInputModule module in Object.FindObjectsOfType<InputSystemUIInputModule>())
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

        private void ConfigureUGUIInputActions()
        {
            var automationDevices = new InputDevice[] { mouse, keyboard, touchscreen };
            foreach (InputSystemUIInputModule module in Object.FindObjectsOfType<InputSystemUIInputModule>())
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

        private UnityEvent CreateKeyEvent(Key key)
        {
            return new UnityEvent { type = EventType.KeyDown, keyCode = ToLegacyKeyCode(key), modifiers = GetEventModifiers() };
        }

        private void ProcessSelectedInputField(UnityEvent inputEvent)
        {
            UnityEngine.GameObject selected = EventSystem.current?.currentSelectedGameObject;
            if (selected == null)
                return;
            UnityEngine.GameObject handler = ExecuteEvents.GetEventHandler<IUpdateSelectedHandler>(selected) ?? selected;
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

        private EventModifiers GetEventModifiers()
        {
            EventModifiers modifiers = EventModifiers.None;
            if (heldKeys.Contains(Key.LeftCtrl) || heldKeys.Contains(Key.RightCtrl))
                modifiers |= EventModifiers.Control;
            if (heldKeys.Contains(Key.LeftShift) || heldKeys.Contains(Key.RightShift))
                modifiers |= EventModifiers.Shift;
            if (heldKeys.Contains(Key.LeftAlt) || heldKeys.Contains(Key.RightAlt))
                modifiers |= EventModifiers.Alt;
            if (heldKeys.Contains(Key.LeftMeta) || heldKeys.Contains(Key.RightMeta))
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
