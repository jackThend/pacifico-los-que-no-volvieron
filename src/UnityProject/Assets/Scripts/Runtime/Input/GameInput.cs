using UnityEngine;
#if PACIFICO_INPUT_SYSTEM && ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
#endif

namespace Pacifico.Input
{
    /// <summary>Teclas y botones usados por los prototipos.</summary>
    public enum GameKey
    {
        W, A, S, D, Q, E, R, F, G, H, Space, Tab, Escape, LeftShift,
        Digit1, Digit2, Digit3,
        C, LeftControl, PageUp, PageDown, V, T, L, N, M, F3, B,
    }

    /// <summary>Botones de un mando tipo Xbox (y las cuatro direcciones del stick izquierdo como botones).</summary>
    public enum PadButton
    {
        None, A, B, X, Y, LB, RB, LT, RT, L3, R3, Start, View,
        DUp, DDown, DLeft, DRight, LUp, LDown, LLeft, LRight,
    }

    /// <summary>
    /// Fachada de entrada que funciona con el Input Manager clásico o con el paquete Input System
    /// (Unity 6 crea los proyectos nuevos con el segundo activo). Evita depender de un único backend.
    /// Con el Input System también lee el mando (Xbox o compatible): cada tecla tiene su botón
    /// (<see cref="PadFor"/>), el stick izquierdo mueve, el derecho mira, RT dispara y LT apunta.
    /// Cuando hace falta un puntero (estrategia, torre, mapas), el stick derecho mueve un cursor virtual.
    /// </summary>
    public static class GameInput
    {
        /// <summary>Píxeles por segundo que equivale el stick derecho a fondo al mirar (la sensibilidad lo escala).</summary>
        private const float PadLookPixelsPerSecond = 2100f;
        /// <summary>El cursor virtual cruza la altura de la pantalla en algo menos de un segundo.</summary>
        private const float PadCursorScreensPerSecond = 1.1f;

        private static int s_frame = -1;
        private static bool s_usingPad;
        private static bool s_decided;
        private static int s_pointerFrame = -1000;
        private static Vector2 s_cursor;

        /// <summary>Solo para las fotos de la interfaz: fuerza las ayudas de mando (true) o de teclado (false).</summary>
        public static bool? ForceGamepadDisplay { get; set; }

        /// <summary>En pausa el juego no lee mandos; solo Esc sigue funcionando, para quitarla.</summary>
        public static bool Blocked { get; set; }

        /// <summary>El jugador usa el mando (la última entrada vino de él); las ayudas muestran sus botones.</summary>
        public static bool UsingGamepad
        {
            get
            {
                Tick();
                return s_usingPad;
            }
        }

        /// <summary>Hay un mando conectado.</summary>
        public static bool PadConnected
        {
            get
            {
#if PACIFICO_INPUT_SYSTEM && ENABLE_INPUT_SYSTEM
                return Gamepad.current != null;
#else
                return false;
#endif
            }
        }

        /// <summary>Algún sistema ha leído el puntero hace poco: el stick derecho mueve el cursor virtual en vez de la cámara.</summary>
        public static bool PointerInUse => Time.frameCount - s_pointerFrame <= 2;

        /// <summary>Pide el puntero en este fotograma aunque aún no se lea (p. ej. mientras se espera un clic en el mapa).</summary>
        public static void WantPointer()
        {
            if (Cursor.lockState != CursorLockMode.Locked) s_pointerFrame = Time.frameCount;
        }

        /// <summary>Posición del cursor virtual del mando (píxeles de pantalla, origen abajo a la izquierda).</summary>
        public static Vector2 VirtualCursor => s_cursor;

        public static bool Held(GameKey key)
        {
            if (Blocked) return false;
            return KeyHeld(key) || PadHeldRaw(PadFor(key));
        }

        public static bool Pressed(GameKey key)
        {
            if (Blocked && key != GameKey.Escape) return false;
            if (KeyPressed(key)) return true;
            return PadPressedRaw(PadFor(key));
        }

        /// <summary>Botón del mando pulsado en este fotograma (respeta la pausa).</summary>
        public static bool PadPressed(PadButton button) => !Blocked && PadPressedRaw(button);

        /// <summary>Botón del mando mantenido (respeta la pausa).</summary>
        public static bool PadHeld(PadButton button) => !Blocked && PadHeldRaw(button);

        /// <summary>Stick izquierdo o derecho (respeta la pausa).</summary>
        public static Vector2 PadStick(bool right) => Blocked ? Vector2.zero : PadStickRaw(right);

