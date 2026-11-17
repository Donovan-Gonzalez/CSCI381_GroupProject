using UnityEngine;
using UnityEngine.InputSystem;

// The player. Handles movement, aiming, weapons/techniques/functions, stamina, and the character ability.
public class HeroController : MonoBehaviour
{
    public float maxHp = 100f;
    public float maxStamina = 100f;
    public float moveSpeed = 5.5f;

    public float hp;
    public float stamina;
    public int weapon;                 // index into CombatData.WeaponNames
    public int tech;                   // index into CombatData.TechNames
    public HeroAbility ability;
    public string heroName;
    public Color heroColor;

    public bool Blocking { get; private set; }
    public bool Charging { get; private set; }
    public float ChargeT { get; private set; }          // 0..1
    public int DashCharges { get; private set; }
    public float SurgeLeft { get; private set; }
    public float SurgeCooldown { get; private set; }
    public float SinceDash { get; private set; }
    public Vector2 Face { get; private set; }
    public Vector2 Velocity { get { return rb != null ? rb.linearVelocity : Vector2.zero; } }

    Rigidbody2D rb;
    Camera cam;
    SpriteRenderer bodySr;
    Transform aimPivot;
    GameObject shield;

    float cd0, cd1;
    float invuln, flash, sinceUse, warnT;
    float dashT, dashRefill;
    Vector2 dashDir;

    public void Setup(CharacterDef c)
    {
        heroName = c.name;
        heroColor = c.color;
        weapon = c.weapon;
        tech = c.tech;
        ability = c.ability;

        hp = maxHp;
        stamina = maxStamina;
        DashCharges = 2;
        SinceDash = 99f;
        Face = Vector2.right;
        cam = Camera.main;

        rb = gameObject.AddComponent<Rigidbody2D>();
        rb.gravityScale = 0f;
        rb.freezeRotation = true;
        rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;

        CircleCollider2D col = gameObject.AddComponent<CircleCollider2D>();
        col.radius = 0.4f;

        Sprite heroTile = SpriteFactory.Tile(heroName == "Ember" ? SpriteFactory.TileEmber : SpriteFactory.TileVanguard);
        bodySr = SpriteFactory.Make("Body", heroTile != null ? heroTile : SpriteFactory.Circle, heroColor,
                                    heroTile != null ? new Vector2(1.5f, 1.5f) : new Vector2(0.8f, 0.8f), 10, transform)
                              .GetComponent<SpriteRenderer>();

        aimPivot = new GameObject("Aim").transform;
        aimPivot.SetParent(transform, false);

        GameObject gun = SpriteFactory.Make("Pointer", SpriteFactory.Square, Color.white, new Vector2(0.45f, 0.14f), 11, aimPivot);
        gun.transform.localPosition = new Vector3(0.5f, 0f, 0f);

        shield = SpriteFactory.Make("Shield", SpriteFactory.Square, new Color(0.6f, 0.85f, 1f), new Vector2(0.14f, 1.0f), 12, aimPivot);
        shield.transform.localPosition = new Vector3(0.6f, 0f, 0f);
        shield.SetActive(false);
    }

    void Update()
    {
        if (RoomDirector.State != GState.Playing || rb == null)
        {
            if (rb != null) rb.linearVelocity = Vector2.zero;
            return;
        }

        Keyboard kb = Keyboard.current;
        Mouse ms = Mouse.current;
        if (kb == null || ms == null) return;
        if (cam == null) cam = Camera.main;

        float dt = Time.deltaTime;

        // ----- timers -----
        float rate = SurgeLeft > 0f ? 1.7f : 1f;
        cd0 -= dt * rate;
        cd1 -= dt * rate;
        if (invuln > 0f) invuln -= dt;
        if (flash > 0f) flash -= dt;
        if (warnT > 0f) warnT -= dt;
        sinceUse += dt;
        SinceDash += dt;
        if (SurgeLeft > 0f) SurgeLeft -= dt;
        if (SurgeCooldown > 0f) SurgeCooldown -= dt;

        if (DashCharges < 2)
        {
            dashRefill -= dt;
            if (dashRefill <= 0f) { DashCharges++; dashRefill = 2.5f; }
        }

        // ----- aim -----
        Vector2 mpos = ms.position.ReadValue();
        Vector3 mw = cam.ScreenToWorldPoint(new Vector3(mpos.x, mpos.y, -cam.transform.position.z));
        Vector2 toMouse = (Vector2)mw - (Vector2)transform.position;
        if (toMouse.sqrMagnitude > 0.01f) Face = toMouse.normalized;
        aimPivot.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(Face.y, Face.x) * Mathf.Rad2Deg);

