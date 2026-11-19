using UnityEngine;

// Makes simple placeholder sprites (square / circle) at runtime so the base needs no art assets.
// Swap these for real sprites later by changing the Sprite passed to Make().
public static class SpriteFactory
{
    // ---- Kenney 1-Bit Pack tiles (Assets/Resources/Sprites/colored-transparent_packed, sliced 16x16, PPU 16) ----
    // Change these numbers to swap a character's look. -1 = use the old circle placeholder.
    public const int TileVanguard = 170;
    public const int TileEmber = 367;
    public const int TileBrute = 421;
    public const int TileArcher = 221;
    public const int TileMage = 418;
    public const int TileBoss = 317;
    public const int TileWall = 742;   // red brick
    public static readonly int[] TileFloor = { 1, 2, 3, 4 };   // dirt / rubble speckles

    static Sprite[] sheet;
    static bool sheetTried;

    public static Sprite Tile(int index)
    {
        if (index < 0) return null;
        if (!sheetTried)
        {
            sheetTried = true;
            sheet = Resources.LoadAll<Sprite>("Sprites/colored-transparent_packed");
        }
        if (sheet == null) return null;
        string suffix = "_" + index;
        foreach (Sprite sp in sheet)
            if (sp.name.EndsWith(suffix)) return sp;
        return null;
    }

    static Sprite square;
    static Sprite circle;

    public static Sprite Square
    {
        get { if (square == null) square = Build(false); return square; }
    }

    public static Sprite Circle
    {
        get { if (circle == null) circle = Build(true); return circle; }
    }

    static Sprite crescent;
    static readonly System.Collections.Generic.Dictionary<int, Sprite> sectors = new System.Collections.Generic.Dictionary<int, Sprite>();

    // A fan ("pizza slice") centered on the sprite's middle, opening toward +x, covering `arcDeg` degrees.
    // Brighter toward the outer edge so it reads like a blade sweep. Size 1 = radius 0.5, so scale it by reach * 2.
    public static Sprite Sector(float arcDeg)
    {
        int key = Mathf.Clamp(Mathf.RoundToInt(arcDeg), 1, 360);
        Sprite sp;
        if (sectors.TryGetValue(key, out sp) && sp != null) return sp;

        const int n = 128;
        Texture2D tex = new Texture2D(n, n, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Bilinear;
        tex.wrapMode = TextureWrapMode.Clamp;
        Color32[] px = new Color32[n * n];
        float c = (n - 1) / 2f;
        float half = key * 0.5f;

        for (int y = 0; y < n; y++)
        {
            for (int x = 0; x < n; x++)
            {
                float dx = x - c, dy = y - c;
                float r = Mathf.Sqrt(dx * dx + dy * dy) / c;          // 0 at the center, 1 at the edge
                float ang = Mathf.Abs(Mathf.Atan2(dy, dx) * Mathf.Rad2Deg);
                float a = 0f;
                if (r <= 1f && (key >= 360 || ang <= half))
                {
                    a = Mathf.Lerp(0.15f, 1f, r * r);                   // faint near the hero, bright at the blade's edge
                    a *= Mathf.Clamp01((1f - r) * c / 2f);              // soft outer rim
                    if (key < 360) a *= Mathf.Clamp01((half - ang) / 4f);   // soft side edges
                }
                px[y * n + x] = new Color32(255, 255, 255, (byte)(a * 255f));
            }
        }

        tex.SetPixels32(px);
        tex.Apply();
        sp = Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), n);
        sectors[key] = sp;
        return sp;
    }

    static Sprite spike;

    // A sharp spike pointing toward +x: wide base on the left, needle tip on the right,
    // with slightly concave sides so it reads as a stab, not a box. Brightest along the center line.
    public static Sprite Spike
    {
        get
        {
            if (spike != null) return spike;
            const int n = 128;
            Texture2D tex = new Texture2D(n, n, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Bilinear;
            tex.wrapMode = TextureWrapMode.Clamp;
            Color32[] px = new Color32[n * n];

            for (int y = 0; y < n; y++)
            {
                for (int x = 0; x < n; x++)
                {
                    float u = (x + 0.5f) / n;                          // 0 = base (hero side), 1 = tip
                    float v = Mathf.Abs((y + 0.5f) / n - 0.5f) * 2f;   // 0 = center line, 1 = top/bottom edge
                    float halfW = Mathf.Pow(1f - u, 1.6f);             // concave taper to a needle point
                    if (u < 0.08f) halfW *= u / 0.08f * 0.6f + 0.4f;   // small rounded-off back end
                    float a = 0f;
                    if (v <= halfW)
                    {
                        float edge = Mathf.Clamp01((halfW - v) * n * 0.25f);   // anti-aliased sides
                        a = edge * Mathf.Lerp(1f, 0.55f, v / Mathf.Max(halfW, 0.0001f));   // bright core
                    }
                    px[y * n + x] = new Color32(255, 255, 255, (byte)(a * 255f));
                }
            }

            tex.SetPixels32(px);
            tex.Apply();
            spike = Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), n);
            return spike;
        }
    }

    // a moon-crescent bulging toward +x (used by the Sword Wave)
    public static Sprite Crescent
    {
        get { if (crescent == null) crescent = Build(true, true); return crescent; }
    }

    static Sprite Build(bool round, bool moon = false)
    {
        const int n = 64;
        Texture2D tex = new Texture2D(n, n, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Bilinear;
        Color32[] px = new Color32[n * n];
        float c = (n - 1) / 2f;
        float cut = n * 0.3f;   // how far left the "bite" circle sits for the crescent

        for (int y = 0; y < n; y++)
        {
            for (int x = 0; x < n; x++)
            {
                bool inside = !round || ((x - c) * (x - c) + (y - c) * (y - c) <= c * c);
                if (moon && inside) inside = (x - c + cut) * (x - c + cut) + (y - c) * (y - c) > c * c * 0.95f;
                px[y * n + x] = inside ? new Color32(255, 255, 255, 255) : new Color32(255, 255, 255, 0);
            }
        }

        tex.SetPixels32(px);
        tex.Apply();
        // pixels-per-unit = n, so a scale of 1 is exactly one world unit
        return Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), n);
    }

    // Creates a colored sprite object. size is in world units (for a circle, size = diameter).
    public static GameObject Make(string name, Sprite sprite, Color color, Vector2 size, int order, Transform parent)
    {
        GameObject go = new GameObject(name);
        if (parent != null) go.transform.SetParent(parent, false);

        SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = sprite;
        sr.color = color;
        sr.sortingOrder = order;

        go.transform.localScale = new Vector3(size.x, size.y, 1f);
        return go;
    }
}