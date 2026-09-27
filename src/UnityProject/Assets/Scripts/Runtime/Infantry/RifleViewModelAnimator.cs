using Pacifico.Core.Common;
using Pacifico.Core.Weapons;
using UnityEngine;

namespace Pacifico.Infantry
{
    /// <summary>
    /// Animación procedimental del fusil en primera persona (ROADMAP 3.2: «temporizador y animaciones sincronizados»).
    /// La posición de cada pieza se calcula en cada fotograma a partir de la etapa y el progreso de
    /// <see cref="RifleCycleModel"/>: el cierre abre durante «abrir», el cartucho entra durante «insertar», etc.
    /// Por construcción no puede desincronizarse del temporizador.
    /// <para>
    /// Si más adelante se usan clips (p. ej. de Mixamo) con un <see cref="Animator"/>, este componente le pasa la etapa
    /// (parámetro entero <c>Etapa</c>) y la velocidad de reproducción (<c>VelocidadEtapa</c> = 1 / duración de la etapa,
    /// para clips normalizados a 1 s), de modo que cada clip dura exactamente lo que dura su etapa.
    /// </para>
    /// Se coloca en un hijo del arma («Mecanica»): <see cref="IronSightViewModel"/> mueve la raíz (cadera ↔ encare)
    /// y este componente añade encima el retroceso y la inclinación de recarga.
    /// </summary>
    public sealed class RifleViewModelAnimator : MonoBehaviour
    {
        [SerializeField] private RifleController rifle;

        [Header("Piezas")]
        [Tooltip("Palanca, cerrojo o bloque: se interpola entre la pose cerrada y la abierta.")]
        [SerializeField] private Transform actionPart;
        [SerializeField] private Vector3 actionOpenEuler = new Vector3(55f, 0f, 0f);
        [SerializeField] private Vector3 actionOpenOffset = Vector3.zero;
        [Tooltip("Martillo o pieza de disparo (Remington, Chassepot). Opcional.")]
        [SerializeField] private Transform hammer;
        [SerializeField] private Vector3 hammerCockedEuler = new Vector3(-35f, 0f, 0f);
        [Tooltip("Cartucho visible durante la inserción.")]
        [SerializeField] private Transform cartridge;
        [SerializeField] private Vector3 cartridgeFromLocal = new Vector3(0.12f, -0.12f, -0.1f);
        [SerializeField] private Vector3 cartridgeToLocal = new Vector3(0f, 0.02f, 0.02f);
        [Tooltip("Punto de expulsión de la vaina.")]
        [SerializeField] private Transform ejectionPort;

        [Header("Poses del arma")]
        [SerializeField] private Vector3 reloadEuler = new Vector3(-12f, 18f, 28f);
        [SerializeField] private Vector3 reloadOffset = new Vector3(-0.04f, -0.05f, -0.04f);
        [SerializeField] private float kickBackM = 0.06f;
        [SerializeField] private float kickPitchDeg = 7f;

        [Header("Animator opcional")]
        [SerializeField] private Animator animator;

        /// <summary>Capa integrada «Ignore Raycast» (índice 2).</summary>
        private const int IgnoreRaycastLayer = 2;

        private static readonly int StageParam = Animator.StringToHash("Etapa");
        private static readonly int SpeedParam = Animator.StringToHash("VelocidadEtapa");

        private Quaternion _actionClosedRotation;
        private Vector3 _actionClosedPosition;
        private Quaternion _hammerRestRotation;
        private float _reloadBlend;
        private float _reloadVelocity;
        private Material _caseMaterial;

        public void Configure(RifleController owner, Transform action, Vector3 openEuler, Transform hammerPart, Transform cartridgePart, Transform port)
        {
            rifle = owner;
            actionPart = action;
            actionOpenEuler = openEuler;
            hammer = hammerPart;
            cartridge = cartridgePart;
            ejectionPort = port;
        }

        private void Start()
        {
            if (actionPart != null)
            {
                _actionClosedRotation = actionPart.localRotation;
                _actionClosedPosition = actionPart.localPosition;
            }
            if (hammer != null) _hammerRestRotation = hammer.localRotation;
            if (cartridge != null)
            {
                Renderer r = cartridge.GetComponentInChildren<Renderer>();
                if (r != null) _caseMaterial = r.sharedMaterial;
                cartridge.gameObject.SetActive(false);
            }
        }