        // ----- equipment strips: left/right = weapon, up/down = technique -----
        if (kb.rightArrowKey.wasPressedThisFrame) { weapon = (weapon + 1) % CombatData.WeaponNames.Length; CancelCharge(); }
        if (kb.leftArrowKey.wasPressedThisFrame) { weapon = (weapon + CombatData.WeaponNames.Length - 1) % CombatData.WeaponNames.Length; CancelCharge(); }
        if (kb.upArrowKey.wasPressedThisFrame) { tech = (tech + 1) % CombatData.TechNames.Length; CancelCharge(); }
        if (kb.downArrowKey.wasPressedThisFrame) { tech = (tech + CombatData.TechNames.Length - 1) % CombatData.TechNames.Length; CancelCharge(); }

        FunctionDef secondary = CombatData.Resolve(weapon, 1, tech, false);

        // ----- block (hold right mouse with a Block function) -----
        Blocking = ms.rightButton.isPressed && secondary.kind == FnKind.Block && stamina > 2f && dashT <= 0f;
        shield.SetActive(Blocking);

        // ----- movement -----
        Vector2 move = new Vector2(
            (kb.dKey.isPressed ? 1f : 0f) - (kb.aKey.isPressed ? 1f : 0f),
            (kb.wKey.isPressed ? 1f : 0f) - (kb.sKey.isPressed ? 1f : 0f));
        if (move.sqrMagnitude > 1f) move.Normalize();

        if (kb.spaceKey.wasPressedThisFrame) UseAbility(move);

        if (dashT > 0f)
        {
            dashT -= dt;
            rb.linearVelocity = dashDir * 14f;
        }
        else
        {
            float sp = moveSpeed * (Blocking ? 0.55f : 1f) * (Charging ? 0.7f : 1f);
            rb.linearVelocity = move * sp;
        }

        // ----- primary function (left mouse) -----
        FunctionDef primary = CombatData.Resolve(weapon, 0, tech, false);
        if (primary.chargeable)
        {
            if (ms.leftButton.isPressed)
            {
                Charging = true;
                ChargeT = Mathf.Min(1f, ChargeT + dt / 0.8f);
            }
            else if (Charging)
            {
                bool full = ChargeT >= 0.5f;
                TryUse(CombatData.Resolve(weapon, 0, tech, full), 0);
                CancelCharge();
            }
        }
        else if (ms.leftButton.isPressed && cd0 <= 0f)
        {
            TryUse(primary, 0);
        }

        // ----- secondary function (right mouse, non-block) -----
        if (secondary.kind != FnKind.Block && ms.rightButton.isPressed && cd1 <= 0f)
            TryUse(secondary, 1);

        // ----- stamina -----
        if (Blocking) stamina -= 6f * dt;
        else if (sinceUse > 0.5f) stamina += (SurgeLeft > 0f ? 60f : 26f) * dt;
        stamina = Mathf.Clamp(stamina, 0f, maxStamina);

