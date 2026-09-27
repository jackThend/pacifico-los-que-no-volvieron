using UnityEngine;
#if PACIFICO_INPUT_SYSTEM && ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace Pacifico.Input
{
    /// <summary>Teclas y botones usados por los prototipos.</summary>
    public enum GameKey
    {
        W, A, S, D, Q, E, R, F, G, H, Space, Tab, Escape, LeftShift,
        Digit1, Digit2, Digit3,
        C, LeftControl, PageUp, PageDown,
    }

    /// <summary>
    /// Fachada de entrada que funciona con el Input Manager clásico o con el paquete Input System
    /// (Unity 6 crea los proyectos nuevos con el segundo activo). Evita depender de un único backend.
    /// </summary>
    public static class GameInput
    {
        public static bool Held(GameKey key)
        {
#if PACIFICO_INPUT_SYSTEM && ENABLE_INPUT_SYSTEM
            var control = Resolve(key);
            return control != null && control.isPressed;
#elif ENABLE_LEGACY_INPUT_MANAGER
            return UnityEngine.Input.GetKey(ToKeyCode(key));
#else
            return false;
#endif
        }

        public static bool Pressed(GameKey key)
        {
#if PACIFICO_INPUT_SYSTEM && ENABLE_INPUT_SYSTEM
            var control = Resolve(key);
            return control != null && control.wasPressedThisFrame;
#elif ENABLE_LEGACY_INPUT_MANAGER
            return UnityEngine.Input.GetKeyDown(ToKeyCode(key));
#else
            return false;
#endif
        }

        /// <summary>Movimiento del ratón en píxeles desde el fotograma anterior.</summary>
        public static Vector2 MouseDelta
        {
            get
            {
#if PACIFICO_INPUT_SYSTEM && ENABLE_INPUT_SYSTEM
                return Mouse.current != null ? Mouse.current.delta.ReadValue() : Vector2.zero;
#elif ENABLE_LEGACY_INPUT_MANAGER
                return new Vector2(UnityEngine.Input.GetAxisRaw("Mouse X"), UnityEngine.Input.GetAxisRaw("Mouse Y")) * 10f;
#else
                return Vector2.zero;
#endif
            }
        }

        /// <summary>Rueda del ratón normalizada (positiva = acercar).</summary>
        public static float ScrollDelta
        {
            get
            {
#if PACIFICO_INPUT_SYSTEM && ENABLE_INPUT_SYSTEM
                return Mouse.current != null ? Mouse.current.scroll.ReadValue().y / 120f : 0f;
#elif ENABLE_LEGACY_INPUT_MANAGER
                return UnityEngine.Input.mouseScrollDelta.y;
#else
                return 0f;
#endif
            }
        }

        public static bool MouseHeld(int button)
        {
#if PACIFICO_INPUT_SYSTEM && ENABLE_INPUT_SYSTEM
            if (Mouse.current == null) return false;
            return button == 0 ? Mouse.current.leftButton.isPressed : Mouse.current.rightButton.isPressed;
#elif ENABLE_LEGACY_INPUT_MANAGER
            return UnityEngine.Input.GetMouseButton(button);
#else
            return false;
#endif
        }

        public static bool MousePressed(int button)
        {
#if PACIFICO_INPUT_SYSTEM && ENABLE_INPUT_SYSTEM
            if (Mouse.current == null) return false;
            return button == 0 ? Mouse.current.leftButton.wasPressedThisFrame : Mouse.current.rightButton.wasPressedThisFrame;
#elif ENABLE_LEGACY_INPUT_MANAGER
            return UnityEngine.Input.GetMouseButtonDown(button);
#else
            return false;
#endif
        }

        /// <summary>Posición del ratón en píxeles de pantalla.</summary>
        public static Vector3 MousePosition
        {
            get
            {
#if PACIFICO_INPUT_SYSTEM && ENABLE_INPUT_SYSTEM
                return Mouse.current != null ? (Vector3)Mouse.current.position.ReadValue() : Vector3.zero;
#elif ENABLE_LEGACY_INPUT_MANAGER
                return UnityEngine.Input.mousePosition;
#else
                return Vector3.zero;
#endif
            }
        }

        /// <summary>-1 si solo se pulsa <paramref name="negative"/>, +1 si solo <paramref name="positive"/>, 0 si ninguna o ambas.</summary>
        public static float Axis(GameKey negative, GameKey positive)
        {
            return (Held(positive) ? 1f : 0f) - (Held(negative) ? 1f : 0f);
        }

#if PACIFICO_INPUT_SYSTEM && ENABLE_INPUT_SYSTEM
        private static UnityEngine.InputSystem.Controls.KeyControl Resolve(GameKey key)
        {
            Keyboard k = Keyboard.current;
            if (k == null) return null;
            switch (key)
            {
                case GameKey.W: return k.wKey;
                case GameKey.A: return k.aKey;
                case GameKey.S: return k.sKey;
                case GameKey.D: return k.dKey;
                case GameKey.Q: return k.qKey;
                case GameKey.E: return k.eKey;
                case GameKey.R: return k.rKey;
                case GameKey.F: return k.fKey;
                case GameKey.G: return k.gKey;
                case GameKey.H: return k.hKey;
                case GameKey.Space: return k.spaceKey;
                case GameKey.Tab: return k.tabKey;
                case GameKey.Escape: return k.escapeKey;
                case GameKey.LeftShift: return k.leftShiftKey;
                case GameKey.Digit1: return k.digit1Key;
                case GameKey.Digit2: return k.digit2Key;
                case GameKey.Digit3: return k.digit3Key;
                case GameKey.C: return k.cKey;
                case GameKey.LeftControl: return k.leftCtrlKey;
                case GameKey.PageUp: return k.pageUpKey;
                case GameKey.PageDown: return k.pageDownKey;
                default: return null;
            }
        }
#endif

        public static KeyCode ToKeyCode(GameKey key)
        {
            switch (key)
            {
                case GameKey.W: return KeyCode.W;
                case GameKey.A: return KeyCode.A;
                case GameKey.S: return KeyCode.S;
                case GameKey.D: return KeyCode.D;
                case GameKey.Q: return KeyCode.Q;
                case GameKey.E: return KeyCode.E;
                case GameKey.R: return KeyCode.R;
                case GameKey.F: return KeyCode.F;
                case GameKey.G: return KeyCode.G;
                case GameKey.H: return KeyCode.H;
                case GameKey.Space: return KeyCode.Space;
                case GameKey.Tab: return KeyCode.Tab;
                case GameKey.Escape: return KeyCode.Escape;
                case GameKey.LeftShift: return KeyCode.LeftShift;
                case GameKey.Digit1: return KeyCode.Alpha1;
                case GameKey.Digit2: return KeyCode.Alpha2;
                case GameKey.Digit3: return KeyCode.Alpha3;
                case GameKey.C: return KeyCode.C;
                case GameKey.LeftControl: return KeyCode.LeftControl;
                case GameKey.PageUp: return KeyCode.PageUp;
                case GameKey.PageDown: return KeyCode.PageDown;
                default: return KeyCode.None;
            }
        }
    }
}
