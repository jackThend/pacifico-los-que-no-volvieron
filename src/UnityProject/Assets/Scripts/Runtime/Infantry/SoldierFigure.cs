using UnityEngine;

namespace Pacifico.Infantry
{
    /// <summary>
    /// Mueve por código el esqueleto de un soldado con modelo, sin clips de animación: paso al andar, rodilla en tierra,
    /// cuerpo a tierra y fusil terciado o encarado. Va bajo la «Figura» del soldado, que el juego acorta (rodilla en
    /// tierra) o tumba hacia delante (cuerpo a tierra): deshace el acortamiento y lee de ahí la postura, así que la
    /// lógica del soldado no cambia. Los brazos siguen al fusil con una cadena de dos huesos (hombro, codo, muñeca).
    /// El esqueleto lo crea el editor (<c>SoldierModelLibrary</c>) con todos los huesos sin girar en la pose de reposo.
    /// Cada dedo lleva dos articulaciones: se cierran una vez alrededor del arma (no cuesta nada por fotograma).
    /// </summary>
    public sealed class SoldierFigure : MonoBehaviour
    {
        public enum Bone
        {
            Hips, Spine, Chest, Neck, Head,
            LeftUpperLeg, LeftLowerLeg, LeftFoot, RightUpperLeg, RightLowerLeg, RightFoot,
            LeftUpperArm, LeftLowerArm, LeftHand, RightUpperArm, RightLowerArm, RightHand,
            LeftThumb1, LeftThumb2, LeftIndex1, LeftIndex2, LeftMiddle1, LeftMiddle2, LeftRing1, LeftRing2, LeftPinky1, LeftPinky2,
            RightThumb1, RightThumb2, RightIndex1, RightIndex2, RightMiddle1, RightMiddle2, RightRing1, RightRing2, RightPinky1, RightPinky2,
        }

        public const int BoneCount = 37;
        /// <summary>Huesos que se mueven cada fotograma; los dedos, después de estos, se cierran una sola vez.</summary>
        private const int BodyBoneCount = (int)Bone.LeftThumb1;
        public const int FingerCount = 5;

        /// <summary>Hueso de un dedo (0 pulgar, 1 índice, 2 medio, 3 anular, 4 meñique; articulación 0 o 1).</summary>
        public static Bone FingerBone(bool left, int finger, int joint) =>
            (left ? Bone.LeftThumb1 : Bone.RightThumb1) + finger * 2 + joint;

        // Fusil respecto al pecho en reposo (origen en la garganta de la culata, donde va la mano derecha; +Z hacia la
        // boca, +Y hacia el alza). Valores de ajuste comprobados con fotos.
        private static readonly Vector3 CarryGrip = new Vector3(0.12f, -0.21f, 0.25f);
        private static readonly Quaternion CarryRotation = Quaternion.LookRotation(new Vector3(-0.55f, 0.8f, 0.25f), Vector3.forward);
        private static readonly Vector3 AimGrip = new Vector3(0.12f, 0.19f, 0.24f);
        private static readonly Quaternion AimRotation = Quaternion.identity;
        // Cuerpo a tierra el modelo yace boca abajo: su +Y apunta al frente y su −Z al cielo.
        private static readonly Vector3 ProneGrip = new Vector3(0.14f, 0.25f, 0.05f);
        private static readonly Quaternion ProneRotation = Quaternion.LookRotation(Vector3.up, Vector3.back);
        // Cómo se apoya cada mano, en ejes del fusil: hacia dónde van los dedos desde la muñeca y hacia dónde mira la
        // palma. Encarando, la derecha rodea la garganta con el pulgar por encima y la izquierda sostiene la caña desde
        // abajo; terciado, las dos cogen el arma por el lado del cuerpo. Valores de ajuste comprobados con fotos.
        private static readonly Quaternion RightAimHand = Quaternion.LookRotation(new Vector3(-0.25f, -0.5f, 0.83f), new Vector3(-1f, 0.2f, 0f));
        private static readonly Quaternion LeftAimHand = Quaternion.LookRotation(new Vector3(0.62f, 0.3f, 0.72f), new Vector3(-0.35f, 1f, 0f));
        private static readonly Quaternion RightCarryHand = Quaternion.LookRotation(new Vector3(0f, 1f, 0.25f), new Vector3(1f, 0f, 0f));
        private static readonly Quaternion LeftCarryHand = Quaternion.LookRotation(new Vector3(0f, 1f, 0.2f), new Vector3(-1f, 0f, 0f));
        /// <summary>Del hueso de la muñeca al punto de la palma que toca el arma, en fracción de lo que hay hasta los nudillos.</summary>
        private const float PalmAlongHand = 0.82f;
        /// <summary>Grosor de la palma hasta la línea del hueso (m, a 1,8 m de estatura).</summary>
        private const float PalmThicknessM = 0.016f;
        /// <summary>Grosor del dedo: el arma se rodea a esta distancia de más (m).</summary>
        private const float FingerThicknessM = 0.01f;
        /// <summary>Parte del giro de la mano que hace el antebrazo (el resto, la muñeca).</summary>
        private const float ForearmTwistShare = 0.6f;
        /// <summary>De la muñeca (donde está el hueso de la mano) al centro de la palma (m, a 1,8 m de estatura).</summary>
        private const float PalmM = 0.08f;
        /// <summary>Dos pasos (m) a paso de marcha; el paso se alarga poco al correr.</summary>
        private const float StrideM = 1.5f;
        private const float WalkSpeedMps = 1.4f;

