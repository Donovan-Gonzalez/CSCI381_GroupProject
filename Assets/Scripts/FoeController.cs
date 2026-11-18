using System.Collections.Generic;
using UnityEngine;

public enum FoeType { Brute, Archer, Mage, Boss }

// Every enemy has an obvious strength (resists an attribute) and an exploitable weakness.
//   Brute : armored       -> resists SLASH,    weak to PUNCTURE   (slow, melee contact)
//   Archer: wind cloak    -> resists PUNCTURE, weak to FIRE       (fast, keeps its distance)
//   Mage  : fire barrier  -> resists FIRE,     weak to SLASH      (slow, long range, accurate; must be rushed)
//   Boss  : weakness CHANGES each phase (Slash -> Fire -> Puncture); the aura color shows the current weakness.
public class FoeController : MonoBehaviour
{
    public static readonly List<FoeController> All = new List<FoeController>();
    static readonly Attr[] BossWeak = { Attr.Slash, Attr.Fire, Attr.Puncture };

    public FoeType type;
    public float hp;
    public float maxHp;
    public float radius;
    public int phase;
    public bool IsDead { get { return dead; } }

    HeroController hero;
    Rigidbody2D rb;
    SpriteRenderer bodySr;
    SpriteRenderer auraSr;
    Color baseColor;

    float speed;
    float slowT, contactCd, shootT, windupT, flash, invuln;
    float burnT, burnTick;          // Fire element: damage over time
    float strafeDir = 1f;
    bool dead;
    bool windingUp;

    void OnEnable() { All.Add(this); }
    void OnDisable() { All.Remove(this); }

    // multipliers per attribute: <1 = resists, >1 = weak. Index = (int)Attr
    public float[] Mods()
    {
        float[] m = { 1f, 1f, 1f, 1f };
        switch (type)
        {
            case FoeType.Brute:
                m[(int)Attr.Slash] = 0.4f; m[(int)Attr.Puncture] = 2f; break;
            case FoeType.Archer:
                m[(int)Attr.Puncture] = 0.4f; m[(int)Attr.Fire] = 2f; break;
            case FoeType.Mage:
                m[(int)Attr.Fire] = 0.4f; m[(int)Attr.Slash] = 2f; break;
            case FoeType.Boss:
                m[(int)Attr.Slash] = 0.5f; m[(int)Attr.Puncture] = 0.5f; m[(int)Attr.Fire] = 0.5f; m[(int)Attr.Frozen] = 0.7f;
                m[(int)BossWeak[phase]] = 2.2f;
                break;
        }
        return m;
    }

    public void Setup(FoeType t, HeroController target)
    {
        type = t;
        hero = target;

        rb = gameObject.AddComponent<Rigidbody2D>();
        rb.gravityScale = 0f;
        rb.freezeRotation = true;
        CircleCollider2D col = gameObject.AddComponent<CircleCollider2D>();

        switch (t)
        {
            case FoeType.Brute:
                hp = 60f; speed = 1.8f; radius = 0.65f; baseColor = new Color(0.55f, 0.57f, 0.62f); rb.mass = 5f; break;
            case FoeType.Archer:
                hp = 36f; speed = 2.9f; radius = 0.40f; baseColor = new Color(0.30f, 0.70f, 0.42f); rb.mass = 1f; break;
            case FoeType.Mage:
                hp = 32f; speed = 1.4f; radius = 0.40f; baseColor = new Color(0.63f, 0.40f, 0.91f); rb.mass = 1f; break;
            default:
                hp = 420f; speed = 1.8f; radius = 1.3f; baseColor = new Color(0.92f, 0.92f, 0.95f); rb.mass = 30f; break;
        }
        maxHp = hp;
        col.radius = radius;
        shootT = Random.Range(0.8f, 1.8f);

        int tileId = t == FoeType.Brute ? SpriteFactory.TileBrute
                   : t == FoeType.Archer ? SpriteFactory.TileArcher
                   : t == FoeType.Mage ? SpriteFactory.TileMage : SpriteFactory.TileBoss;
        Sprite foeTile = SpriteFactory.Tile(tileId);
        if (foeTile != null) baseColor = Color.Lerp(Color.white, baseColor, 0.75f); // keep a class tint on the beige tile
        bodySr = SpriteFactory.Make("Body", foeTile != null ? foeTile : SpriteFactory.Circle, baseColor,
                                    Vector2.one * radius * (foeTile != null ? 2.6f : 2f), 10, transform)
                              .GetComponent<SpriteRenderer>();

        // little details so each type reads differently (only needed for the circle placeholders)
        if (foeTile != null && t != FoeType.Boss) return;
        switch (t)
        {
            case FoeType.Brute:
                SpriteFactory.Make("Plate", SpriteFactory.Square, new Color(0.82f, 0.84f, 0.9f), new Vector2(radius * 1.3f, radius * 0.45f), 11, transform);
                SpriteFactory.Make("Visor", SpriteFactory.Square, new Color(0.15f, 0.15f, 0.2f), new Vector2(radius * 0.9f, radius * 0.18f), 12, transform)
                             .transform.localPosition = new Vector3(0f, radius * 0.3f, 0f);
                break;
            case FoeType.Archer:
                GameObject hood = SpriteFactory.Make("Hood", SpriteFactory.Circle, new Color(0.18f, 0.45f, 0.26f), Vector2.one * radius * 1.2f, 11, transform);
                hood.transform.localPosition = new Vector3(0f, radius * 0.25f, 0f);
                GameObject bow = SpriteFactory.Make("Bow", SpriteFactory.Square, new Color(0.55f, 0.38f, 0.2f), new Vector2(0.1f, 0.8f), 11, transform);
                bow.transform.localPosition = new Vector3(radius * 0.8f, 0f, 0f);
                break;
            case FoeType.Mage:
                GameObject hat = SpriteFactory.Make("Hat", SpriteFactory.Square, new Color(0.35f, 0.18f, 0.6f), Vector2.one * radius * 1.1f, 11, transform);
                hat.transform.localPosition = new Vector3(0f, radius * 0.6f, 0f);
                hat.transform.localRotation = Quaternion.Euler(0f, 0f, 45f);
                break;
            default:
                auraSr = SpriteFactory.Make("Aura", SpriteFactory.Circle, Color.white, Vector2.one * radius * 2.6f, 9, transform)
                                      .GetComponent<SpriteRenderer>();
                if (foeTile == null)
                {
                    GameObject h1 = SpriteFactory.Make("HornL", SpriteFactory.Square, new Color(1f, 0.8f, 0.2f), new Vector2(0.25f, 0.8f), 11, transform);
                    h1.transform.localPosition = new Vector3(-0.7f, 1.1f, 0f);
                    h1.transform.localRotation = Quaternion.Euler(0f, 0f, 20f);
                    GameObject h2 = SpriteFactory.Make("HornR", SpriteFactory.Square, new Color(1f, 0.8f, 0.2f), new Vector2(0.25f, 0.8f), 11, transform);
                    h2.transform.localPosition = new Vector3(0.7f, 1.1f, 0f);
                    h2.transform.localRotation = Quaternion.Euler(0f, 0f, -20f);
                }
                break;
        }
    }

