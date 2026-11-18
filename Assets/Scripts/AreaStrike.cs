using System.Collections.Generic;
using UnityEngine;

// Arrow Rain: a zone on the ground that keeps raining arrows for `duration` seconds.
// Every `tick` seconds, every enemy inside takes damage (attributes / elements apply on each tick).
public class AreaStrike : MonoBehaviour
{
    float radius;
    float duration;
    float tick;
    float damage;
    float t;
    float tickT;
    float streakT;
    List<Attr> attrs;
    Color color;
    SpriteRenderer marker;
    SpriteRenderer rim;

    public static void Spawn(Vector2 pos, float radius, float duration, float tick, float damage, List<Attr> attrs, Color color)
    {
        GameObject go = new GameObject("ArrowRain");
        go.transform.position = pos;

        AreaStrike a = go.AddComponent<AreaStrike>();
        a.radius = radius;
        a.duration = Mathf.Max(0.1f, duration);
        a.tick = Mathf.Max(0.1f, tick);
        a.tickT = 0.15f;              // first hit comes almost right away
        a.damage = damage;
        a.attrs = attrs != null ? new List<Attr>(attrs) : new List<Attr>();
        a.color = color;

        // filled zone + a slightly bigger ring so the edge is easy to read
        a.rim = SpriteFactory.Make("Rim", SpriteFactory.Circle, new Color(color.r, color.g, color.b, 0.35f),
                                   Vector2.one * (radius * 2f + 0.12f), 2, go.transform).GetComponent<SpriteRenderer>();
        a.marker = SpriteFactory.Make("Zone", SpriteFactory.Circle, new Color(0.05f, 0.05f, 0.08f, 0.55f),
                                      Vector2.one * radius * 2f, 3, go.transform).GetComponent<SpriteRenderer>();
    }

    void Update()
    {
        if (RoomDirector.State != GState.Playing) return;
        float dt = Time.deltaTime;
        t += dt;

        // zone fades in quickly, pulses with each tick, fades out at the end
        float fadeIn = Mathf.Clamp01(t / 0.15f);
        float fadeOut = Mathf.Clamp01((duration - t) / 0.3f);
        float pulse = 1f - Mathf.Clamp01(tickT / tick);
        float k = fadeIn * fadeOut;
        rim.color = new Color(color.r, color.g, color.b, (0.3f + 0.35f * pulse) * k);
        marker.color = new Color(color.r * 0.25f, color.g * 0.25f, color.b * 0.25f, 0.5f * k);

        // falling arrow streaks the whole time
        streakT -= dt;
        if (streakT <= 0f)
        {
            streakT = 0.05f;
            Vector2 p = (Vector2)transform.position + Random.insideUnitCircle * radius * 0.9f;
            FadeSprite.Spawn(p, new Vector2(0.06f, 0.45f), new Color(color.r, color.g, color.b, 0.85f), 0.18f, false);
        }

        // damage ticks
        tickT -= dt;
        if (tickT <= 0f)
        {
            tickT = tick;
            HitInside();
        }

        if (t >= duration) Destroy(gameObject);
    }

    void HitInside()
    {
        Vector2 pos = transform.position;
        foreach (FoeController foe in new List<FoeController>(FoeController.All))
        {
            if (foe == null || foe.IsDead) continue;
            if (Vector2.Distance(pos, foe.transform.position) <= radius + foe.radius) foe.Damage(damage, attrs);
        }
    }
}
