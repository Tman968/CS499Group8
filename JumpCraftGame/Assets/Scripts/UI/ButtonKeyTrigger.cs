using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using System.Collections.Generic;

[RequireComponent(typeof(Button))]
public class ButtonKeyTrigger : MonoBehaviour
{
    [System.Serializable]
    public enum InputType { KeyboardKey, ControllerButton }

    [System.Serializable]
    public class KeybindConfig
    {
        [Tooltip("Which player index this keybind belongs to (0 for Player 1, 1 for Player 2).")]
        public int playerIndex = 0;

        [Tooltip("Choose whether this specific keybind uses a Keyboard Key or a Controller Button.")]
        public InputType inputType = InputType.KeyboardKey;

        [Tooltip("The keyboard key that triggers this.")]
        public Key keyboardKey = Key.Enter;

        [Tooltip("The controller button that triggers this.")]
        public ControllerButton gamepadButton = ControllerButton.CrossOrA;
    }

    [Header("Keybinds List")]
    public List<KeybindConfig> keybinds = new List<KeybindConfig>();

    public enum ControllerButton
    {
        CrossOrA, CircleOrB, SquareOrX, TriangleOrY,
        Start, Select, Options,
        LeftBumper, RightBumper, LeftTrigger, RightTrigger,
        DPadUp, DPadDown, DPadLeft, DPadRight,
        LeftStickUp, LeftStickDown, LeftStickLeft, LeftStickRight
    }

    private Button button;

    void Start()
    {
        button = GetComponent<Button>();

        if (keybinds.Count == 0)
        {
            keybinds.Add(new KeybindConfig { playerIndex = 0, inputType = InputType.KeyboardKey, keyboardKey = Key.Enter });
        }
    }

    void Update()
    {
        bool triggered = false;
        int totalControllers = Gamepad.all.Count;

        foreach (var bind in keybinds)
        {
            if (bind.inputType == InputType.KeyboardKey)
            {
                // Keyboard check
                if (Keyboard.current != null && Keyboard.current[bind.keyboardKey].wasPressedThisFrame)
                {
                    triggered = true;
                    break;
                }
            }
            else if (bind.inputType == InputType.ControllerButton)
            {
                Gamepad pad = null;

                // SMART RESOLUTION:
                // If Player 1 (index 0) is configured for a controller, but there is only 1 controller total 
                // in a Keyboard+Controller setup, that controller actually belongs to Player 2 (index 0 of connected pads).
                // We map indices dynamically based on how many controllers are plugged in.
                int resolvedGamepadIndex = bind.playerIndex;

                if (totalControllers == 1 && bind.playerIndex == 0)
                {
                    // In 1-controller mode, Player 1 is on keyboard. If this bind looks for Player 1's gamepad, 
                    // check if they intended the solo gamepad (which is at index 0).
                    resolvedGamepadIndex = 0;
                }

                if (totalControllers > resolvedGamepadIndex)
                {
                    pad = Gamepad.all[resolvedGamepadIndex];
                }
                else if (Gamepad.current != null && totalControllers > 0)
                {
                    pad = Gamepad.current;
                }

                if (pad != null)
                {
                    bool padPressed = false;

                    switch (bind.gamepadButton)
                    {
                        case ControllerButton.CrossOrA:
                            if (pad.buttonSouth.wasPressedThisFrame) padPressed = true;
                            break;
                        case ControllerButton.CircleOrB:
                            if (pad.buttonEast.wasPressedThisFrame) padPressed = true;
                            break;
                        case ControllerButton.SquareOrX:
                            if (pad.buttonWest.wasPressedThisFrame) padPressed = true;
                            break;
                        case ControllerButton.TriangleOrY:
                            if (pad.buttonNorth.wasPressedThisFrame) padPressed = true;
                            break;
                        case ControllerButton.Start:
                        case ControllerButton.Options:
                            if (pad.startButton.wasPressedThisFrame) padPressed = true;
                            break;
                        case ControllerButton.Select:
                            if (pad.selectButton.wasPressedThisFrame) padPressed = true;
                            break;
                        case ControllerButton.LeftBumper:
                            if (pad.leftShoulder.wasPressedThisFrame) padPressed = true;
                            break;
                        case ControllerButton.RightBumper:
                            if (pad.rightShoulder.wasPressedThisFrame) padPressed = true;
                            break;
                        case ControllerButton.LeftTrigger:
                            if (pad.leftTrigger.wasPressedThisFrame) padPressed = true;
                            break;
                        case ControllerButton.RightTrigger:
                            if (pad.rightTrigger.wasPressedThisFrame) padPressed = true;
                            break;
                        case ControllerButton.DPadUp:
                            if (pad.dpad.up.wasPressedThisFrame) padPressed = true;
                            break;
                        case ControllerButton.DPadDown:
                            if (pad.dpad.down.wasPressedThisFrame) padPressed = true;
                            break;
                        case ControllerButton.DPadLeft:
                            if (pad.dpad.left.wasPressedThisFrame) padPressed = true;
                            break;
                        case ControllerButton.DPadRight:
                            if (pad.dpad.right.wasPressedThisFrame) padPressed = true;
                            break;
                        case ControllerButton.LeftStickUp:
                            if (pad.leftStick.up.wasPressedThisFrame) padPressed = true;
                            break;
                        case ControllerButton.LeftStickDown:
                            if (pad.leftStick.down.wasPressedThisFrame) padPressed = true;
                            break;
                        case ControllerButton.LeftStickLeft:
                            if (pad.leftStick.left.wasPressedThisFrame) padPressed = true;
                            break;
                        case ControllerButton.LeftStickRight:
                            if (pad.leftStick.right.wasPressedThisFrame) padPressed = true;
                            break;
                    }

                    if (padPressed)
                    {
                        triggered = true;
                        break;
                    }
                }
            }
        }

        if (triggered && button != null && button.IsInteractable())
        {
            bool usesTriangle = false;
            foreach (var bind in keybinds)
            {
                if (bind.inputType == InputType.ControllerButton && bind.gamepadButton == ControllerButton.TriangleOrY)
                {
                    usesTriangle = true;
                    break;
                }
            }

            if (usesTriangle)
            {
                ExecuteEvents.Execute(button.gameObject, new PointerEventData(EventSystem.current), ExecuteEvents.pointerClickHandler);
            }
            else
            {
                button.onClick.Invoke();
            }

            button.Select();
        }
    }
}