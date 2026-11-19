using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

// All on-screen UI, drawn with Unity's immediate-mode GUI so there is no Canvas to set up:
//   title / character select, in-game controls screen, HUD (portrait + health/stamina, ability block,
//   equipment block), enemy health bars + WEAK/TOUGH labels, floating combat text, pause, win, game over.
// A teammate can later replace this with a proper UGUI Canvas - the data it reads is all public.
public class HudView : MonoBehaviour
{
    class FloatText
    {
        public Vector3 pos;
        public string text;
        public Color color;
        public float t;
    }

    static readonly List<FloatText> floats = new List<FloatText>();

    public static void Float(Vector3 pos, string text, Color color)
    {
        FloatText f = new FloatText();
        f.pos = pos; f.text = text; f.color = color;
        floats.Add(f);
    }

    public static void ClearFloats() { floats.Clear(); }

    GUIStyle label;
    float s = 1f;      // GUI scale (layout is designed for 720 px tall)
    float vw = 1280f;  // virtual width
    Camera cam;

    static readonly Color Gold = new Color(1f, 0.85f, 0.25f);
    static readonly Color Dim = new Color(0.62f, 0.65f, 0.72f);

    void Update()
    {
        float dt = Time.deltaTime;
        for (int i = floats.Count - 1; i >= 0; i--)
        {
            floats[i].t += dt;
            if (floats[i].t > 1f) floats.RemoveAt(i);
        }
    }

    // ------------------------------------------------------------------ helpers
    void Fill(Rect r, Color c)
    {
        GUI.color = c;
        GUI.DrawTexture(r, Texture2D.whiteTexture);
        GUI.color = Color.white;
    }

    void Txt(Rect r, string text, int size, Color c, TextAnchor anchor, bool bold, bool wrap)
    {
        label.fontSize = size;
        label.alignment = anchor;
        label.fontStyle = bold ? FontStyle.Bold : FontStyle.Normal;
        label.wordWrap = wrap;
        label.normal.textColor = c;
        GUI.Label(r, text, label);
    }

    // current key(s) for an action, e.g. "P | Escape"
    static string K(InputAction action) { return action.GetBindingDisplayString(); }

    Vector2 ToGui(Vector3 world)
    {
        Vector3 sp = cam.WorldToScreenPoint(world);
        return new Vector2(sp.x / s, (Screen.height - sp.y) / s);
    }

    // ------------------------------------------------------------------ main
    void OnGUI()
    {
        if (Event.current.type != EventType.Repaint) return;
        RoomDirector rd = RoomDirector.Instance;
        if (rd == null) return;

        if (label == null)
        {
            label = new GUIStyle(GUI.skin.label);
            label.richText = true;
        }
        if (cam == null) cam = Camera.main;

        s = Screen.height / 720f;
        vw = Screen.width / s;
        GUI.matrix = Matrix4x4.Scale(new Vector3(s, s, 1f));

        GState st = RoomDirector.State;
        if (st == GState.Keybinds) { DrawKeybinds(rd); return; }
        if (st == GState.Title) { DrawTitle(rd); return; }

        DrawWorldLabels(rd);
        DrawPanel(rd);
        DrawTopBar(rd);
        if (st == GState.Playing) DrawTip(rd);

        if (st == GState.Paused) DrawPause();
        else if (st == GState.Win) DrawEnd(rd, true);
        else if (st == GState.Lose) DrawEnd(rd, false);
    }

