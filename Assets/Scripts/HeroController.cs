using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

// The player. Handles movement, AUTO-ATTACKING the nearest enemy, weapon / element switching,
// and the character ability. The right hand only needs the arrow keys:
//   Left / Right = switch weapon (primary / secondary),  Up / Down = switch element (none / Ignite / Chill).
public class HeroController : MonoBehaviour
{
    // A weapon the hero owns, with the progress made on it this run.
    public class OwnedWeapon
    {
        public int id;                                    // index into CombatData.Weapons
        public int action;                                // chosen action (0 = default)
        public readonly List<int> elements = new List<int>();   // unlocked elements, in the order they were earned
        public int elementSlot;                           // 0 = no element, 1.. = elements[elementSlot - 1]

        public WeaponDef Def { get { return CombatData.Weapons[id]; } }
        public int SlotCount { get { return 1 + elements.Count; } }
        public int ElementAt(int slot) { return slot <= 0 ? -1 : elements[slot - 1]; }
        public FunctionDef Resolve() { return CombatData.Resolve(id, action, ElementAt(elementSlot)); }

        // label for a slot on the Up / Down strip
        public string SlotName(int slot)
        {
            return slot <= 0 ? Def.actions[action].name : CombatData.ElementNames[elements[slot - 1]];
        }
    }

    public float maxHp = 100f;
    public float moveSpeed = 5.5f;

    public float hp;
    public HeroAbility ability;
    public string heroName;
    public Color heroColor;
    public CharacterDef Character { get; private set; }

    public readonly List<OwnedWeapon> Weapons = new List<OwnedWeapon>();
    public int WeaponSlot { get; private set; }
    public OwnedWeapon Equipped { get { return Weapons[WeaponSlot]; } }

    public int DashCharges { get; private set; }
    public float SurgeLeft { get; private set; }
    public float SurgeCooldown { get; private set; }
    public float SinceDash { get; private set; }
    public Vector2 Face { get; private set; }
    public FoeController Target { get; private set; }
    public Vector2 Velocity { get { return rb != null ? rb.linearVelocity : Vector2.zero; } }

    Rigidbody2D rb;
    SpriteRenderer bodySr;
    Transform aimPivot;
    GameObject held, heldPointer;
    Transform heldPivot;
    bool heldHasSprite;
    float heldBaseRot = -45f;
    // held-weapon animation: 1 = sweep (swing), 2 = thrust, 3 = recoil (shots)
    int animKind; float animT, animDur, animArc, animDir, animReach, heldHideT;
    SpriteRenderer heldSr;
    string heldName;

    float cd;
    float invuln, flash;
    float dashT, dashRefill;
    Vector2 dashDir;
    int swingDir = 1;     // swings alternate left->right / right->left

    public void Setup(CharacterDef c, int primaryWeaponId)
    {
        Character = c;
        heroName = c.name;
        heroColor = c.color;
        ability = c.ability;

        Weapons.Clear();
        AddWeapon(primaryWeaponId);
        WeaponSlot = 0;

        hp = maxHp;
        DashCharges = 2;
        SinceDash = 99f;
        Face = Vector2.right;

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

        // small pointer that shows where the auto-attack is aimed
        aimPivot = new GameObject("Aim").transform;
        aimPivot.SetParent(transform, false);
        GameObject pointer = SpriteFactory.Make("Pointer", SpriteFactory.Square, Color.white, new Vector2(0.45f, 0.14f), 11, aimPivot);
        pointer.transform.localPosition = new Vector3(0.5f, 0f, 0f);
        heldPointer = pointer;

        // the weapon the hero is actually holding (Kenney tile); the white pointer is only the fallback
        heldPivot = new GameObject("HeldPivot").transform;      // rotates around the hero during a swing
        heldPivot.SetParent(aimPivot, false);
        held = SpriteFactory.Make("Held", SpriteFactory.Square, Color.white, Vector2.one, 12, heldPivot);
        held.transform.localPosition = new Vector3(0.75f, 0f, 0f);
        held.transform.localRotation = Quaternion.Euler(0f, 0f, -45f);   // Kenney weapons point up-right; turn the tip to face forward
        heldSr = held.GetComponent<SpriteRenderer>();
        heldName = null;
        UpdateHeldWeapon();
    }

