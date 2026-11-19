using UnityEngine;

// Swing animation: the fan is "painted" from one side of the arc to the other (a blade sweep),
// with a bright line on the leading edge, then the whole fan fades out.
// It's a child of the hero so it follows him while it plays. Damage is applied instantly by HeroController;
// this is only the visual.
public class SwingFx : MonoBehaviour
{
    const float Step = 6f;          // the fan sprite is rebuilt in 6-degree steps (cached in SpriteFactory)
    const float FadeTime = 0.1f;

    SpriteRenderer fan;
    SpriteRenderer edge;
    Transform edgePivot;
    float faceAngle, arc, sweepTime, t;
    int dir;                        // +1 = sweep counter-clockwise, -1 = clockwise
    Color color;

    // faceAngle: where the swing is aimed (degrees). dir alternates so consecutive swings go back and forth.
    public static void Spawn(Transform parent, float faceAngle, float arc, float reach, Color color, int dir)
    {
        GameObject go = new GameObject("SwingFx");
        go.transform.SetParent(parent, false);
        go.transform.localPosition = Vector3.zero;

        SwingFx fx = go.AddComponent<SwingFx>();
        fx.faceAngle = faceAngle;
        fx.arc = Mathf.Clamp(arc, Step, 360f);
        fx.dir = dir >= 0 ? 1 : -1;
        fx.color = color;
        fx.sweepTime = arc >= 360f ? 0.2f : Mathf.Lerp(0.08f, 0.14f, Mathf.Clamp01(arc / 150f));   // wider = a bit slower

        fx.fan = SpriteFactory.Make("Fan", SpriteFactory.Sector(Step), color, Vector2.one * reach * 2f, 20, go.transform)
                              .GetComponent<SpriteRenderer>();

        // leading-edge "blade" line: a thin bar from the hero out to the reach, rotated by an unscaled pivot
        fx.edgePivot = new GameObject("EdgePivot").transform;
        fx.edgePivot.SetParent(go.transform, false);
        GameObject e = SpriteFactory.Make("Edge", SpriteFactory.Square, Color.white, new Vector2(reach, 0.07f), 21, fx.edgePivot);
        e.transform.localPosition = new Vector3(reach * 0.5f, 0f, 0f);
        fx.edge = e.GetComponent<SpriteRenderer>();

        fx.Apply();
    }

    void Update()
    {
        if (RoomDirector.State != GState.Playing) return;
        t += Time.deltaTime;
        if (t >= sweepTime + FadeTime) { Destroy(gameObject); return; }
        Apply();
    }

    void Apply()
    {
        float p = Mathf.Clamp01(t / sweepTime);
        float covered = Mathf.Max(Step, arc * p);
        float shown = p >= 1f ? arc : Mathf.Min(arc, Mathf.Ceil(covered / Step) * Step);

        // the fan always starts at the same side and grows toward the other side
        float start = faceAngle - dir * arc * 0.5f;
        fan.sprite = SpriteFactory.Sector(shown);
        fan.transform.localRotation = Quaternion.Euler(0f, 0f, start + dir * shown * 0.5f);
        edgePivot.localRotation = Quaternion.Euler(0f, 0f, start + dir * covered);

        float fade = 1f - Mathf.Clamp01((t - sweepTime) / FadeTime);
        fan.color = new Color(color.r, color.g, color.b, color.a * fade);
        Color ec = Color.Lerp(Color.white, color, 0.3f);
        edge.color = new Color(ec.r, ec.g, ec.b, 0.95f * fade * fade);
    }
}
