using System;
using System.Collections.Generic;
using System.IO;
using Pacifico.Campaign;
using Pacifico.Core.Campaign;
using Pacifico.Core.Common;
using Pacifico.Core.Weapons;
using Pacifico.Data;
using Pacifico.Infantry;
using Pacifico.Tactics;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Pacifico.EditorTools
{
    /// <summary>
    /// Escena jugable del Capítulo 6, «Hasta el último cartucho» (ROADMAP 6.4): el Morro de Arica con su acantilado
    /// sobre el mar al oeste, la ladera este por la que trepan el 3.º y el 4.º de Línea, el parapeto de caliza y sacos
    /// con la Gatling y el detonador de las minas, y la explanada superior con la bandera, Bolognesi y sus oficiales y
    /// Ugarte a caballo. La junta se muestra sobre la lámina «La respuesta de Bolognesi» del Archivo Histórico.
    /// Escala de juego: el acantilado mide 100 m (licencia; el guion habla de 260 y el Morro real ronda los 130).
    /// Modo batch:
    /// <c>Unity -batchmode -quit -projectPath src/UnityProject -executeMethod Pacifico.EditorTools.Capitulo6MorroDeAricaBuilder.RunBatch</c>
    /// </summary>
    public static class Capitulo6MorroDeAricaBuilder
    {
        public const string ScenePath = ProjectPaths.Scenes + "/Capitulo6_Morro_de_Arica.unity";
        private const string CouncilImage = "Archivo_Historico/05_Cartas_y_Documentos/03_La_Respuesta_de_Bolognesi.jpg";

        private const float SummitY = 100f;
        private const float SummitEastX = -10f;
        private const float CliffX = -80f;
        private const float LowY = 20f;
        private const float LowX = 150f;
        private const int PlayerCartridges = 40;

        private static readonly Color Rock = new Color(0.6f, 0.55f, 0.48f);
        private static readonly Color Limestone = new Color(0.82f, 0.79f, 0.7f);
        private static readonly Color Sack = new Color(0.62f, 0.56f, 0.42f);
        private static readonly Color Sea = new Color(0.12f, 0.2f, 0.26f);
        private static readonly Color Iron = new Color(0.15f, 0.15f, 0.16f);
        private static readonly Color Wood = new Color(0.4f, 0.3f, 0.2f);

        [MenuItem("Pacífico/Capítulos/Capítulo 6: Morro de Arica")]
        public static void Build()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            BuildScene();
            EditorUtility.DisplayDialog("Pacífico", "Escena creada en " + ScenePath +
                "\n\nWASD y ratón · clic derecho: encarar · clic: fuego · R: cargar · F: bayoneta · E: detonador o recoger un fusil.", "Aceptar");
        }

        public static void RunBatch()
        {
            try
            {
                BuildScene();
                EditorApplication.Exit(0);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                EditorApplication.Exit(1);
            }
        }

        /// <summary>Altura de la ladera este en <paramref name="x"/>.</summary>
        private static float SlopeY(float x) => Mathf.Lerp(SummitY, LowY, Mathf.InverseLerp(SummitEastX, LowX, x));

        private static Material Mat(string key, Color color) => PrototypeSceneKit.Material(key, color);

        private static WeaponDataSO LoadWeapon(string id)
        {
            var data = AssetDatabase.LoadAssetAtPath<WeaponDataSO>(ProjectPaths.WeaponData + "/Weapon_" + id + ".asset");
            if (data == null) throw new InvalidOperationException("No se generó el WeaponDataSO " + id + ".");
            return data;
        }

        private static void BuildScene()
        {
            HistoricalDataAssetGenerator.GenerateAll();
            WeaponDataSO chassepot = LoadWeapon(WeaponCatalog.ChassepotId);
            WeaponDataSO comblain = LoadWeapon(WeaponCatalog.ComblainId);
            WeaponDataSO remington = LoadWeapon(WeaponCatalog.RemingtonId);
            WeaponDataSO bayonet = LoadWeapon(WeaponCatalog.TriangularBayonetId);
            string root = HistoricalDataAssetGenerator.RepositoryRoot;
            Texture2D council = HistoricalDataAssetGenerator.ImportArchiveTexture(root, CouncilImage);

            string guion = File.ReadAllText(Path.Combine(root, GuionQuotes.ScriptFile));
            string chapter = GuionQuotes.Section(guion, AricaChapter.ChapterHeading);
            AricaChapter.Build(GuionQuotes.Extract(chapter, AricaChapter.ChapterHeading)); // falla aquí si el guion cambió

            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            CreateLight();
            var world = new GameObject("Morro_de_Arica").transform;
            CreateTerrain(world);
            Transform parapet = CreateParapet(world, remington, out GatlingGun gatling, out Transform detonatorPoint, out List<Transform> posts);
            Transform summit = CreateSummit(world, out Transform[] officers, out ScriptedRider ugarte);

            var baker = new GameObject("NavMesh_Tiempo_de_Ejecucion").AddComponent<NavMeshRuntimeBaker>();
            baker.transform.position = new Vector3(80f, 60f, 0f);
            var bakerSo = new SerializedObject(baker);
            bakerSo.FindProperty("size").vector3Value = new Vector3(440f, 140f, 180f);
            bakerSo.ApplyModifiedPropertiesWithoutUndo();

            // Manuel Salazar en el parapeto, mirando al este (a la ladera por la que suben los asaltantes).
            float px = parapet.position.x - 4f;
            FirstPersonController player = PisaguaPrototypeBuilder.CreatePlayer(chassepot, null, new Vector3(px, SlopeY(px) + 0.1f, 2f), 90f, PlayerCartridges);
            player.gameObject.AddComponent<Combatant>().Configure(Faction.Peru, true, null, "Manuel Salazar", 4f);
            var hud = new GameObject("HUD_Infanteria").AddComponent<InfantryHud>();
            hud.Player = player;
            hud.Rifle = player.GetComponent<RifleController>();
            hud.Melee = player.GetComponent<MeleeController>();
            PisaguaPrototypeBuilder.CreateSmoke(player.ViewCamera.transform);

            var detonator = detonatorPoint.gameObject.AddComponent<InteractionPoint>();
            detonator.Configure("Accionar el detonador de las minas", player.transform);

            var spawns = new List<Transform>();
            for (int i = 0; i < 8; i++)
            {
                float x = LowX + 60f + (i % 2) * 20f;
                spawns.Add(Marker("Asalto_" + (i + 1), new Vector3(x, LowY, -56f + i * 16f), -90f));
            }

            var chileLook = new SoldierLook
            {
                uniform = Mat("Uniforme_Chile", new Color(0.14f, 0.17f, 0.3f)), trousers = Mat("Pantalon_Chile", new Color(0.55f, 0.18f, 0.14f)),
                skin = Mat("Piel", new Color(0.62f, 0.47f, 0.36f)), kepi = Mat("Quepis_Chile", new Color(0.12f, 0.14f, 0.25f)),
            };
            var peruLook = new SoldierLook
            {
                uniform = Mat("Uniforme_Peru", new Color(0.82f, 0.8f, 0.72f)), trousers = Mat("Pantalon_Peru", new Color(0.78f, 0.76f, 0.68f)),
                skin = Mat("Piel", new Color(0.62f, 0.47f, 0.36f)), kepi = Mat("Quepis_Peru", new Color(0.35f, 0.12f, 0.1f)),
            };

            var director = new GameObject("Director_Capitulo6").AddComponent<AricaMissionDirector>();
            director.Configure(chapter, player, chassepot, comblain, bayonet, council, posts.ToArray(), spawns.ToArray(), parapet, summit,
                               gatling, detonator, officers, ugarte, chileLook, peruLook);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, ScenePath);
            PrototypeSceneKit.AddToBuildSettings(ScenePath);
            Debug.Log("[Pacífico] Capítulo 6 guardado en " + ScenePath);
        }

        private static Transform Marker(string name, Vector3 position, float heading)
        {
            var t = new GameObject("Puesto_" + name).transform;
            t.SetPositionAndRotation(position, Quaternion.Euler(0f, heading, 0f));
            return t;
        }

        /// <summary>Madrugada del 7 de junio: sol bajo por el este, luz fría y bruma sobre el mar.</summary>
        private static void CreateLight()
        {
            var sun = new GameObject("Alba").AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.intensity = 0.8f;
            sun.color = new Color(1f, 0.82f, 0.66f);
            sun.shadows = LightShadows.Soft;
            sun.transform.rotation = Quaternion.Euler(10f, -90f, 0f); // desde el este, rasante
            RenderSettings.ambientLight = new Color(0.32f, 0.35f, 0.42f);
            RenderSettings.fog = true;
            RenderSettings.fogColor = new Color(0.6f, 0.64f, 0.7f);
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogStartDistance = 120f;
            RenderSettings.fogEndDistance = 700f;
        }

        /// <summary>El macizo del Morro (acantilado vertical al oeste), la ladera este de ~27° y el llano de los fuertes del bajo.</summary>
        private static void CreateTerrain(Transform world)
        {
            GameObject sea = PrototypeSceneKit.Primitive(PrimitiveType.Plane, "Mar", world, new Vector3(-400f, 0f, 0f), new Vector3(60f, 1f, 60f), Mat("Mar", Sea), keepCollider: false);
            sea.name = "Mar";
            float width = SummitEastX - CliffX;
            PrototypeSceneKit.Primitive(PrimitiveType.Cube, "Macizo_del_Morro", world, new Vector3((CliffX + SummitEastX) * 0.5f, SummitY * 0.5f, 0f),
                new Vector3(width, SummitY, 140f), Mat("Roca", Rock));
            float run = LowX - SummitEastX, rise = SummitY - LowY;
            GameObject slope = PrototypeSceneKit.Primitive(PrimitiveType.Cube, "Ladera_Este", world,
                new Vector3((SummitEastX + LowX) * 0.5f, (SummitY + LowY) * 0.5f - 0.5f, 0f), new Vector3(Mathf.Sqrt(run * run + rise * rise), 1f, 140f), Mat("Roca", Rock));
            slope.transform.rotation = Quaternion.Euler(0f, 0f, -Mathf.Atan2(rise, run) * Mathf.Rad2Deg);
            PrototypeSceneKit.Primitive(PrimitiveType.Cube, "Llano_de_los_Fuertes", world, new Vector3(LowX + 100f, LowY - 0.5f, 0f), new Vector3(200f, 1f, 160f), Mat("Tierra", Sack));
            // Restos de los fuertes del bajo (San José, Santa Bárbara): ya en manos del asalto.
            PrototypeSceneKit.Primitive(PrimitiveType.Cube, "Fuerte_San_Jose", world, new Vector3(LowX + 30f, LowY + 1.5f, -30f), new Vector3(16f, 3f, 10f), Mat("Caliza", Limestone));
            PrototypeSceneKit.Primitive(PrimitiveType.Cube, "Fuerte_Santa_Barbara", world, new Vector3(LowX + 30f, LowY + 1.5f, 35f), new Vector3(16f, 3f, 10f), Mat("Caliza", Limestone));
        }

        /// <summary>El parapeto de caliza y sacos a media ladera, con la Gatling, el detonador y los puestos de los Artesanos.</summary>
        private static Transform CreateParapet(Transform world, WeaponDataSO gatlingAmmo, out GatlingGun gatling, out Transform detonator, out List<Transform> posts)
        {
            const float x = 70f;
            float y = SlopeY(x);
            // Punto del parapeto (objetivo de los asaltantes); las piezas cuelgan del mundo con coordenadas absolutas.
            var line = new GameObject("Parapeto_de_Caliza").transform;
            line.position = new Vector3(x, y, 0f);
            for (int i = -3; i <= 3; i++)
            {
                float z = i * 9f;
                GameObject wall = PrototypeSceneKit.Primitive(PrimitiveType.Cube, "Caliza_" + (i + 4), world, new Vector3(x + 1f, y + 0.7f, z), new Vector3(1.4f, 1.4f, 7f), Mat("Caliza", Limestone));
                wall.transform.rotation = Quaternion.Euler(0f, 0f, -26.6f);
                PrototypeSceneKit.Primitive(PrimitiveType.Cube, "Sacos_" + (i + 4), world, new Vector3(x + 1.8f, y + 1.3f, z + 3.5f), new Vector3(1f, 0.6f, 2f), Mat("Sacos", Sack));
            }
            posts = new List<Transform>();
            for (int i = 0; i < 6; i++)
            {
                float z = -22f + i * 9f;
                if (Mathf.Abs(z - 2f) < 3f) z += 4f; // el puesto del jugador
                posts.Add(Marker("Artesanos_" + (i + 1), new Vector3(x - 1.5f, SlopeY(x - 1.5f), z), 90f));
            }

            var gun = new GameObject("Gatling").transform;
            gun.SetParent(line, false);
            gun.SetPositionAndRotation(new Vector3(x - 2f, SlopeY(x - 2f), 12f), Quaternion.Euler(0f, 90f, 0f));
            PrototypeSceneKit.Primitive(PrimitiveType.Cube, "Cureña", gun, new Vector3(0f, 0.5f, 0f), new Vector3(0.8f, 0.4f, 1.2f), Mat("Cureña", Wood));
            var barrels = new GameObject("Canones").transform;
            barrels.SetParent(gun, false);
            barrels.localPosition = new Vector3(0f, 1f, 0.5f);
            GameObject cluster = PrototypeSceneKit.Primitive(PrimitiveType.Cylinder, "Haz_de_Canones", barrels, Vector3.zero, new Vector3(0.22f, 0.5f, 0.22f), Mat("Hierro", Iron), keepCollider: false);
            cluster.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            gatling = gun.gameObject.AddComponent<GatlingGun>();
            gatling.Configure(gatlingAmmo, Faction.Peru, barrels);

            detonator = new GameObject("Detonador_de_las_Minas").transform;
            detonator.SetParent(line, false);
            detonator.position = new Vector3(x - 3f, SlopeY(x - 3f) + 0.4f, -8f);
            PrototypeSceneKit.Primitive(PrimitiveType.Cube, "Caja", detonator, Vector3.zero, new Vector3(0.5f, 0.4f, 0.4f), Mat("Cureña", Wood));
            PrototypeSceneKit.Primitive(PrimitiveType.Cylinder, "Manivela", detonator, new Vector3(0f, 0.35f, 0f), new Vector3(0.05f, 0.2f, 0.05f), Mat("Hierro", Iron), keepCollider: false);
            // El cable baja por la ladera hacia las minas.
            GameObject cable = PrototypeSceneKit.Primitive(PrimitiveType.Cube, "Cable", detonator, new Vector3(20f, -10f, 0f), new Vector3(45f, 0.03f, 0.03f), Mat("Hierro", Iron), keepCollider: false);
            cable.transform.localRotation = Quaternion.Euler(0f, 0f, -26.6f);
            return line;
        }

        /// <summary>La explanada: bandera, cañones, Bolognesi y sus oficiales, y Ugarte a caballo junto al borde.</summary>
        private static Transform CreateSummit(Transform world, out Transform[] officers, out ScriptedRider ugarte)
        {
            // Punto de la explanada (la zona de llegada); las piezas cuelgan del mundo con coordenadas absolutas.
            var summit = new GameObject("Explanada_Superior").transform;
            summit.position = new Vector3(-35f, SummitY, 0f);

            PrototypeSceneKit.Primitive(PrimitiveType.Cylinder, "Asta", world, new Vector3(-60f, SummitY + 5f, 0f), new Vector3(0.15f, 5f, 0.15f), Mat("Cureña", Wood), keepCollider: false);
            // Bandera del Perú: roja, blanca y roja en franjas verticales.
            for (int i = 0; i < 3; i++)
            {
                Color c = i == 1 ? Color.white : new Color(0.78f, 0.1f, 0.12f);
                PrototypeSceneKit.Primitive(PrimitiveType.Cube, "Bandera_" + (i + 1), world, new Vector3(-60f, SummitY + 8.8f, 0.6f + i * 0.6f), new Vector3(0.03f, 1.2f, 0.6f),
                    Mat(i == 1 ? "Bandera_Blanco" : "Bandera_Rojo", c), keepCollider: false);
            }
            for (int i = 0; i < 3; i++)
            {
                GameObject cannon = PrototypeSceneKit.Primitive(PrimitiveType.Cylinder, "Canon_de_la_Cima_" + (i + 1), world, new Vector3(-20f, SummitY + 0.8f, -20f + i * 20f), new Vector3(0.5f, 1.6f, 0.5f), Mat("Hierro", Iron));
                cannon.transform.rotation = Quaternion.Euler(0f, 0f, 80f);
            }
            var staff = new List<Transform>();
            for (int i = 0; i < 3; i++)
            {
                var officer = new GameObject(i == 0 ? "Bolognesi" : "Oficial_" + i).transform;
                officer.SetParent(summit, false);
                officer.SetPositionAndRotation(new Vector3(-54f + i * 1.6f, SummitY, 4f + i), Quaternion.Euler(0f, 90f, 0f));
                PrototypeSceneKit.Primitive(PrimitiveType.Capsule, "Cuerpo", officer, new Vector3(0f, 0.9f, 0f), new Vector3(0.46f, 0.85f, 0.46f), Mat("Uniforme_Oficial_Peru", new Color(0.12f, 0.12f, 0.2f)), keepCollider: false);
                PrototypeSceneKit.Primitive(PrimitiveType.Sphere, "Cabeza", officer, new Vector3(0f, 1.6f, 0f), Vector3.one * 0.24f, Mat("Piel", new Color(0.62f, 0.47f, 0.36f)), keepCollider: false);
                staff.Add(officer);
            }
            officers = staff.ToArray();

            var rider = new GameObject("Ugarte_a_Caballo").transform;
            rider.SetPositionAndRotation(new Vector3(-30f, SummitY, -12f), Quaternion.Euler(0f, -90f, 0f));
            Material horse = Mat("Caballo", new Color(0.18f, 0.13f, 0.1f));
            PrototypeSceneKit.Primitive(PrimitiveType.Cube, "Caballo", rider, new Vector3(0f, 1.2f, 0f), new Vector3(0.6f, 0.8f, 2f), horse, keepCollider: false);
            PrototypeSceneKit.Primitive(PrimitiveType.Cube, "Cuello", rider, new Vector3(0f, 1.75f, 0.95f), new Vector3(0.35f, 0.8f, 0.35f), horse, keepCollider: false);
            for (int i = 0; i < 4; i++)
            {
                PrototypeSceneKit.Primitive(PrimitiveType.Cube, "Pata", rider, new Vector3(i % 2 == 0 ? -0.22f : 0.22f, 0.4f, i < 2 ? 0.8f : -0.8f), new Vector3(0.14f, 0.8f, 0.14f), horse, keepCollider: false);
            }
            PrototypeSceneKit.Primitive(PrimitiveType.Capsule, "Ugarte", rider, new Vector3(0f, 2.25f, -0.1f), new Vector3(0.45f, 0.55f, 0.45f), Mat("Uniforme_Oficial_Peru", new Color(0.12f, 0.12f, 0.2f)), keepCollider: false);
            // La bandera que se lleva consigo.
            PrototypeSceneKit.Primitive(PrimitiveType.Cube, "Bandera_de_Ugarte", rider, new Vector3(0.4f, 3f, 0f), new Vector3(0.03f, 0.9f, 1.4f), Mat("Bandera_Rojo", new Color(0.78f, 0.1f, 0.12f)), keepCollider: false);
            ugarte = rider.gameObject.AddComponent<ScriptedRider>();
            ugarte.Configure(new[] { new Vector3(-50f, SummitY, -8f), new Vector3(CliffX + 6f, SummitY, -6f) }, 9f, offEdge: true, startNow: false);
            return summit;
        }
    }
}
