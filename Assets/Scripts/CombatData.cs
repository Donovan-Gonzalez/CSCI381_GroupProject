using System.Collections.Generic;
using UnityEngine;

// ---------------------------------------------------------------------------
//  All the "design data" lives here so teammates can tune it in one place:
//  weapons, functions, techniques, and the characters.
// ---------------------------------------------------------------------------

public enum Attr { Slash, Puncture, Fire, Frozen }          // function attributes (used to hit weaknesses)
public enum FnKind { Arc, Projectile, Volley, Block }       // how a function behaves
public enum HeroAbility { Dash, Surge }                     // character abilities

// A function = what a weapon does (Swing, Shot, Block ...). Each has stats + attributes.
public class FunctionDef
{
    public string name;
    public FnKind kind;
    public float damage;
    public float reach;       // melee arc reach (world units)
    public float arc;         // melee arc angle (degrees)
    public float speed;       // projectile speed
    public float cooldown;
    public float stamina;
    public float radius;      // projectile radius
    public float aoe;         // explosion radius (0 = none)
    public int pierce;        // extra enemies a projectile can pass through
    public List<Attr> attrs = new List<Attr>();

    public bool special;      // true when a technique changed this function
    public bool chargeable;   // Charge technique: hold the button, release to fire
    public bool noCombo;      // a technique is equipped but doesn't combine with this function

    public FunctionDef Clone()
    {
        FunctionDef f = (FunctionDef)MemberwiseClone();
        f.attrs = new List<Attr>(attrs);
        return f;
    }
}

public class CharacterDef
{
    public string name;
    public string blurb;
    public Color color;
    public int weapon;        // starting weapon index
    public int tech;          // starting technique index
    public HeroAbility ability;
}

public static class CombatData
{
    public static readonly string[] WeaponNames = { "Sword & Shield", "Bow", "Staff" };
    public static readonly string[] TechNames = { "None", "Charge", "Ignite", "Chill" };

    // ----- Characters (each starts with a different loadout and ability) -----
    public static readonly CharacterDef[] Roster =
    {
        new CharacterDef
        {
            name = "Vanguard",
            blurb = "Sword & Shield + Charge.\nAbility: Dash (2 charges that refill).\nDash then Swing = Dash Slash.",
            color = new Color(0.30f, 0.65f, 1f),
            weapon = 0, tech = 1, ability = HeroAbility.Dash
        },
        new CharacterDef
        {
            name = "Ember",
            blurb = "Bow + Ignite (fire arrows).\nAbility: Surge (4s of fast cooldowns\nand stamina regen, then a long cooldown).",
            color = new Color(1f, 0.55f, 0.20f),
            weapon = 1, tech = 2, ability = HeroAbility.Surge
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

    // ----- Base functions: [weapon, 0 = primary / 1 = secondary] -----
    static FunctionDef Fn(string name, FnKind kind, float dmg, float reach, float arc, float speed,
                          float cd, float stam, float radius, params Attr[] attrs)
    {
        FunctionDef f = new FunctionDef();
        f.name = name; f.kind = kind; f.damage = dmg; f.reach = reach; f.arc = arc; f.speed = speed;
        f.cooldown = cd; f.stamina = stam; f.radius = radius;
        f.attrs.AddRange(attrs);
        return f;
    }

    static readonly FunctionDef[,] baseFns =
    {
        // Sword & Shield
        { Fn("Swing", FnKind.Arc, 18f, 1.7f, 110f, 0f, 0.42f, 12f, 0f, Attr.Slash),
          Fn("Block", FnKind.Block, 0f, 0f, 0f, 0f, 0f, 0f, 0f) },
        // Bow
        { Fn("Shot", FnKind.Projectile, 14f, 0f, 0f, 13f, 0.50f, 10f, 0.12f, Attr.Puncture),
          Fn("Volley", FnKind.Volley, 8f, 0f, 0f, 12f, 1.10f, 24f, 0.11f, Attr.Puncture) },
        // Staff
        { Fn("Fire Bolt", FnKind.Projectile, 16f, 0f, 0f, 9.5f, 0.60f, 14f, 0.20f, Attr.Fire),
          Fn("Frost Shard", FnKind.Projectile, 8f, 0f, 0f, 10.5f, 0.45f, 11f, 0.15f, Attr.Frozen) }
    };

    // Applies the equipped technique to a weapon function and returns the final function.
    //   slot: 0 = primary, 1 = secondary.   charged: only matters for the Charge technique.
    public static FunctionDef Resolve(int weapon, int slot, int tech, bool charged)
    {
        FunctionDef f = baseFns[weapon, slot].Clone();

        // Technique 1: Charge (primary only) -> hold to charge, release for a stronger version
        if (tech == 1)
        {
            if (slot != 0) { f.noCombo = true; return f; }

            f.chargeable = true;
            f.special = true;
            if (charged)
            {
                f.stamina *= 1.6f;
                if (f.name == "Swing")
                {
                    f.name = "Charged Swing"; f.damage *= 2.2f; f.reach *= 1.4f; f.arc = 170f; f.cooldown *= 1.3f;
                }
                else if (f.name == "Shot")
                {
                    f.name = "Charged Shot"; f.damage *= 2.4f; f.speed = 20f; f.pierce = 3;
                }
                else if (f.name == "Fire Bolt")
                {
                    f.name = "Fireball"; f.damage *= 1.8f; f.aoe = 2.2f; f.radius = 0.35f;
                }
            }
            return f;
        }

        // Techniques 2 and 3: Ignite / Chill -> add an attribute (and rename) when they combine
        if (tech == 2 || tech == 3)
        {
            bool fire = (tech == 2);
            string newName = null;

            if (f.name == "Swing") newName = fire ? "Flame Swing" : "Frost Swing";
            else if (f.name == "Shot") newName = fire ? "Fire Arrow" : "Ice Arrow";
            else if (f.name == "Volley") newName = fire ? "Fire Volley" : "Ice Volley";

            if (newName == null) { f.noCombo = true; return f; }   // e.g. Block, Fire Bolt, Frost Shard

            f.name = newName;
            Attr add = fire ? Attr.Fire : Attr.Frozen;
            if (!f.attrs.Contains(add)) f.attrs.Add(add);
            f.special = true;
        }

        return f;
    }
}