        /// <summary>Botón del mando que hace de cada tecla. Pensado para un mando Xbox; ver la tabla en DEV_LOG.</summary>
        public static PadButton PadFor(GameKey key)
        {
            switch (key)
            {
                case GameKey.W: return PadButton.LUp;
                case GameKey.S: return PadButton.LDown;
                case GameKey.A: return PadButton.LLeft;
                case GameKey.D: return PadButton.LRight;
                case GameKey.Space: return PadButton.A;
                case GameKey.N: return PadButton.A;
                case GameKey.E: return PadButton.X;
                case GameKey.R: return PadButton.X;
                case GameKey.F: return PadButton.RB;
                case GameKey.G: return PadButton.LB;
                case GameKey.L: return PadButton.LB;
                case GameKey.C: return PadButton.B;
                case GameKey.LeftControl: return PadButton.B;
                case GameKey.LeftShift: return PadButton.L3;
                case GameKey.Tab: return PadButton.View;
                case GameKey.Escape: return PadButton.Start;
                case GameKey.Digit1: return PadButton.DLeft;
                case GameKey.Digit2: return PadButton.DUp;
                case GameKey.Digit3: return PadButton.DRight;
                case GameKey.H: return PadButton.DDown;
                case GameKey.PageUp: return PadButton.DUp;
                case GameKey.PageDown: return PadButton.DDown;
                case GameKey.M: return PadButton.Y;
                case GameKey.B: return PadButton.Y;
                case GameKey.T: return PadButton.Y;
                default: return PadButton.None;
            }
        }

        /// <summary>Movimiento del ratón en píxeles desde el fotograma anterior (con mando: el stick derecho).</summary>
        public static Vector2 MouseDelta
        {
            get
            {
                if (Blocked) return Vector2.zero;
                Vector2 delta = Vector2.zero;
#if PACIFICO_INPUT_SYSTEM && ENABLE_INPUT_SYSTEM
                if (Mouse.current != null) delta = Mouse.current.delta.ReadValue();
#elif ENABLE_LEGACY_INPUT_MANAGER
                delta = new Vector2(UnityEngine.Input.GetAxisRaw("Mouse X"), UnityEngine.Input.GetAxisRaw("Mouse Y")) * 10f;
#endif
                if (!PointerInUse)
                {
                    Vector2 stick = Curve(PadStickRaw(true));
                    delta += stick * (PadLookPixelsPerSecond * Time.unscaledDeltaTime);
                }
                return delta;
            }
        }

        /// <summary>Rueda del ratón normalizada (positiva = acercar).</summary>
        public static float ScrollDelta
        {
            get
            {
                if (Blocked) return 0f;
#if PACIFICO_INPUT_SYSTEM && ENABLE_INPUT_SYSTEM
                return Mouse.current != null ? Mouse.current.scroll.ReadValue().y / 120f : 0f;
#elif ENABLE_LEGACY_INPUT_MANAGER
                return UnityEngine.Input.mouseScrollDelta.y;
#else
                return 0f;
#endif
            }
        }

        /// <summary>Botón 0 (izquierdo; en el mando, RT) o 1 (derecho; en el mando, LT).</summary>
        public static bool MouseHeld(int button)
        {
            if (Blocked) return false;
            if (PadHeldRaw(button == 0 ? PadButton.RT : PadButton.LT)) return true;
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
            if (Blocked) return false;
            if (PadPressedRaw(button == 0 ? PadButton.RT : PadButton.LT)) return true;
#if PACIFICO_INPUT_SYSTEM && ENABLE_INPUT_SYSTEM
            if (Mouse.current == null) return false;
            return button == 0 ? Mouse.current.leftButton.wasPressedThisFrame : Mouse.current.rightButton.wasPressedThisFrame;
#elif ENABLE_LEGACY_INPUT_MANAGER
            return UnityEngine.Input.GetMouseButtonDown(button);
#else
            return false;
#endif
        }

        public static bool MouseReleased(int button)
        {
            if (Blocked) return false;
            if (PadReleasedRaw(button == 0 ? PadButton.RT : PadButton.LT)) return true;
#if PACIFICO_INPUT_SYSTEM && ENABLE_INPUT_SYSTEM
            if (Mouse.current == null) return false;
            return button == 0 ? Mouse.current.leftButton.wasReleasedThisFrame : Mouse.current.rightButton.wasReleasedThisFrame;
#elif ENABLE_LEGACY_INPUT_MANAGER
            return UnityEngine.Input.GetMouseButtonUp(button);
#else
            return false;
#endif
        }

