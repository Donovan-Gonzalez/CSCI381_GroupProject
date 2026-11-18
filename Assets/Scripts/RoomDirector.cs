using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public enum GState { Title, WeaponPick, Playing, Paused, Reward, Win, Lose }

// Owns the game flow: title -> pick a hero -> pick a primary weapon -> rooms (doors locked until enemies are cleared)
// -> boss -> win / game over. Clearing a room gives a reward (see CombatData for the progression).
// Also builds the room geometry (floor, walls, door) from code.
public class RoomDirector : MonoBehaviour
{
    public class RoomDef
    {
        public string name;
        public string tip;
        public FoeType[] foes;
    }

    // one choice on the reward screen
    public class RewardOption
    {
        public string title;
        public string desc;
        public System.Action apply;    // null = "keep what you have"
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
            tip = "Your attacks fire AUTOMATICALLY at the nearest enemy - just move! BRUTES resist SLASH but are weak to PUNCTURE. Clear the room to learn a new ACTION.",
            foes = new[] { FoeType.Brute, FoeType.Brute }
        },
        new RoomDef
        {
            name = "1-2  Archery Gallery",
            tip = "ARCHERS shrug off PUNCTURE but burn easily, and keep their distance. Clear the room to earn your first ELEMENT.",
            foes = new[] { FoeType.Archer, FoeType.Archer, FoeType.Brute }
        },
        new RoomDef
        {
            name = "1-3  Mage Sanctum",
            tip = "MAGES resist FIRE but crumple to SLASH. Their orbs aim where you're going - keep moving! Press UP / DOWN to switch element.",
            foes = new[] { FoeType.Mage, FoeType.Mage, FoeType.Archer }
        },
        new RoomDef
        {
            name = "BOSS  The Warden",
            tip = "The Warden's weakness CHANGES each phase. Watch his glowing aura and the WEAK label, then switch element with UP / DOWN!",
            foes = new[] { FoeType.Boss }
        }
    };

    public int RoomIndex { get; private set; }
    public int Remaining { get; private set; }
    public float RoomTime { get; private set; }
    public bool DoorOpen { get; private set; }
    public HeroController Hero { get; private set; }
    public int SelectedChar { get; private set; }
    public int SelectedWeapon { get; private set; }     // index into the selected hero's CharacterDef.weapons

    // reward screen
    public string RewardTitle { get; private set; }
    public string RewardSub { get; private set; }
    public readonly List<RewardOption> RewardOptions = new List<RewardOption>();
    public int RewardSel { get; private set; }
    public string EarnedWeapon { get; private set; }    // shown on the win screen
    GState afterReward;
    bool rewardPending;     // a room reward is about to show: keep the door shut until it's picked

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
                if (kb.digit1Key.wasPressedThisFrame) SelectedChar = 0;
                if (kb.digit2Key.wasPressedThisFrame) SelectedChar = 1;
                if (kb.leftArrowKey.wasPressedThisFrame || kb.rightArrowKey.wasPressedThisFrame)
                    SelectedChar = (SelectedChar + 1) % CombatData.Roster.Length;
                if (kb.enterKey.wasPressedThisFrame || kb.spaceKey.wasPressedThisFrame)
                {
                    SelectedWeapon = 0;
                    SetState(GState.WeaponPick);
                }
                break;

            case GState.WeaponPick:
            {
                int n = CombatData.Roster[SelectedChar].weapons.Length;
                if (kb.digit1Key.wasPressedThisFrame) SelectedWeapon = 0;
                if (kb.digit2Key.wasPressedThisFrame && n > 1) SelectedWeapon = 1;
                if (kb.rightArrowKey.wasPressedThisFrame) SelectedWeapon = (SelectedWeapon + 1) % n;
                if (kb.leftArrowKey.wasPressedThisFrame) SelectedWeapon = (SelectedWeapon + n - 1) % n;
                if (kb.escapeKey.wasPressedThisFrame || kb.backspaceKey.wasPressedThisFrame) SetState(GState.Title);
                else if (kb.enterKey.wasPressedThisFrame || kb.spaceKey.wasPressedThisFrame) StartGame();
                break;
            }

            case GState.Reward:
            {
                int n = RewardOptions.Count;
                if (n == 0) { SetState(afterReward); break; }
                if (kb.digit1Key.wasPressedThisFrame) RewardSel = 0;
                if (kb.digit2Key.wasPressedThisFrame && n > 1) RewardSel = 1;
                if (kb.digit3Key.wasPressedThisFrame && n > 2) RewardSel = 2;
                if (kb.rightArrowKey.wasPressedThisFrame) RewardSel = (RewardSel + 1) % n;
                if (kb.leftArrowKey.wasPressedThisFrame) RewardSel = (RewardSel + n - 1) % n;
                if (kb.enterKey.wasPressedThisFrame)      // Enter only, so Space doesn't also trigger the ability
                {
                    RewardOption o = RewardOptions[RewardSel];
                    if (o.apply != null) o.apply();
                    SetState(afterReward);
                }
                break;
            }

            case GState.Playing:
                if (kb.pKey.wasPressedThisFrame || kb.escapeKey.wasPressedThisFrame) SetState(GState.Paused);
                RoomTime += Time.deltaTime;
                if (DoorOpen && !rewardPending && Hero != null && Hero.transform.position.x > Center.x + RoomW / 2f + 0.3f) NextRoom();
                break;

            case GState.Paused:
                if (kb.pKey.wasPressedThisFrame || kb.escapeKey.wasPressedThisFrame) SetState(GState.Playing);
                break;

            case GState.Win:
            case GState.Lose:
                if (kb.rKey.wasPressedThisFrame) StartGame();                 // retry with the same hero
                if (kb.tKey.wasPressedThisFrame) { ClearWorld(); SetState(GState.Title); }
                break;
        }
    }

    void ClearWorld()
    {
        CancelInvoke();         // drop any reward / win that was still scheduled
        rewardPending = false;
        foreach (FoeController f in FindObjectsByType<FoeController>()) Destroy(f.gameObject);
        foreach (BoltProjectile b in FindObjectsByType<BoltProjectile>()) Destroy(b.gameObject);
        foreach (AreaStrike a in FindObjectsByType<AreaStrike>()) Destroy(a.gameObject);
        if (Hero != null) Destroy(Hero.gameObject);
        Hero = null;
        HudView.ClearFloats();
    }

    void StartGame()
    {
        ClearWorld();

        GameObject h = new GameObject("Hero");
        Hero = h.AddComponent<HeroController>();
        CharacterDef c = CombatData.Roster[SelectedChar];
        Hero.Setup(c, c.weapons[SelectedWeapon]);
        EarnedWeapon = null;

        LoadRoom(0);
        SetState(GState.Playing);
    }

    void LoadRoom(int index)
    {
        RoomIndex = index;
        RoomTime = 0f;

        foreach (FoeController f in FindObjectsByType<FoeController>()) Destroy(f.gameObject);
        foreach (BoltProjectile b in FindObjectsByType<BoltProjectile>()) Destroy(b.gameObject);
        foreach (AreaStrike a in FindObjectsByType<AreaStrike>()) Destroy(a.gameObject);

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
            Invoke("BossCleared", 1.2f);                 // boss down -> secondary weapon reward -> win screen
        }
        else
        {
            SetDoor(true);                               // room cleared -> unlock the door and heal a bit
            Hero.Heal(15f);
            HudView.Float(Hero.transform.position + Vector3.up * 1.6f, "DOOR UNLOCKED", new Color(0.5f, 1f, 0.6f));
            rewardPending = true;
            Invoke("RoomReward", 0.6f);
        }
    }

    // ---------------------------------------------------------------- rewards
    void OfferReward(string title, string sub, List<RewardOption> options, GState after)
    {
        RewardTitle = title;
        RewardSub = sub;
        RewardOptions.Clear();
        RewardOptions.AddRange(options);
        RewardSel = 0;
        afterReward = after;
        SetState(options.Count > 0 ? GState.Reward : after);
    }

    // room 1 -> an ACTION for the primary weapon, rooms 2 and 3 -> an ELEMENT
    void RoomReward()
    {
        rewardPending = false;
        if (State != GState.Playing || Hero == null) return;

        HeroController.OwnedWeapon w = Hero.Weapons[0];
        WeaponDef wd = w.Def;
        List<RewardOption> opts = new List<RewardOption>();

        if (RoomIndex == 0)
        {
            for (int i = 1; i < wd.actions.Length; i++)
            {
                int a = i;      // copy for the closure
                FunctionDef f = wd.actions[a];
                opts.Add(new RewardOption
                {
                    title = f.name,
                    desc = f.desc + "\n" + CombatData.StatLine(f) + "\n" + AttrList(f),
                    apply = () => Hero.SetAction(0, a)
                });
            }
            FunctionDef d = wd.actions[0];
            opts.Add(new RewardOption
            {
                title = "Keep " + d.name,
                desc = d.desc + "\n" + CombatData.StatLine(d) + "\n" + AttrList(d),
                apply = null
            });
            OfferReward("ROOM CLEARED  -  CHOOSE AN ACTION",
                        "This decides how your " + wd.name + " attacks for the rest of the run.", opts, GState.Playing);
        }
        else
        {
            for (int e = 0; e < CombatData.ElementNames.Length; e++)
            {
                if (w.elements.Contains(e)) continue;
                int el = e;
                FunctionDef f = CombatData.Resolve(w.id, w.action, el);
                opts.Add(new RewardOption
                {
                    title = CombatData.ElementNames[el],
                    desc = "Adds " + CombatData.ElementEffect[el] + "\nYour attack becomes: " + f.name + "\nSwitch with UP / DOWN",
                    apply = () => Hero.AddElement(0, el)
                });
            }
            OfferReward("ROOM CLEARED  -  " + (opts.Count > 1 ? "CHOOSE AN ELEMENT" : "NEW ELEMENT"),
                        "Elements add an attribute and a special effect to your " + wd.name + ".", opts, GState.Playing);
        }
    }

    // stage boss -> pick a SECONDARY weapon from the class's other weapons
    void BossCleared()
    {
        if (State != GState.Playing || Hero == null) return;

        List<RewardOption> opts = new List<RewardOption>();
        foreach (int id in Hero.Character.weapons)
        {
            if (Hero.HasWeapon(id)) continue;
            int wid = id;
            WeaponDef wd = CombatData.Weapons[wid];
            opts.Add(new RewardOption
            {
                title = wd.name,
                desc = wd.blurb,
                apply = () => { Hero.AddWeapon(wid); EarnedWeapon = wd.name; }
            });
        }
        OfferReward("STAGE CLEARED  -  CHOOSE A SECONDARY WEAPON",
                    "Switch weapons with LEFT / RIGHT. It starts with its default action.", opts, GState.Win);
    }

    static string AttrList(FunctionDef f)
    {
        string s = "";
        foreach (Attr a in f.attrs) s += (s == "" ? "" : " + ") + "<color=#" + CombatData.AttrHex(a) + ">" + a.ToString().ToUpper() + "</color>";
        return s;
    }

    public void PlayerDied()
    {
        SetState(GState.Lose);
    }
}