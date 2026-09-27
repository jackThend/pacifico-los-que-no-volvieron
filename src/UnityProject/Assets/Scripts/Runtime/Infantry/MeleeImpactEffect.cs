using Pacifico.Effects;
using UnityEngine;

namespace Pacifico.Infantry
{
    /// <summary>
    /// Partículas del impacto cuerpo a cuerpo: un único <see cref="ParticleSystem"/> compartido, creado al vuelo,
    /// que lanza fragmentos (paja del muñeco, astillas, tierra) en la dirección de la hoja y los deja caer.
    /// </summary>
    public static class MeleeImpactEffect
    {
        private static ParticleSystem s_system;
        private static Material s_material;

        public static void Play(Vector3 point, Vector3 bladeDirection, Color color, int count)
        {
            ParticleSystem system = Ensure();
            Vector3 forward = bladeDirection.sqrMagnitude > 1e-6f ? bladeDirection.normalized : Vector3.forward;
            var emit = new ParticleSystem.EmitParams();
            for (int i = 0; i < count; i++)
            {
                // Cono de ~50° alrededor de la dirección del golpe, con un poco de rebote hacia arriba.
                Vector3 spread = Random.insideUnitSphere * 0.9f + Vector3.up * 0.4f;
                emit.position = point;
                emit.velocity = (forward + spread).normalized * Random.Range(1.2f, 3.5f);
                emit.startSize = Random.Range(0.015f, 0.045f);
                emit.startLifetime = Random.Range(0.5f, 1.1f);
                emit.rotation = Random.Range(0f, 360f);
                emit.startColor = Color.Lerp(color, color * 0.7f, Random.value);
                system.Emit(emit, 1);
            }
        }

        private static ParticleSystem Ensure()
        {
            if (s_system != null) return s_system;
            var go = new GameObject("Impactos_Cuerpo_a_Cuerpo");
            s_system = go.AddComponent<ParticleSystem>();
            s_system.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            ParticleSystem.MainModule main = s_system.main;
            main.loop = false;
            main.playOnAwake = false;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.gravityModifier = 1f;
            main.maxParticles = 512;
            ParticleSystem.EmissionModule emission = s_system.emission;
            emission.enabled = false;
            ParticleSystem.ShapeModule shape = s_system.shape;
            shape.enabled = false;
            // Rozamiento con el aire: la paja se frena y cae.
            ParticleSystem.LimitVelocityOverLifetimeModule drag = s_system.limitVelocityOverLifetime;
            drag.enabled = true;
            drag.drag = 2.5f;

            var renderer = go.GetComponent<ParticleSystemRenderer>();
            if (s_material == null) s_material = BlackPowderSmoke.CreateMaterial();
            renderer.sharedMaterial = s_material;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            s_system.Play();
            return s_system;
        }
    }
}
