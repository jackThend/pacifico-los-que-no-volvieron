using System;
using Pacifico.Core.Infantry;
using Pacifico.Infantry;
using Pacifico.Input;
using UnityEngine;

namespace Pacifico.Campaign
{
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