    // shows the sprite of the currently equipped weapon (called every frame; only swaps when the weapon changed)
    void UpdateHeldWeapon()
    {
        if (held == null) return;
        string wn = Equipped.Def.name;
        if (wn == heldName) return;
        heldName = wn;

        Sprite sp = SpriteFactory.Tile(SpriteFactory.WeaponTile(wn));
        heldHasSprite = sp != null;
        held.SetActive(heldHasSprite);
        heldPointer.SetActive(!heldHasSprite);
        if (sp == null) return;
        heldSr.sprite = sp;
        heldSr.color = Color.white;
        heldBaseRot = (wn == "Bow") ? 45f : -45f;     // the bow's belly faces forward; blades point their tip forward
        held.transform.localRotation = Quaternion.Euler(0f, 0f, heldBaseRot);
        held.transform.localScale = Vector3.one * 1.3f;
    }

    void StartWeaponAnim(int kind, float dur, float arc, float dir, float reach)
    {
        animKind = kind; animT = 0f; animDur = dur; animArc = arc; animDir = dir; animReach = reach;
    }

    // plays the swing / thrust / recoil on the weapon in the hero's hand
    void AnimateHeld()
    {
        if (held == null || !heldHasSprite) return;
        float dt = Time.deltaTime;

        if (heldHideT > 0f) heldHideT -= dt;
        held.SetActive(heldHideT <= 0f);

        float x = 0.75f, scale = 1.3f, sweep = 0f;
        if (animKind != 0)
        {
            animT += dt;
            float p = Mathf.Clamp01(animT / animDur);
            if (animKind == 1)          // swing: sweep around the hero from one side of the fan to the other
            {
                float e = 1f - (1f - p) * (1f - p);
                sweep = animDir * animArc * (e - 0.5f);
                x = 0.95f; scale = 1.6f;
            }
            else if (animKind == 2)     // thrust: stab straight out and back
            {
                x = 0.75f + animReach * 0.55f * Mathf.Sin(p * Mathf.PI);
                scale = 1.5f;
            }
            else                        // recoil when shooting / throwing
            {
                x = 0.75f - 0.28f * Mathf.Sin(p * Mathf.PI);
            }
            if (p >= 1f) animKind = 0;
        }
        heldPivot.localRotation = Quaternion.Euler(0f, 0f, sweep);
        held.transform.localPosition = new Vector3(x, 0f, 0f);
        held.transform.localScale = Vector3.one * scale;
    }

    // ---------------- progression (called by RoomDirector rewards) ----------------
    public bool HasWeapon(int id)
    {
        foreach (OwnedWeapon w in Weapons) if (w.id == id) return true;
        return false;
    }

    public void AddWeapon(int id)
    {
        if (HasWeapon(id)) return;
        Weapons.Add(new OwnedWeapon { id = id });
    }

    public void SetAction(int weaponSlot, int action)
    {
        Weapons[weaponSlot].action = action;
    }

    public void AddElement(int weaponSlot, int element)
    {
        OwnedWeapon w = Weapons[weaponSlot];
        if (w.elements.Contains(element)) return;
        w.elements.Add(element);
        w.elementSlot = w.elements.Count;      // equip the new element right away so the player sees it
    }