    // ------------------------------------------------------------------ in-world labels
    void DrawWorldLabels(RoomDirector rd)
    {
        if (cam == null) return;

        // enemy health bars + strength / weakness labels (the "clear visual cues")
        for (int i = 0; i < FoeController.All.Count; i++)
        {
            FoeController foe = FoeController.All[i];
            if (foe == null) continue;

            Vector2 p = ToGui(foe.transform.position + Vector3.up * (foe.radius + 0.35f));
            float w = foe.type == FoeType.Boss ? 130f : 56f;
            float ratio = Mathf.Clamp01(foe.hp / foe.maxHp);

            Fill(new Rect(p.x - w / 2f, p.y - 4f, w, 7f), new Color(0f, 0f, 0f, 0.8f));
            Fill(new Rect(p.x - w / 2f + 1f, p.y - 3f, (w - 2f) * ratio, 5f), Color.Lerp(Color.red, Color.green, ratio));

            float[] m = foe.Mods();
            string weak = "";
            string tough = "";
            for (int a = 0; a < m.Length; a++)
            {
                string n = ((Attr)a).ToString().ToUpper();
                if (m[a] >= 1.9f) weak += (weak == "" ? "" : ", ") + n;
                if (m[a] <= 0.5f) tough += (tough == "" ? "" : ", ") + n;
            }
            if (weak != "") Txt(new Rect(p.x - 110f, p.y - 34f, 220f, 16f), "WEAK: " + weak, 12, Gold, TextAnchor.MiddleCenter, true, false);
            if (tough != "") Txt(new Rect(p.x - 110f, p.y - 20f, 220f, 16f), "TOUGH: " + tough, 11, Dim, TextAnchor.MiddleCenter, true, false);
        }

        // charge meter over the hero
        HeroController h = rd.Hero;
        if (h != null && h.Charging)
        {
            Vector2 p = ToGui(h.transform.position + Vector3.up * 0.9f);
            Fill(new Rect(p.x - 30f, p.y - 4f, 60f, 7f), new Color(0f, 0f, 0f, 0.8f));
            Fill(new Rect(p.x - 29f, p.y - 3f, 58f * h.ChargeT, 5f), h.ChargeT >= 0.5f ? Gold : Dim);
        }

        // floating combat text
        for (int i = 0; i < floats.Count; i++)
        {
            FloatText f = floats[i];
            Vector2 p = ToGui(f.pos + Vector3.up * (f.t * 0.9f));
            Color c = f.color;
            c.a = 1f - f.t;
            Txt(new Rect(p.x - 100f, p.y - 12f, 200f, 24f), f.text, 17, c, TextAnchor.MiddleCenter, true, false);
        }
    }

    // ------------------------------------------------------------------ bottom HUD
    string FnText(FunctionDef f, string key)
    {
        string attrs = "";
        foreach (Attr a in f.attrs)
            attrs += (attrs == "" ? "" : "+") + "<color=#" + CombatData.AttrHex(a) + ">" + a.ToString().ToLower() + "</color>";

        string t = "<b>" + key + "</b>  " + f.name;
        if (f.chargeable) t += " (hold)";
        if (attrs != "") t += "  [" + attrs + "]";
        if (f.noCombo) t += "  <color=#888888>(no combo)</color>";
        return t;
    }