        [SerializeField] private Transform[] bones = new Transform[BoneCount];
        [SerializeField] private Transform rifle;
        /// <summary>Centro del arma donde va cada mano y su radio, en ejes del fusil (los mide el editor).</summary>
        [SerializeField] private Vector3 rightGrip;
        [SerializeField] private Vector3 leftGrip = new Vector3(0f, 0f, 0.38f);
        [SerializeField] private float rightGripRadius = 0.022f;
        [SerializeField] private float leftGripRadius = 0.022f;
        [SerializeField] private Renderer body;
        /// <summary>
        /// Prenda de cada submalla del cuerpo (0 resto, 1 casaca, 2 pantalón, 3 quepis, 4 vivos del uniforme): para teñir
        /// uniformes y vestir civiles.
        /// </summary>
        [SerializeField] private int[] uniformParts = new int[0];

        public const int PartCoat = 1, PartTrousers = 2, PartCap = 3, PartFacings = 4;

        /// <summary>Postura de fondo de los personajes de escena: de pie (soldados), a caballo o sentado en el suelo.</summary>
        public enum Rest { Standing, Mounted, Seated }

        [SerializeField] private Rest rest = Rest.Standing;

        /// <summary>Lo llama el editor al colocar un personaje a caballo o sentado.</summary>
        public void SetRest(Rest value) => rest = value;
        public int[] UniformParts => uniformParts;

        /// <summary>1 para encarar el fusil, 0 para llevarlo terciado al pecho; la figura pasa de uno a otro en ~0,35 s.</summary>
        public float AimTarget { get; set; }

        /// <summary>Lo que le faltó a la peor mano para llegar a su sitio en la última pose (m): para comprobar poses.</summary>
        public float GripErrorM { get; private set; }

        /// <summary>Medidas de una mano en reposo, en ejes del soldado.</summary>
        private struct HandRest
        {
            public Quaternion Frame;
            public Vector3 Thumb;
            public Vector3 Palm;
            public Vector3 Normal;
        }

        private HandRest _rightHand;
        private HandRest _leftHand;

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
        public void Setup(Transform[] skeleton, Transform rifleModel, Renderer skin, int[] parts,
                          Vector3 rightGripPoint, float rightRadius, Vector3 leftGripPoint, float leftRadius)
        {
            bones = skeleton;
            rifle = rifleModel;
            rightGrip = rightGripPoint;
            rightGripRadius = rightRadius;
            leftGrip = leftGripPoint;
            leftGripRadius = leftRadius;
            body = skin;
            uniformParts = parts ?? new int[0];
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
            bool armed = rifle != null && rifle.gameObject.activeSelf;
            _rightHand = MeasureHand(false);
            _leftHand = MeasureHand(true);
            CloseFingers(false, _rightHand, armed ? rightGripRadius : -1f);
            CloseFingers(true, _leftHand, armed ? leftGripRadius : -1f);
            _ready = true;
        }