        /// <summary>Posición del ratón en píxeles de pantalla (con mando y cursor libre: el cursor virtual).</summary>
        public static Vector3 MousePosition
        {
            get
            {
                Tick();
                if (Cursor.lockState != CursorLockMode.Locked) s_pointerFrame = Time.frameCount;
                if (s_usingPad && Cursor.lockState != CursorLockMode.Locked) return s_cursor;
                return RawMousePosition();
            }
        }

        /// <summary>
        /// -1 si solo se pulsa <paramref name="negative"/>, +1 si solo <paramref name="positive"/>, 0 si ninguna o ambas.
        /// Con mando: A/D y S/W son el stick izquierdo (analógico) y Q/E los gatillos superiores LB/RB.
        /// </summary>
        public static float Axis(GameKey negative, GameKey positive)
        {
            if (Blocked) return 0f;
            float value = (KeyHeld(positive) ? 1f : 0f) - (KeyHeld(negative) ? 1f : 0f);
            Vector2 stick = PadStickRaw(false);
            if (negative == GameKey.A && positive == GameKey.D) value += stick.x;
            else if (negative == GameKey.S && positive == GameKey.W) value += stick.y;
            else if (negative == GameKey.Q && positive == GameKey.E) value += (PadHeldRaw(PadButton.RB) ? 1f : 0f) - (PadHeldRaw(PadButton.LB) ? 1f : 0f);
            return Mathf.Clamp(value, -1f, 1f);
        }

        /// <summary>Respuesta curva del stick: fino cerca del centro, rápido a fondo.</summary>
        private static Vector2 Curve(Vector2 stick) => stick * stick.magnitude;

        // ------------------------------------------------------------------------------------------
        // Estado por fotograma: qué dispositivo usa el jugador y dónde está el cursor virtual
        // ------------------------------------------------------------------------------------------

        /// <summary>Se llama al leer la entrada; solo trabaja una vez por fotograma.</summary>
        public static void Tick()
        {
            if (s_frame == Time.frameCount) return;
            s_frame = Time.frameCount;
#if PACIFICO_INPUT_SYSTEM && ENABLE_INPUT_SYSTEM
            Gamepad pad = Gamepad.current;
            bool padActive = pad != null && (pad.leftStick.ReadValue().sqrMagnitude > 0.1f || pad.rightStick.ReadValue().sqrMagnitude > 0.1f
                || pad.leftTrigger.ReadValue() > 0.3f || pad.rightTrigger.ReadValue() > 0.3f || AnyPadButton(pad));
            bool keysActive = (Keyboard.current != null && Keyboard.current.anyKey.wasPressedThisFrame)
                || (Mouse.current != null && (Mouse.current.delta.ReadValue().sqrMagnitude > 9f
                    || Mouse.current.leftButton.wasPressedThisFrame || Mouse.current.rightButton.wasPressedThisFrame));
            if (!s_decided)
            {
                s_decided = true;
                s_usingPad = pad != null;
                s_cursor = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
            }
            if (padActive && !s_usingPad)
            {
                s_usingPad = true;
                Vector3 mouse = RawMousePosition();
                if (mouse.x > 0f && mouse.y > 0f && mouse.x < Screen.width && mouse.y < Screen.height) s_cursor = mouse;
            }
            else if (keysActive && !padActive) s_usingPad = false;
            if (pad == null) s_usingPad = false;
            if (ForceGamepadDisplay.HasValue) s_usingPad = ForceGamepadDisplay.Value;

            if (s_usingPad && PointerInUse && Cursor.lockState != CursorLockMode.Locked)
            {
                Vector2 move = Curve(pad.rightStick.ReadValue());
                s_cursor += move * (Screen.height * PadCursorScreensPerSecond * Time.unscaledDeltaTime);
                s_cursor.x = Mathf.Clamp(s_cursor.x, 0f, Screen.width - 1f);
                s_cursor.y = Mathf.Clamp(s_cursor.y, 0f, Screen.height - 1f);
            }
#endif
        }

#if PACIFICO_INPUT_SYSTEM && ENABLE_INPUT_SYSTEM
        private static bool AnyPadButton(Gamepad pad)
        {
            return pad.buttonSouth.isPressed || pad.buttonEast.isPressed || pad.buttonWest.isPressed || pad.buttonNorth.isPressed
                || pad.leftShoulder.isPressed || pad.rightShoulder.isPressed || pad.startButton.isPressed || pad.selectButton.isPressed
                || pad.dpad.ReadValue().sqrMagnitude > 0.1f || pad.leftStickButton.isPressed || pad.rightStickButton.isPressed;
        }

