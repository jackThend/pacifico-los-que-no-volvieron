using System;
using Pacifico.Core.Naval;
using Pacifico.Runtime.Data;
using UnityEngine;

namespace Pacifico.Runtime.Naval
{
    /// <summary>
    /// Conecta <see cref="ShipMotionModel"/> con la escena: lee W/S (telégrafo,
    /// por pulsación) y A/D (timón, mantenido), avanza la simulación en
    /// FixedUpdate y mueve el buque. Con Rigidbody se usa MovePosition /
    /// MoveRotation cinemáticos para que las colisiones (espolonazo, 2.4) se detecten.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ShipNavigationController : MonoBehaviour
    {
        [SerializeField] private ShipDataSO shipData;
        [SerializeField, Tooltip("Si está desactivado, el buque se controla por código (IA, cinemáticas).")]
        private bool playerControlled = true;
        [SerializeField] private EngineOrder initialOrder = EngineOrder.Stop;
        [SerializeField] private bool showDebugHud = true;

        private ShipMotionModel motion;
        private Rigidbody body;

        public ShipMotionModel Motion => motion;
        public ShipDataSO ShipData => shipData;
        public bool PlayerControlled => playerControlled;

        /// <summary>Se dispara al cambiar la orden del telégrafo (sonido de campana, HUD).</summary>
        public event Action<EngineOrder> OrderChanged;

        /// <summary>Configuración desde código (constructor de escenas, spawners).</summary>
        public void Configure(ShipDataSO data, bool isPlayerControlled, EngineOrder order = EngineOrder.Stop)
        {
            shipData = data;
            playerControlled = isPlayerControlled;
            initialOrder = order;
        }

        private void Awake()
        {
            if (shipData == null)
            {
                Debug.LogError($"[ShipNavigationController] {name}: falta asignar ShipDataSO.", this);
                enabled = false;
                return;
            }

            var position = transform.position;
            motion = new ShipMotionModel(ShipHandling.FromSpec(shipData.ToSpec()),
                position.x, position.z, transform.eulerAngles.y);
            motion.SetOrder(initialOrder);

            body = GetComponent<Rigidbody>();
            if (body != null)
            {
                body.isKinematic = true;
                body.useGravity = false;
                body.interpolation = RigidbodyInterpolation.Interpolate;
            }
        }

        private void Update()
        {
            if (!playerControlled) return;

            if (GameInput.KeyDown(GameKey.W)) TelegraphUp();
            if (GameInput.KeyDown(GameKey.S)) TelegraphDown();

            var steer = 0f;
            if (GameInput.KeyHeld(GameKey.A)) steer -= 1f;
            if (GameInput.KeyHeld(GameKey.D)) steer += 1f;
            motion.SetRudderCommand(steer);
        }

        private void FixedUpdate()
        {
            motion.Step(Time.fixedDeltaTime);

            var position = new Vector3(motion.PositionX, transform.position.y, motion.PositionZ);
            var rotation = Quaternion.Euler(0f, motion.HeadingDegrees, 0f);
            if (body != null)
            {
                body.MovePosition(position);
                body.MoveRotation(rotation);
            }
            else
            {
                transform.SetPositionAndRotation(position, rotation);
            }
        }

        public void TelegraphUp()
        {
            if (motion.TelegraphUp()) OrderChanged?.Invoke(motion.Order);
        }

        public void TelegraphDown()
        {
            if (motion.TelegraphDown()) OrderChanged?.Invoke(motion.Order);
        }

        public void SetOrder(EngineOrder order)
        {
            if (motion.Order == order) return;
            motion.SetOrder(order);
            OrderChanged?.Invoke(order);
        }

        public void SetRudderCommand(float command) => motion.SetRudderCommand(command);

        private void OnGUI()
        {
            if (!showDebugHud || !playerControlled || motion == null) return;
            GUI.Label(new Rect(12f, 12f, 360f, 110f),
                $"{shipData.name}\n" +
                $"Telégrafo: {motion.Order.DisplayName()} (máquinas {motion.EngineOutput * 100f:0}%)\n" +
                $"Velocidad: {motion.SpeedKnots:0.0} nudos\n" +
                $"Timón: {motion.RudderDegrees:+0;-0;0}°   Rumbo: {motion.HeadingDegrees:000}°\n" +
                "W/S telégrafo · A/D timón");
        }
    }
}
