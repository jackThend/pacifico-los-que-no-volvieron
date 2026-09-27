using System;
using System.Collections.Generic;
using Pacifico.Core.Narrative;
using Pacifico.Data;
using Pacifico.Input;
using UnityEngine;

namespace Pacifico.Narrative
{
    /// <summary>Lo que el visor necesita de un documento: facsímil, soporte, textos y voz.</summary>
    public sealed class DocumentSpec
    {
        public string Title = string.Empty;
        /// <summary>Remitente, lugar y fecha («Miguel Grau · Pisagua, 2 de junio de 1879»).</summary>
        public string Subtitle = string.Empty;
        public Texture2D Facsimile;
        public DocumentForm Form = DocumentForm.Letter;
        public string Transcription = string.Empty;
        /// <summary>Lo que se lee en el reverso (dedicatorias de fotografías); vacío si está en blanco.</summary>
        public string ReverseText = string.Empty;
        public AudioClip Narration;

        public float Aspect => Facsimile != null && Facsimile.height > 0 ? Facsimile.width / (float)Facsimile.height : 21f / 27f;

        public static DocumentSpec From(CollectibleDataSO data)
        {
            CollectibleRecord record = data.ToRecord();
            return new DocumentSpec
            {
                Title = record.Title,
                Subtitle = record.Sender + (string.IsNullOrEmpty(record.DateLabel) ? string.Empty : " · " + record.DateLabel),
                Facsimile = data.facsimile,
                Form = record.Type == CollectibleType.Photograph ? DocumentForm.Photograph : DocumentForm.Letter,
                Transcription = TranscriptionLayout.FullText(record),
                ReverseText = record.ReverseInscription,
                Narration = data.narration,
            };
        }
    }

    /// <summary>
    /// Visor 3D de documentos históricos (ROADMAP 5.1): modal que pausa el juego y muestra el objeto (carta,
    /// daguerrotipo, fotografía o plano) con su facsímil sobre un pliego curvado. Arrastrar con el botón izquierdo
    /// lo gira; F le da la vuelta; la rueda acerca hacia el cursor; el botón derecho desplaza; T alterna la
    /// transcripción (RePág/AvPág para pasar páginas); R restablece; Esc cierra. La interacción la resuelve
    /// <see cref="DocumentViewerModel"/>, probado fuera del motor.
    /// <para>
    /// El documento se monta lejos de la escena (a 1 km bajo el suelo) con una cámara propia de plano lejano corto,
    /// que dibuja encima de la del juego: el visor no depende de capas ni del contenido de la escena.
    /// </para>
    /// </summary>
    public sealed class DocumentViewer : MonoBehaviour
    {
        [SerializeField] private Vector3 stage = new Vector3(0f, -1000f, 0f);
        [SerializeField] private Color backdrop = new Color(0.07f, 0.06f, 0.05f);
        [SerializeField] private Color paperColor = new Color(0.86f, 0.8f, 0.66f);
        [SerializeField] private Color plateColor = new Color(0.25f, 0.2f, 0.16f);
        [SerializeField, Range(10f, 60f)] private float fieldOfView = 30f;
        [Tooltip("Fracción de la vista que ocupa el documento con zoom 1.")]
        [SerializeField, Range(0.4f, 0.95f)] private float fill = 0.82f;

        private const int CharsPerLine = 46;
        private const int LinesPerPage = 18;

        private Camera _camera;
        private Light _light;
        private GameObject _document;
        private Mesh _mesh;
        private Material _front;
        private Material _back;
        private AudioSource _voice;
        private DocumentViewerModel _model;
        private DocumentSpec _spec;
        private List<List<string>> _pages = new List<List<string>>();
        private int _page;
        private float _previousTimeScale = 1f;
        private CursorLockMode _previousLock;
        private bool _previousVisible;
        private Vector3 _lastMouse;

        private GUIStyle _title;
        private GUIStyle _text;
        private GUIStyle _hint;
        private Texture2D _white;

        public static DocumentViewer Instance { get; private set; }
        public bool IsOpen => _spec != null;
        public DocumentViewerModel Model => _model;

