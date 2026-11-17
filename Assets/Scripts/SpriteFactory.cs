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

    static Sprite Build(bool round)
    {
        const int n = 64;
        Texture2D tex = new Texture2D(n, n, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Bilinear;
        Color32[] px = new Color32[n * n];
        float c = (n - 1) / 2f;

        for (int y = 0; y < n; y++)
        {
            for (int x = 0; x < n; x++)
            {
                bool inside = !round || ((x - c) * (x - c) + (y - c) * (y - c) <= c * c);
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