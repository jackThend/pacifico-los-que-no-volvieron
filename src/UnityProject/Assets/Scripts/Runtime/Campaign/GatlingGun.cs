using Pacifico.Core.Common;
using Pacifico.Core.Infantry;
using Pacifico.Core.Weapons;
using Pacifico.Data;
using Pacifico.Effects;
using Pacifico.Infantry;
using UnityEngine;

namespace Pacifico.Campaign
{
    /// <summary>
    /// Ametralladora Gatling de la defensa del Morro (capítulo 6: «las ametralladoras Gatling rugen»). La sirve su
    /// dotación: ráfagas contra el asaltante visible más cercano, con balas reales. Cartucho y balística de la ficha que
    /// se le asigne (en el prototipo, la del Remington); la cadencia de ráfaga es una estimación de juego.
    /// </summary>
    public sealed class GatlingGun : MonoBehaviour
    {
        [SerializeField] private WeaponDataSO cartridge;
        [SerializeField] private Faction side = Faction.Peru;
        [SerializeField] private Transform barrels;
        [SerializeField] private float rangeM = 300f;
        [SerializeField] private int burstRounds = 12;
        [SerializeField] private float roundsPerSecond = 8f;
        [SerializeField] private float pauseSeconds = 4f;
        [Tooltip("Dispersión respecto a un fusilero (montura fija, pero tiro rápido).")]
        [SerializeField] private float dispersionScale = 1.6f;

        private static readonly RaycastHit[] Hits = new RaycastHit[16];
        private WeaponSpec _weapon;
        private float _drag;
        private System.Random _random;
        private int _left;
        private float _next;
        private float _spin;

        public bool Firing { get; set; }

        public void Configure(WeaponDataSO ammo, Faction owner, Transform spinning)
        {
            cartridge = ammo;
            side = owner;
            barrels = spinning;
        }

        private void Start()
        {
            _weapon = cartridge != null ? cartridge.ToSpec() : WeaponCatalog.Remington();
            _drag = SmallArmsBallistics.DragFactor(_weapon);
            _random = new System.Random(GetInstanceID());
        }

        private void Update()
        {
            if (_weapon == null || !Firing || Time.time < _next) return;
            Combatant target = FindTarget(out float distance);
            if (target == null)
            {
                _next = Time.time + 0.5f;
                return;
            }
            Vector3 muzzle = barrels != null ? barrels.position : transform.position + Vector3.up;
            transform.rotation = Quaternion.RotateTowards(transform.rotation,
                Quaternion.LookRotation(Vector3.ProjectOnPlane(target.transform.position - transform.position, Vector3.up)), 90f);
            SmallArmsBallistics.TrySolveElevation(distance, _weapon.MuzzleVelocityMps, _drag, out float elevation, 0f);
            float sigma = FireModelSigma() * dispersionScale;
            Quaternion look = Quaternion.LookRotation(target.AimPoint - muzzle);
            Vector3 direction = look * Quaternion.Euler(-(elevation + Gaussian() * sigma), Gaussian() * sigma, 0f) * Vector3.forward;
            RifleBullet.Fire(muzzle + direction * 0.5f, direction * _weapon.MuzzleVelocityMps, _weapon, _drag, gameObject, Physics.DefaultRaycastLayers);
            BlackPowderSmoke.Emit(muzzle + direction * 0.6f, direction, _weapon.PowderChargeG, Vector3.zero);
            _spin += 36f;
            if (barrels != null) barrels.localRotation = Quaternion.Euler(0f, 0f, _spin);

            if (_left <= 0) _left = burstRounds;
            _left--;
            _next = Time.time + (_left > 0 ? 1f / roundsPerSecond : pauseSeconds);
        }

        private float FireModelSigma() => Pacifico.Core.Tactics.FireModel.SigmaDeg(_weapon);

        private Combatant FindTarget(out float distance)
        {
            Combatant best = null;
            distance = rangeM;
            Vector3 eye = transform.position + Vector3.up * 1.2f;
            foreach (Combatant c in Combatant.All)
            {
                if (!c.Alive || (c.Faction == Faction.Chile) == (side == Faction.Chile)) continue;
                float d = Vector3.Distance(eye, c.AimPoint);
                if (d >= distance || !Clear(eye, c)) continue;
                best = c;
                distance = d;
            }
            return best;
        }

        private bool Clear(Vector3 eye, Combatant other)
        {
            Vector3 to = other.AimPoint - eye;
            int count = Physics.RaycastNonAlloc(eye, to.normalized, Hits, to.magnitude, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++)
            {
                Transform t = Hits[i].collider.transform;
                if (t.IsChildOf(transform) || t.IsChildOf(other.transform) || t.GetComponentInParent<Combatant>() != null) continue;
                return false;
            }
            return true;
        }

        private float Gaussian() => RifleShotSolver.Gaussian(_random);
    }

    /// <summary>Algo que el jugador acciona con E a su lado (el detonador de las minas).</summary>
    public sealed class InteractionPoint : MonoBehaviour
    {
        [SerializeField] private string prompt = "Accionar";
        [SerializeField] private float reach = 2.2f;

        private Transform _player;
        private GUIStyle _style;

        public bool Active { get; set; }
        public bool Used { get; private set; }
        public event System.Action<InteractionPoint> OnUsed;

        public void Configure(string text, Transform player)
        {
            prompt = text;
            _player = player;
        }

        private bool InReach => _player != null && Vector3.Distance(_player.position, transform.position) <= reach;

        private void Update()
        {
            if (!Active || Used || !InReach || !Input.GameInput.Pressed(Input.GameKey.E)) return;
            Used = true;
            OnUsed?.Invoke(this);
        }

        private void OnGUI()
        {
            if (!Active || Used || !InReach) return;
            if (_style == null)
            {
                _style = new GUIStyle(GUI.skin.label) { fontSize = 16, alignment = TextAnchor.MiddleCenter };
                _style.normal.textColor = new Color(1f, 0.95f, 0.8f);
            }
            GUI.Label(new Rect(Screen.width * 0.5f - 200f, Screen.height * 0.5f + 40f, 400f, 24f), "[E] " + prompt, _style);
        }
    }
}