        /// <summary>Se emite al cerrar el visor.</summary>
        public event Action Closed;

        private void Awake()
        {
            Instance = this;
            var cameraObject = new GameObject("Camara_Visor");
            cameraObject.transform.SetParent(transform, false);
            _camera = cameraObject.AddComponent<Camera>();
            _camera.clearFlags = CameraClearFlags.SolidColor;
            _camera.backgroundColor = backdrop;
            _camera.fieldOfView = fieldOfView;
            _camera.nearClipPlane = 0.01f;
            _camera.farClipPlane = 6f;
            _camera.depth = 50f; // por encima de la cámara del juego
            _camera.enabled = false;

            var lightObject = new GameObject("Luz_Visor");
            lightObject.transform.SetParent(transform, false);
            _light = lightObject.AddComponent<Light>();
            _light.type = LightType.Point;
            _light.range = 4f;
            _light.intensity = 1.6f;
            _light.color = new Color(1f, 0.92f, 0.78f); // luz cálida de quinqué
            _light.enabled = false;

            _voice = gameObject.AddComponent<AudioSource>();
            _voice.playOnAwake = false;
            _voice.spatialBlend = 0f;
            _voice.ignoreListenerPause = true;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
            DestroyDocument();
        }

        // ------------------------------------------------------------------------------------------
        // Abrir y cerrar
        // ------------------------------------------------------------------------------------------

        public void Open(DocumentSpec spec)
        {
            if (spec == null) return;
            if (IsOpen) CloseInternal(false);
            _spec = spec;
            DocumentShape shape = DocumentShape.For(spec.Form, spec.Aspect);
            _model = new DocumentViewerModel(shape);
            BuildDocument(shape);

            _pages = TranscriptionLayout.Paginate(TranscriptionLayout.Wrap(spec.Transcription, CharsPerLine), LinesPerPage);
            _page = 0;

            // Modal: pausa el juego y libera el ratón.
            _previousTimeScale = Time.timeScale;
            Time.timeScale = 0f;
            _previousLock = Cursor.lockState;
            _previousVisible = Cursor.visible;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            _camera.enabled = true;
            _light.enabled = true;
            _lastMouse = GameInput.MousePosition;
        }

        public void Close() => CloseInternal(true);

        private void CloseInternal(bool notify)
        {
            if (!IsOpen) return;
            _voice.Stop();
            DestroyDocument();
            _spec = null;
            _model = null;
            _camera.enabled = false;
            _light.enabled = false;
            Time.timeScale = _previousTimeScale;
            Cursor.lockState = _previousLock;
            Cursor.visible = _previousVisible;
            if (notify) Closed?.Invoke();
        }

        private void BuildDocument(DocumentShape shape)
        {
            DestroyDocument();
            _mesh = DocumentMeshBuilder.Build(shape);
            _document = new GameObject("Documento_" + _spec.Title);
            _document.transform.SetParent(transform, false);
            _document.transform.position = stage;
            _document.AddComponent<MeshFilter>().sharedMesh = _mesh;
            var renderer = _document.AddComponent<MeshRenderer>();

            bool plate = _spec.Form == DocumentForm.Daguerreotype;
            _front = CreateMaterial(Color.white, plate ? 0.85f : 0.1f, plate ? 0.6f : 0f);
            if (_spec.Facsimile != null) SetTexture(_front, _spec.Facsimile);
            else SetColor(_front, paperColor);
            _back = CreateMaterial(plate ? plateColor : paperColor * 0.92f, 0.05f, 0f);
            renderer.sharedMaterials = new[] { _front, _back };
        }

        private void DestroyDocument()
        {
            if (_document != null) Destroy(_document);
            if (_mesh != null) Destroy(_mesh);
            if (_front != null) Destroy(_front);
            if (_back != null) Destroy(_back);
            _document = null;
            _mesh = null;
            _front = _back = null;
        }