        /// <summary>Ejes de la mano en reposo, a partir de sus huesos: los dedos hacia delante y la palma según el pulgar.</summary>
        private HandRest MeasureHand(bool left)
        {
            Vector3 wrist = RestPoint(left ? Bone.LeftHand : Bone.RightHand);
            Vector3 middle = RestPoint(FingerBone(left, 2, 0));
            Vector3 toward = Vector3.Normalize(middle - wrist);
            Vector3 thumb = Vector3.ProjectOnPlane(RestPoint(FingerBone(left, 1, 0)) - RestPoint(FingerBone(left, 4, 0)), toward).normalized;
            // Mano derecha: dedos × pulgar da la palma; en la izquierda, al revés.
            Vector3 normal = left ? Vector3.Cross(thumb, toward) : Vector3.Cross(toward, thumb);
            return new HandRest
            {
                Frame = Quaternion.LookRotation(toward, normal),
                Thumb = thumb,
                Palm = toward * ((middle - wrist).magnitude * PalmAlongHand),
                Normal = normal,
            };
        }

        /// <summary>Posición de un hueso en la pose de reposo, en ejes del soldado (suma los tramos, sin mirar los giros actuales).</summary>
        private Vector3 RestPoint(Bone bone)
        {
            Vector3 point = Vector3.zero;
            for (Transform t = bones[(int)bone]; t != null && t != transform; t = t.parent) point += t.localPosition;
            return point;
        }

