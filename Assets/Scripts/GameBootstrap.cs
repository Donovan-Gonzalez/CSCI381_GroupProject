using UnityEngine;

// Put this on ONE empty GameObject in the scene. It sets up the camera and creates everything else from code.
public class GameBootstrap : MonoBehaviour
{
    void Awake()
    {
        Camera cam = Camera.main;
        if (cam == null)
        {
            GameObject go = new GameObject("Main Camera");
            go.tag = "MainCamera";
            cam = go.AddComponent<Camera>();
        }

        cam.orthographic = true;
        // Zoomed out a bit and shifted down so the whole room sits above the taller bottom HUD
        // (the equipment cross needs ~196 of the 720 virtual pixels).
        cam.orthographicSize = 7.5f;
        cam.transform.position = new Vector3(0f, -2.07f, -10f);
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0.04f, 0.04f, 0.06f);

        gameObject.AddComponent<RoomDirector>();
        gameObject.AddComponent<HudView>();
    }
}