    // ---------------- main loop ----------------
    void Update()
    {
        if (RoomDirector.State != GState.Playing || rb == null)
        {
            if (rb != null) rb.linearVelocity = Vector2.zero;
            return;
        }

        Keyboard kb = Keyboard.current;
        if (kb == null) return;

        float dt = Time.deltaTime;

        // ----- timers -----
        cd -= dt * (SurgeLeft > 0f ? 1.7f : 1f);
        if (invuln > 0f) invuln -= dt;
        if (flash > 0f) flash -= dt;
        SinceDash += dt;
        if (SurgeLeft > 0f) SurgeLeft -= dt;
        if (SurgeCooldown > 0f) SurgeCooldown -= dt;

        if (DashCharges < 2)
        {
            dashRefill -= dt;
            if (dashRefill <= 0f) { DashCharges++; dashRefill = 2.5f; }
        }

        // ----- equipment: left/right = weapon, up/down = element -----
        int nw = Weapons.Count;
        if (kb.rightArrowKey.wasPressedThisFrame) WeaponSlot = (WeaponSlot + 1) % nw;
        if (kb.leftArrowKey.wasPressedThisFrame) WeaponSlot = (WeaponSlot + nw - 1) % nw;

        OwnedWeapon eq = Equipped;
        int ns = eq.SlotCount;
        if (kb.upArrowKey.wasPressedThisFrame) eq.elementSlot = (eq.elementSlot + 1) % ns;
        if (kb.downArrowKey.wasPressedThisFrame) eq.elementSlot = (eq.elementSlot + ns - 1) % ns;

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
            rb.linearVelocity = move * moveSpeed;
        }