        /// <summary>
        /// Cierra los dedos alrededor de un arma de ese <paramref name="radius"/> (negativo: mano relajada), juntándolos
        /// antes, que en el modelo vienen abiertos en abanico.
        /// </summary>
        private void CloseFingers(bool left, in HandRest hand, float radius)
        {
            bool grip = radius > 0f;
            float around = radius + FingerThicknessM;
            Vector3 along = hand.Frame * Vector3.forward;
            for (int finger = 1; finger < FingerCount; finger++)
            {
                Transform first = Get(FingerBone(left, finger, 0));
                Transform second = Get(FingerBone(left, finger, 1));
                // Sin girar, la posición local del segundo hueso es el tramo del primero.
                Vector3 direction = second.localPosition.normalized;
                // Cada falange ocupa este ángulo del arma; relajada, la mano apenas se cierra.
                float step = grip ? Mathf.Asin(Mathf.Clamp01(second.localPosition.magnitude / (2f * around))) * Mathf.Rad2Deg : 12f;
                Vector3 axis = Vector3.Cross(direction, hand.Normal).normalized;
                Quaternion together = Quaternion.FromToRotation(direction, Vector3.Slerp(direction, along, grip ? 0.7f : 0.3f));
                first.localRotation = Quaternion.AngleAxis(Mathf.Clamp(step, 10f, 62f), axis) * together;
                second.localRotation = Quaternion.AngleAxis(Mathf.Clamp(step * 1.8f, 15f, 95f), axis);
            }
            Transform thumbFirst = Get(FingerBone(left, 0, 0));
            Transform thumbSecond = Get(FingerBone(left, 0, 1));
            Vector3 thumbAxis = Vector3.Cross(thumbSecond.localPosition.normalized, hand.Normal).normalized;
            // El pulgar se opone a los dedos: gira hacia la palma alrededor del eje de la mano y luego se dobla.
            float oppose = (left ? -1f : 1f) * (grip ? 42f : 12f);
            thumbFirst.localRotation = Quaternion.AngleAxis(oppose, along);
            thumbSecond.localRotation = Quaternion.AngleAxis(grip ? 32f : 10f, thumbAxis);
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
            // A caballo o sentado no se dan pasos (el caballo lleva al jinete).
            float swing = rest == Rest.Standing ? Mathf.Clamp(gait, 0f, 1.8f) * stand : 0f;
            float s = Mathf.Sin(phase), c = Mathf.Cos(phase);

            for (int i = 0; i < BodyBoneCount; i++) bones[i].localRotation = Quaternion.identity;

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
            if (rest == Rest.Mounted)
            {
                // A caballo: muslos abiertos sobre el lomo y rodillas dobladas hacia los estribos.
                Get(Bone.LeftUpperLeg).localRotation = Quaternion.Euler(-45f, 0f, -24f);
                Get(Bone.RightUpperLeg).localRotation = Quaternion.Euler(-45f, 0f, 24f);
                Get(Bone.LeftLowerLeg).localRotation = Quaternion.Euler(65f, 0f, 0f);
                Get(Bone.RightLowerLeg).localRotation = Quaternion.Euler(65f, 0f, 0f);
                Get(Bone.LeftFoot).localRotation = Quaternion.Euler(-20f, 0f, 0f);
                Get(Bone.RightFoot).localRotation = Quaternion.Euler(-20f, 0f, 0f);
            }
            else if (rest == Rest.Seated)
            {
                // Sentado en el suelo con la espalda apoyada: la cadera a ras de suelo y las piernas estiradas.
                hipsOffset = new Vector3(0f, 0.16f - _hipsRest.y, 0.05f);
                Get(Bone.Hips).localPosition = _hipsRest + hipsOffset;
                Get(Bone.LeftUpperLeg).localRotation = Quaternion.Euler(-86f, 0f, -6f);
                Get(Bone.RightUpperLeg).localRotation = Quaternion.Euler(-86f, 0f, 6f);
                Get(Bone.LeftLowerLeg).localRotation = Quaternion.Euler(8f, 0f, 0f);
                Get(Bone.RightLowerLeg).localRotation = Quaternion.Euler(8f, 0f, 0f);
                Get(Bone.LeftFoot).localRotation = Quaternion.Euler(-30f, 0f, 0f);
                Get(Bone.RightFoot).localRotation = Quaternion.Euler(-30f, 0f, 0f);
            }

            // Torso: se inclina al avanzar; al encarar gira el hombro izquierdo hacia delante. Cuerpo a tierra se
            // alza un poco el pecho y la cabeza para ver por encima del arma.
            float lean = 6f * Mathf.Min(swing, 2f);
            float twist = 28f * aim * (1f - prone);
            Get(Bone.Spine).localRotation = Quaternion.Euler(lean * 0.5f - 8f * prone, twist * 0.5f, 0f);
            Get(Bone.Chest).localRotation = Quaternion.Euler(lean * 0.5f - 8f * prone, twist * 0.5f, 0f);
            Get(Bone.Neck).localRotation = Quaternion.Euler(-lean * 0.5f - 25f * prone, -twist * 0.6f, 0f);
            Get(Bone.Head).localRotation = Quaternion.Euler(8f * aim * (1f - prone) - 20f * prone, -twist * 0.3f, 0f);
            if (rest == Rest.Seated) Get(Bone.Spine).localRotation = Quaternion.Euler(-14f, 0f, 0f) * Get(Bone.Spine).localRotation;

            if (rifle == null || !rifle.gameObject.activeSelf)
            {
                HangArms(hipsOffset, Mathf.Min(swing, 1.5f) * s);
                return;
            }

            // Fusil: terciado al pecho, encarado al hombro o, cuerpo a tierra, junto a la cara.
            Vector3 grip = Vector3.Lerp(Vector3.Lerp(CarryGrip, AimGrip, aim), ProneGrip, prone);
            Quaternion turn = Quaternion.Slerp(Quaternion.Slerp(CarryRotation, AimRotation, aim), ProneRotation, prone);
            rifle.localPosition = _chestRest + hipsOffset + grip;
            rifle.localRotation = turn;
            // Codos hacia abajo y afuera; al encarar, el derecho se alza hacia fuera y el izquierdo queda bajo el arma;
            // cuerpo a tierra, apoyados en el suelo (+Z del modelo tumbado).
            Vector3 rightPole = Vector3.Lerp(Vector3.Lerp(new Vector3(0.5f, -1f, -0.3f), new Vector3(1f, -0.6f, -0.4f), aim), new Vector3(0.6f, -0.2f, 1f), prone);
            Vector3 leftPole = Vector3.Lerp(Vector3.Lerp(new Vector3(-0.4f, -1f, 0f), new Vector3(-0.15f, -1f, 0.1f), aim), new Vector3(-0.6f, -0.2f, 1f), prone);
            // Cuerpo a tierra las manos van como al encarar: el fusil ya está tumbado con el cuerpo.
            float firing = Mathf.Max(aim, prone);
            GripErrorM = 0f;
            Grip(Get(Bone.RightUpperArm), Get(Bone.RightLowerArm), Get(Bone.RightHand), rifle.TransformPoint(rightGrip),
                 transform.TransformDirection(rightPole), rifle.rotation * Quaternion.Slerp(RightCarryHand, RightAimHand, firing), _rightHand, rightGripRadius);
            Grip(Get(Bone.LeftUpperArm), Get(Bone.LeftLowerArm), Get(Bone.LeftHand), rifle.TransformPoint(leftGrip),
                 transform.TransformDirection(leftPole), rifle.rotation * Quaternion.Slerp(LeftCarryHand, LeftAimHand, firing), _leftHand, leftGripRadius);
        }

