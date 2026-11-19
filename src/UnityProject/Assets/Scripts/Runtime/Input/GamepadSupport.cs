using UnityEngine;

namespace Pacifico.Input
{
    /// <summary>
    /// Se crea sola al arrancar y vive entre escenas: actualiza la entrada antes que nadie en cada fotograma y, cuando
    /// se juega con mando y la escena usa el puntero (estrategia, torre, mapas), dibuja el cursor virtual que mueve el
    /// stick derecho. Con RT se hace clic y con LT clic derecho.
    /// </summary>
    [DefaultExecutionOrder(-10000)]
    public sealed class GamepadSupport : MonoBehaviour
    {
        private Texture2D _ring;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Create()
        {
            var go = new GameObject("Soporte_de_Mando");
            DontDestroyOnLoad(go);
            go.AddComponent<GamepadSupport>();
        }

        private void Update() => GameInput.Tick();

        private void OnGUI()
        {
            if (Event.current.type != EventType.Repaint) return;
            if (!GameInput.UsingGamepad || !GameInput.PointerInUse || Cursor.lockState == CursorLockMode.Locked) return;
            GUI.depth = -2000;
            if (_ring == null) _ring = MakeRing(48);
            Vector2 c = GameInput.VirtualCursor;
            float size = Mathf.Max(32f, Screen.height * 0.035f);
            var rect = new Rect(c.x - size * 0.5f, Screen.height - c.y - size * 0.5f, size, size);
            GUI.color = new Color(0f, 0f, 0f, 0.6f);
            GUI.DrawTexture(new Rect(rect.x + 2f, rect.y + 2f, size, size), _ring);
            GUI.color = new Color(1f, 0.9f, 0.35f);
            GUI.DrawTexture(rect, _ring);
            GUI.DrawTexture(new Rect(c.x - 3f, Screen.height - c.y - 3f, 6f, 6f), Texture2D.whiteTexture);
            GUI.color = Color.white;
        }

        /// <summary>Aro blanco con borde suave, para teñirlo con GUI.color.</summary>
        private static Texture2D MakeRing(int size)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.DontSave };
            float r = size * 0.5f - 1f, thickness = size * 0.1f;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(size * 0.5f, size * 0.5f));
                    float a = Mathf.Clamp01(thickness * 0.5f - Mathf.Abs(d - (r - thickness * 0.5f)) + 0.5f);
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
                }
            }
            tex.Apply();
            return tex;
        }
    }
}
