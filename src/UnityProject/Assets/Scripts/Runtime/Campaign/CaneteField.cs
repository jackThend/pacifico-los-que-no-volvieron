using System;
using Pacifico.Core.Infantry;
using Pacifico.Infantry;
using Pacifico.Input;
using UnityEngine;

namespace Pacifico.Campaign
{
    /// <summary>
    /// Torpedo de tierra en escena (capítulo 7): el estado es <see cref="Tripwire"/>. El alambre no se dibuja hasta que el
    /// jugador lo descubre avanzando agachado; agachado y quieto junto a la carga, manteniendo E, lo corta. Si lo cruza
    /// armado, estalla y mata a quien esté cerca.
    /// </summary>
    public sealed class TripwireMine : MonoBehaviour
    {
        [SerializeField] private Vector3 anchorA;
        [SerializeField] private Vector3 anchorB;
        [SerializeField] private Renderer wire;
        [SerializeField] private Material blast;
        [Tooltip("Caña clavada que marca el paso seguro una vez cortado el alambre.")]
        [SerializeField] private GameObject safeMarker;

        private Tripwire _model;
        private FirstPersonController _player;
        private Vector3 _lastPlayer;
        private bool _hasLast;
        private GUIStyle _style;

        public Tripwire Model => _model;

        public event Action<TripwireMine> Disarmed;
        /// <summary>Ha estallado (true si lo hizo estallar el jugador).</summary>
        public event Action<TripwireMine, bool> Exploded;

        public void Configure(Vector3 a, Vector3 b, Renderer wireRenderer, Material blastMaterial, GameObject marker)
        {
            anchorA = a;
            anchorB = b;
            wire = wireRenderer;
            blast = blastMaterial;
            safeMarker = marker;
        }

        public void Bind(FirstPersonController player) => _player = player;

        private void Awake()
        {
            _model = new Tripwire(anchorA.x, anchorA.z, anchorB.x, anchorB.z);
            if (wire != null) wire.enabled = false;
            if (safeMarker != null) safeMarker.SetActive(false);
        }

        private bool Crouched => _player != null && _player.Motor != null && _player.Motor.Stance == Stance.Crouching;

        private void Update()
        {
            if (_player == null || !_model.Armed) return;
            Vector3 p = _player.transform.position;
            _model.Probe(p.x, p.z, Crouched);
            if (_model.Revealed && wire != null) wire.enabled = true;

            float speed = _player.Motor != null ? _player.Motor.HorizontalSpeed : 0f;
            if (_model.StepDisarm(Time.deltaTime, GameInput.Held(GameKey.E), p.x, p.z, speed, Crouched))
            {
                if (wire != null) wire.enabled = false;
                if (safeMarker != null) safeMarker.SetActive(true);
                Disarmed?.Invoke(this);
            }
            else if (_hasLast && _model.Crossed(_lastPlayer.x, _lastPlayer.z, p.x, p.z))
            {
                Blow(true);
            }
            _lastPlayer = p;
            _hasLast = true;
        }

        /// <summary>Estalla ya (la mina que pisa la patrulla chilena).</summary>
        public void Detonate()
        {
            if (!_model.Armed) return;
            _model.Detonate();
            Blow(false);
        }

        private void Blow(bool byPlayer)
        {
            Vector3 charge = new Vector3(_model.ChargeX, transform.position.y, _model.ChargeZ);
            DustBurst.Spawn(charge + Vector3.up * 0.5f, blast, 10f);
            if (wire != null) wire.enabled = false;
            Combatant[] nearby = new Combatant[Combatant.All.Count]; // Hurt puede sacar al caído de la lista
            for (int i = 0; i < nearby.Length; i++) nearby[i] = Combatant.All[i];
            foreach (Combatant c in nearby)
            {
                if (c != null && c.Alive && _model.IsLethalAt(c.transform.position.x, c.transform.position.z)) c.Hurt(1000f, gameObject, charge);
            }
            Exploded?.Invoke(this, byPlayer);
        }

        private void OnGUI()
        {
            if (_player == null || !_model.Armed || !_model.Revealed) return;
            Vector3 p = _player.transform.position;
            float dx = p.x - _model.ChargeX, dz = p.z - _model.ChargeZ;
            if (dx * dx + dz * dz > 9f) return;
            if (_style == null)
            {
                _style = new GUIStyle(GUI.skin.label) { fontSize = 16, alignment = TextAnchor.MiddleCenter };
                _style.normal.textColor = new Color(1f, 0.95f, 0.8f);
            }
            string text = !Crouched ? "Agáchate (C) junto a la carga"
                : dx * dx + dz * dz > Tripwire.DisarmReachM * Tripwire.DisarmReachM ? "Acércate a la carga"
                : _model.Progress > 0f ? "Cortando el alambre… " + (_model.Progress * 100f).ToString("0") + "%  (no te muevas)"
                : "[E] Mantén para cortar el alambre";
            GUI.Label(new Rect(Screen.width * 0.5f - 250f, Screen.height * 0.5f + 40f, 500f, 24f), text, _style);
        }
    }

