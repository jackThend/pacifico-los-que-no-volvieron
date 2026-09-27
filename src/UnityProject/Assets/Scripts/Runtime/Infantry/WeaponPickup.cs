using System.Collections.Generic;
using Pacifico.Core.Weapons;
using Pacifico.Data;
using Pacifico.Input;
using UnityEngine;

namespace Pacifico.Infantry
{
    /// <summary>
    /// Fusil en el suelo con los cartuchos de su dueño (capítulo 4: «cuando se agotan los tiros de su Chassepot, el
    /// jugador despoja un fusil Comblain chileno del suelo»). A menos de 2 m, E lo empuña; lo que quedaba en las
    /// cartucheras del arma anterior se guarda por calibre (<see cref="RifleController"/>). Si es del mismo cartucho que
    /// el arma empuñada, solo se toman los cartuchos.
    /// </summary>
    public sealed class WeaponPickup : MonoBehaviour
    {
        public const float ReachM = 2f;

        [SerializeField] private WeaponDataSO weapon;
        [SerializeField] private int rounds = 10;

        private static readonly List<WeaponPickup> s_all = new List<WeaponPickup>();
        private WeaponSpec _spec;
        private GUIStyle _style;

        public WeaponDataSO Weapon => weapon;
        public int Rounds => rounds;
        public static IReadOnlyList<WeaponPickup> All => s_all;

        /// <summary>El jugador recoge un arma (para el guion: «fusil cambiado»).</summary>
        public static event System.Action<WeaponPickup, FirstPersonController> PickedUp;

        public static WeaponPickup Spawn(WeaponDataSO data, int roundsLeft, Vector3 position)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = "Fusil_Caido_" + data.name;
            Object.Destroy(go.GetComponent<Collider>());
            go.transform.position = new Vector3(position.x, position.y + 0.05f, position.z);
            go.transform.rotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);
            go.transform.localScale = new Vector3(0.06f, 0.06f, 1.3f);
            var renderer = go.GetComponent<Renderer>();
            renderer.material.color = new Color(0.33f, 0.22f, 0.12f);
            var pickup = go.AddComponent<WeaponPickup>();
            pickup.weapon = data;
            pickup.rounds = Mathf.Max(0, roundsLeft);
            return pickup;
        }

        private void OnEnable() => s_all.Add(this);
        private void OnDisable() => s_all.Remove(this);

        private void Start()
        {
            if (weapon != null) _spec = weapon.ToSpec();
        }

        /// <summary>El más cercano al alcance del jugador, o null.</summary>
        public static WeaponPickup Nearest(Vector3 position)
        {
            WeaponPickup best = null;
            float bestDistance = ReachM;
            foreach (WeaponPickup p in s_all)
            {
                float d = Vector3.Distance(position, p.transform.position);
                if (d <= bestDistance)
                {
                    best = p;
                    bestDistance = d;
                }
            }
            return best;
        }

        public void TakeBy(FirstPersonController player)
        {
            var rifle = player.GetComponent<RifleController>();
            bool same = player.WeaponData == weapon || (player.Weapon != null && _spec != null && player.Weapon.Id == _spec.Id);
            if (!same) player.EquipWeapon(weapon);
            if (rifle != null && _spec != null) rifle.AddAmmo(_spec.Cartridge, rounds);
            PickedUp?.Invoke(this, player);
            Destroy(gameObject);
        }

        /// <summary>Busca armas cerca del jugador y lee la tecla E (lo llama el propio jugador cada fotograma).</summary>
        public static void UpdatePlayer(FirstPersonController player)
        {
            if (player == null || !player.HasInputFocus) return;
            WeaponPickup near = Nearest(player.transform.position);
            if (near != null && GameInput.Pressed(GameKey.E)) near.TakeBy(player);
        }

        private void OnGUI()
        {
            Camera camera = Camera.main;
            if (camera == null || _spec == null) return;
            Vector3 eye = camera.transform.position;
            if (Vector3.Distance(eye, transform.position) > ReachM + 1.7f) return;
            Vector3 screen = camera.WorldToScreenPoint(transform.position + Vector3.up * 0.3f);
            if (screen.z <= 0f) return;
            if (_style == null)
            {
                _style = new GUIStyle(GUI.skin.label) { fontSize = 15, alignment = TextAnchor.MiddleCenter };
                _style.normal.textColor = new Color(1f, 0.95f, 0.8f);
            }
            GUI.Label(new Rect(screen.x - 160f, Screen.height - screen.y - 12f, 320f, 24f), "[E] " + _spec.DisplayName + " · " + rounds + " cartuchos", _style);
        }
    }
}
