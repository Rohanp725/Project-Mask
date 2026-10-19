using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Semblance.Core
{
    /// <summary>
    /// Every control in the game, as Input System actions with keyboard/mouse and gamepad bindings.
    /// Keyboard and mouse bindings can be rebound in Settings > Controls; overrides are saved in PlayerPrefs.
    /// Nothing else in the game reads Keyboard.current or Mouse.current directly.
    /// </summary>
    public static class GameInput
    {
        private const string PrefsKey = "semblance.bindings";
        private static InputActionMap map;

        public static InputAction Move, LookMouse, LookStick, Interact, Sprint, Crouch, Flashlight, Notebook, Camera, Shoot, Focus,
            Cancel, Advance, Brake, PrevChoice, NextChoice;

        /// <summary>The actions a player can rebind, with their names as shown in Settings.</summary>
        public static (string label, InputAction action)[] Rebindable => new[]
        {
            ("Interact", Interact), ("Sprint", Sprint), ("Crouch", Crouch), ("Flashlight", Flashlight), ("Notebook", Notebook),
            ("Raise camera", Camera), ("Take photo", Shoot), ("Look closer", Focus), ("Brake (car)", Brake),
        };

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            map?.Disable();
            map = null;
        }

        public static void Ensure()
        {
            if (map != null) return;
            map = new InputActionMap("Semblance");

            Move = map.AddAction("Move", InputActionType.Value);
            Move.AddCompositeBinding("2DVector").With("Up", "<Keyboard>/w").With("Down", "<Keyboard>/s").With("Left", "<Keyboard>/a").With("Right", "<Keyboard>/d");
            Move.AddCompositeBinding("2DVector").With("Up", "<Keyboard>/upArrow").With("Down", "<Keyboard>/downArrow").With("Left", "<Keyboard>/leftArrow").With("Right", "<Keyboard>/rightArrow");
            Move.AddBinding("<Gamepad>/leftStick");

            LookMouse = map.AddAction("LookMouse", InputActionType.Value, "<Mouse>/delta");
            LookStick = map.AddAction("LookStick", InputActionType.Value, "<Gamepad>/rightStick");

            Interact = Button("Interact", "<Keyboard>/e", "<Gamepad>/buttonSouth");
            Sprint = Button("Sprint", "<Keyboard>/leftShift", "<Gamepad>/leftStickPress");
            Crouch = Button("Crouch", "<Keyboard>/leftCtrl", "<Gamepad>/rightStickPress");
            Crouch.AddBinding("<Keyboard>/c");
            Flashlight = Button("Flashlight", "<Keyboard>/f", "<Gamepad>/dpad/up");
            Notebook = Button("Notebook", "<Keyboard>/tab", "<Gamepad>/select");
            Camera = Button("Camera", "<Keyboard>/q", "<Gamepad>/leftShoulder");
            Shoot = Button("Shoot", "<Mouse>/leftButton", "<Gamepad>/rightTrigger");
            Focus = Button("Focus", "<Mouse>/rightButton", "<Gamepad>/leftTrigger");
            Cancel = Button("Cancel", "<Keyboard>/escape", "<Gamepad>/start");
            Cancel.AddBinding("<Gamepad>/buttonEast");
            Advance = Button("Advance", "<Keyboard>/space", "<Gamepad>/buttonSouth");
            Advance.AddBinding("<Keyboard>/enter");
            Advance.AddBinding("<Mouse>/leftButton");
            Brake = Button("Brake", "<Keyboard>/space", "<Gamepad>/buttonWest");
            PrevChoice = Button("PrevChoice", "<Keyboard>/upArrow", "<Gamepad>/dpad/up");
            NextChoice = Button("NextChoice", "<Keyboard>/downArrow", "<Gamepad>/dpad/down");

            string saved = PlayerPrefs.GetString(PrefsKey, "");
            if (!string.IsNullOrEmpty(saved))
            {
                try { map.LoadBindingOverridesFromJson(saved); }
                catch (Exception e) { Debug.LogWarning("[Semblance] Ignoring saved bindings: " + e.Message); }
            }
            map.Enable();
        }

        private static InputAction Button(string name, string key, string pad)
        {
            var a = map.AddAction(name, InputActionType.Button);
            a.AddBinding(key);
            a.AddBinding(pad);
            return a;
        }

        // ------------------------------------------------------------ Reading

        public static Vector2 MoveValue { get { Ensure(); return Move.ReadValue<Vector2>(); } }

        private static bool usingGamepad;

        /// <summary>True when the last thing the player touched was a gamepad (prompts and markers follow it).</summary>
        public static bool UsingGamepad
        {
            get
            {
                var pad = Gamepad.current;
                if (pad != null && (pad.wasUpdatedThisFrame && (pad.leftStick.ReadValue().sqrMagnitude > 0.1f || pad.rightStick.ReadValue().sqrMagnitude > 0.1f
                    || pad.buttonSouth.isPressed || pad.buttonEast.isPressed || pad.dpad.ReadValue().sqrMagnitude > 0.1f || pad.startButton.isPressed)))
                    usingGamepad = true;
                var k = Keyboard.current;
                var m = Mouse.current;
                if ((k != null && k.anyKey.isPressed) || (m != null && (m.delta.ReadValue().sqrMagnitude > 4f || m.leftButton.isPressed)))
                    usingGamepad = false;
                return usingGamepad;
            }
        }

        /// <summary>The button for an action on whatever the player is using right now ("E" or "A").</summary>
        public static string Glyph(InputAction action)
        {
            Ensure();
            if (action == null) return "?";
            bool pad = UsingGamepad;
            for (int i = 0; i < action.bindings.Count; i++)
            {
                var path = action.bindings[i].effectivePath ?? "";
                if (action.bindings[i].isComposite || action.bindings[i].isPartOfComposite) continue;
                if (pad == path.StartsWith("<Gamepad>"))
                    return action.GetBindingDisplayString(i);
            }
            return Display(action);
        }

        /// <summary>Replaces {interact}, {flashlight}... in hint text with the current device's buttons.</summary>
        public static string Format(string text)
        {
            if (string.IsNullOrEmpty(text) || text.IndexOf('{') < 0) return text;
            Ensure();
            bool pad = UsingGamepad;
            return text
                .Replace("{move}", pad ? "Left stick" : "WASD")
                .Replace("{look}", pad ? "Right stick" : "the mouse")
                .Replace("{interact}", Glyph(Interact)).Replace("{sprint}", Glyph(Sprint)).Replace("{crouch}", Glyph(Crouch))
                .Replace("{flashlight}", Glyph(Flashlight)).Replace("{notebook}", Glyph(Notebook)).Replace("{camera}", Glyph(Camera))
                .Replace("{shoot}", Glyph(Shoot)).Replace("{focus}", Glyph(Focus)).Replace("{cancel}", Glyph(Cancel))
                .Replace("{brake}", Glyph(Brake)).Replace("{advance}", Glyph(Advance));
        }

        /// <summary>Look input in mouse-delta units (gamepad stick scaled to feel the same at default sensitivity).</summary>
        public static Vector2 LookDelta
        {
            get
            {
                Ensure();
                Vector2 stick = LookStick.ReadValue<Vector2>();
                return LookMouse.ReadValue<Vector2>() + stick * (1400f * Time.unscaledDeltaTime);
            }
        }

        /// <summary>True while the player is typing into a text field: game hotkeys are ignored.</summary>
        public static bool TextEntry { get; set; }

        public static bool Pressed(InputAction a) { Ensure(); return !TextEntry && a != null && a.WasPressedThisFrame(); }
        public static bool Held(InputAction a) { Ensure(); return !TextEntry && a != null && a.IsPressed(); }

        /// <summary>Number keys pick dialogue choices directly.</summary>
        public static int PressedDigit()
        {
            var k = Keyboard.current;
            if (k == null) return -1;
            for (int i = 0; i < 9; i++)
                if (k[(Key)((int)Key.Digit1 + i)].wasPressedThisFrame) return i;
            return -1;
        }

        /// <summary>A mouse click specifically (re-capturing the cursor), separate from the Shoot action.</summary>
        public static bool MouseClicked => Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame;

        // ------------------------------------------------------------ Rebinding

        /// <summary>The keyboard/mouse binding shown in Settings (e.g. "E", "Left Shift").</summary>
        public static string Display(InputAction action)
        {
            Ensure();
            int i = KeyboardBindingIndex(action);
            return i < 0 ? "-" : action.GetBindingDisplayString(i);
        }

        private static int KeyboardBindingIndex(InputAction action)
        {
            for (int i = 0; i < action.bindings.Count; i++)
            {
                var path = action.bindings[i].effectivePath ?? "";
                if (path.StartsWith("<Keyboard>") || path.StartsWith("<Mouse>")) return i;
            }
            return -1;
        }

        /// <summary>Waits for the next key or mouse button and binds it to the action.</summary>
        public static void Rebind(InputAction action, Action done)
        {
            Ensure();
            int index = KeyboardBindingIndex(action);
            if (index < 0) { done?.Invoke(); return; }
            action.Disable();
            action.PerformInteractiveRebinding(index)
                .WithControlsExcluding("<Gamepad>")
                .WithControlsExcluding("<Mouse>/delta")
                .WithControlsExcluding("<Mouse>/position")
                .WithCancelingThrough("<Keyboard>/escape")
                .OnComplete(op => { op.Dispose(); action.Enable(); Save(); done?.Invoke(); })
                .OnCancel(op => { op.Dispose(); action.Enable(); done?.Invoke(); })
                .Start();
        }

        public static void ResetBindings()
        {
            Ensure();
            map.RemoveAllBindingOverrides();
            Save();
        }

        private static void Save()
        {
            PlayerPrefs.SetString(PrefsKey, map.SaveBindingOverridesAsJson());
            PlayerPrefs.Save();
        }
    }
}
