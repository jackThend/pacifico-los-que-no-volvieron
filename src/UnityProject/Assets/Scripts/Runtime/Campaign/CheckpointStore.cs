using System;
using Pacifico.Core.Campaign;
using Pacifico.Core.Common;
using Pacifico.Core.Naval;
using Pacifico.Infantry;
using Pacifico.Naval;
using UnityEngine;

namespace Pacifico.Campaign
{
    /// <summary>
    /// El último punto de control de la campaña (uno solo, guardado en las preferencias del jugador) y la petición de
    /// volver a él al cargar la escena. Los directores lo guardan al entrar en una etapa y lo recogen en su Start.
    /// </summary>
    public static class CheckpointStore
    {
        public const string Key = "pacifico.punto_de_control";

        private static MissionCheckpoint s_current;
        private static bool s_loaded;
        private static bool s_resumeRequested;

        /// <summary>Se acaba de guardar un punto de control (el aviso en pantalla).</summary>
        public static event Action<MissionCheckpoint> Saved;

        public static MissionCheckpoint Current
        {
            get
            {
                if (!s_loaded)
                {
                    s_loaded = true;
                    s_current = MissionCheckpoint.Parse(PlayerPrefs.GetString(Key, string.Empty));
                }
                return s_current;
            }
        }

        public static bool ExistsFor(string chapterId) => Current != null && Current.ChapterId == chapterId;

        public static void Save(MissionCheckpoint checkpoint)
        {
            s_current = checkpoint;
            s_loaded = true;
            PlayerPrefs.SetString(Key, checkpoint.Serialize());
            PlayerPrefs.Save();
            Saved?.Invoke(checkpoint);
        }

        public static void Clear(string chapterId)
        {
            if (!ExistsFor(chapterId)) return;
            s_current = null;
            PlayerPrefs.DeleteKey(Key);
            PlayerPrefs.Save();
        }

        /// <summary>La próxima escena de este capítulo debe empezar en el punto de control.</summary>
        public static void RequestResume() => s_resumeRequested = true;

        /// <summary>Lo que debe retomar este capítulo sin consumir la petición (para quien prepara la escena en Awake).</summary>
        public static MissionCheckpoint Peek(string chapterId) => s_resumeRequested && ExistsFor(chapterId) ? Current : null;

        /// <summary>El punto de control que debe retomar este capítulo, o null. Consume la petición.</summary>
        public static MissionCheckpoint Take(string chapterId)
        {
            MissionCheckpoint checkpoint = Peek(chapterId);
            s_resumeRequested = false;
            return checkpoint;
        }

        // ------------------------------------------------------------------------------------------
        // Lo que cuesta repetir en cada director
        // ------------------------------------------------------------------------------------------

        public static void WriteShip(MissionCheckpoint checkpoint, string key, ShipController ship)
        {
            if (ship == null || ship.Motion == null) return;
            Vector3 p = ship.transform.position;
            checkpoint.Data[key + ".x"] = p.x;
            checkpoint.Data[key + ".z"] = p.z;
            checkpoint.Data[key + ".rumbo"] = ship.Motion.HeadingDeg;
            checkpoint.Data[key + ".vel"] = ship.Motion.Speed;
            var damage = ship.GetComponent<ShipDamageController>();
            ShipDamageState state = damage != null ? damage.State : null;
            if (state == null) return;
            checkpoint.Data[key + ".casco"] = state.StructuralDamage;
            checkpoint.Data[key + ".agua"] = state.WaterTonnes;
            checkpoint.Data[key + ".via"] = state.InflowRate;
            checkpoint.Data[key + ".calderas"] = state.BoilerDamage;
            checkpoint.Data[key + ".cuadernas"] = state.FramesBroken ? 1f : 0f;
        }

        public static void ReadShip(MissionCheckpoint checkpoint, string key, ShipController ship)
        {
            if (ship == null || !checkpoint.Data.ContainsKey(key + ".x")) return;
            ship.Teleport(new Vector3(checkpoint.Get(key + ".x"), ship.transform.position.y, checkpoint.Get(key + ".z")), checkpoint.Get(key + ".rumbo"));
            if (ship.Motion != null) ship.Motion.SetSpeed(checkpoint.Get(key + ".vel"));
            var damage = ship.GetComponent<ShipDamageController>();
            if (damage != null && damage.State != null)
            {
                damage.State.Restore(checkpoint.Get(key + ".casco"), checkpoint.Get(key + ".agua"), checkpoint.Get(key + ".via"),
                                     checkpoint.Get(key + ".calderas"), checkpoint.Get(key + ".cuadernas") > 0.5f);
            }
        }

        public static void WritePlayer(MissionCheckpoint checkpoint, FirstPersonController player)
        {
            if (player == null) return;
            Vector3 p = player.transform.position;
            checkpoint.Data["jugador.x"] = p.x;
            checkpoint.Data["jugador.y"] = p.y;
            checkpoint.Data["jugador.z"] = p.z;
            checkpoint.Data["jugador.rumbo"] = player.transform.eulerAngles.y;
            var rifle = player.GetComponent<RifleController>();
            if (rifle != null && rifle.Model != null) checkpoint.Data["jugador.cartuchos"] = rifle.Model.TotalRounds;
        }

        /// <summary>
        /// Recoloca al jugador. Vuelve con la salud entera y nunca con menos cartuchos de los que tenía (reintentar no
        /// debe castigar); si llevaba otra arma, la pasan los <paramref name="weapons"/> que el capítulo le pudo dar.
        /// </summary>
        public static void ReadPlayer(MissionCheckpoint checkpoint, FirstPersonController player, params Data.WeaponDataSO[] weapons)
        {
            if (player == null || !checkpoint.Data.ContainsKey("jugador.x")) return;
            player.Teleport(new Vector3(checkpoint.Get("jugador.x"), checkpoint.Get("jugador.y"), checkpoint.Get("jugador.z")), checkpoint.Get("jugador.rumbo"));
            string weaponId = null;
            foreach (string key in checkpoint.Flags)
            {
                if (key.StartsWith("arma:", StringComparison.Ordinal)) weaponId = key.Substring(5);
            }
            foreach (Data.WeaponDataSO weapon in weapons)
            {
                if (weapon != null && weaponId != null && weapon.ToSpec().Id == weaponId && player.WeaponData != weapon) player.EquipWeapon(weapon);
            }
            var rifle = player.GetComponent<RifleController>();
            if (rifle != null && rifle.Model != null)
            {
                int missing = Mathf.RoundToInt(checkpoint.Get("jugador.cartuchos")) - rifle.Model.TotalRounds;
                if (missing > 0) rifle.AddAmmo(rifle.Model.Weapon.Cartridge, missing);
            }
        }

        /// <summary>Anota el arma que empuña el jugador (como marca: es un texto).</summary>
        public static void WritePlayerWeapon(MissionCheckpoint checkpoint, FirstPersonController player)
        {
            if (player != null && player.Weapon != null) checkpoint.Flags.Add("arma:" + player.Weapon.Id);
        }
    }
}
