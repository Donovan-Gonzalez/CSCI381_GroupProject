using UnityEngine;
using UnityEngine.InputSystem;

public enum GState { Title, Playing, Paused, Win, Lose, Keybinds }

// Owns the game flow: title -> rooms (doors locked until enemies are cleared) -> boss -> win / game over.
// Also builds the room geometry (floor, walls, door) from code.
public class RoomDirector : MonoBehaviour
{
    public class RoomDef
    {
        public string name;
        public string tip;
        public FoeType[] foes;
    }

    public static RoomDirector Instance { get; private set; }
    public static GState State { get; private set; }

    public const float RoomW = 16f;
    public const float RoomH = 8f;
    public static readonly Vector2 Center = new Vector2(0f, -0.4f);

    // ---- The mock has 4 rooms. The full game (per the proposal) would be 3 stages x 3 rooms + boss. ----
    public readonly RoomDef[] Rooms =
    {
        new RoomDef
        {
            name = "1-1  Training Hall",
            tip = "BRUTES are armored: they resist SLASH but are weak to PUNCTURE. Press the RIGHT ARROW to equip the Bow!",
            foes = new[] { FoeType.Brute, FoeType.Brute }
        },
        new RoomDef
        {
            name = "1-2  Archery Gallery",
            tip = "ARCHERS shrug off PUNCTURE but burn easily. Press UP/DOWN to equip IGNITE and add FIRE to your attacks!",
            foes = new[] { FoeType.Archer, FoeType.Archer, FoeType.Brute }
        },
        new RoomDef
        {
            name = "1-3  Mage Sanctum",
            tip = "MAGES resist FIRE but crumple to SLASH. Close the distance, use Sword & Shield, and BLOCK their orbs (hold right mouse).",
            foes = new[] { FoeType.Mage, FoeType.Mage, FoeType.Archer }
        },
        new RoomDef
        {
            name = "BOSS  The Warden",
            tip = "The Warden's weakness CHANGES each phase. Watch his glowing aura and the WEAK label, then switch weapons!",
            foes = new[] { FoeType.Boss }
        }
    };

    public int RoomIndex { get; private set; }
    public int Remaining { get; private set; }
    public float RoomTime { get; private set; }
    public bool DoorOpen { get; private set; }
    public HeroController Hero { get; private set; }
    public int SelectedChar { get; private set; }
    public int KeybindSel { get; private set; }      // highlighted row in the keybinds menu
    GState keybindsReturn;                           // screen to go back to when the menu closes
    bool enterArmed;                                 // Enter was pressed in the menu (not while rebinding)

    GameObject doorObj;
    BoxCollider2D doorCol;
    SpriteRenderer doorSr;

    void Awake()
    {
        Instance = this;
        SetState(GState.Title);
        BuildRoom();
    }

    static void SetState(GState s)
    {
        State = s;
        Time.timeScale = (s == GState.Playing) ? 1f : 0f;
    }

    // ---------------------------------------------------------------- room geometry
    void BuildRoom()
    {
        Transform root = new GameObject("RoomGeometry").transform;

        // checkered floor
        for (int x = 0; x < 8; x++)
        {
            for (int y = 0; y < 4; y++)
            {
                bool alt = (x + y) % 2 == 0;
                Color c = alt ? new Color(0.14f, 0.15f, 0.20f) : new Color(0.17f, 0.18f, 0.24f);
                GameObject tile = SpriteFactory.Make("Tile", SpriteFactory.Square, c, new Vector2(2f, 2f), -10, root);
                tile.transform.position = new Vector3(Center.x - RoomW / 2f + 1f + x * 2f, Center.y - RoomH / 2f + 1f + y * 2f, 0f);

                // 4 small dirt tiles on top of each checker square (skipped if the sprite sheet isn't found)
                for (int fx = 0; fx < 2; fx++)
                {
                    for (int fy = 0; fy < 2; fy++)
                    {
                        Sprite dirt = SpriteFactory.Tile(SpriteFactory.TileFloor[(x * 3 + y * 5 + fx * 7 + fy * 11) % SpriteFactory.TileFloor.Length]);
                        if (dirt == null) continue;
                        GameObject d = SpriteFactory.Make("Floor", dirt, new Color(0.45f, 0.50f, 0.68f, 0.55f), Vector2.one, -9, root);
                        d.transform.position = tile.transform.position + new Vector3(fx - 0.5f, fy - 0.5f, 0f);
                    }
                }
            }
        }

        Color wall = new Color(0.32f, 0.34f, 0.44f);
        float hx = RoomW / 2f;
        float hy = RoomH / 2f;
        float gap = 2.2f;

        Wall(root, Center.x, Center.y + hy + 0.5f, RoomW + 2f, 1f, wall);                 // top
        Wall(root, Center.x, Center.y - hy - 0.5f, RoomW + 2f, 1f, wall);                 // bottom
        Wall(root, Center.x - hx - 0.5f, Center.y, 1f, RoomH, wall);                      // left

        float segH = hy - gap / 2f;                                                       // right wall, with a doorway
        Wall(root, Center.x + hx + 0.5f, Center.y + gap / 2f + segH / 2f, 1f, segH, wall);
        Wall(root, Center.x + hx + 0.5f, Center.y - gap / 2f - segH / 2f, 1f, segH, wall);

        // door blocker: red + solid while locked, green + passable when open
        doorObj = Wall(root, Center.x + hx + 0.5f, Center.y, 1f, gap, new Color(0.8f, 0.2f, 0.2f), false);
        doorCol = doorObj.GetComponent<BoxCollider2D>();
        doorSr = doorObj.GetComponent<SpriteRenderer>();
    }