        private void OnEnable()
        {
            if (rifle != null) rifle.CycleEvent += OnCycleEvent;
        }

        private void OnDisable()
        {
            if (rifle != null) rifle.CycleEvent -= OnCycleEvent;
        }

        private void OnCycleEvent(RifleEvent e)
        {
            if (e.Type == RifleEventType.CaseEjected) EjectCase();
            if (e.Type == RifleEventType.StageStarted && animator != null)
            {
                animator.SetInteger(StageParam, (int)e.Stage);
                animator.SetFloat(SpeedParam, e.Duration > 0f ? 1f / e.Duration : 1f);
            }
        }

        private void LateUpdate()
        {
            if (rifle == null || rifle.Model == null) return;
            RifleCycleModel m = rifle.Model;
            float p = m.StageProgress;
            float eased = RifleAnimationCurves.Ease(p);

            // 1) Mecanismo: exactamente la fracción de la etapa en curso.
            float open = RifleAnimationCurves.ActionOpen(m.Stage, p, m.Profile.UsesMagazine);
            if (actionPart != null)
            {
                actionPart.localRotation = _actionClosedRotation * Quaternion.Euler(actionOpenEuler * open);
                actionPart.localPosition = _actionClosedPosition + actionOpenOffset * open;
            }
            if (hammer != null)
            {
                float cocked = RifleAnimationCurves.HammerCocked(m.Stage, p, m.Profile.NeedsManualCocking, m.IsCocked);
                hammer.localRotation = _hammerRestRotation * Quaternion.Euler(hammerCockedEuler * cocked);
            }
            if (cartridge != null)
            {
                bool inserting = m.Stage == RifleStage.InsertingCartridge || m.Stage == RifleStage.LoadingMagazine;
                cartridge.gameObject.SetActive(inserting);
                if (inserting) cartridge.localPosition = Vector3.Lerp(cartridgeFromLocal, cartridgeToLocal, eased);
            }

            // 2) Arma entera: retroceso al percutir e inclinación hacia el soldado mientras se recarga.
            float kick = RifleAnimationCurves.Kick(m.Stage, p);
            // Pose de recarga solo mientras el fusil se manipula de verdad (el ciclo de palanca no la usa).
            float targetReload = m.BlocksAiming ? 1f
                : m.Stage == RifleStage.Shouldering && !m.Profile.UsesMagazine ? 1f - eased
                : 0f;
            _reloadBlend = MathUtil.SmoothDamp(_reloadBlend, targetReload, ref _reloadVelocity, 0.06f, Time.deltaTime);

            transform.localPosition = reloadOffset * _reloadBlend + new Vector3(0f, 0f, -kickBackM * kick);
            transform.localRotation = Quaternion.Euler(reloadEuler * _reloadBlend) * Quaternion.Euler(-kickPitchDeg * kick, 0f, 0f);
        }

        private void EjectCase()
        {
            Transform port = ejectionPort != null ? ejectionPort : transform;
            GameObject shell = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            shell.name = "Vaina";
            shell.transform.SetPositionAndRotation(port.position, port.rotation * Quaternion.Euler(90f, 0f, 0f));
            shell.transform.localScale = new Vector3(0.012f, 0.028f, 0.012f);
            if (_caseMaterial != null) shell.GetComponent<Renderer>().sharedMaterial = _caseMaterial;
            // Capa «Ignore Raycast»: las balas siguientes no chocan con la vaina en el aire.
            shell.layer = IgnoreRaycastLayer;
            Collider shellCollider = shell.GetComponent<Collider>();
            var player = rifle.GetComponent<CharacterController>();
            if (player != null) Physics.IgnoreCollision(shellCollider, player);
            var body = shell.AddComponent<Rigidbody>();
            body.mass = 0.015f;
            Vector3 velocity = port.right * 2.2f + port.up * 1.6f - port.forward * 0.4f;
#if UNITY_6000_0_OR_NEWER
            body.linearVelocity = velocity;
#else
            body.velocity = velocity;
#endif
            body.angularVelocity = new Vector3(Random.Range(-20f, 20f), Random.Range(-20f, 20f), 0f);
            Destroy(shell, 6f);
        }
    }
}
