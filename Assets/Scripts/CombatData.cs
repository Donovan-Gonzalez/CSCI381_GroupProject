using System.Collections.Generic;
using UnityEngine;

// ---------------------------------------------------------------------------
//  All the "design data" lives here so teammates can tune it in one place:
//  weapons, their actions, elements, and the characters.
//
//  Progression inside one stage (per weapon):
//    start        -> pick a primary weapon (it uses its default action)
//    room 1 clear -> pick an ACTION (keep the default, or switch to one of the two alternates).
//                    The action decides the weapon's attack style from then on.
//    room 2 clear -> pick an ELEMENT (Ignite or Chill)
//    room 3 clear -> get the other ELEMENT
//    boss clear   -> pick a SECONDARY weapon from the class's other weapons
//  In game: Left / Right = switch weapon, Up / Down = switch element (none / Ignite / Chill).
// ---------------------------------------------------------------------------

public enum Attr { Slash, Puncture, Fire, Frozen }               // attack attributes (used to hit weaknesses)
public enum FnKind { Arc, Thrust, Projectile, Volley, Boomerang, Rain }   // how an action behaves
public enum ShotLook { Orb, Arrow, Knife, Crescent, Axe }        // how a projectile looks
public enum HeroAbility { Dash, Surge }                          // character abilities

// One attack ("action"): stats + attributes. Attacks fire automatically at the nearest enemy.
public class FunctionDef
{
    public string name;
    public string desc;
    public FnKind kind;
    public ShotLook look;
    public float damage;
    public float reach;       // melee reach (world units)
    public float arc;         // Arc: angle in degrees (360 = full circle)
    public float width;       // Thrust: half-width of the stab line
    public float speed;       // projectile speed
    public float range;       // ranged: how close an enemy must be before the hero auto-fires
    public float cooldown;
    public float radius;      // projectile hit radius
    public float aoe;         // Rain: circle radius
    public int pierce;        // extra enemies a projectile can pass through
    public int count = 1;     // Volley: number of projectiles
    public float spread;      // Volley: degrees between projectiles
    public float duration;    // Rain: how long the zone lasts (seconds)
    public float tick;        // Rain: seconds between damage ticks
    public List<Attr> attrs = new List<Attr>();
    public int element = -1;  // -1 = no element, otherwise index into CombatData.ElementNames

    public bool IsMelee { get { return kind == FnKind.Arc || kind == FnKind.Thrust; } }

    // the distance at which the hero starts attacking an enemy
    public float EngageRange { get { return IsMelee ? reach : range; } }

    public FunctionDef Clone()
    {
        FunctionDef f = (FunctionDef)MemberwiseClone();
        f.attrs = new List<Attr>(attrs);
        return f;
    }
}

public class WeaponDef
{
    public string name;
    public string blurb;            // shown on the weapon pick / reward screens
    public FunctionDef[] actions;   // [0] = default action, [1] and [2] = learnable alternates
}

public class CharacterDef
{
    public string name;
    public string blurb;
    public Color color;
    public int[] weapons;           // the class's weapons (index into CombatData.Weapons); pick 1 at the start
    public HeroAbility ability;
}

public static class CombatData
{
    // ----- Elements (rewarded in rooms 2 and 3). Fire = burn over time, Frozen = slow. -----
    public static readonly string[] ElementNames = { "Ignite", "Chill" };
    public static readonly Attr[] ElementAttrs = { Attr.Fire, Attr.Frozen };
    static readonly string[] ElementPrefix = { "Flame", "Frost" };
    public static readonly string[] ElementEffect = { "FIRE: burns for 3s", "FROZEN: slows for 2.5s" };

    public const float BurnDps = 4f;
    public const float BurnTime = 3f;
    public const float SlowTime = 2.5f;