    void DrawPanel(RoomDirector rd)
    {
        HeroController h = rd.Hero;
        if (h == null) return;

        Fill(new Rect(0f, 590f, vw, 130f), new Color(0.07f, 0.08f, 0.11f, 0.96f));
        Fill(new Rect(0f, 590f, vw, 2f), new Color(0.3f, 0.32f, 0.4f));

        // portrait + health / stamina
        Fill(new Rect(16f, 602f, 100f, 106f), new Color(h.heroColor.r * 0.5f, h.heroColor.g * 0.5f, h.heroColor.b * 0.5f));
        Txt(new Rect(16f, 606f, 100f, 70f), h.heroName.Substring(0, 1), 56, h.heroColor, TextAnchor.MiddleCenter, true, false);
        Txt(new Rect(16f, 680f, 100f, 24f), h.heroName, 15, Color.white, TextAnchor.MiddleCenter, true, false);

        Fill(new Rect(132f, 606f, 280f, 18f), new Color(0.2f, 0.05f, 0.05f));
        Fill(new Rect(132f, 606f, 280f * (h.hp / h.maxHp), 18f), new Color(0.85f, 0.2f, 0.2f));
        Txt(new Rect(132f, 606f, 280f, 18f), "HEALTH  " + Mathf.CeilToInt(h.hp) + " / " + Mathf.CeilToInt(h.maxHp), 12, Color.white, TextAnchor.MiddleCenter, true, false);

        Fill(new Rect(132f, 630f, 280f, 14f), new Color(0.15f, 0.13f, 0.03f));
        Fill(new Rect(132f, 630f, 280f * (h.stamina / h.maxStamina), 14f), new Color(0.95f, 0.8f, 0.2f));
        Txt(new Rect(132f, 630f, 280f, 14f), "STAMINA", 11, Color.black, TextAnchor.MiddleCenter, true, false);

        // abilities block
        Fill(new Rect(132f, 652f, 280f, 56f), new Color(0.12f, 0.13f, 0.18f));
        if (h.ability == HeroAbility.Dash)
        {
            Txt(new Rect(140f, 654f, 264f, 16f), "ABILITY  [" + K(KeyBinds.Ability) + "]  Dash", 13, Color.white, TextAnchor.MiddleLeft, true, false);
            for (int i = 0; i < 2; i++)
                Fill(new Rect(140f + i * 26f, 676f, 20f, 20f), i < h.DashCharges ? h.heroColor : new Color(0.25f, 0.27f, 0.33f));
            Txt(new Rect(198f, 672f, 208f, 32f), "Dash, then Swing = Dash Slash", 11, Dim, TextAnchor.MiddleLeft, false, true);
        }
        else
        {
            Txt(new Rect(140f, 654f, 264f, 16f), "ABILITY  [" + K(KeyBinds.Ability) + "]  Surge", 13, Color.white, TextAnchor.MiddleLeft, true, false);
            string status;
            Color sc;
            if (h.SurgeLeft > 0f) { status = "ACTIVE  " + h.SurgeLeft.ToString("0.0") + "s"; sc = Gold; }
            else if (h.SurgeCooldown > 0f) { status = "Recharging  " + Mathf.CeilToInt(h.SurgeCooldown) + "s"; sc = Dim; }
            else { status = "READY"; sc = new Color(0.5f, 1f, 0.6f); }
            Txt(new Rect(140f, 676f, 264f, 16f), status, 13, sc, TextAnchor.MiddleLeft, true, false);
        }

        // equipment block: weapon strip, technique strip, current functions
        float ex = 432f;
        Txt(new Rect(ex, 598f, 400f, 16f), "WEAPON   (" + K(KeyBinds.PrevWeapon) + " / " + K(KeyBinds.NextWeapon) + ")", 12, Dim, TextAnchor.MiddleLeft, true, false);
        for (int i = 0; i < CombatData.WeaponNames.Length; i++)
        {
            bool cur = (i == h.weapon);
            Rect r = new Rect(ex + i * 178f, 616f, 170f, 26f);
            Fill(r, cur ? h.heroColor : new Color(0.16f, 0.17f, 0.22f));
            Txt(r, CombatData.WeaponNames[i], 14, cur ? Color.black : Dim, TextAnchor.MiddleCenter, cur, false);
        }

        Txt(new Rect(ex, 646f, 400f, 16f), "SKILL   (" + K(KeyBinds.PrevSkill) + " / " + K(KeyBinds.NextSkill) + ")", 12, Dim, TextAnchor.MiddleLeft, true, false);
        for (int i = 0; i < CombatData.TechNames.Length; i++)
        {
            bool cur = (i == h.tech);
            Rect r = new Rect(ex + i * 138f, 664f, 130f, 26f);
            Fill(r, cur ? Gold : new Color(0.16f, 0.17f, 0.22f));
            Txt(r, CombatData.TechNames[i], 14, cur ? Color.black : Dim, TextAnchor.MiddleCenter, cur, false);
        }

        FunctionDef prim = CombatData.Resolve(h.weapon, 0, h.tech, true);
        FunctionDef sec = CombatData.Resolve(h.weapon, 1, h.tech, false);
        string line = FnText(prim, K(KeyBinds.Primary)) + "      " + FnText(sec, K(KeyBinds.Secondary));
        if (h.ability == HeroAbility.Dash && h.SinceDash < 0.5f && prim.kind == FnKind.Arc)
            line += "   <color=#ffd84a>DASH SLASH READY!</color>";
        Txt(new Rect(ex, 694f, vw - ex - 16f, 22f), line, 14, Color.white, TextAnchor.MiddleLeft, false, false);
    }

    // ------------------------------------------------------------------ top bar + tip
    void DrawTopBar(RoomDirector rd)
    {
        Fill(new Rect(0f, 0f, vw, 40f), new Color(0.07f, 0.08f, 0.11f, 0.96f));
        Txt(new Rect(16f, 8f, 420f, 24f), rd.Rooms[rd.RoomIndex].name, 18, Color.white, TextAnchor.MiddleLeft, true, false);

        int n = rd.Rooms.Length;
        float x0 = vw / 2f - n * 30f / 2f;
        for (int i = 0; i < n; i++)
        {
            Color c = new Color(0.25f, 0.27f, 0.33f);
            if (i < rd.RoomIndex) c = new Color(0.3f, 0.75f, 0.4f);
            else if (i == rd.RoomIndex) c = Gold;
            Fill(new Rect(x0 + i * 30f, 13f, 24f, 14f), c);
        }

        Txt(new Rect(vw - 276f, 4f, 260f, 20f), "Enemies left: " + rd.Remaining, 16, Color.white, TextAnchor.MiddleRight, true, false);
        Txt(new Rect(vw - 276f, 22f, 260f, 16f), "Pause: " + K(KeyBinds.Pause), 11, Dim, TextAnchor.MiddleRight, false, false);

        if (rd.DoorOpen && RoomDirector.State == GState.Playing)
            Txt(new Rect(0f, 44f, vw, 24f), "DOOR UNLOCKED  -  head RIGHT to the next room", 16, new Color(0.5f, 1f, 0.6f), TextAnchor.MiddleCenter, true, false);
    }

