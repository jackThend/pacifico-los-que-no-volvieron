using UnityEngine;

namespace Pacifico.Infantry
{
    /// <summary>
    /// Mueve por código el esqueleto de un soldado con modelo, sin clips de animación: paso al andar, rodilla en tierra,
    /// cuerpo a tierra y fusil terciado o encarado. Va bajo la «Figura» del soldado, que el juego acorta (rodilla en
    /// tierra) o tumba hacia delante (cuerpo a tierra): deshace el acortamiento y lee de ahí la postura, así que la
    /// lógica del soldado no cambia. Los brazos siguen al fusil con una cadena de dos huesos (hombro, codo, muñeca).
    /// El esqueleto lo crea el editor (<c>SoldierModelLibrary</c>) con todos los huesos sin girar en la pose de reposo.
    /// </summary>
    public sealed class SoldierFigure : MonoBehaviour
    {
        public enum Bone
        {
            Hips, Spine, Chest, Neck, Head,
            LeftUpperLeg, LeftLowerLeg, LeftFoot, RightUpperLeg, RightLowerLeg, RightFoot,
            LeftUpperArm, LeftLowerArm, LeftHand, RightUpperArm, RightLowerArm, RightHand,
        }

        public const int BoneCount = 17;

        // Fusil respecto al pecho en reposo (origen en la garganta de la culata, donde va la mano derecha; +Z hacia la
        // boca, +Y hacia el alza). Valores de ajuste comprobados con fotos.
        private static readonly Vector3 CarryGrip = new Vector3(0.12f, -0.28f, 0.2f);
        private static readonly Quaternion CarryRotation = Quaternion.LookRotation(new Vector3(-0.55f, 0.8f, 0.25f), Vector3.forward);
        private static readonly Vector3 AimGrip = new Vector3(0.12f, 0.19f, 0.24f);
        private static readonly Quaternion AimRotation = Quaternion.identity;
        // Cuerpo a tierra el modelo yace boca abajo: su +Y apunta al frente y su −Z al cielo.
        private static readonly Vector3 ProneGrip = new Vector3(0.14f, 0.25f, 0.05f);
        private static readonly Quaternion ProneRotation = Quaternion.LookRotation(Vector3.up, Vector3.back);
        /// <summary>Distancia de la mano derecha a la izquierda a lo largo del fusil (m).</summary>
        private const float LeftHandAlongRifleM = 0.28f;
        /// <summary>De la muñeca (donde está el hueso de la mano) al centro de la palma (m, a 1,8 m de estatura).</summary>
        private const float PalmM = 0.08f;
        /// <summary>Dos pasos (m) a paso de marcha; el paso se alarga poco al correr.</summary>
        private const float StrideM = 1.5f;
        private const float WalkSpeedMps = 1.4f;

        [SerializeField] private Transform[] bones = new Transform[BoneCount];
        [SerializeField] private Transform rifle;
        [SerializeField] private Renderer body;

        /// <summary>1 para encarar el fusil, 0 para llevarlo terciado al pecho; la figura pasa de uno a otro en ~0,35 s.</summary>
        public float AimTarget { get; set; }

        private bool _ready;
        private Vector3 _hipsRest;
        private Vector3 _chestRest;
        private float _thighM;
        private float _phase;
        private float _speed;
        private float _aim;
        private Vector3 _lastPosition;
        private Transform _mover;

        /// <summary>Lo llama el editor al crear el modelo con esqueleto.</summary>
        public void Setup(Transform[] skeleton, Transform rifleModel, Renderer skin)
        {
            bones = skeleton;
            rifle = rifleModel;
            body = skin;
        }