    void Update()
    {
        if (rb == null) return;
        if (RoomDirector.State != GState.Playing || dead || hero == null)
        {
            rb.linearVelocity = Vector2.zero;
            return;
        }

        float dt = Time.deltaTime;
        if (slowT > 0f) slowT -= dt;
        if (burnT > 0f)
        {
            burnT -= dt;
            burnTick -= dt;
            if (burnTick <= 0f && invuln <= 0f)
            {
                burnTick = 0.5f;
                float tick = CombatData.BurnDps * 0.5f;
                HudView.Float(transform.position + Vector3.up * (radius + 0.3f), Mathf.RoundToInt(tick).ToString(), new Color(1f, 0.55f, 0.2f));
                if (ApplyDamage(tick)) return;
            }
        }
        if (invuln > 0f) invuln -= dt;
        if (contactCd > 0f) contactCd -= dt;
        if (flash > 0f) flash -= dt;

        Vector2 pos = transform.position;
        Vector2 hpos = hero.transform.position;
        Vector2 to = hpos - pos;
        float dist = to.magnitude;
        Vector2 dir = dist > 0.001f ? to / dist : Vector2.right;
        Vector2 vel = Vector2.zero;

        switch (type)
        {
            case FoeType.Brute:
                vel = dir * speed;
                TryContact(pos, dist, 12f, 0.8f);
                break;

            case FoeType.Archer:
                if (dist < 4f) vel = -dir * speed;
                else if (dist > 6f) vel = dir * speed;
                else vel = new Vector2(-dir.y, dir.x) * strafeDir * speed * 0.6f;

                shootT -= dt;
                if (shootT <= 0f && dist < 11f)
                {
                    Vector2 aim = Quaternion.Euler(0f, 0f, Random.Range(-7f, 7f)) * dir;   // a little inaccuracy
                    BoltProjectile.Spawn(pos + dir * (radius + 0.2f), aim, 7f, 8f, null, 0.12f, false, new Color(0.5f, 0.9f, 0.5f), 0, 0f);
                    shootT = 1.7f;
                    if (Random.value < 0.4f) strafeDir = -strafeDir;
                }
                break;

            case FoeType.Mage:
                if (windingUp)
                {
                    windupT -= dt;                       // stands still and flashes before firing (telegraph)
                    flash = 0.05f;
                    if (windupT <= 0f)
                    {
                        windingUp = false;
                        // near-perfect accuracy: lead the target
                        float orbSpeed = 8f;
                        Vector2 predicted = hpos + hero.Velocity * (dist / orbSpeed);
                        Vector2 aim = (predicted - pos).normalized;
                        BoltProjectile.Spawn(pos + aim * (radius + 0.2f), aim, orbSpeed, 14f, null, 0.22f, false, new Color(0.8f, 0.5f, 1f), 0, 0f);
                        shootT = 2.3f;
                    }
                }
                else
                {
                    if (dist < 6f) vel = -dir * speed;
                    else if (dist > 8f) vel = dir * speed;
                    shootT -= dt;
                    if (shootT <= 0f) { windingUp = true; windupT = 0.5f; }
                }
                break;

            case FoeType.Boss:
                vel = dir * (speed * (1f + 0.3f * phase));
                TryContact(pos, dist, 18f, 1f);
                shootT -= dt;
                if (shootT <= 0f)
                {
                    Ring(8 + 2 * phase);
                    shootT = 2.6f - 0.4f * phase;
                }
                break;
        }

        rb.linearVelocity = vel * (slowT > 0f ? 0.4f : 1f);

        if (flash > 0f) bodySr.color = Color.white;
        else if (burnT > 0f) bodySr.color = Color.Lerp(baseColor, new Color(1f, 0.5f, 0.2f), 0.45f + 0.15f * Mathf.Sin(Time.time * 20f));
        else if (slowT > 0f) bodySr.color = Color.Lerp(baseColor, new Color(0.5f, 0.8f, 1f), 0.6f);
        else bodySr.color = baseColor;

        if (auraSr != null)
        {
            Color a = CombatData.AttrColor(BossWeak[phase]);
            a.a = 0.35f + 0.15f * Mathf.Sin(Time.time * 6f);   // pulsing aura = the current weak point
            auraSr.color = a;
        }
    }