    void DrawTip(RoomDirector rd)
    {
        if (rd.RoomTime > 25f || rd.DoorOpen) return;
        Rect r = new Rect(vw / 2f - 420f, 70f, 840f, 44f);
        Fill(r, new Color(0f, 0f, 0f, 0.6f));
        Txt(new Rect(r.x + 10f, r.y, r.width - 20f, r.height), rd.Rooms[rd.RoomIndex].tip, 14, Color.white, TextAnchor.MiddleCenter, false, true);
    }

    // ------------------------------------------------------------------ screens
    void DrawControls(Rect area)
    {
        Fill(area, new Color(0.1f, 0.11f, 0.15f, 0.95f));
        Txt(new Rect(area.x + 16f, area.y + 8f, 300f, 24f), "CONTROLS", 18, Gold, TextAnchor.MiddleLeft, true, false);

        // key names come from KeyBinds, so this list stays right after a rebind
        string[,] rows =
        {
            { K(KeyBinds.Move), "Move" },
            { "Mouse", "Aim" },
            { K(KeyBinds.Primary), "Primary function (hold, then release, when the Charge skill is equipped)" },
            { K(KeyBinds.Secondary), "Secondary function (hold to Block with the Sword & Shield)" },
            { K(KeyBinds.PrevWeapon) + " / " + K(KeyBinds.NextWeapon), "Change weapon  (weapon strip)" },
            { K(KeyBinds.PrevSkill) + " / " + K(KeyBinds.NextSkill), "Change skill  (skill strip)" },
            { K(KeyBinds.Ability), "Character ability  (Vanguard: Dash, Ember: Surge)" },
            { K(KeyBinds.Pause), "Pause" }
        };

        for (int i = 0; i < rows.GetLength(0); i++)
        {
            float y = area.y + 40f + i * 26f;
            Txt(new Rect(area.x + 16f, y, 190f, 24f), rows[i, 0], 15, Color.white, TextAnchor.MiddleLeft, true, false);
            Txt(new Rect(area.x + 210f, y, area.width - 226f, 24f), rows[i, 1], 14, Dim, TextAnchor.MiddleLeft, false, false);
        }
    }

    void DrawTitle(RoomDirector rd)
    {
        Fill(new Rect(0f, 0f, vw, 720f), new Color(0.05f, 0.05f, 0.08f, 1f));
        Txt(new Rect(0f, 18f, vw, 52f), "SUPERHERO ROOMS", 42, Color.white, TextAnchor.MiddleCenter, true, false);
        Txt(new Rect(0f, 70f, vw, 24f), "Playable base for our group project  -  placeholder art and numbers", 15, Dim, TextAnchor.MiddleCenter, false, false);

        for (int i = 0; i < CombatData.Roster.Length; i++)
        {
            CharacterDef c = CombatData.Roster[i];
            Rect r = new Rect(vw / 2f - 410f + i * 420f, 110f, 400f, 150f);
            bool sel = (i == rd.SelectedChar);
            if (sel) Fill(new Rect(r.x - 3f, r.y - 3f, r.width + 6f, r.height + 6f), Gold);
            Fill(r, new Color(0.12f, 0.13f, 0.18f));
            Txt(new Rect(r.x + 14f, r.y + 6f, 372f, 34f), "[" + (i + 1) + "]  " + c.name, 24, c.color, TextAnchor.MiddleLeft, true, false);
            Txt(new Rect(r.x + 14f, r.y + 44f, 372f, 100f), c.blurb, 14, Color.white, TextAnchor.UpperLeft, false, true);
        }

        DrawControls(new Rect(vw / 2f - 410f, 280f, 820f, 260f));

        Txt(new Rect(0f, 560f, vw, 36f), "Press " + K(KeyBinds.Confirm) + " to start    (" + K(KeyBinds.PickChar1) + " / " + K(KeyBinds.PickChar2) + " or " + K(KeyBinds.CycleChar) + " to pick a hero)", 22, Color.white, TextAnchor.MiddleCenter, true, false);
        Txt(new Rect(0f, 586f, vw, 18f), "K = keybinds", 14, Gold, TextAnchor.MiddleCenter, true, false);
        Txt(new Rect(0f, 604f, vw, 24f), "Rooms stay locked until every enemy is dead. Every enemy has a strength and a weakness - read the labels!", 14, Dim, TextAnchor.MiddleCenter, false, false);
        Txt(new Rect(0f, 630f, vw, 24f), "Attributes:  <color=#d9f2ff>SLASH</color>   <color=#80e08c>PUNCTURE</color>   <color=#ff9a3d>FIRE</color>   <color=#8fd4ff>FROZEN</color> (slows)", 14, Color.white, TextAnchor.MiddleCenter, false, false);
    }