        // ----- color feedback -----
        bodySr.color = flash > 0f ? Color.white : (invuln > 0f ? Color.Lerp(heroColor, Color.red, 0.5f) : heroColor);
    }

    void CancelCharge()
    {
        Charging = false;
        ChargeT = 0f;
    }

    // ---------------- abilities ----------------
    void UseAbility(Vector2 move)
    {
        if (ability == HeroAbility.Dash)
        {
            if (DashCharges <= 0 || dashT > 0f) return;
            if (DashCharges == 2) dashRefill = 2.5f;
            DashCharges--;
            dashDir = move.sqrMagnitude > 0.01f ? move.normalized : Face;
            dashT = 0.14f;
            invuln = Mathf.Max(invuln, 0.22f);
            SinceDash = 0f;
            FadeSprite.Spawn(transform.position, new Vector2(1f, 1f), new Color(heroColor.r, heroColor.g, heroColor.b, 0.5f), 0.25f, true);
        }
        else
        {
            if (SurgeCooldown > 0f || SurgeLeft > 0f) return;
            SurgeLeft = 4f;
            SurgeCooldown = 12f;
            HudView.Float(transform.position + Vector3.up * 0.9f, "SURGE!", new Color(1f, 0.75f, 0.2f));
        }
    }

    // ---------------- functions ----------------
    bool TryUse(FunctionDef f, int slot)
    {
        if (stamina < f.stamina)
        {
            if (warnT <= 0f)
            {
                HudView.Float(transform.position + Vector3.up * 0.9f, "NO STAMINA", new Color(1f, 0.8f, 0.2f));
                warnT = 0.6f;
            }
            return false;
        }

        // special function from an ability: Dash + Swing = Dash Slash
        if (ability == HeroAbility.Dash && f.kind == FnKind.Arc && SinceDash < 0.5f)
        {
            f = f.Clone();
            f.name = "Dash Slash";
            f.damage *= 1.5f;
            f.reach *= 1.25f;
            f.arc = Mathf.Max(f.arc, 150f);
            SinceDash = 99f;
        }

        stamina -= f.stamina;
        sinceUse = 0f;
        if (slot == 0) cd0 = f.cooldown; else cd1 = f.cooldown;

        switch (f.kind)
        {
            case FnKind.Arc: DoArc(f); break;
            case FnKind.Projectile: Shoot(f, Face); break;
            case FnKind.Volley:
                Shoot(f, Face);
                Shoot(f, Quaternion.Euler(0f, 0f, 9f) * Face);
                Shoot(f, Quaternion.Euler(0f, 0f, -9f) * Face);
                break;
        }
        return true;
    }

    void DoArc(FunctionDef f)
    {
        Vector2 pos = transform.position;
        Collider2D[] hits = Physics2D.OverlapCircleAll(pos, f.reach + 1.5f);
        foreach (Collider2D c in hits)
        {
            FoeController foe = c.GetComponent<FoeController>();
            if (foe == null) continue;

            Vector2 to = (Vector2)foe.transform.position - pos;
            if (to.magnitude > f.reach + foe.radius) continue;
            if (Vector2.Angle(Face, to) > f.arc * 0.5f) continue;

            foe.Damage(f.damage, f.attrs);
        }

        Color col = f.attrs.Count > 0 ? CombatData.AttrColor(f.attrs[f.attrs.Count - 1]) : Color.white;
        col.a = 0.55f;
        FadeSprite.Spawn(pos + Face * f.reach * 0.55f, new Vector2(f.reach * 1.1f, f.reach * 0.5f), col, 0.14f, true);
    }

    void Shoot(FunctionDef f, Vector2 dir)
    {
        Color col = f.attrs.Count > 0 ? CombatData.AttrColor(f.attrs[f.attrs.Count - 1]) : Color.white;
        Vector2 origin = (Vector2)transform.position + dir.normalized * 0.6f;
        BoltProjectile.Spawn(origin, dir, f.speed, f.damage, f.attrs, f.radius, true, col, f.pierce, f.aoe);
    }

    // ---------------- taking damage ----------------
    public void Hit(float dmg, Vector2 sourcePos)
    {
        if (invuln > 0f || RoomDirector.State != GState.Playing) return;

        Vector2 from = sourcePos - (Vector2)transform.position;
        if (Blocking && Vector2.Angle(Face, from) < 75f)
        {
            dmg *= 0.15f;
            stamina = Mathf.Max(0f, stamina - 10f);
            invuln = 0.2f;
            flash = 0.08f;
            HudView.Float(transform.position + Vector3.up * 0.9f, "BLOCKED", new Color(0.6f, 0.85f, 1f));
        }
        else
        {
            invuln = 0.5f;
            HudView.Float(transform.position + Vector3.up * 0.9f, "-" + Mathf.RoundToInt(dmg), new Color(1f, 0.35f, 0.35f));
        }

        hp -= dmg;
        if (hp <= 0f)
        {
            hp = 0f;
            RoomDirector.Instance.PlayerDied();
        }
    }

    public void Heal(float amount)
    {
        hp = Mathf.Min(maxHp, hp + amount);
        HudView.Float(transform.position + Vector3.up * 0.9f, "+" + Mathf.RoundToInt(amount) + " HP", new Color(0.5f, 1f, 0.6f));
    }
}