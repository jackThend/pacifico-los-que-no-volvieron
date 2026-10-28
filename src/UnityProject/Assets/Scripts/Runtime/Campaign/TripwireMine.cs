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
}
