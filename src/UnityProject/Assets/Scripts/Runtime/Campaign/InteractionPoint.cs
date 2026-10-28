using Pacifico.Core.Common;
using Pacifico.Core.Infantry;
using Pacifico.Core.Weapons;
using Pacifico.Data;
using Pacifico.Effects;
using Pacifico.Infantry;
using UnityEngine;

namespace Pacifico.Campaign
{
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
