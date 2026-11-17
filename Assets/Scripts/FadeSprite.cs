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
        GameObject go = SpriteFactory.Make("Fx", round ? SpriteFactory.Circle : SpriteFactory.Square, color, size, 20, null);
        go.transform.position = pos;
        FadeSprite f = go.AddComponent<FadeSprite>();
        f.life = life;
        f.sr = go.GetComponent<SpriteRenderer>();
        f.startColor = color;
    }

    void Update()
    {
        t += Time.deltaTime;
        if (sr != null)
        {
            Color c = startColor;
            c.a = startColor.a * Mathf.Clamp01(1f - t / life);
            sr.color = c;
        }
        if (t >= life) Destroy(gameObject);
    }
}
