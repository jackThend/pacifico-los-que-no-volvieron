using System.Collections.Generic;
using Pacifico.Core.Naval;
using UnityEngine;

namespace Pacifico.Naval
{
    /// <summary>
    /// Espolón de proa (ROADMAP 2.4): un trigger en la roda que, al entrar en contacto con otro buque, resuelve la
    /// embestida con <see cref="RamModel"/> y aplica daños a ambos. Tras el choque el atacante pierde arrancada.
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public sealed class RamBow : MonoBehaviour
    {
        [SerializeField] private ShipController owner;
        [Tooltip("Segundos antes de poder volver a embestir al mismo buque (evita dobles contactos).")]
        [SerializeField] private float rearmSeconds = 3f;

        private readonly Dictionary<ShipController, float> _lastRam = new Dictionary<ShipController, float>();

        public RamResult? LastResult { get; private set; }

        public ShipController Owner
        {
            get => owner;
            set => owner = value;
        }

        private void Reset()
        {
            GetComponent<Collider>().isTrigger = true;
            owner = GetComponentInParent<ShipController>();
        }

        private void Awake()
        {
            GetComponent<Collider>().isTrigger = true;
            if (owner == null) owner = GetComponentInParent<ShipController>();
        }

        private void OnTriggerEnter(Collider other)
        {
            if (owner == null || owner.Motion == null) return;
            ShipController target = other.GetComponentInParent<ShipController>();
            if (target == null || target == owner || target.Motion == null) return;
            if (_lastRam.TryGetValue(target, out float last) && Time.time - last < rearmSeconds) return;
            _lastRam[target] = Time.time;

            RamResult ram = RamModel.Resolve(owner.Spec, owner.Motion.HeadingDeg, owner.Motion.Speed,
                                             target.Spec, target.Motion.HeadingDeg, target.Motion.Speed);
            LastResult = ram;
            if (ram.ClosingSpeed <= 0f) return;

            var targetDamage = target.GetComponent<ShipDamageController>();
            if (targetDamage != null) targetDamage.ReceiveRam(ram);
            var ownDamage = owner.GetComponent<ShipDamageController>();
            if (ownDamage != null) ownDamage.DealtRam(ram);

            owner.Motion.ApplySpeedImpulse(owner.Motion.Speed * (ram.RammerSpeedRetained - 1f));
        }
    }
}
