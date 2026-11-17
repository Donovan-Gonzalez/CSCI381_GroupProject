using UnityEngine;
using UnityEngine.InputSystem;

// Debug helper: shows every tile of a sliced sprite sheet with its index number.
public class SpriteBrowser : MonoBehaviour
{
    public string sheetPath = "Sprites/colored-transparent_packed";
    Sprite[] sprites;
    int scrollRows;
    GUIStyle st;

    void Start() { sprites = Resources.LoadAll<Sprite>(sheetPath); }

    static int Idx(string name)
    {
        int u = name.LastIndexOf('_');
        int v;
        return (u >= 0 && int.TryParse(name.Substring(u + 1), out v)) ? v : -1;
    }

    void Update()
    {
        Keyboard kb = Keyboard.current;
        if (kb == null) return;
        if (kb.downArrowKey.wasPressedThisFrame) scrollRows++;
        if (kb.upArrowKey.wasPressedThisFrame) scrollRows = Mathf.Max(0, scrollRows - 1);
    }

    void OnGUI()
    {
        if (Event.current.type != EventType.Repaint) return;
        if (st == null) { st = new GUIStyle(GUI.skin.label); st.fontSize = 10; st.fontStyle = FontStyle.Bold; }

        GUI.color = new Color(0.25f, 0.27f, 0.32f);
        GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), Texture2D.whiteTexture);
        GUI.color = Color.white;
        GUI.Label(new Rect(8, 4, 900, 20), "Sheet: " + sheetPath + "   (Up / Down arrows scroll)", st);

        if (sprites == null) return;
        const float cell = 38f;
        foreach (Sprite s in sprites)
        {
            Texture2D tex = s.texture;
            int col = Mathf.RoundToInt(s.rect.x / s.rect.width);
            int row = Mathf.RoundToInt((tex.height - s.rect.yMax) / s.rect.height) - scrollRows;
            if (row < 0) continue;

            Rect r = new Rect(col * cell + 4f, row * cell + 28f, 32f, 32f);
            if (r.y > Screen.height) continue;

            Rect uv = new Rect(s.rect.x / tex.width, s.rect.y / tex.height, s.rect.width / tex.width, s.rect.height / tex.height);
            GUI.DrawTextureWithTexCoords(r, tex, uv);

            string label = Idx(s.name).ToString();
            st.normal.textColor = Color.black;
            GUI.Label(new Rect(r.x + 1, r.y + 1, 36, 14), label, st);
            st.normal.textColor = Color.yellow;
            GUI.Label(new Rect(r.x, r.y, 36, 14), label, st);
        }
    }
}