    // ----- helpers to build actions -----
    static FunctionDef ArcFn(string name, string desc, float dmg, float reach, float arc, float cd, params Attr[] a)
    {
        FunctionDef f = new FunctionDef { name = name, desc = desc, kind = FnKind.Arc, damage = dmg, reach = reach, arc = arc, cooldown = cd };
        f.attrs.AddRange(a);
        return f;
    }

    static FunctionDef ThrustFn(string name, string desc, float dmg, float reach, float halfWidth, float cd, params Attr[] a)
    {
        FunctionDef f = new FunctionDef { name = name, desc = desc, kind = FnKind.Thrust, damage = dmg, reach = reach, width = halfWidth, cooldown = cd };
        f.attrs.AddRange(a);
        return f;
    }

    static FunctionDef ShotFn(string name, string desc, ShotLook look, float dmg, float speed, float range, float radius,
                              int pierce, float cd, params Attr[] a)
    {
        FunctionDef f = new FunctionDef
        {
            name = name, desc = desc, kind = FnKind.Projectile, look = look, damage = dmg, speed = speed,
            range = range, radius = radius, pierce = pierce, cooldown = cd
        };
        f.attrs.AddRange(a);
        return f;
    }

    // Weapon ids (index into Weapons)
    public const int Sword = 0, Axe = 1, Dagger = 2, Bow = 3;

    public static readonly WeaponDef[] Weapons =
    {
        // ---------- Sword: fast, straight stab ----------
        new WeaponDef
        {
            name = "Sword",
            blurb = "Fast, straight THRUST that pierces a whole line.\nActions to learn: Swing (fan) / Sword Wave (ranged crescent)",
            actions = new[]
            {
                ThrustFn("Thrust", "Straight stab that pierces every enemy in a line", 12f, 2.3f, 0.35f, 0.38f, Attr.Puncture),
                ArcFn("Swing", "Fan-shaped slash in front of you", 11f, 1.8f, 120f, 0.42f, Attr.Slash),
                ShotFn("Sword Wave", "Crescent wave that flies through enemies", ShotLook.Crescent, 10f, 9f, 7.5f, 0.45f, 99, 0.7f, Attr.Slash)
            }
        },
        // ---------- Axe: slow, big area ----------
        new WeaponDef
        {
            name = "Axe",
            blurb = "Slow, heavy SWING that hits a wide fan.\nActions to learn: Spin (full circle) / Axe Throw (comes back)",
            actions = new[]
            {
                ArcFn("Swing", "Wide, heavy chop in front of you", 22f, 2.1f, 150f, 0.95f, Attr.Slash),
                ArcFn("Spin", "Spin attack that hits everything around you", 18f, 2.2f, 360f, 1.05f, Attr.Slash),
                new FunctionDef
                {
                    name = "Axe Throw", desc = "Thrown axe that flies out and returns to you", kind = FnKind.Boomerang,
                    look = ShotLook.Axe, damage = 16f, speed = 10f, range = 5.5f, radius = 0.35f, pierce = 99,
                    cooldown = 1.3f, attrs = new List<Attr> { Attr.Slash }
                }
            }
        },
        // ---------- Dagger: very fast, short range ----------
        new WeaponDef
        {
            name = "Dagger",
            blurb = "Very fast, short-range SWING (small fan).\nActions to learn: Stab (straight thrust) / Knife Throw (ranged)",
            actions = new[]
            {
                ArcFn("Swing", "Quick, short slash in front of you", 7f, 1.35f, 100f, 0.22f, Attr.Slash),
                ThrustFn("Stab", "Quick straight stab that pierces a short line", 8f, 2.0f, 0.25f, 0.22f, Attr.Puncture),
                ShotFn("Knife Throw", "Throws a fast knife at range", ShotLook.Knife, 7f, 15f, 8f, 0.12f, 0, 0.32f, Attr.Puncture)
            }
        },
        // ---------- Bow: medium speed, ranged ----------
        new WeaponDef
        {
            name = "Bow",
            blurb = "Medium-speed single SHOT with high damage.\nActions to learn: Triple Shot (fan of 3) / Arrow Rain (area)",
            actions = new[]
            {
                ShotFn("Shot", "One arrow, high single-target damage", ShotLook.Arrow, 16f, 14f, 10f, 0.12f, 0, 0.6f, Attr.Puncture),
                new FunctionDef
                {
                    name = "Triple Shot", desc = "Three arrows in a fan - damage is spread out", kind = FnKind.Volley,
                    look = ShotLook.Arrow, damage = 7f, speed = 13f, range = 9f, radius = 0.12f, count = 3, spread = 14f,
                    cooldown = 0.7f, attrs = new List<Attr> { Attr.Puncture }
                },
                new FunctionDef
                {
                    name = "Arrow Rain", desc = "Arrows keep raining on a circle - damages everything inside over time", kind = FnKind.Rain,
                    damage = 3f, tick = 0.4f, duration = 2.4f, range = 8f, aoe = 1.9f, cooldown = 1.6f,
                    attrs = new List<Attr> { Attr.Puncture }
                }
            }
        }
    };

