using UnityEngine.InputSystem;
using System.Collections.Generic;
using UnityEngine;

public static class KeyBinds
{
    // ----- gameplay -----
    // WASD is set up in Init() as one 2D composite, read with Move.ReadValue<Vector2>()
    public static InputAction Move = new InputAction("Move", InputActionType.Value, expectedControlType: "Vector2");
    public static InputAction Aim = new InputAction("Aim", InputActionType.Value, "<Mouse>/position");

    // one key: pass it straight to the constructor
    public static InputAction Primary = new InputAction("Primary", binding: "<Mouse>/leftButton");
    public static InputAction Secondary = new InputAction("Secondary", binding: "<Mouse>/rightButton");
    public static InputAction Ability = new InputAction("Ability", binding: "<Keyboard>/space");

    public static InputAction NextWeapon = new InputAction("NextWeapon", binding: "<Keyboard>/rightArrow");
    public static InputAction PrevWeapon = new InputAction("PrevWeapon", binding: "<Keyboard>/leftArrow");
    public static InputAction NextSkill = new InputAction("NextSkill", binding: "<Keyboard>/upArrow");
    public static InputAction PrevSkill = new InputAction("PrevSkill", binding: "<Keyboard>/downArrow");

    // several keys: add the extras in Init()
    public static InputAction Pause = new InputAction("Pause", binding: "<Keyboard>/p");

    // ----- menus (title, win, game over) -----
    public static InputAction Confirm = new InputAction("Confirm", binding: "<Keyboard>/enter");
    public static InputAction PickChar1 = new InputAction("PickChar1", binding: "<Keyboard>/1");
    public static InputAction PickChar2 = new InputAction("PickChar2", binding: "<Keyboard>/2");
    public static InputAction CycleChar = new InputAction("CycleChar", binding: "<Keyboard>/leftArrow");
    public static InputAction Retry = new InputAction("Retry", binding: "<Keyboard>/r");
    public static InputAction ToTitle = new InputAction("ToTitle", binding: "<Keyboard>/t");

    // declared AFTER the actions above, or it fills with nulls
    public static InputAction[] binds =
    {
        Move, Aim, Primary, Secondary, Ability,
        NextWeapon, PrevWeapon, NextSkill, PrevSkill, Pause,
        Confirm, PickChar1, PickChar2, CycleChar, Retry, ToTitle
    };

    static bool initialized;

    public static void Init()
    {
        if (initialized) return;          // a second call would add every extra binding twice
        initialized = true;

        Move.AddCompositeBinding("2DVector")
            .With("Up", "<Keyboard>/w")
            .With("Down", "<Keyboard>/s")
            .With("Left", "<Keyboard>/a")
            .With("Right", "<Keyboard>/d");

        Pause.AddBinding("<Keyboard>/escape");
        Confirm.AddBinding("<Keyboard>/space");
        CycleChar.AddBinding("<Keyboard>/rightArrow");

        Load();                           // apply the player's saved rebinds on top of the defaults
        BuildRows();

        foreach (InputAction action in binds) action.Enable();
    }

    // ===================================================================== rebinding
    // One row in the keybinds menu = one binding of one action.
    // Move gets 4 rows (Up/Down/Left/Right); Pause gets 2 (P and Esc).
    public class Row
    {
        public InputAction action;
        public int index;                 // which binding of the action
        public string label;
    }

    public static readonly List<Row> Rows = new List<Row>();

    static void BuildRows()
    {
        Rows.Clear();
        foreach (InputAction action in binds)
        {
            if (action == Aim) continue;       // mouse position, nothing to rebind

            int extra = 0;
            for (int i = 0; i < action.bindings.Count; i++)
            {
                InputBinding b = action.bindings[i];
                if (b.isComposite) continue;          // the "2DVector" header itself isn't a key

                Row r = new Row();
                r.action = action;
                r.index = i;
                if (b.isPartOfComposite) r.label = action.name + " " + b.name;   // "Move Up"
                else r.label = extra == 0 ? action.name : action.name + " (alt)";     // "Pause (alt)"
                if (!b.isPartOfComposite) extra++;
                Rows.Add(r);
            }
        }
    }

    // Text for the menu, e.g. "P", "Escape", "Left Button", "Scroll Up"
    public static string KeyText(Row r)
    {
        return r.action.GetBindingDisplayString(r.index);
    }

    static InputActionRebindingExtensions.RebindingOperation op;
    public static bool IsRebinding { get { return op != null; } }

    // Frame the last rebind ended on. The menu ignores input on that frame,
    // so the key you just pressed (Enter, Esc ...) doesn't also act as a menu key.
    public static int LastRebindFrame = -1;

    public static void StartRebind(Row r)
    {
        if (op != null) return;

        r.action.Disable();               // an action has to be off while it is rebound
        op = r.action.PerformInteractiveRebinding(r.index)
            .WithControlsExcluding("<Mouse>/position")    // moving the mouse shouldn't count as a key
            .WithControlsExcluding("<Mouse>/delta")
            .WithCancelingThrough("<Keyboard>/escape")    // Esc = cancel, keep the old key
            .OnComplete(o => FinishRebind(r))
            .OnCancel(o => FinishRebind(r))
            .Start();
    }

    static void FinishRebind(Row r)
    {
        op.Dispose();
        op = null;
        r.action.Enable();
        LastRebindFrame = Time.frameCount;
        Save();
    }

    public static void ResetAll()
    {
        foreach (InputAction action in binds)
        {
            action.RemoveAllBindingOverrides();
            for (int i = 0; i < action.bindings.Count; i++)
                PlayerPrefs.DeleteKey(SaveKey(action, i));
        }
        PlayerPrefs.Save();
    }

    // Saved in PlayerPrefs as one key per changed binding, by position:
    //   "keybind_Pause_1" = "<Keyboard>/backspace"
    // Not Unity's SaveBindingOverridesAsJson: that saves by binding id, and bindings made in code
    // get a NEW random id every time you press Play, so the saved overrides could never be found again.
    static string SaveKey(InputAction action, int index)
    {
        return "keybind_" + action.name + "_" + index;
    }

    static void Save()
    {
        foreach (InputAction action in binds)
        {
            for (int i = 0; i < action.bindings.Count; i++)
            {
                string path = action.bindings[i].overridePath;
                if (string.IsNullOrEmpty(path)) PlayerPrefs.DeleteKey(SaveKey(action, i));
                else PlayerPrefs.SetString(SaveKey(action, i), path);
            }
        }
        PlayerPrefs.Save();
    }

    static void Load()
    {
        foreach (InputAction action in binds)
        {
            PlayerPrefs.DeleteKey("keybind_" + action.name);     // old JSON format, can't be loaded

            for (int i = 0; i < action.bindings.Count; i++)
            {
                string path = PlayerPrefs.GetString(SaveKey(action, i), "");
                if (path != "") action.ApplyBindingOverride(i, path);
            }
        }
    }
}