        // ----- auto aim: nearest enemy, otherwise the way we're walking -----
        Target = FindTarget();
        Vector2 pos = transform.position;
        if (Target != null)
        {
            Vector2 to = (Vector2)Target.transform.position - pos;
            if (to.sqrMagnitude > 0.0001f) Face = to.normalized;
        }
        else if (move.sqrMagnitude > 0.01f)
        {
            Face = move.normalized;
        }
        aimPivot.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(Face.y, Face.x) * Mathf.Rad2Deg);
        UpdateHeldWeapon();
        AnimateHeld();

        // ----- auto attack -----
        FunctionDef f = eq.Resolve();
        if (cd <= 0f && Target != null)
        {
            float dist = Vector2.Distance(pos, Target.transform.position);
            if (dist <= f.EngageRange + Target.radius) Attack(f, Target);
        }

        // ----- color feedback -----
        bodySr.color = flash > 0f ? Color.white : (invuln > 0f ? Color.Lerp(heroColor, Color.red, 0.5f) : heroColor);
    }

    FoeController FindTarget()
    {
        FoeController best = null;
        float bestD = float.MaxValue;
        Vector2 pos = transform.position;
        foreach (FoeController foe in FoeController.All)
        {
            if (foe == null || foe.IsDead) continue;
            float d = ((Vector2)foe.transform.position - pos).sqrMagnitude;
            if (d < bestD) { bestD = d; best = foe; }
        }
        return best;
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

    // ---------------- attacks ----------------
    void Attack(FunctionDef f, FoeController target)
    {
        // special from an ability: Dash + a melee hit = Dash Slash
        if (ability == HeroAbility.Dash && f.IsMelee && SinceDash < 0.5f)
        {
            f = f.Clone();
            f.damage *= 1.5f;
            f.reach *= 1.25f;
            if (f.kind == FnKind.Arc) f.arc = Mathf.Max(f.arc, 150f);
            SinceDash = 99f;
            HudView.Float(transform.position + Vector3.up * 0.9f, "DASH SLASH!", new Color(1f, 0.85f, 0.3f));
        }

        cd = f.cooldown;
        Color col = AttackColor(f);

        switch (f.kind)
        {
            case FnKind.Arc: DoArc(f, col); break;
            case FnKind.Thrust: DoThrust(f, col); break;
            case FnKind.Projectile: Shoot(f, Face, col); StartWeaponAnim(3, 0.16f, 0f, 0f, 0f); break;
            case FnKind.Volley:
                for (int i = 0; i < f.count; i++)
                {
                    float a = (i - (f.count - 1) * 0.5f) * f.spread;
                    Shoot(f, Quaternion.Euler(0f, 0f, a) * Face, col);
                }
                StartWeaponAnim(3, 0.16f, 0f, 0f, 0f);
                break;
            case FnKind.Boomerang:
                BoltProjectile.SpawnBoomerang((Vector2)transform.position + Face * 0.6f, Face, f.speed, f.damage, f.attrs,
                                              f.radius, col, f.range, transform);
                StartWeaponAnim(3, 0.16f, 0f, 0f, 0f);
                heldHideT = f.cooldown * 0.85f;     // the axe is out of your hand until it comes back
                break;
            case FnKind.Rain:
                // aim a little ahead of a moving target
                Rigidbody2D trb = target.GetComponent<Rigidbody2D>();
                Vector2 lead = trb != null ? trb.linearVelocity * 0.3f : Vector2.zero;
                AreaStrike.Spawn((Vector2)target.transform.position + lead, f.aoe, f.duration, f.tick, f.damage, f.attrs, col);
                break;
        }
    }

    static Color AttackColor(FunctionDef f)
    {
        return f.attrs.Count > 0 ? CombatData.AttrColor(f.attrs[f.attrs.Count - 1]) : Color.white;
    }

    static float Angle(Vector2 v) { return Mathf.Atan2(v.y, v.x) * Mathf.Rad2Deg; }

    // fan (or full circle when arc >= 360)
    void DoArc(FunctionDef f, Color col)
    {
        Vector2 pos = transform.position;
        foreach (FoeController foe in new List<FoeController>(FoeController.All))
        {
            if (foe == null || foe.IsDead) continue;
            Vector2 to = (Vector2)foe.transform.position - pos;
            if (to.magnitude > f.reach + foe.radius) continue;
            if (f.arc < 360f && Vector2.Angle(Face, to) > f.arc * 0.5f) continue;
            foe.Damage(f.damage, f.attrs);
        }

        // blade sweep from one side of the fan to the other; consecutive swings alternate direction
        col.a = f.arc >= 360f ? 0.55f : 0.75f;
        int sd = f.arc >= 360f ? 1 : swingDir;
        SwingFx.Spawn(transform, Angle(Face), f.arc, f.reach, col, sd);
        StartWeaponAnim(1, f.arc >= 360f ? 0.22f : 0.16f, f.arc, sd, f.reach);
        swingDir = -swingDir;
    }

    // straight line that pierces every enemy in it
    void DoThrust(FunctionDef f, Color col)
    {
        Vector2 pos = transform.position;
        foreach (FoeController foe in new List<FoeController>(FoeController.All))
        {
            if (foe == null || foe.IsDead) continue;
            Vector2 rel = (Vector2)foe.transform.position - pos;
            float along = Vector2.Dot(rel, Face);
            float side = Mathf.Abs(Face.x * rel.y - Face.y * rel.x);
            if (along < -foe.radius || along > f.reach + foe.radius) continue;
            if (side > f.width + foe.radius) continue;
            foe.Damage(f.damage, f.attrs);
        }

        // sharp spike shooting out from the hero toward the target (wide at the base, needle tip at full reach)
        StartWeaponAnim(2, 0.22f, 0f, 0f, f.reach);
        col.a = 0.85f;
        FadeSprite.Spawn(pos + Face * f.reach * 0.5f, new Vector2(f.reach, f.width * 2.6f), col, 0.14f,
                         SpriteFactory.Spike, Angle(Face), true);
    }

    void Shoot(FunctionDef f, Vector2 dir, Color col)
    {
        Vector2 origin = (Vector2)transform.position + dir.normalized * 0.6f;
        BoltProjectile.Create(origin, dir, f.speed, f.damage, f.attrs, f.radius, true, col, f.pierce, f.aoe, f.look);
    }

    // ---------------- taking damage ----------------
    public void Hit(float dmg, Vector2 sourcePos)
    {
        if (invuln > 0f || RoomDirector.State != GState.Playing) return;

        invuln = 0.5f;
        HudView.Float(transform.position + Vector3.up * 0.9f, "-" + Mathf.RoundToInt(dmg), new Color(1f, 0.35f, 0.35f));

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