    GameObject Wall(Transform parent, float x, float y, float w, float h, Color color, bool brick = true)
    {
        GameObject go = SpriteFactory.Make("Wall", SpriteFactory.Square, color, new Vector2(w, h), 5, parent);
        go.transform.position = new Vector3(x, y, 0f);
        BoxCollider2D col = go.AddComponent<BoxCollider2D>();
        col.size = Vector2.one;        // scaled by the transform

        // brick tiles along the wall (the collider/square stays as the invisible hitbox)
        Sprite brickSp = brick ? SpriteFactory.Tile(SpriteFactory.TileWall) : null;
        if (brickSp != null)
        {
            go.GetComponent<SpriteRenderer>().enabled = false;
            bool horiz = w >= h;
            int n = Mathf.Max(1, Mathf.CeilToInt((horiz ? w : h) - 0.01f));
            float stepW = horiz ? w / n : w;
            float stepH = horiz ? h : h / n;
            for (int i = 0; i < n; i++)
            {
                GameObject b = SpriteFactory.Make("Brick", brickSp, new Color(0.62f, 0.66f, 0.86f), new Vector2(stepW, stepH), 5, parent);
                float ox = horiz ? -w / 2f + stepW * (i + 0.5f) : 0f;
                float oy = horiz ? 0f : -h / 2f + stepH * (i + 0.5f);
                b.transform.position = new Vector3(x + ox, y + oy, 0f);
            }
        }
        return go;
    }

    void SetDoor(bool open)
    {
        DoorOpen = open;
        doorCol.enabled = !open;
        doorSr.color = open ? new Color(0.25f, 0.8f, 0.35f, 0.5f) : new Color(0.8f, 0.2f, 0.2f);
    }

    // ---------------------------------------------------------------- game flow
    void Update()
    {
        Keyboard kb = Keyboard.current;
        if (kb == null) return;

        switch (State)
        {
            case GState.Title:
                if (KeyBinds.PickChar1.WasPressedThisFrame()) SelectedChar = 0;
                if (KeyBinds.PickChar2.WasPressedThisFrame()) SelectedChar = 1;
                if (KeyBinds.CycleChar.WasPressedThisFrame())
                    SelectedChar = (SelectedChar + 1) % CombatData.Roster.Length;
                if (KeyBinds.Confirm.WasPressedThisFrame()) StartGame();
                if (kb.kKey.wasPressedThisFrame) OpenKeybinds();
                break;

            case GState.Playing:
                if (KeyBinds.Pause.WasPressedThisFrame()) SetState(GState.Paused);
                RoomTime += Time.deltaTime;
                if (DoorOpen && Hero != null && Hero.transform.position.x > Center.x + RoomW / 2f + 0.3f) NextRoom();
                break;

            case GState.Paused:
                if (KeyBinds.Pause.WasPressedThisFrame()) SetState(GState.Playing);
                if (kb.kKey.wasPressedThisFrame) OpenKeybinds();
                break;

            case GState.Keybinds:
                UpdateKeybindsMenu(kb);
                break;

            case GState.Win:
            case GState.Lose:
                if (KeyBinds.Retry.WasPressedThisFrame()) StartGame();                 // retry with the same hero
                if (KeyBinds.ToTitle.WasPressedThisFrame()) { ClearWorld(); SetState(GState.Title); }
                break;
        }
    }