        private static Material CreateMaterial(Color color, float smoothness, float metallic)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) shader = Shader.Find("Standard");
            var m = new Material(shader);
            SetColor(m, color);
            if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", smoothness);
            if (m.HasProperty("_Glossiness")) m.SetFloat("_Glossiness", smoothness);
            if (m.HasProperty("_Metallic")) m.SetFloat("_Metallic", metallic);
            return m;
        }

        private static void SetColor(Material m, Color c)
        {
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", c);
            if (m.HasProperty("_Color")) m.SetColor("_Color", c);
        }

        private static void SetTexture(Material m, Texture t)
        {
            if (m.HasProperty("_BaseMap")) m.SetTexture("_BaseMap", t);
            if (m.HasProperty("_MainTex")) m.SetTexture("_MainTex", t);
        }

        // ------------------------------------------------------------------------------------------
        // Interacción
        // ------------------------------------------------------------------------------------------

        private void Update()
        {
            if (!IsOpen) return;
            float dt = Time.unscaledDeltaTime;
            Vector3 mouse = GameInput.MousePosition;
            Vector3 delta = mouse - _lastMouse;
            _lastMouse = mouse;
            // Al ver el reverso, izquierda y derecha del documento están invertidas respecto a la pantalla.
            float mirror = _model.ShowingReverse ? -1f : 1f;
            Rect docRect = DocumentScreenRect();

            bool overPanel = _model.ShowTranscription && mouse.x > Screen.width * 0.58f;
            if (!overPanel)
            {
                if (GameInput.MouseHeld(0) && !GameInput.MousePressed(0)) _model.Rotate(delta.x, delta.y);
                if (GameInput.MouseHeld(1) && !GameInput.MousePressed(1)) _model.Pan(mirror * delta.x / docRect.width, delta.y / docRect.height);
                float scroll = GameInput.ScrollDelta;
                if (Mathf.Abs(scroll) > 0.01f)
                {
                    float cx = (mouse.x - docRect.center.x) / docRect.width;
                    float cy = (mouse.y - docRect.center.y) / docRect.height;
                    _model.ZoomAt(scroll, mirror * cx, cy);
                }
            }

            if (GameInput.Pressed(GameKey.F)) _model.Flip();
            if (GameInput.Pressed(GameKey.T)) _model.ToggleTranscription();
            if (GameInput.Pressed(GameKey.R)) _model.Reset();
            if (GameInput.Pressed(GameKey.PageDown)) _page = Mathf.Min(_page + 1, Mathf.Max(0, _pages.Count - 1));
            if (GameInput.Pressed(GameKey.PageUp)) _page = Mathf.Max(0, _page - 1);
            if (GameInput.Pressed(GameKey.L) && _spec.Narration != null)
            {
                if (_voice.isPlaying) _voice.Stop();
                else _voice.PlayOneShot(_spec.Narration);
            }
            if (GameInput.Pressed(GameKey.Escape))
            {
                Close();
                return;
            }

            _model.Step(dt);
            ApplyPose();
        }

        /// <summary>
        /// Coloca documento y cámara: el documento gira sobre su centro; la cámara se acerca con el zoom y mira al
        /// punto del documento que marca el desplazamiento.
        /// </summary>
        private void ApplyPose()
        {
            DocumentShape s = _model.Shape;
            Quaternion rotation = Quaternion.Euler(_model.Pitch, _model.Yaw, 0f);
            // El documento se desplaza en su propio plano (el punto (pan) queda en el eje de la cámara).
            Vector3 pan = new Vector3(_model.PanX * s.WidthM, _model.PanY * s.HeightM, 0f);
            _document.transform.SetPositionAndRotation(stage - rotation * pan, rotation);

            float distance = FitDistance(s) / _model.Zoom;
            _camera.transform.SetPositionAndRotation(stage + new Vector3(0f, 0f, -distance), Quaternion.identity);
            _light.transform.position = stage + new Vector3(-0.25f, 0.35f, -Mathf.Max(0.5f, distance));
        }

        /// <summary>Distancia a la que el documento ocupa la fracción <see cref="fill"/> de la vista.</summary>
        private float FitDistance(DocumentShape s)
        {
            float tan = Mathf.Tan(fieldOfView * 0.5f * Mathf.Deg2Rad);
            float aspect = Mathf.Max(0.1f, Screen.width / (float)Mathf.Max(1, Screen.height));
            float byHeight = s.HeightM / fill / (2f * tan);
            float byWidth = s.WidthM / fill / (2f * tan * aspect);
            return Mathf.Max(byHeight, byWidth);
        }

        /// <summary>Rectángulo del documento en pantalla con zoom 1 (píxeles, origen abajo a la izquierda).</summary>
        private Rect DocumentScreenRect()
        {
            DocumentShape s = _model.Shape;
            float tan = Mathf.Tan(fieldOfView * 0.5f * Mathf.Deg2Rad);
            float distance = FitDistance(s);
            float pixelsPerMeter = Screen.height / (2f * tan * distance);
            float w = s.WidthM * pixelsPerMeter, h = s.HeightM * pixelsPerMeter;
            return new Rect(Screen.width * 0.5f - w * 0.5f, Screen.height * 0.5f - h * 0.5f, Mathf.Max(1f, w), Mathf.Max(1f, h));
        }

        // ------------------------------------------------------------------------------------------
        // Interfaz (IMGUI)
        // ------------------------------------------------------------------------------------------

        private void OnGUI()
        {
            if (!IsOpen) return;
            EnsureStyles();
            GUI.depth = -100;

            GUI.Label(new Rect(24f, 18f, Screen.width - 48f, 30f), _spec.Title, _title);
            GUI.Label(new Rect(24f, 50f, Screen.width - 48f, 22f), _spec.Subtitle, _hint);

            if (_model.ShowingReverse)
            {
                string reverse = string.IsNullOrEmpty(_spec.ReverseText) ? "Reverso en blanco" : "Reverso: «" + _spec.ReverseText + "»";
                GUI.Label(new Rect(24f, 74f, Screen.width - 48f, 22f), reverse, _hint);
            }

            string listen = _spec.Narration != null ? " · L escuchar" : string.Empty;
            GUI.Label(new Rect(24f, Screen.height - 34f, Screen.width - 48f, 22f),
                "Arrastrar: girar · F: dar la vuelta · rueda: acercar · botón derecho: desplazar · T: transcripción · R: restablecer" + listen + " · Esc: cerrar", _hint);

            float blend = _model.TranscriptionBlend;
            if (blend < 0.01f || _pages.Count == 0) return;
            var panel = new Rect(Screen.width * 0.6f, 90f, Screen.width * 0.38f, Screen.height - 150f);
            GUI.color = new Color(0.93f, 0.89f, 0.8f, 0.94f * blend);
            GUI.DrawTexture(panel, _white);
            GUI.color = new Color(1f, 1f, 1f, blend);
            string text = string.Join("\n", _pages[Mathf.Clamp(_page, 0, _pages.Count - 1)]);
            GUI.Label(new Rect(panel.x + 18f, panel.y + 14f, panel.width - 36f, panel.height - 50f), text, _text);
            GUI.Label(new Rect(panel.x + 18f, panel.yMax - 30f, panel.width - 36f, 22f),
                "Página " + (_page + 1) + " de " + _pages.Count + "   (RePág / AvPág)", _text);
            GUI.color = Color.white;
        }

        private void EnsureStyles()
        {
            if (_title != null) return;
            _white = Texture2D.whiteTexture;
            _title = new GUIStyle(GUI.skin.label) { fontSize = 22, fontStyle = FontStyle.Bold };
            _title.normal.textColor = new Color(0.95f, 0.9f, 0.8f);
            _hint = new GUIStyle(GUI.skin.label) { fontSize = 14 };
            _hint.normal.textColor = new Color(0.85f, 0.8f, 0.7f);
            _text = new GUIStyle(GUI.skin.label) { fontSize = 15, wordWrap = false };
            _text.normal.textColor = new Color(0.16f, 0.12f, 0.08f);
        }
    }
}
