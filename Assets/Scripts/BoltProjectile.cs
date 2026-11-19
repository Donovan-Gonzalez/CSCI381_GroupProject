using System.Collections.Generic;
using UnityEngine;

// A projectile. Friendly ones (arrows, knives, sword waves, thrown axes) hurt foes;
// hostile ones (arrows, magic orbs) hurt the hero.
// The collider lives on the root object; the picture is a child so it can be stretched / rotated / spun.
public class BoltProjectile : MonoBehaviour
{
    bool friendly;
    float damage;
    List<Attr> attrs = new List<Attr>();
    int pierce;
    float aoe;
    float life = 3.5f;
    readonly HashSet<FoeController> alreadyHit = new HashSet<FoeController>();

    Rigidbody2D rb;
    Transform visual;
    bool spin;

    // boomerang (Axe Throw): flies out `outDist`, then homes back to `owner`
    bool boomerang;
    bool returning;
    float outDist;
    float travelled;
    float speed;
    Transform owner;

    // Old signature, still used by enemies: a round orb.
    public static void Spawn(Vector2 pos, Vector2 dir, float speed, float damage, List<Attr> attrs, float radius,
                             bool friendly, Color color, int pierce, float aoe)
    {
        Create(pos, dir, speed, damage, attrs, radius, friendly, color, pierce, aoe, ShotLook.Orb);
    }

    public static BoltProjectile Create(Vector2 pos, Vector2 dir, float speed, float damage, List<Attr> attrs, float radius,
                                        bool friendly, Color color, int pierce, float aoe, ShotLook look)
    {
        dir = dir.sqrMagnitude > 0.0001f ? dir.normalized : Vector2.right;

        GameObject go = new GameObject(friendly ? "HeroShot" : "Bolt");
        go.transform.position = pos;

        Sprite sp = SpriteFactory.Circle;
        Vector2 size = new Vector2(radius * 2f, radius * 2f);
        switch (look)
        {
            case ShotLook.Arrow: sp = SpriteFactory.Square; size = new Vector2(0.6f, 0.09f); break;
            case ShotLook.Knife: sp = SpriteFactory.Square; size = new Vector2(0.42f, 0.13f); break;
            case ShotLook.Crescent: sp = SpriteFactory.Crescent; size = new Vector2(radius * 1.4f, radius * 3.4f); break;
            case ShotLook.Axe: sp = SpriteFactory.Square; size = new Vector2(radius * 1.4f, radius * 1.4f); break;
        }

        GameObject vis = SpriteFactory.Make("Visual", sp, color, size, 15, go.transform);
        vis.transform.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg);

        Rigidbody2D rb = go.AddComponent<Rigidbody2D>();
        rb.bodyType = RigidbodyType2D.Kinematic;
        rb.gravityScale = 0f;

        CircleCollider2D col = go.AddComponent<CircleCollider2D>();
        col.isTrigger = true;
        col.radius = radius;

        BoltProjectile b = go.AddComponent<BoltProjectile>();
        b.friendly = friendly;
        b.damage = damage;
        if (attrs != null) b.attrs = new List<Attr>(attrs);
        b.pierce = pierce;
        b.aoe = aoe;
        b.rb = rb;
        b.visual = vis.transform;
        b.speed = speed;
        b.spin = (look == ShotLook.Axe);

        rb.linearVelocity = dir * speed;
        return b;
    }

    public static void SpawnBoomerang(Vector2 pos, Vector2 dir, float speed, float damage, List<Attr> attrs, float radius,
                                      Color color, float range, Transform owner)
    {
        BoltProjectile b = Create(pos, dir, speed, damage, attrs, radius, true, color, 999, 0f, ShotLook.Axe);
        b.boomerang = true;
        b.outDist = range;
        b.owner = owner;
        b.life = 6f;
    }

    void Update()
    {
        if (RoomDirector.State != GState.Playing) return;
        float dt = Time.deltaTime;

        if (spin && visual != null) visual.Rotate(0f, 0f, -900f * dt);

        if (boomerang)
        {
            if (!returning)
            {
                travelled += speed * dt;
                if (travelled >= outDist) StartReturn();
            }
            else
            {
                if (owner == null) { Destroy(gameObject); return; }
                Vector2 to = (Vector2)owner.position - (Vector2)transform.position;
                if (to.magnitude < 0.6f) { Destroy(gameObject); return; }
                rb.linearVelocity = to.normalized * speed * 1.25f;
            }
        }

        life -= dt;
        if (life <= 0f) Destroy(gameObject);
    }

    void StartReturn()
    {
        returning = true;
        alreadyHit.Clear();          // the axe can hit the same enemy again on the way back
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (RoomDirector.State != GState.Playing) return;
        if (other.isTrigger) return;

        if (friendly)
        {
            FoeController foe = other.GetComponent<FoeController>();
            if (foe != null)
            {
                if (alreadyHit.Contains(foe)) return;
                alreadyHit.Add(foe);

                if (aoe > 0f)
                {
                    Explode();
                    Destroy(gameObject);
                    return;
                }

                foe.Damage(damage, attrs);
                if (pierce > 0) pierce--;
                else Destroy(gameObject);
            }
            else if (other.GetComponent<HeroController>() == null)
            {
                if (boomerang) { if (!returning) StartReturn(); }   // bounce off the wall and come back
                else Destroy(gameObject);                          // hit a wall
            }
        }
        else
        {
            HeroController hero = other.GetComponent<HeroController>();
            if (hero != null)
            {
                hero.Hit(damage, transform.position);
                Destroy(gameObject);
            }
            else if (other.GetComponent<FoeController>() == null)
            {
                Destroy(gameObject);      // hit a wall
            }
        }
    }

    void Explode()
    {
        Collider2D[] hits = Physics2D.OverlapCircleAll(transform.position, aoe);
        foreach (Collider2D c in hits)
        {
            FoeController foe = c.GetComponent<FoeController>();
            if (foe != null) foe.Damage(damage, attrs);
        }
        FadeSprite.Spawn(transform.position, new Vector2(aoe * 2f, aoe * 2f), new Color(1f, 0.55f, 0.15f, 0.6f), 0.35f, true);
    }
}