    // ----- Characters: each class has its own weapons; the player picks one as the primary -----
    // TODO (design): each class should get a 3rd weapon so the boss reward is a real choice of 2.
    public static readonly CharacterDef[] Roster =
    {
        new CharacterDef
        {
            name = "Vanguard",
            blurb = "Weapons: Sword / Axe\nAbility: Dash (2 charges that refill).\nDash, then a melee hit = Dash Slash.",
            color = new Color(0.30f, 0.65f, 1f),
            weapons = new[] { Sword, Axe },
            ability = HeroAbility.Dash
        },
        new CharacterDef
        {
            name = "Ember",
            blurb = "Weapons: Bow / Dagger\nAbility: Surge (4s of much faster attacks,\nthen a long cooldown).",
            color = new Color(1f, 0.55f, 0.20f),
            weapons = new[] { Bow, Dagger },
            ability = HeroAbility.Surge
        }
    };

    public static Color AttrColor(Attr a)
    {
        switch (a)
        {
            case Attr.Slash: return new Color(0.85f, 0.95f, 1f);
            case Attr.Puncture: return new Color(0.50f, 0.88f, 0.55f);
            case Attr.Fire: return new Color(1f, 0.60f, 0.24f);
            default: return new Color(0.56f, 0.83f, 1f);
        }
    }

    public static string AttrHex(Attr a)
    {
        switch (a)
        {
            case Attr.Slash: return "d9f2ff";
            case Attr.Puncture: return "80e08c";
            case Attr.Fire: return "ff9a3d";
            default: return "8fd4ff";
        }
    }

    // Final attack = the weapon's chosen action, plus an element (-1 = none).
    public static FunctionDef Resolve(int weaponId, int action, int element)
    {
        FunctionDef f = Weapons[weaponId].actions[action].Clone();
        if (element >= 0)
        {
            f.element = element;
            f.name = ElementPrefix[element] + " " + f.name;
            Attr add = ElementAttrs[element];
            if (!f.attrs.Contains(add)) f.attrs.Add(add);
        }
        return f;
    }

    // "DMG 12 · RCH 2.3 · CD 0.38s" style summary used by the HUD and reward cards
    public static string StatLine(FunctionDef f)
    {
        string dmg = f.kind == FnKind.Volley ? f.count + "x" + f.damage.ToString("0")
                   : f.kind == FnKind.Rain ? Mathf.RoundToInt(f.duration / f.tick) + "x" + f.damage.ToString("0")   // ticks x damage
                   : f.damage.ToString("0");
        string second;
        if (f.kind == FnKind.Rain) second = "AOE " + f.aoe.ToString("0.#");
        else if (f.IsMelee) second = "RCH " + f.reach.ToString("0.#");
        else second = "RNG " + f.range.ToString("0.#");
        return "DMG " + dmg + " · " + second + " · CD " + f.cooldown.ToString("0.0#") + "s";
    }
}