    void TryContact(Vector2 pos, float dist, float dmg, float cooldown)
    {
        if (contactCd <= 0f && dist < radius + 0.55f)
        {
            hero.Hit(dmg, pos);
            contactCd = cooldown;
        }
    }

    void Ring(int n)
    {
        Vector2 pos = transform.position;
        float off = Random.Range(0f, 360f);
        for (int i = 0; i < n; i++)
        {
            float a = (off + i * 360f / n) * Mathf.Deg2Rad;
            Vector2 d = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
            BoltProjectile.Spawn(pos + d * (radius + 0.2f), d, 5f, 8f, null, 0.18f, false, new Color(1f, 0.4f, 0.25f), 0, 0f);
        }
    }

    // Called by the hero's functions. attrs decide whether this hit exploits a weakness.
    public void Damage(float dmg, List<Attr> attrs)
    {
        if (dead || RoomDirector.State != GState.Playing) return;

        Vector3 above = transform.position + Vector3.up * (radius + 0.5f);

        if (invuln > 0f)
        {
            HudView.Float(above, "SHIELDED", Color.gray);
            return;
        }

        float[] m = Mods();
        float mult = 0f;
        foreach (Attr a in attrs) mult = Mathf.Max(mult, m[(int)a]);   // best attribute counts
        if (attrs.Count == 0) mult = 1f;

        float final = dmg * mult;
        flash = 0.08f;

        if (mult >= 1.9f) HudView.Float(above, "WEAK! " + Mathf.RoundToInt(final), new Color(1f, 0.85f, 0.2f));
        else if (mult <= 0.5f) HudView.Float(above, "TOUGH " + Mathf.RoundToInt(final), new Color(0.7f, 0.7f, 0.75f));
        else HudView.Float(above, Mathf.RoundToInt(final).ToString(), Color.white);

        // element special effects
        if (attrs.Contains(Attr.Frozen)) slowT = CombatData.SlowTime;                    // Frozen = slows the target
        if (attrs.Contains(Attr.Fire)) { if (burnT <= 0f) burnTick = 0.5f; burnT = CombatData.BurnTime; }   // Fire = burns

        ApplyDamage(final);
    }

    // Removes hp, handles death and boss phases. Returns true if this foe died.
    bool ApplyDamage(float final)
    {
        if (dead) return true;
        hp -= final;

        if (hp <= 0f)
        {
            dead = true;
            FadeSprite.Spawn(transform.position, Vector2.one * radius * 2.4f, new Color(baseColor.r, baseColor.g, baseColor.b, 0.7f), 0.35f, true);
            RoomDirector.Instance.FoeDied(this);
            Destroy(gameObject);
            return true;
        }

        // boss phases: every phase has a different weakness
        if (type == FoeType.Boss)
        {
            float ratio = hp / maxHp;
            int np = ratio > 0.66f ? 0 : (ratio > 0.33f ? 1 : 2);
            if (np > phase)
            {
                phase = np;
                invuln = 1f;
                HudView.Float(transform.position + Vector3.up * 2.4f,
                    "PHASE " + (phase + 1) + "! Weak to " + BossWeak[phase].ToString().ToUpper(), CombatData.AttrColor(BossWeak[phase]));
                Ring(12);
            }
        }
        return false;
    }
}