        private static ButtonControl Control(PadButton button)
        {
            Gamepad pad = Gamepad.current;
            if (pad == null) return null;
            switch (button)
            {
                case PadButton.A: return pad.buttonSouth;
                case PadButton.B: return pad.buttonEast;
                case PadButton.X: return pad.buttonWest;
                case PadButton.Y: return pad.buttonNorth;
                case PadButton.LB: return pad.leftShoulder;
                case PadButton.RB: return pad.rightShoulder;
                case PadButton.LT: return pad.leftTrigger;
                case PadButton.RT: return pad.rightTrigger;
                case PadButton.L3: return pad.leftStickButton;
                case PadButton.R3: return pad.rightStickButton;
                case PadButton.Start: return pad.startButton;
                case PadButton.View: return pad.selectButton;
                case PadButton.DUp: return pad.dpad.up;
                case PadButton.DDown: return pad.dpad.down;
                case PadButton.DLeft: return pad.dpad.left;
                case PadButton.DRight: return pad.dpad.right;
                case PadButton.LUp: return pad.leftStick.up;
                case PadButton.LDown: return pad.leftStick.down;
                case PadButton.LLeft: return pad.leftStick.left;
                case PadButton.LRight: return pad.leftStick.right;
                default: return null;
            }
        }
#endif

        /// <summary>Lectura del mando sin mirar la pausa (para los menús).</summary>
        public static bool PadPressedRaw(PadButton button)
        {
#if PACIFICO_INPUT_SYSTEM && ENABLE_INPUT_SYSTEM
            ButtonControl control = Control(button);
            return control != null && control.wasPressedThisFrame;
#else
            return false;
#endif
        }

        public static bool PadHeldRaw(PadButton button)
        {
#if PACIFICO_INPUT_SYSTEM && ENABLE_INPUT_SYSTEM
            ButtonControl control = Control(button);
            return control != null && control.isPressed;
#else
            return false;
#endif
        }

        private static bool PadReleasedRaw(PadButton button)
        {
#if PACIFICO_INPUT_SYSTEM && ENABLE_INPUT_SYSTEM
            ButtonControl control = Control(button);
            return control != null && control.wasReleasedThisFrame;
#else
            return false;
#endif
        }

        private static Vector2 PadStickRaw(bool right)
        {
#if PACIFICO_INPUT_SYSTEM && ENABLE_INPUT_SYSTEM
            Gamepad pad = Gamepad.current;
            if (pad == null) return Vector2.zero;
            return right ? pad.rightStick.ReadValue() : pad.leftStick.ReadValue();
#else
            return Vector2.zero;
#endif
        }

        private static Vector3 RawMousePosition()
        {
#if PACIFICO_INPUT_SYSTEM && ENABLE_INPUT_SYSTEM
            return Mouse.current != null ? (Vector3)Mouse.current.position.ReadValue() : Vector3.zero;
#elif ENABLE_LEGACY_INPUT_MANAGER
            return UnityEngine.Input.mousePosition;
#else
            return Vector3.zero;
#endif
        }

        // ------------------------------------------------------------------------------------------
        // Teclado
        // ------------------------------------------------------------------------------------------

        private static bool KeyHeld(GameKey key)
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

        private static bool KeyPressed(GameKey key)
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

#if PACIFICO_INPUT_SYSTEM && ENABLE_INPUT_SYSTEM
        private static KeyControl Resolve(GameKey key)
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
                case GameKey.V: return k.vKey;
                case GameKey.T: return k.tKey;
                case GameKey.L: return k.lKey;
                case GameKey.N: return k.nKey;
                case GameKey.M: return k.mKey;
                case GameKey.F3: return k.f3Key;
                case GameKey.B: return k.bKey;
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
                case GameKey.V: return KeyCode.V;
                case GameKey.T: return KeyCode.T;
                case GameKey.L: return KeyCode.L;
                case GameKey.N: return KeyCode.N;
                case GameKey.M: return KeyCode.M;
                case GameKey.F3: return KeyCode.F3;
                case GameKey.B: return KeyCode.B;
                case GameKey.LeftControl: return KeyCode.LeftControl;
                case GameKey.PageUp: return KeyCode.PageUp;
                case GameKey.PageDown: return KeyCode.PageDown;
                default: return KeyCode.None;
            }
        }
    }
}