    void DrawPause()
    {
        Fill(new Rect(0f, 0f, vw, 720f), new Color(0f, 0f, 0f, 0.75f));
        Txt(new Rect(0f, 70f, vw, 60f), "PAUSED", 44, Color.white, TextAnchor.MiddleCenter, true, false);
        DrawControls(new Rect(vw / 2f - 410f, 160f, 820f, 260f));
        Txt(new Rect(0f, 440f, vw, 30f), "Press " + K(KeyBinds.Pause) + " to resume     |     K = keybinds", 20, Gold, TextAnchor.MiddleCenter, true, false);
    }

    void DrawKeybinds(RoomDirector rd)
    {
        Fill(new Rect(0f, 0f, vw, 720f), new Color(0.05f, 0.05f, 0.08f, 1f));
        Txt(new Rect(0f, 14f, vw, 44f), "KEYBINDS", 34, Color.white, TextAnchor.MiddleCenter, true, false);
        Txt(new Rect(0f, 56f, vw, 22f), "Up / Down: choose     Enter: rebind     Backspace: reset all     Esc: back", 15, Dim, TextAnchor.MiddleCenter, false, false);

        // two columns so ~22 rows fit on a 720 px screen
        int perCol = 12;
        float colW = 400f;
        float x0 = vw / 2f - colW - 10f;
        for (int i = 0; i < KeyBinds.Rows.Count; i++)
        {
            KeyBinds.Row row = KeyBinds.Rows[i];
            float x = x0 + (i / perCol) * (colW + 20f);
            float y = 96f + (i % perCol) * 44f;
            Rect r = new Rect(x, y, colW, 38f);

            bool sel = (i == rd.KeybindSel);
            Fill(r, sel ? new Color(0.22f, 0.24f, 0.32f) : new Color(0.12f, 0.13f, 0.18f));
            if (sel) Fill(new Rect(r.x, r.y, 4f, r.height), Gold);

            string key = (sel && KeyBinds.IsRebinding) ? "Press a key...  (Esc cancels)" : KeyBinds.KeyText(row);
            Txt(new Rect(r.x + 14f, r.y, 180f, r.height), row.label, 15, Color.white, TextAnchor.MiddleLeft, true, false);
            Txt(new Rect(r.x + 190f, r.y, colW - 200f, r.height), key, 15, sel ? Gold : Dim, TextAnchor.MiddleRight, sel, false);
        }
    }

    void DrawEnd(RoomDirector rd, bool won)
    {
        Fill(new Rect(0f, 0f, vw, 720f), new Color(0f, 0f, 0f, 0.8f));
        if (won)
        {
            Txt(new Rect(0f, 200f, vw, 70f), "YOU WIN!", 58, Gold, TextAnchor.MiddleCenter, true, false);
            Txt(new Rect(0f, 280f, vw, 30f), "The Warden is defeated and the rooms are clear.", 20, Color.white, TextAnchor.MiddleCenter, false, false);
        }
        else
        {
            Txt(new Rect(0f, 200f, vw, 70f), "GAME OVER", 58, new Color(1f, 0.35f, 0.35f), TextAnchor.MiddleCenter, true, false);
            Txt(new Rect(0f, 280f, vw, 30f), "You fell in  " + rd.Rooms[rd.RoomIndex].name, 20, Color.white, TextAnchor.MiddleCenter, false, false);
        }
        Txt(new Rect(0f, 360f, vw, 30f), "Press " + K(KeyBinds.Retry) + " to retry     |     " + K(KeyBinds.ToTitle) + " for the title screen", 20, Color.white, TextAnchor.MiddleCenter, true, false);
    }
}
