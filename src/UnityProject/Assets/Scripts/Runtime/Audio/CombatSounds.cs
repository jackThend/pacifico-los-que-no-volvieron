using Pacifico.Infantry;
using Pacifico.Naval;
using UnityEngine;

namespace Pacifico.Audio
{
    /// <summary>
    /// Escucha los sucesos de combate que ya avisan las balas, los proyectiles y los soldados (impactos, piques,
    /// caídos) y les pone sonido. Se engancha sola al arrancar el juego; las armas llaman a <see cref="GameAudio"/>
    /// directamente al disparar.
    /// </summary>
    public static class CombatSounds
    {
        private static bool s_hooked;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Hook()
        {
            if (s_hooked) return;
            s_hooked = true;
            RifleBullet.Impacted += OnBullet;
            NavalShell.Splashed += OnSplash;
            NavalShell.Impacted += OnShell;
            Combatant.Downed += OnDowned;
        }

        private static void OnBullet(BulletHit hit)
        {
            bool body = hit.Collider != null && hit.Collider.GetComponentInParent<Combatant>() != null;
            GameAudio.Play(body ? Sfx.BodyHit : Sfx.BulletImpact, hit.Point);
        }

        private static void OnSplash(Vector3 point) => GameAudio.Play(Sfx.Splash, point);

        private static void OnShell(ShellHit hit)
        {
            // Contra un buque, el golpe del acero; contra tierra, la explosión de la granada.
            bool ship = hit.Collider != null && hit.Collider.GetComponentInParent<ShipController>() != null;
            GameAudio.Play(ship ? Sfx.ArmorHit : Sfx.Explosion, hit.Point, ship ? 1f : 0.7f);
        }

        private static void OnDowned(Combatant who, GameObject by)
        {
            // No todos gritan (valor de ajuste): en una descarga caen varios a la vez.
            if (who != null && !who.IsPlayer && Random.value < 0.45f) GameAudio.Play(Sfx.Scream, who.transform.position + Vector3.up);
        }
    }
}
