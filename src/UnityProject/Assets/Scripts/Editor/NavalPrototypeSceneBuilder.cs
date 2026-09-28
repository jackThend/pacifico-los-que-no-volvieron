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
            Debug.Log($"[Pacífico] Escena creada: {ScenePath}. Play y usa W/S (telégrafo) y A/D (timón).");
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

            if (spec.HasTurret)
            {
                var turret = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                turret.name = "Torre Coles";
                turret.transform.SetParent(ship.transform, false);
                turret.transform.localScale = new Vector3(7f, 1.5f, 7f);
                turret.transform.localPosition = new Vector3(0f, 5.5f, spec.LengthM * 0.1f);
            }

            ship.AddComponent<Rigidbody>();
            var controller = ship.AddComponent<ShipNavigationController>();
            controller.Configure(data, playerControlled,
                playerControlled ? EngineOrder.Stop : EngineOrder.Quarter);
            return ship;
        }
    }
}