        /// <summary>
        /// Lleva la mano a su sitio del arma: la orienta como pide <paramref name="frame"/>, calcula dónde queda entonces
        /// la muñeca para que la palma toque el arma y lleva el brazo hasta ahí. El antebrazo hace parte del giro.
        /// </summary>
        private void Grip(Transform upper, Transform lower, Transform hand, Vector3 grip, Vector3 pole, Quaternion frame, in HandRest rest, float radius)
        {
            Quaternion target = frame * Quaternion.Inverse(rest.Frame);
            Vector3 palm = rest.Palm + rest.Normal * (radius + PalmThicknessM);
            Vector3 wrist = grip - target * palm * transform.lossyScale.y;
            Reach(upper, lower, hand, wrist, pole, 0f);
            GripErrorM = Mathf.Max(GripErrorM, Vector3.Distance(hand.position, wrist));

            Vector3 axis = hand.position - lower.position;
            if (axis.sqrMagnitude > 1e-8f)
            {
                axis.Normalize();
                float roll = Vector3.SignedAngle(Vector3.ProjectOnPlane(hand.rotation * rest.Thumb, axis),
                                                 Vector3.ProjectOnPlane(target * rest.Thumb, axis), axis);
                lower.rotation = Quaternion.AngleAxis(roll * ForearmTwistShare, axis) * lower.rotation;
            }
            hand.rotation = target;
        }

        /// <summary>
        /// Sin fusil (civiles): brazos colgando junto al cuerpo que se balancean al andar, cada uno al revés que su
        /// pierna (<paramref name="swing"/> va de −1,5 a 1,5 con el paso).
        /// </summary>
        private void HangArms(Vector3 hipsOffset, float swing)
        {
            float palm = PalmM * transform.lossyScale.y;
            Vector3 right = _chestRest + hipsOffset + new Vector3(0.22f, -0.5f, 0.05f + 0.16f * swing);
            Vector3 left = _chestRest + hipsOffset + new Vector3(-0.22f, -0.5f, 0.05f - 0.16f * swing);
            Reach(Get(Bone.RightUpperArm), Get(Bone.RightLowerArm), Get(Bone.RightHand), transform.TransformPoint(right),
                  transform.TransformDirection(new Vector3(0.2f, -0.2f, -1f)), palm);
            Reach(Get(Bone.LeftUpperArm), Get(Bone.LeftLowerArm), Get(Bone.LeftHand), transform.TransformPoint(left),
                  transform.TransformDirection(new Vector3(-0.2f, -0.2f, -1f)), palm);
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