    /// <summary>
    /// El soldado chileno herido en el campo de tiro (capítulo 7): el estado es <see cref="WoundedComrade"/>. Manteniendo E
    /// a su lado, el jugador se lo carga a la espalda (avanza más despacio y sin armas); al llegar a la acequia lo deja a
    /// cubierto.
    /// </summary>
    public sealed class WoundedSoldier : MonoBehaviour
    {
        [SerializeField] private Transform body;
        [SerializeField] private Transform safeSpot;
        [SerializeField] private float safeRadiusM = 4f;

        private WoundedComrade _model;
        private FirstPersonController _player;
        private GUIStyle _style;

        public WoundedComrade Model => _model;
        public bool Active => _model != null;
        public Vector3 Position => body != null ? body.position : transform.position;

        /// <summary>Distancia del enemigo más cercano (la pone el director).</summary>
        public Func<Vector3, float> NearestEnemy { get; set; }

        public event Action<WoundedSoldier> PickedUp;
        public event Action<WoundedSoldier> Saved;
        public event Action<WoundedSoldier> Lost;

        public void Configure(Transform lyingBody, Transform safe)
        {
            body = lyingBody;
            safeSpot = safe;
        }

        /// <summary>Queda herido en el campo (la mina de la patrulla).</summary>
        public void Activate(FirstPersonController player, float bleedSeconds)
        {
            _player = player;
            _model = new WoundedComrade(bleedSeconds);
            if (body != null) body.gameObject.SetActive(true);
        }

        private void Update()
        {
            if (_model == null || _model.Safe || _model.Dead || _player == null) return;
            bool wasCarried = _model.Carried;
            float distance = Vector3.Distance(Flat(_player.transform.position), Flat(Position));
            float enemy = NearestEnemy != null ? NearestEnemy(Position) : float.PositiveInfinity;
            _model.Step(Time.deltaTime, GameInput.Held(GameKey.E), distance, enemy);

            if (_model.Dead)
            {
                SetCarrying(false);
                Lost?.Invoke(this);
                return;
            }
            if (_model.Carried && !wasCarried)
            {
                SetCarrying(true);
                PickedUp?.Invoke(this);
            }
            if (_model.Carried && safeSpot != null && Vector3.Distance(Flat(_player.transform.position), Flat(safeSpot.position)) <= safeRadiusM)
            {
                _model.SetDownSafe();
                SetCarrying(false);
                if (body != null) body.SetPositionAndRotation(safeSpot.position + Vector3.up * 0.25f, Quaternion.Euler(0f, 0f, 90f));
                Saved?.Invoke(this);
            }
        }

        /// <summary>A la espalda: el cuerpo va con el jugador, que no puede correr ni empuñar el arma.</summary>
        private void SetCarrying(bool carrying)
        {
            if (_player.Motor != null) _player.Motor.LoadSpeedFactor = carrying ? WoundedComrade.CarrySpeedFactor : 1f;
            var rifle = _player.GetComponent<RifleController>();
            if (rifle != null) rifle.enabled = !carrying;
            var melee = _player.GetComponent<MeleeController>();
            if (melee != null) melee.enabled = !carrying;
            if (body == null) return;
            if (carrying)
            {
                body.SetParent(_player.transform, false);
                body.localPosition = new Vector3(0f, 1.25f, -0.35f);
                body.localRotation = Quaternion.Euler(0f, 90f, 80f);
            }
            else
            {
                body.SetParent(null, true);
            }
        }

        private static Vector3 Flat(Vector3 v) => new Vector3(v.x, 0f, v.z);

        private void OnGUI()
        {
            if (_model == null || _model.Safe || _model.Dead || _player == null) return;
            if (_style == null)
            {
                _style = new GUIStyle(GUI.skin.label) { fontSize = 16, alignment = TextAnchor.MiddleCenter };
                _style.normal.textColor = new Color(1f, 0.85f, 0.75f);
            }
            string state = _model.Carried ? "A cuestas: llévalo a la acequia" :
                Vector3.Distance(Flat(_player.transform.position), Flat(Position)) <= WoundedComrade.PickUpReachM
                    ? (_model.PickUpProgress > 0f ? "Cargándolo… " + (_model.PickUpProgress * 100f).ToString("0") + "%" : "[E] Mantén para cargar al herido")
                    : "Herido en el campo";
            GUI.Label(new Rect(Screen.width * 0.5f - 250f, Screen.height * 0.5f + 64f, 500f, 24f),
                state + "  ·  se desangra en " + _model.Remaining.ToString("0") + " s", _style);
        }
    }
}
