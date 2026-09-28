using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace Pacifico.Runtime
{
    /// <summary>Teclas que usa el juego (independientes del sistema de entrada).</summary>
    public enum GameKey
    {
        W,
        A,
        S,
        D,
        Digit1,
        Digit2,
        Digit3
    }

    /// <summary>
    /// Capa única de entrada. Usa el Input System nuevo si el proyecto lo tiene
    /// activo (ENABLE_INPUT_SYSTEM) y, si no, el Input Manager clásico, de modo
    /// que el prototipo responde al teclado sin tocar Project Settings.
    /// </summary>
    public static class GameInput
    {
        public static bool KeyDown(GameKey key)
        {
#if ENABLE_INPUT_SYSTEM
            var control = KeyControlFor(key);
            if (control != null) return control.wasPressedThisFrame;
#endif
#if ENABLE_LEGACY_INPUT_MANAGER || !ENABLE_INPUT_SYSTEM
            return UnityEngine.Input.GetKeyDown(KeyCodeFor(key));
#else
            return false;
#endif
        }

        public static bool KeyHeld(GameKey key)
        {
#if ENABLE_INPUT_SYSTEM
            var control = KeyControlFor(key);
            if (control != null) return control.isPressed;
#endif
#if ENABLE_LEGACY_INPUT_MANAGER || !ENABLE_INPUT_SYSTEM
            return UnityEngine.Input.GetKey(KeyCodeFor(key));
#else
            return false;
#endif
        }

        public static bool PrimaryClickDown()
        {
#if ENABLE_INPUT_SYSTEM
            if (Mouse.current != null) return Mouse.current.leftButton.wasPressedThisFrame;
#endif
#if ENABLE_LEGACY_INPUT_MANAGER || !ENABLE_INPUT_SYSTEM
            return UnityEngine.Input.GetMouseButtonDown(0);
#else
            return false;
#endif
        }

        /// <summary>Posición del puntero en píxeles de pantalla; false si no hay ratón.</summary>
        public static bool TryGetPointer(out Vector3 screenPosition)
        {
#if ENABLE_INPUT_SYSTEM
            if (Mouse.current != null)
            {
                screenPosition = Mouse.current.position.ReadValue();
                return true;
            }
#endif
#if ENABLE_LEGACY_INPUT_MANAGER || !ENABLE_INPUT_SYSTEM
            screenPosition = UnityEngine.Input.mousePosition;
            return true;
#else
            screenPosition = default;
            return false;
#endif
        }

#if ENABLE_INPUT_SYSTEM
        private static UnityEngine.InputSystem.Controls.KeyControl KeyControlFor(GameKey key)
        {
            var keyboard = Keyboard.current;
            if (keyboard == null) return null;
            switch (key)
            {
                case GameKey.W: return keyboard.wKey;
                case GameKey.A: return keyboard.aKey;
                case GameKey.S: return keyboard.sKey;
                case GameKey.D: return keyboard.dKey;
                case GameKey.Digit1: return keyboard.digit1Key;
                case GameKey.Digit2: return keyboard.digit2Key;
                default: return keyboard.digit3Key;
            }
        }
#endif

#if ENABLE_LEGACY_INPUT_MANAGER || !ENABLE_INPUT_SYSTEM
        private static KeyCode KeyCodeFor(GameKey key)
        {
            switch (key)
            {
                case GameKey.W: return KeyCode.W;
                case GameKey.A: return KeyCode.A;
                case GameKey.S: return KeyCode.S;
                case GameKey.D: return KeyCode.D;
                case GameKey.Digit1: return KeyCode.Alpha1;
                case GameKey.Digit2: return KeyCode.Alpha2;
                default: return KeyCode.Alpha3;
            }
        }
#endif
    }
}