    // ---------------------------------------------------------------- keybinds menu
    // The menu's own keys (arrows, Enter, Esc, Backspace) read the keyboard directly on purpose:
    // they are NOT rebindable, so a player can never lock themselves out of the menu.
    void OpenKeybinds()
    {
        keybindsReturn = State;
        KeybindSel = 0;
        enterArmed = false;
        SetState(GState.Keybinds);
    }

    void UpdateKeybindsMenu(Keyboard kb)
    {
        if (KeyBinds.IsRebinding) return;                              // waiting for the player's new key
        if (Time.frameCount == KeyBinds.LastRebindFrame) return;       // that key shouldn't also drive the menu

        int count = KeyBinds.Rows.Count;
        if (kb.downArrowKey.wasPressedThisFrame) KeybindSel = (KeybindSel + 1) % count;
        if (kb.upArrowKey.wasPressedThisFrame) KeybindSel = (KeybindSel + count - 1) % count;

        // start on RELEASE, so the Enter key itself isn't picked up as the new binding.
        // enterArmed makes sure it was pressed here, not as the new key at the end of a rebind.
        if (kb.enterKey.wasPressedThisFrame) enterArmed = true;
        if (kb.enterKey.wasReleasedThisFrame && enterArmed)
        {
            enterArmed = false;
            KeyBinds.StartRebind(KeyBinds.Rows[KeybindSel]);
        }
        if (kb.backspaceKey.wasPressedThisFrame) KeyBinds.ResetAll();
        if (kb.escapeKey.wasPressedThisFrame) SetState(keybindsReturn);
    }

    void ClearWorld()
    {
        foreach (FoeController f in FindObjectsByType<FoeController>()) Destroy(f.gameObject);
        foreach (BoltProjectile b in FindObjectsByType<BoltProjectile>()) Destroy(b.gameObject);
        if (Hero != null) Destroy(Hero.gameObject);
        Hero = null;
        HudView.ClearFloats();
    }

    void StartGame()
    {
        ClearWorld();

        GameObject h = new GameObject("Hero");
        Hero = h.AddComponent<HeroController>();
        Hero.Setup(CombatData.Roster[SelectedChar]);

        LoadRoom(0);
        SetState(GState.Playing);
    }

    void LoadRoom(int index)
    {
        RoomIndex = index;
        RoomTime = 0f;

        foreach (FoeController f in FindObjectsByType<FoeController>()) Destroy(f.gameObject);
        foreach (BoltProjectile b in FindObjectsByType<BoltProjectile>()) Destroy(b.gameObject);

        Hero.transform.position = new Vector3(Center.x - RoomW / 2f + 1.5f, Center.y, 0f);
        SetDoor(false);

        RoomDef def = Rooms[index];
        foreach (FoeType t in def.foes)
        {
            Vector2 p;
            int tries = 0;
            do
            {
                p = new Vector2(Random.Range(Center.x + 1f, Center.x + RoomW / 2f - 1.5f),
                                Random.Range(Center.y - RoomH / 2f + 1.2f, Center.y + RoomH / 2f - 1.2f));
                tries++;
            } while (Vector2.Distance(p, Hero.transform.position) < 5f && tries < 30);

            if (t == FoeType.Boss) p = new Vector2(Center.x + 4f, Center.y);

            GameObject go = new GameObject(t.ToString());
            go.transform.position = p;
            go.AddComponent<FoeController>().Setup(t, Hero);
        }
        Remaining = def.foes.Length;
    }

    void NextRoom()
    {
        if (RoomIndex + 1 < Rooms.Length) LoadRoom(RoomIndex + 1);
    }

    public void FoeDied(FoeController foe)
    {
        Remaining--;
        if (Remaining > 0) return;

        if (RoomIndex == Rooms.Length - 1)
        {
            Invoke("Win", 1.2f);                         // boss down -> win screen
        }
        else
        {
            SetDoor(true);                               // room cleared -> unlock the door and heal a bit
            Hero.Heal(15f);
            HudView.Float(Hero.transform.position + Vector3.up * 1.6f, "DOOR UNLOCKED", new Color(0.5f, 1f, 0.6f));
        }
    }

    void Win()
    {
        if (State == GState.Playing) SetState(GState.Win);
    }

    public void PlayerDied()
    {
        SetState(GState.Lose);
    }
}