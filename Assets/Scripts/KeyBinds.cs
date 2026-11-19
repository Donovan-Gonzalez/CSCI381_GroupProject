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

    public static void Init()
    {
        Move.AddCompositeBinding("2DVector")
            .With("Up", "<Keyboard>/w")
            .With("Down", "<Keyboard>/s")
            .With("Left", "<Keyboard>/a")
            .With("Right", "<Keyboard>/d");

        Pause.AddBinding("<Keyboard>/escape");
        Confirm.AddBinding("<Keyboard>/space");
        CycleChar.AddBinding("<Keyboard>/rightArrow");

        foreach (InputAction a in binds) a.Enable();
    }
}