using System.Collections.Generic;
using UnityEngine;

// All on-screen UI, drawn with Unity's immediate-mode GUI so there is no Canvas to set up:
//   title / character select, weapon select, reward screens, in-game controls screen, HUD (portrait + health,
//   ability block, cross-shaped equipment block), enemy health bars + WEAK/TOUGH labels, floating combat text,
//   pause, win, game over.
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
    Texture2D triUp, triDown, triLeft, triRight;

    static readonly Color Gold = new Color(1f, 0.85f, 0.25f);
    static readonly Color Dim = new Color(0.62f, 0.65f, 0.72f);
    static readonly Color BoxFill = new Color(0.09f, 0.10f, 0.13f);
    static readonly Color BoxLine = new Color(0.42f, 0.44f, 0.52f);
    static readonly Color PanelBg = new Color(0.07f, 0.08f, 0.11f, 0.97f);

    const int MaxWeaponsPerClass = 3;   // design target: 3 weapons per class (unknown ones show as ???)

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

    // rounded box: filled, with an optional border
    void Box(Rect r, Color fill, Color line, float lineW, float radius)
    {
        GUI.DrawTexture(r, Texture2D.whiteTexture, ScaleMode.StretchToFill, true, 0f, fill, 0f, radius);
        if (lineW > 0f)
            GUI.DrawTexture(r, Texture2D.whiteTexture, ScaleMode.StretchToFill, true, 0f, line, lineW, radius);
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

    // draws a Kenney tile (weapon icon) with a dark plate behind it; does nothing if the tile or sheet is missing
    void Icon(Rect r, int tile, Color tint)
    {
        Sprite sp = SpriteFactory.Tile(tile);
        if (sp == null) return;
        Box(r, new Color(0.04f, 0.05f, 0.07f), new Color(0.3f, 0.32f, 0.4f), 1.5f, 6f);
        Texture2D tex = sp.texture;
        Rect uv = new Rect(sp.rect.x / tex.width, sp.rect.y / tex.height, sp.rect.width / tex.width, sp.rect.height / tex.height);
        float pad = r.width * 0.12f;
        GUI.color = tint;
        GUI.DrawTextureWithTexCoords(new Rect(r.x + pad, r.y + pad, r.width - 2f * pad, r.height - 2f * pad), tex, uv);
        GUI.color = Color.white;
    }

    float TextWidth(string text, int size, bool bold)
    {
        label.fontSize = size;
        label.fontStyle = bold ? FontStyle.Bold : FontStyle.Normal;
        label.wordWrap = false;
        return label.CalcSize(new GUIContent(text)).x;
    }

    Vector2 ToGui(Vector3 world)
    {
        Vector3 sp = cam.WorldToScreenPoint(world);
        return new Vector2(sp.x / s, (Screen.height - sp.y) / s);
    }

    // little triangle textures for the arrow hints (dir: 0 up, 1 down, 2 left, 3 right)
    static Texture2D MakeTri(int dir)
    {
        const int n = 32;
        Texture2D t = new Texture2D(n, n, TextureFormat.RGBA32, false);
        t.filterMode = FilterMode.Bilinear;
        t.wrapMode = TextureWrapMode.Clamp;
        Color32[] px = new Color32[n * n];
        for (int y = 0; y < n; y++)
        {
            for (int x = 0; x < n; x++)
            {
                // "along" goes from the base (0) to the tip (1); the triangle narrows toward the tip
                float u = (x + 0.5f) / n, v = (y + 0.5f) / n;   // v = 0 is the bottom row of the texture
                float along, across;
                switch (dir)
                {
                    case 0: along = v; across = u; break;            // tip at top
                    case 1: along = 1f - v; across = u; break;       // tip at bottom
                    case 2: along = 1f - u; across = v; break;       // tip at left
                    default: along = u; across = v; break;           // tip at right
                }
                bool inside = Mathf.Abs(across - 0.5f) <= 0.5f * (1f - along);
                px[y * n + x] = inside ? new Color32(255, 255, 255, 255) : new Color32(255, 255, 255, 0);
            }
        }
        t.SetPixels32(px);
        t.Apply();
        return t;
    }

    void Tri(Rect r, int dir, Color c)
    {
        Texture2D t = dir == 0 ? triUp : dir == 1 ? triDown : dir == 2 ? triLeft : triRight;
        GUI.color = c;
        GUI.DrawTexture(r, t);
        GUI.color = Color.white;
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
        if (triUp == null) { triUp = MakeTri(0); triDown = MakeTri(1); triLeft = MakeTri(2); triRight = MakeTri(3); }
        if (cam == null) cam = Camera.main;

        s = Screen.height / 720f;
        vw = Screen.width / s;
        Matrix4x4 baseM = Matrix4x4.Scale(new Vector3(s, s, 1f));
        GUI.matrix = baseM;

        GState st = RoomDirector.State;
        if (st == GState.Title || st == GState.WeaponPick) { DrawTitle(rd); return; }

        DrawWorldLabels(rd);
        DrawTopBar(rd);
        if (st == GState.Playing) DrawTip(rd);

        // bottom HUD is laid out for 1280 wide; shrink it (anchored bottom-left) on narrower windows
        float k = Mathf.Min(1f, vw / 1280f);
        GUI.matrix = baseM * Matrix4x4.TRS(new Vector3(0f, 720f * (1f - k), 0f), Quaternion.identity, new Vector3(k, k, 1f));
        DrawPanel(rd, vw / k);
        GUI.matrix = baseM;

        if (st == GState.Paused) DrawPause();
        else if (st == GState.Reward) DrawReward(rd);
        else if (st == GState.Win) DrawEnd(rd, true);
        else if (st == GState.Lose) DrawEnd(rd, false);
    }

    // ------------------------------------------------------------------ in-world labels
    void DrawWorldLabels(RoomDirector rd)
    {
        if (cam == null) return;
        HeroController h = rd.Hero;

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

            // auto-aim marker on the current target
            if (h != null && h.Target == foe)
            {
                Vector2 b = ToGui(foe.transform.position - Vector3.up * (foe.radius + 0.15f));
                Tri(new Rect(b.x - 7f, b.y, 14f, 10f), 0, new Color(1f, 0.85f, 0.25f, 0.9f));
            }
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
    void DrawPanel(RoomDirector rd, float pw)
    {
        HeroController h = rd.Hero;
        if (h == null) return;

        Fill(new Rect(0f, 524f, pw, 196f), PanelBg);
        Fill(new Rect(0f, 524f, pw, 2f), new Color(0.3f, 0.32f, 0.4f));

        // ---- portrait + health ----
        Box(new Rect(16f, 538f, 92f, 104f), new Color(h.heroColor.r * 0.45f, h.heroColor.g * 0.45f, h.heroColor.b * 0.45f), h.heroColor, 2f, 8f);
        Txt(new Rect(16f, 540f, 92f, 76f), h.heroName.Substring(0, 1), 54, h.heroColor, TextAnchor.MiddleCenter, true, false);
        Txt(new Rect(16f, 612f, 92f, 24f), h.heroName, 14, Color.white, TextAnchor.MiddleCenter, true, false);

        Box(new Rect(120f, 540f, 222f, 22f), new Color(0.2f, 0.05f, 0.05f), Color.clear, 0f, 4f);
        if (h.hp > 0f) Box(new Rect(120f, 540f, 222f * (h.hp / h.maxHp), 22f), new Color(0.85f, 0.2f, 0.2f), Color.clear, 0f, 4f);
        Txt(new Rect(120f, 540f, 222f, 22f), "HEALTH  " + Mathf.CeilToInt(h.hp) + " / " + Mathf.CeilToInt(h.maxHp), 12, Color.white, TextAnchor.MiddleCenter, true, false);

        // ---- ability ----
        Box(new Rect(120f, 570f, 222f, 72f), new Color(0.12f, 0.13f, 0.18f), Color.clear, 0f, 6f);
        if (h.ability == HeroAbility.Dash)
        {
            Txt(new Rect(130f, 574f, 206f, 18f), "ABILITY  [Space]  Dash", 13, Color.white, TextAnchor.MiddleLeft, true, false);
            for (int i = 0; i < 2; i++)
                Box(new Rect(130f + i * 26f, 600f, 20f, 20f), i < h.DashCharges ? h.heroColor : new Color(0.25f, 0.27f, 0.33f), Color.clear, 0f, 4f);
            Txt(new Rect(186f, 594f, 150f, 44f), "Dash, then a melee hit = Dash Slash", 11, Dim, TextAnchor.MiddleLeft, false, true);
        }
        else
        {
            Txt(new Rect(130f, 574f, 206f, 18f), "ABILITY  [Space]  Surge", 13, Color.white, TextAnchor.MiddleLeft, true, false);
            string status;
            Color sc;
            if (h.SurgeLeft > 0f) { status = "ACTIVE  " + h.SurgeLeft.ToString("0.0") + "s"; sc = Gold; }
            else if (h.SurgeCooldown > 0f) { status = "Recharging  " + Mathf.CeilToInt(h.SurgeCooldown) + "s"; sc = Dim; }
            else { status = "READY"; sc = new Color(0.5f, 1f, 0.6f); }
            Txt(new Rect(130f, 598f, 206f, 18f), status, 13, sc, TextAnchor.MiddleLeft, true, false);
            Txt(new Rect(130f, 616f, 206f, 18f), "Much faster attacks for 4s", 11, Dim, TextAnchor.MiddleLeft, false, false);
        }

        Txt(new Rect(16f, 650f, 326f, 60f),
            "Attacks fire <b>automatically</b> at the nearest enemy (gold marker).\nYou just move: <b>W A S D</b>.",
            12, Dim, TextAnchor.UpperLeft, false, true);

        DrawEquipment(h, Mathf.Max(356f, pw - 16f - 904f));
    }

    // Picks labels for the slots around the selected one, in priority order of the offsets.
    // Real items first (no repeats), then "???" for things not earned yet, then "" (empty).
    static string[] Carousel(int count, int cur, int maxCount, int[] offsets, System.Func<int, string> name)
    {
        string[] result = new string[offsets.Length];
        HashSet<int> shown = new HashSet<int> { cur };
        int unknown = Mathf.Max(0, maxCount - count);
        for (int i = 0; i < offsets.Length; i++)
        {
            int idx = ((cur + offsets[i]) % count + count) % count;
            if (!shown.Contains(idx)) { shown.Add(idx); result[i] = name(idx); }
            else if (unknown > 0) { unknown--; result[i] = "???"; }
            else result[i] = "";
        }
        return result;
    }

    void SlotBox(Rect r, string text)
    {
        if (text == "")
        {
            Box(r, new Color(0.08f, 0.09f, 0.11f, 0.6f), new Color(0.25f, 0.27f, 0.32f), 1f, 6f);
            return;
        }
        Box(r, BoxFill, BoxLine, 2f, 6f);
        Txt(r, text, 12, text == "???" ? Dim : Color.white, TextAnchor.MiddleCenter, true, false);
    }

    // The equipment block from the mockup:
    //   left card = PRIMARY weapon, right card = SECONDARY weapon,
    //   middle = a cross: Left / Right row = weapons, Up / Down column = action / elements of the equipped weapon.
    void DrawEquipment(HeroController h, float ex)
    {
        const float W = 904f, BW = 84f, BH = 32f, G = 4f;
        Box(new Rect(ex, 530f, W, 184f), new Color(0.06f, 0.07f, 0.09f), new Color(0.3f, 0.32f, 0.4f), 2f, 10f);
        Txt(new Rect(ex + 14f, 534f, 200f, 16f), "EQUIPMENT", 12, Dim, TextAnchor.MiddleLeft, true, false);

        float cx = ex + W / 2f;
        float rowY = 622f;
        HeroController.OwnedWeapon eq = h.Equipped;

        // ---- vertical column: action (no element) / elements ----
        int[] vOff = { 1, -1, 2 };      // +1 and +2 go above, -1 below (up arrow = +1)
        string[] v = Carousel(eq.SlotCount, eq.elementSlot, 1 + CombatData.ElementNames.Length, vOff, eq.SlotName);
        SlotBox(new Rect(cx - BW / 2f, rowY - (BH + G), BW, BH), v[0]);
        SlotBox(new Rect(cx - BW / 2f, rowY + (BH + G), BW, BH), v[1]);
        SlotBox(new Rect(cx - BW / 2f, rowY - 2f * (BH + G), BW, BH), v[2]);
        Tri(new Rect(cx - 8f, rowY - 2f * (BH + G) - 14f, 16f, 10f), 0, Gold);
        Tri(new Rect(cx - 8f, rowY + 2f * (BH + G) - G + 4f, 16f, 10f), 1, Gold);

        // ---- horizontal row: weapons ----
        int[] hOff = { 1, -1, 2, -2 };
        int maxW = Mathf.Max(MaxWeaponsPerClass, h.Character.weapons.Length);
        string[] hw = Carousel(h.Weapons.Count, h.WeaponSlot, maxW, hOff, i => h.Weapons[i].Def.name);
        SlotBox(new Rect(cx - BW / 2f + (BW + G), rowY, BW, BH), hw[0]);
        SlotBox(new Rect(cx - BW / 2f - (BW + G), rowY, BW, BH), hw[1]);
        SlotBox(new Rect(cx - BW / 2f + 2f * (BW + G), rowY, BW, BH), hw[2]);
        SlotBox(new Rect(cx - BW / 2f - 2f * (BW + G), rowY, BW, BH), hw[3]);
        float edge = BW / 2f + 2f * (BW + G);
        Tri(new Rect(cx - edge - 16f, rowY + BH / 2f - 8f, 10f, 16f), 2, Gold);
        Tri(new Rect(cx + edge + 6f, rowY + BH / 2f - 8f, 10f, 16f), 3, Gold);

        // ---- center: the current weapon + action / element ----
        Rect c = new Rect(cx - BW / 2f - 3f, rowY - 3f, BW + 6f, BH + 6f);
        Box(c, new Color(0.26f, 0.21f, 0.07f), Gold, 3f, 7f);
        string sub = eq.elementSlot == 0 ? eq.SlotName(0) : CombatData.ElementNames[eq.ElementAt(eq.elementSlot)];
        Txt(c, eq.Def.name + "\n<color=#ffd84a>+ " + sub + "</color>", 11, Color.white, TextAnchor.MiddleCenter, true, false);

        // ---- cards ----
        DrawWeaponCard(new Rect(ex + 12f, 552f, 196f, 136f), h, 0);
        DrawWeaponCard(new Rect(ex + W - 12f - 196f, 552f, 196f, 136f), h, 1);
    }

    void DrawWeaponCard(Rect r, HeroController h, int slot)
    {
        string role = slot == 0 ? "PRIMARY" : "SECONDARY";
        bool owned = slot < h.Weapons.Count;
        bool active = owned && slot == h.WeaponSlot;

        Box(r, new Color(0.08f, 0.09f, 0.12f), active ? Gold : BoxLine, active ? 2.5f : 1.5f, 9f);
        float x = r.x + 12f, w = r.width - 24f;

        if (!owned)
        {
            Txt(new Rect(x, r.y + 8f, w, 18f), role + " · ???", 13, Dim, TextAnchor.MiddleLeft, true, false);
            Txt(new Rect(x, r.y + 40f, w, 60f), "Defeat the stage boss\nto earn a second weapon.", 13, Dim, TextAnchor.UpperLeft, false, true);
            return;
        }

        HeroController.OwnedWeapon ow = h.Weapons[slot];
        FunctionDef f = ow.Resolve();
        Color head = active ? Gold : Dim;
        Txt(new Rect(x, r.y + 8f, w, 18f), role + " · " + ow.Def.name, 13, head, TextAnchor.MiddleLeft, true, false);
        Icon(new Rect(r.xMax - 46f, r.y + 6f, 34f, 34f), SpriteFactory.WeaponTile(ow.Def.name), active ? Color.white : new Color(0.6f, 0.6f, 0.6f));
        Txt(new Rect(x, r.y + 28f, w, 14f), "action:", 11, Dim, TextAnchor.MiddleLeft, false, false);
        Txt(new Rect(x, r.y + 42f, w + 6f, 24f), f.name, f.name.Length > 16 ? 15 : 17, active ? Color.white : Dim, TextAnchor.MiddleLeft, true, false);
        Txt(new Rect(x, r.y + 68f, w + 6f, 16f), CombatData.StatLine(f), 11, active ? Color.white : Dim, TextAnchor.MiddleLeft, true, false);
        Txt(new Rect(x, r.y + 88f, w, 14f), "attributes:", 11, Dim, TextAnchor.MiddleLeft, false, false);

        float cxp = x;
        foreach (Attr a in f.attrs)
        {
            string t = a.ToString();
            float tw = TextWidth(t, 11, true) + 16f;
            Color ac = CombatData.AttrColor(a);
            if (!active) ac = Color.Lerp(ac, new Color(0.3f, 0.3f, 0.3f), 0.5f);
            Box(new Rect(cxp, r.y + 106f, tw, 20f), ac, Color.clear, 0f, 10f);
            Txt(new Rect(cxp, r.y + 106f, tw, 20f), t, 11, new Color(0.1f, 0.08f, 0.05f), TextAnchor.MiddleCenter, true, false);
            cxp += tw + 6f;
        }
    }

    // ------------------------------------------------------------------ top bar + tip
    void DrawTopBar(RoomDirector rd)
    {
        Fill(new Rect(0f, 0f, vw, 40f), PanelBg);
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
        Txt(new Rect(vw - 276f, 22f, 260f, 16f), "P = pause + controls", 11, Dim, TextAnchor.MiddleRight, false, false);

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
        Box(area, new Color(0.1f, 0.11f, 0.15f, 0.95f), Color.clear, 0f, 8f);
        Txt(new Rect(area.x + 16f, area.y + 8f, 300f, 24f), "CONTROLS", 18, Gold, TextAnchor.MiddleLeft, true, false);

        string[,] rows =
        {
            { "W A S D", "Move  (that's all your left hand does, plus Space)" },
            { "(automatic)", "Attacks fire on their own at the nearest enemy in range" },
            { "Left / Right arrows", "Switch weapon  (primary / secondary)" },
            { "Up / Down arrows", "Switch element  (none / Ignite / Chill)" },
            { "Space", "Character ability  (Vanguard: Dash, Ember: Surge)" },
            { "P or Esc", "Pause" }
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

        if (RoomDirector.State == GState.WeaponPick) { DrawWeaponPick(rd); return; }

        Txt(new Rect(0f, 70f, vw, 24f), "Step 1 of 2  -  choose a hero", 15, Dim, TextAnchor.MiddleCenter, false, false);

        for (int i = 0; i < CombatData.Roster.Length; i++)
        {
            CharacterDef c = CombatData.Roster[i];
            Rect r = new Rect(vw / 2f - 410f + i * 420f, 110f, 400f, 150f);
            bool sel = (i == rd.SelectedChar);
            Box(r, new Color(0.12f, 0.13f, 0.18f), sel ? Gold : BoxLine, sel ? 3f : 1f, 10f);
            Txt(new Rect(r.x + 14f, r.y + 6f, 372f, 34f), "[" + (i + 1) + "]  " + c.name, 24, c.color, TextAnchor.MiddleLeft, true, false);
            Txt(new Rect(r.x + 14f, r.y + 44f, 372f, 100f), c.blurb, 14, Color.white, TextAnchor.UpperLeft, false, true);
        }

        DrawControls(new Rect(vw / 2f - 410f, 280f, 820f, 210f));

        Txt(new Rect(0f, 520f, vw, 36f), "Press ENTER to choose this hero    (1 / 2 or Left / Right to switch)", 22, Color.white, TextAnchor.MiddleCenter, true, false);
        Txt(new Rect(0f, 572f, vw, 24f), "Rooms stay locked until every enemy is dead. Every enemy has a strength and a weakness - read the labels!", 14, Dim, TextAnchor.MiddleCenter, false, false);
        Txt(new Rect(0f, 598f, vw, 24f), "Clear rooms to earn an ACTION (room 1) and ELEMENTS (rooms 2 and 3). Beat the boss for a second weapon.", 14, Dim, TextAnchor.MiddleCenter, false, false);
        Txt(new Rect(0f, 630f, vw, 24f), "Attributes:  <color=#d9f2ff>SLASH</color>   <color=#80e08c>PUNCTURE</color>   <color=#ff9a3d>FIRE</color> (burns)   <color=#8fd4ff>FROZEN</color> (slows)", 14, Color.white, TextAnchor.MiddleCenter, false, false);
    }

    void DrawWeaponPick(RoomDirector rd)
    {
        CharacterDef c = CombatData.Roster[rd.SelectedChar];
        Txt(new Rect(0f, 70f, vw, 24f), "Step 2 of 2  -  choose " + c.name + "'s primary weapon", 15, Dim, TextAnchor.MiddleCenter, false, false);

        for (int i = 0; i < c.weapons.Length; i++)
        {
            WeaponDef w = CombatData.Weapons[c.weapons[i]];
            FunctionDef f = w.actions[0];
            Rect r = new Rect(vw / 2f - 410f + i * 420f, 110f, 400f, 190f);
            bool sel = (i == rd.SelectedWeapon);
            Box(r, new Color(0.12f, 0.13f, 0.18f), sel ? Gold : BoxLine, sel ? 3f : 1f, 10f);
            Txt(new Rect(r.x + 14f, r.y + 6f, 372f, 34f), "[" + (i + 1) + "]  " + w.name, 24, c.color, TextAnchor.MiddleLeft, true, false);
            Icon(new Rect(r.xMax - 54f, r.y + 4f, 40f, 40f), SpriteFactory.WeaponTile(w.name), Color.white);
            Txt(new Rect(r.x + 14f, r.y + 44f, 372f, 60f), w.blurb, 14, Color.white, TextAnchor.UpperLeft, false, true);
            Txt(new Rect(r.x + 14f, r.y + 112f, 372f, 18f), "Starts with:  <b>" + f.name + "</b>", 14, Color.white, TextAnchor.MiddleLeft, false, false);
            Txt(new Rect(r.x + 14f, r.y + 134f, 372f, 18f), CombatData.StatLine(f), 13, Dim, TextAnchor.MiddleLeft, true, false);
            string attrs = "";
            foreach (Attr a in f.attrs) attrs += "<color=#" + CombatData.AttrHex(a) + ">" + a.ToString().ToUpper() + "</color>  ";
            Txt(new Rect(r.x + 14f, r.y + 156f, 372f, 18f), attrs, 13, Color.white, TextAnchor.MiddleLeft, true, false);
        }

        DrawControls(new Rect(vw / 2f - 410f, 320f, 820f, 210f));

        Txt(new Rect(0f, 550f, vw, 36f), "Press ENTER to start    (1 / 2 or Left / Right to switch)", 22, Color.white, TextAnchor.MiddleCenter, true, false);
        Txt(new Rect(0f, 594f, vw, 24f), "Esc = back to hero select.  The other weapon can be earned as your secondary weapon after the stage boss.", 14, Dim, TextAnchor.MiddleCenter, false, false);
    }

    void DrawReward(RoomDirector rd)
    {
        Fill(new Rect(0f, 0f, vw, 720f), new Color(0f, 0f, 0f, 0.8f));
        Txt(new Rect(0f, 110f, vw, 50f), rd.RewardTitle, 34, Gold, TextAnchor.MiddleCenter, true, false);
        Txt(new Rect(0f, 162f, vw, 26f), rd.RewardSub, 17, Color.white, TextAnchor.MiddleCenter, false, false);

        int n = rd.RewardOptions.Count;
        float cw = 320f, gap = 24f;
        float x0 = vw / 2f - (n * cw + (n - 1) * gap) / 2f;
        for (int i = 0; i < n; i++)
        {
            RoomDirector.RewardOption o = rd.RewardOptions[i];
            Rect r = new Rect(x0 + i * (cw + gap), 220f, cw, 190f);
            bool sel = (i == rd.RewardSel);
            Box(r, new Color(0.11f, 0.12f, 0.17f), sel ? Gold : BoxLine, sel ? 3f : 1f, 10f);
            Txt(new Rect(r.x + 14f, r.y + 8f, cw - 28f, 34f), "[" + (i + 1) + "]  " + o.title, 22, sel ? Gold : Color.white, TextAnchor.MiddleLeft, true, false);
            Icon(new Rect(r.xMax - 54f, r.y + 4f, 40f, 40f), SpriteFactory.WeaponTile(o.title), Color.white);
            Txt(new Rect(r.x + 14f, r.y + 50f, cw - 28f, 130f), o.desc, 14, Color.white, TextAnchor.UpperLeft, false, true);
        }

        string keys = n > 1 ? "1 / 2" + (n > 2 ? " / 3" : "") + " or Left / Right to choose    |    " : "";
        Txt(new Rect(0f, 440f, vw, 30f), keys + "ENTER to confirm", 20, Color.white, TextAnchor.MiddleCenter, true, false);
    }

    void DrawPause()
    {
        Fill(new Rect(0f, 0f, vw, 720f), new Color(0f, 0f, 0f, 0.75f));
        Txt(new Rect(0f, 70f, vw, 60f), "PAUSED", 44, Color.white, TextAnchor.MiddleCenter, true, false);
        DrawControls(new Rect(vw / 2f - 410f, 160f, 820f, 210f));
        Txt(new Rect(0f, 400f, vw, 30f), "Press P or Esc to resume", 20, Gold, TextAnchor.MiddleCenter, true, false);
    }

    void DrawEnd(RoomDirector rd, bool won)
    {
        Fill(new Rect(0f, 0f, vw, 720f), new Color(0f, 0f, 0f, 0.8f));
        if (won)
        {
            Txt(new Rect(0f, 180f, vw, 70f), "STAGE 1 CLEAR!", 58, Gold, TextAnchor.MiddleCenter, true, false);
            Txt(new Rect(0f, 260f, vw, 30f), "The Warden is defeated and the rooms are clear.", 20, Color.white, TextAnchor.MiddleCenter, false, false);
            if (!string.IsNullOrEmpty(rd.EarnedWeapon))
                Txt(new Rect(0f, 294f, vw, 30f), "Secondary weapon earned: <b>" + rd.EarnedWeapon + "</b>  (Stage 2 would start here in the full game)", 18, Dim, TextAnchor.MiddleCenter, false, false);
        }
        else
        {
            Txt(new Rect(0f, 200f, vw, 70f), "GAME OVER", 58, new Color(1f, 0.35f, 0.35f), TextAnchor.MiddleCenter, true, false);
            Txt(new Rect(0f, 280f, vw, 30f), "You fell in  " + rd.Rooms[rd.RoomIndex].name, 20, Color.white, TextAnchor.MiddleCenter, false, false);
        }
        Txt(new Rect(0f, 360f, vw, 30f), "Press R to retry (same hero + weapon)     |     T for the title screen", 20, Color.white, TextAnchor.MiddleCenter, true, false);
    }
}