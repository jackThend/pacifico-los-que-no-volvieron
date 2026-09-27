namespace Pacifico.Core.Weapons
{
    /// <summary>
    /// Curvas de animación del mecanismo derivadas del ciclo (ROADMAP 3.2). Al depender solo de la etapa y su
    /// progreso, cualquier animación que las use queda sincronizada con el temporizador por construcción.
    /// </summary>
    public static class RifleAnimationCurves
    {
        /// <summary>Suavizado «smoothstep»: arranca y termina con velocidad nula.</summary>
        public static float Ease(float t)
        {
            if (t <= 0f) return 0f;
            if (t >= 1f) return 1f;
            return t * t * (3f - 2f * t);
        }

        /// <summary>Apertura del cierre [0, 1]: abre durante «abrir», sigue abierto al extraer e insertar, cierra al «cerrar».</summary>
        public static float ActionOpen(RifleStage stage, float stageProgress, bool leverAction)
        {
            float eased = Ease(stageProgress);
            switch (stage)
            {
                case RifleStage.OpeningAction: return eased;
                case RifleStage.Extracting:
                case RifleStage.InsertingCartridge: return 1f;
                case RifleStage.ClosingAction: return 1f - eased;
                // La Winchester carga el depósito por la portilla con la palanca cerrada.
                case RifleStage.LoadingMagazine: return leverAction ? 0f : 1f;
                default: return 0f;
            }
        }

        /// <summary>
        /// Martillo o percutor montado [0, 1]: cae al percutir y queda montado hasta el siguiente disparo. En el
        /// Remington y el Chassepot se monta a mano (etapa de amartillar); en el Comblain, el Gras y la Winchester lo
        /// monta el propio cierre al abrirse. Fuera de esas etapas manda el estado real del arma (<paramref name="isCocked"/>):
        /// tras el último disparo sin munición el martillo queda abatido.
        /// </summary>
        public static float HammerCocked(RifleStage stage, float stageProgress, bool manualCocking, bool isCocked)
        {
            if (stage == RifleStage.Firing) return 0f;
            RifleStage cockingStage = manualCocking ? RifleStage.Cocking : RifleStage.OpeningAction;
            if (stage == cockingStage) return Ease(stageProgress);
            return isCocked ? 1f : 0f;
        }

        /// <summary>Retroceso visible del arma [0, 1] durante la etapa de percutir.</summary>
        public static float Kick(RifleStage stage, float stageProgress)
        {
            if (stage != RifleStage.Firing) return 0f;
            float r = 1f - stageProgress;
            return r * r;
        }
    }
}
