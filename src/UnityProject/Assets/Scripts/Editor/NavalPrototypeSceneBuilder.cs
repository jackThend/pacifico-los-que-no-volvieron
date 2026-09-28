using Pacifico.Core.Naval;
using Pacifico.Core.Ships;
using Pacifico.Runtime.Data;
using Pacifico.Runtime.Naval;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Pacifico.Editor
{
    /// <summary>
    /// Construye por código la escena greybox de la rada de Iquique para
    /// probar la navegación (tarea 2.1). Solo primitivas, sin assets externos.
    /// </summary>
    public static class NavalPrototypeSceneBuilder
    {
        public const string ScenePath = "Assets/Scenes/Prototipo_Naval_Iquique.unity";

        [MenuItem("Pacífico/Escenas/Crear prototipo naval (Iquique)")]
        public static void Create()
        {
            HistoricalDataGenerator.GenerateShips();
            EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);

            var sea = GameObject.CreatePrimitive(PrimitiveType.Plane);
            sea.name = "Mar (greybox)";
            sea.transform.localScale = new Vector3(500f, 1f, 500f); // 5 × 5 km

            var huascar = CreateShip(HistoricalShips.HuascarId, new Vector3(0f, 0f, 0f), 0f, playerControlled: true);
            CreateShip(HistoricalShips.EsmeraldaId, new Vector3(250f, 0f, 900f), 90f, playerControlled: false);

            var camera = Camera.main;
            if (camera != null)
            {
                var follow = camera.gameObject.AddComponent<ShipCameraFollow>();
                follow.SetTarget(huascar.transform);
                camera.transform.position = new Vector3(0f, 45f, -120f);
                camera.farClipPlane = 6000f;
            }

            EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene(), ScenePath);
            Debug.Log($"[Pacífico] Escena creada: {ScenePath}. Play: W/S telégrafo, A/D timón, ratón apunta la torre Coles.");
        }

        private static GameObject CreateShip(string shipId, Vector3 position, float heading, bool playerControlled)
        {
            var data = AssetDatabase.LoadAssetAtPath<ShipDataSO>($"Assets/ScriptableObjects/Ships/Ship_{shipId}.asset");
            var spec = HistoricalShips.FindById(shipId);

            var ship = new GameObject(spec.DisplayName);
            ship.transform.SetPositionAndRotation(position, Quaternion.Euler(0f, heading, 0f));

            // Casco greybox con las dimensiones reales (manga × puntal × eslora).
            var hull = GameObject.CreatePrimitive(PrimitiveType.Cube);
            hull.name = "Casco";
            hull.transform.SetParent(ship.transform, false);
            hull.transform.localScale = new Vector3(spec.BeamM, 5f, spec.LengthM);
            hull.transform.localPosition = new Vector3(0f, 1.5f, 0f);

            ship.AddComponent<Rigidbody>();
            var controller = ship.AddComponent<ShipNavigationController>();
            controller.Configure(data, playerControlled,
                playerControlled ? EngineOrder.Stop : EngineOrder.Quarter);

            if (spec.Turret != null) CreateColesTurret(ship, controller, spec.Turret, playerControlled);
            return ship;
        }

        /// <summary>Torre (cilindro) con pivote de giro y pivote de elevación para los dos cañones.</summary>
        private static void CreateColesTurret(GameObject ship, ShipNavigationController controller,
            TurretMount mount, bool playerControlled)
        {
            var yawPivot = new GameObject("Torre Coles (giro)");
            yawPivot.transform.SetParent(ship.transform, false);
            yawPivot.transform.localPosition = new Vector3(0f, 4f, mount.offsetForwardM);

            var drum = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            drum.name = "Torre Coles";
            drum.transform.SetParent(yawPivot.transform, false);
            drum.transform.localScale = new Vector3(7f, 1.25f, 7f);
            drum.transform.localPosition = new Vector3(0f, 1.25f, 0f);

            var pitchPivot = new GameObject("Cañones (elevación)");
            pitchPivot.transform.SetParent(yawPivot.transform, false);
            pitchPivot.transform.localPosition = new Vector3(0f, 1.5f, 2.5f);

            var half = mount.barrelSeparationM * 0.5f;
            foreach (var side in new[] { -1f, 1f })
            {
                var barrel = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                barrel.name = side < 0f ? "Armstrong babor" : "Armstrong estribor";
                barrel.transform.SetParent(pitchPivot.transform, false);
                barrel.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
                barrel.transform.localScale = new Vector3(0.6f, 2.25f, 0.6f); // 4.5 m de caña
                barrel.transform.localPosition = new Vector3(side * half, 0f, 2.25f);
            }

            var turret = ship.AddComponent<ColesTurretController>();
            turret.Configure(controller, yawPivot.transform, pitchPivot.transform, Camera.main, playerControlled);
        }
    }
}
