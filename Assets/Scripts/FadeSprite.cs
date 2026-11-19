using UnityEngine;

// A short-lived sprite that fades out. Used for swing arcs, explosions and death puffs.
public class FadeSprite : MonoBehaviour
{
    float life = 0.15f;
    float t;
    SpriteRenderer sr;
    Color startColor;

    public static void Spawn(Vector2 pos, Vector2 size, Color color, float life, bool round)
    {
        Spawn(pos, size, color, life, round, 0f);
    }

    // angle = rotation in degrees (0 = the sprite's width runs along +x)
    public static void Spawn(Vector2 pos, Vector2 size, Color color, float life, bool round, float angle)
    {
        Spawn(pos, size, color, life, round ? SpriteFactory.Circle : SpriteFactory.Square, angle, false);
    }

    // any sprite; grow = starts at 70% size and expands while it fades (used for the swing fan)
    public static void Spawn(Vector2 pos, Vector2 size, Color color, float life, Sprite sprite, float angle, bool grow)
    {
        GameObject go = SpriteFactory.Make("Fx", sprite, color, size, 20, null);
        go.transform.position = pos;
        go.transform.rotation = Quaternion.Euler(0f, 0f, angle);
        FadeSprite f = go.AddComponent<FadeSprite>();
        f.life = life;
        f.sr = go.GetComponent<SpriteRenderer>();
        f.startColor = color;
        f.grow = grow;
        f.fullScale = go.transform.localScale;
        if (grow) go.transform.localScale = f.fullScale * 0.7f;
    }

    bool grow;
    Vector3 fullScale;

    void Update()
    {
        t += Time.deltaTime;
        if (grow) transform.localScale = fullScale * Mathf.Lerp(0.7f, 1f, Mathf.Clamp01(t / (life * 0.5f)));
        if (sr != null)
        {
            Color c = startColor;
            c.a = startColor.a * Mathf.Clamp01(1f - t / life);
            sr.color = c;
        }
        if (t >= life) Destroy(gameObject);
    }
}
