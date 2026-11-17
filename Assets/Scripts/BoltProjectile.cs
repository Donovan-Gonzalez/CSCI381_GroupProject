using System.Collections.Generic;
using UnityEngine;

// A projectile. Friendly ones (arrows, bolts) hurt foes; hostile ones (arrows, magic orbs) hurt the hero.
public class BoltProjectile : MonoBehaviour
{
    bool friendly;
    float damage;
    List<Attr> attrs = new List<Attr>();
    int pierce;
    float aoe;
    float life = 3.5f;
    readonly HashSet<FoeController> alreadyHit = new HashSet<FoeController>();

    public static void Spawn(Vector2 pos, Vector2 dir, float speed, float damage, List<Attr> attrs, float radius,
                             bool friendly, Color color, int pierce, float aoe)
    {
        GameObject go = SpriteFactory.Make("Bolt", SpriteFactory.Circle, color, new Vector2(radius * 2f, radius * 2f), 15, null);
        go.transform.position = pos;

        Rigidbody2D rb = go.AddComponent<Rigidbody2D>();
        rb.bodyType = RigidbodyType2D.Kinematic;
        rb.gravityScale = 0f;

        CircleCollider2D col = go.AddComponent<CircleCollider2D>();
        col.isTrigger = true;
        col.radius = 0.5f;   // local space: scaled by the sprite size above

        BoltProjectile b = go.AddComponent<BoltProjectile>();
        b.friendly = friendly;
        b.damage = damage;
        if (attrs != null) b.attrs = new List<Attr>(attrs);
        b.pierce = pierce;
        b.aoe = aoe;

        rb.linearVelocity = dir.normalized * speed;
    }

    void Update()
    {
        if (RoomDirector.State != GState.Playing) return;
        life -= Time.deltaTime;
        if (life <= 0f) Destroy(gameObject);
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
                Destroy(gameObject);      // hit a wall
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