        /// <summary>
        /// Pone el modelo <paramref name="prefab"/> en la <paramref name="figure"/> de un soldado de primitivas y oculta
        /// las primitivas, que conservan sus colisionadores (balas, bayoneta, ratón).
        /// </summary>
        public static SoldierFigure Attach(GameObject prefab, Transform figure)
        {
            foreach (Renderer primitive in figure.GetComponentsInChildren<Renderer>()) primitive.enabled = false;
            var model = Instantiate(prefab, figure, false);
            model.name = "Modelo";
            foreach (Transform t in model.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = figure.gameObject.layer;
            return model.GetComponent<SoldierFigure>();
        }

        private void Awake() => Bind();

        private void Bind()
        {
            if (_ready || bones == null || bones.Length != BoneCount) return;
            foreach (Transform b in bones)
            {
                if (b == null) return;
            }
            _hipsRest = Get(Bone.Hips).localPosition;
            _chestRest = transform.InverseTransformPoint(Get(Bone.Chest).position);
            _thighM = Vector3.Distance(Get(Bone.LeftUpperLeg).position, Get(Bone.LeftLowerLeg).position) / Mathf.Max(1e-4f, transform.lossyScale.y);
            _mover = transform.parent != null && transform.parent.parent != null ? transform.parent.parent : transform;
            _lastPosition = _mover.position;
            _ready = true;
        }

        private Transform Get(Bone bone) => bones[(int)bone];

        private void LateUpdate()
        {
            if (!_ready) return;
            float dt = Time.deltaTime;
            if (dt <= 0f) return;

            Vector3 moved = Vector3.ProjectOnPlane(_mover.position - _lastPosition, Vector3.up);
            _lastPosition = _mover.position;
            _speed = Mathf.Lerp(_speed, moved.magnitude / dt, 1f - Mathf.Exp(-dt * 8f));
            _phase = Mathf.Repeat(_phase + _speed * dt / StrideM * 2f * Mathf.PI, 2f * Mathf.PI);
            _aim = Mathf.MoveTowards(_aim, Mathf.Clamp01(AimTarget), dt / 0.35f);

            // La figura se acorta al echar rodilla en tierra (65 %) y se tumba 88° cuerpo a tierra: se deshace el
            // acortamiento (el cuerpo no se aplasta) y la pose se pone con los huesos.
            float kneel = 0f, prone = 0f;
            Transform figure = transform.parent;
            if (figure != null)
            {
                float height = Mathf.Max(0.05f, figure.localScale.y);
                transform.localScale = new Vector3(1f, 1f / height, 1f);
                kneel = Mathf.InverseLerp(1f, 0.65f, height);
                float pitch = Mathf.DeltaAngle(0f, figure.localEulerAngles.x);
                prone = Mathf.Clamp01(pitch / 88f);
            }
            if (body != null && !body.isVisible) return;
            ApplyPose(_phase, _speed / WalkSpeedMps, kneel, prone, _aim);
        }

        /// <summary>
        /// Pone la pose: <paramref name="phase"/> del paso (radianes), <paramref name="gait"/> 0 quieto, 1 al paso, ~2
        /// a la carrera; <paramref name="kneel"/>, <paramref name="prone"/> y <paramref name="aim"/> de 0 a 1.
        /// </summary>
        public void ApplyPose(float phase, float gait, float kneel, float prone, float aim)
        {
            Bind();
            if (!_ready) return;
            float stand = (1f - kneel) * (1f - prone);
            float swing = Mathf.Clamp(gait, 0f, 1.8f) * stand;
            float s = Mathf.Sin(phase), c = Mathf.Cos(phase);

            foreach (Transform b in bones) b.localRotation = Quaternion.identity;

            // Cadera: sube y baja con el paso; rodilla en tierra queda a la altura del muslo sobre la rodilla apoyada.
            float drop = Mathf.Max(0f, _hipsRest.y - (_thighM + 0.08f)) * kneel;
            Vector3 hipsOffset = new Vector3(0f, -drop - 0.025f * swing * Mathf.Abs(c), 0f);
            Get(Bone.Hips).localPosition = _hipsRest + hipsOffset;

            // Piernas (X negativo adelanta el muslo; X positivo dobla la rodilla hacia atrás).
            float thigh = 28f * Mathf.Min(swing, 1.3f);
            float knee = 55f * Mathf.Min(swing, 1.4f);
            float leftThigh = -thigh * s, rightThigh = thigh * s;
            float leftKnee = knee * Mathf.Max(0f, c) + 6f * swing, rightKnee = knee * Mathf.Max(0f, -c) + 6f * swing;
            // Rodilla en tierra: la izquierda adelante (muslo horizontal, espinilla vertical), la derecha apoyada.
            leftThigh = Mathf.Lerp(leftThigh, -82f, kneel);
            leftKnee = Mathf.Lerp(leftKnee, 82f, kneel);
            rightThigh = Mathf.Lerp(rightThigh, 8f, kneel);
            rightKnee = Mathf.Lerp(rightKnee, 92f, kneel);
            float spread = 7f * prone;
            Get(Bone.LeftUpperLeg).localRotation = Quaternion.Euler(leftThigh, 0f, -spread);
            Get(Bone.LeftLowerLeg).localRotation = Quaternion.Euler(leftKnee, 0f, 0f);
            Get(Bone.RightUpperLeg).localRotation = Quaternion.Euler(rightThigh, 0f, spread);
            Get(Bone.RightLowerLeg).localRotation = Quaternion.Euler(rightKnee, 0f, 0f);
            Get(Bone.LeftFoot).localRotation = Quaternion.Euler(-0.5f * leftKnee * (1f - kneel), 0f, 0f);
            Get(Bone.RightFoot).localRotation = Quaternion.Euler(Mathf.Lerp(-0.5f * rightKnee, -60f, kneel), 0f, 0f);

            // Torso: se inclina al avanzar; al encarar gira el hombro izquierdo hacia delante. Cuerpo a tierra se
            // alza un poco el pecho y la cabeza para ver por encima del arma.
            float lean = 6f * Mathf.Min(swing, 2f);
            float twist = 28f * aim * (1f - prone);
            Get(Bone.Spine).localRotation = Quaternion.Euler(lean * 0.5f - 8f * prone, twist * 0.5f, 0f);
            Get(Bone.Chest).localRotation = Quaternion.Euler(lean * 0.5f - 8f * prone, twist * 0.5f, 0f);
            Get(Bone.Neck).localRotation = Quaternion.Euler(-lean * 0.5f - 25f * prone, -twist * 0.6f, 0f);
            Get(Bone.Head).localRotation = Quaternion.Euler(8f * aim * (1f - prone) - 20f * prone, -twist * 0.3f, 0f);

            // Fusil: terciado al pecho, encarado al hombro o, cuerpo a tierra, junto a la cara.
            Vector3 grip = Vector3.Lerp(Vector3.Lerp(CarryGrip, AimGrip, aim), ProneGrip, prone);
            Quaternion turn = Quaternion.Slerp(Quaternion.Slerp(CarryRotation, AimRotation, aim), ProneRotation, prone);
            if (rifle != null)
            {
                rifle.localPosition = _chestRest + hipsOffset + grip;
                rifle.localRotation = turn;
            }
            Vector3 right = rifle != null ? rifle.position : transform.TransformPoint(_chestRest + grip);
            Vector3 left = right + (rifle != null ? rifle.forward : transform.TransformDirection(turn * Vector3.forward)) * LeftHandAlongRifleM * transform.lossyScale.y;

            // Codos hacia abajo y afuera; al encarar, el derecho se alza hacia fuera y el izquierdo queda bajo el arma;
            // cuerpo a tierra, apoyados en el suelo (+Z del modelo tumbado).
            Vector3 rightPole = Vector3.Lerp(Vector3.Lerp(new Vector3(0.5f, -1f, -0.3f), new Vector3(1f, -0.15f, -0.4f), aim), new Vector3(0.6f, -0.2f, 1f), prone);
            Vector3 leftPole = Vector3.Lerp(Vector3.Lerp(new Vector3(-0.4f, -1f, 0f), new Vector3(-0.15f, -1f, 0.1f), aim), new Vector3(-0.6f, -0.2f, 1f), prone);
            float palm = PalmM * transform.lossyScale.y;
            Reach(Get(Bone.RightUpperArm), Get(Bone.RightLowerArm), Get(Bone.RightHand), right, transform.TransformDirection(rightPole), palm);
            Reach(Get(Bone.LeftUpperArm), Get(Bone.LeftLowerArm), Get(Bone.LeftHand), left, transform.TransformDirection(leftPole), palm);
            // El modelo no tiene dedos articulados: se dobla la muñeca para que la mano rodee el arma por debajo.
            if (rifle != null)
            {
                Wrap(Get(Bone.RightHand), Get(Bone.RightLowerArm), -rifle.up);
                Wrap(Get(Bone.LeftHand), Get(Bone.LeftLowerArm), -rifle.up);
            }
        }

        private static void Wrap(Transform hand, Transform forearm, Vector3 under)
        {
            Vector3 along = hand.position - forearm.position;
            if (along.sqrMagnitude < 1e-8f) return;
            along.Normalize();
            hand.rotation = Quaternion.FromToRotation(along, Vector3.Slerp(along, under, 0.25f)) * hand.rotation;
        }

        /// <summary>
        /// Gira hombro y codo para que la palma (a <paramref name="palm"/> de la muñeca, siguiendo el antebrazo) llegue a
        /// <paramref name="target"/> con el codo hacia <paramref name="pole"/>.
        /// </summary>
        private static void Reach(Transform upper, Transform lower, Transform hand, Vector3 target, Vector3 pole, float palm)
        {
            Vector3 shoulder = upper.position;
            target -= (target - shoulder).normalized * palm;
            float a = Vector3.Distance(shoulder, lower.position);
            float b = Vector3.Distance(lower.position, hand.position);
            Vector3 toTarget = target - shoulder;
            Vector3 dir = toTarget.sqrMagnitude > 1e-8f ? toTarget.normalized : Vector3.down;
            float d = Mathf.Clamp(toTarget.magnitude, Mathf.Abs(a - b) + 1e-3f, a + b - 1e-3f);
            float cos = Mathf.Clamp((a * a + d * d - b * b) / (2f * a * d), -1f, 1f);
            Vector3 bend = Vector3.ProjectOnPlane(pole, dir);
            if (bend.sqrMagnitude < 1e-6f) bend = Vector3.ProjectOnPlane(Vector3.down, dir);
            bend.Normalize();
            Vector3 elbow = shoulder + dir * (a * cos) + bend * (a * Mathf.Sqrt(1f - cos * cos));

            upper.rotation = Quaternion.FromToRotation(lower.position - shoulder, elbow - shoulder) * upper.rotation;
            Vector3 wrist = shoulder + dir * d;
            lower.rotation = Quaternion.FromToRotation(hand.position - lower.position, wrist - lower.position) * lower.rotation;
        }
    }
}
