using Pacifico.Core.Common;
using Pacifico.Core.Melee;
using UnityEngine;

namespace Pacifico.Infantry
{
    /// <summary>
    /// Animación en primera persona del cuerpo a cuerpo (ROADMAP 3.4). Coloca el fusil y el corvo para que la hoja
    /// visible coincida con la trayectoria con la que <see cref="MeleeAttackModel"/> calcula los impactos: lo que se
    /// ve es exactamente lo que golpea.
    /// <list type="bullet">
    /// <item>Al acercarse un objetivo, el fusil pasa a la guardia de bayoneta (punta a la altura del pecho enemigo).</item>
    /// <item>Estocada: la punta de la bayoneta sigue la trayectoria del modelo (armar, lanzar, retirar).</item>
    /// <item>Tajo: el fusil se aparta y el corvo barre el arco del modelo.</item>
    /// </list>
    /// Va en un nodo intermedio del fusil («Esgrima»), entre la raíz (<see cref="IronSightViewModel"/>) y el
    /// mecanismo (<see cref="RifleViewModelAnimator"/>); el corvo es un hijo del pivote de cámara.
    /// </summary>
    [DefaultExecutionOrder(10)]
    public sealed class MeleeViewModelAnimator : MonoBehaviour
    {
        [SerializeField] private MeleeController melee;
        [SerializeField] private FirstPersonController owner;
        [Tooltip("Punta de la bayoneta (hijo del mecanismo).")]
        [SerializeField] private Transform bayonetTip;
        [Tooltip("Corvo en primera persona: origen en la empuñadura, hoja a lo largo de +Z.")]
        [SerializeField] private Transform corvo;
        [SerializeField] private Vector3 stowedOffset = new Vector3(0.05f, -0.35f, -0.15f);
        [SerializeField] private Vector3 stowedEuler = new Vector3(35f, -20f, 0f);

        private Vector3 _tipInRifle;
        private float _stow;
        private float _stowVelocity;
        private float _corvoScale;
        private float _corvoVelocity;
        private Vector3 _corvoBaseScale = Vector3.one;

        public void Configure(MeleeController controller, FirstPersonController fps, Transform tip, Transform corvoView)
        {
            melee = controller;
            owner = fps;
            bayonetTip = tip;
            corvo = corvoView;
        }

        private void Start()
        {
            // Punta de la bayoneta en el espacio de este nodo con el mecanismo en reposo.
            if (bayonetTip != null) _tipInRifle = transform.InverseTransformPoint(bayonetTip.position);
            if (corvo != null)
            {
                _corvoBaseScale = corvo.localScale;
                corvo.gameObject.SetActive(false);
            }
        }

        private void LateUpdate()
        {
            if (melee == null || owner == null) return;
            float dt = Time.deltaTime;
            MeleeAttackModel model = melee.Model;
            bool sidearm = melee.UsingSidearm;
            bool bayonet = model.IsBusy && !sidearm;

            // 1) Fusil: guardia y estocada, o apartado mientras actúa el corvo.
            _stow = MathUtil.SmoothDamp(_stow, sidearm ? 1f : 0f, ref _stowVelocity, 0.07f, dt);
            float aim = owner.Sights != null ? owner.Sights.Eased : 0f;
            float guard = bayonet ? 1f : melee.Guard * (1f - aim);
            MeleeAttackProfile thrust = melee.ThrustProfile;

            Vector3 position = Vector3.zero;
            Quaternion rotation = Quaternion.identity;
            if (guard > 1e-3f && thrust != null && thrust.Weapon.IsFirearm && bayonetTip != null)
            {
                float stroke = bayonet ? model.Stroke : 0f;
                thrust.Pose(stroke, out Vec3 hilt, out Vec3 tip);
                SolveRiflePose(ToVector3(hilt), ToVector3(tip), out Vector3 p, out Quaternion q);
                position = Vector3.Lerp(Vector3.zero, p, guard);
                rotation = Quaternion.Slerp(Quaternion.identity, q, guard);
            }
            position += stowedOffset * _stow;
            rotation = Quaternion.Euler(stowedEuler * _stow) * rotation;
            transform.localPosition = position;
            transform.localRotation = rotation;

            // 2) Corvo: aparece al armar el golpe y sigue la hoja del modelo.
            if (corvo == null) return;
            _corvoScale = MathUtil.SmoothDamp(_corvoScale, sidearm ? 1f : 0f, ref _corvoVelocity, 0.04f, dt);
            bool visible = _corvoScale > 0.02f && model.Profile != null && !model.Profile.Weapon.IsFirearm;
            corvo.gameObject.SetActive(visible);
            if (!visible) return;
            model.CurrentPose(out Vec3 corvoHilt, out Vec3 corvoTip);
            Vector3 h = ToVector3(corvoHilt), t = ToVector3(corvoTip);
            corvo.localPosition = h;
            // El filo mira en el sentido del corte (de derecha a izquierda y hacia abajo).
            corvo.localRotation = Quaternion.LookRotation(t - h, new Vector3(-0.45f, 1f, 0f));
            corvo.localScale = _corvoBaseScale * _corvoScale;
        }

        /// <summary>
        /// Pose local de este nodo tal que la bayoneta quede sobre el segmento empuñadura–punta del modelo
        /// (coordenadas del pivote de cámara, que son las de vista).
        /// </summary>
        private void SolveRiflePose(Vector3 hilt, Vector3 tip, out Vector3 localPosition, out Quaternion localRotation)
        {
            Transform root = transform.parent;
            Transform pivot = root != null ? root.parent : null;
            // Raíz del fusil respecto al pivote (la mueve IronSightViewModel: cadera, encare, carrera…).
            Quaternion rootRotation = root != null ? root.localRotation : Quaternion.identity;
            Vector3 rootPosition = root != null ? root.localPosition : Vector3.zero;
            if (pivot == null)
            {
                rootRotation = Quaternion.identity;
                rootPosition = Vector3.zero;
            }

            Quaternion desired = Quaternion.LookRotation((tip - hilt).normalized, Vector3.up);
            Quaternion inverseRoot = Quaternion.Inverse(rootRotation);
            localRotation = inverseRoot * desired;
            localPosition = inverseRoot * (tip - rootPosition) - localRotation * _tipInRifle;
        }

        private static Vector3 ToVector3(Vec3 v) => new Vector3(v.X, v.Y, v.Z);
    }
}
