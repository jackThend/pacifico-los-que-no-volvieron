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
    /// Escena jugable del Capítulo 4, «Sed en la quebrada» (ROADMAP 6.2): el fondo de la quebrada de Tarapacá con el
    /// pueblo de adobe, la iglesia, pircas y sauces secos; las laderas por las que baja la vanguardia chilena; la
    /// pampa superior con dos cañones Krupp; y el camino hacia Arica con el tambor herido. El jugador es Mariano
    /// Santos, del Zepita, con un Chassepot y pocos cartuchos. Greyboxing con primitivas (AGENTS.md §4.A).
    /// Modo batch:
    /// <c>Unity -batchmode -quit -projectPath src/UnityProject -executeMethod Pacifico.EditorTools.Capitulo4TarapacaBuilder.RunBatch</c>
    /// </summary>
    public static class Capitulo4TarapacaBuilder
    {
        public const string ScenePath = ProjectPaths.Scenes + "/Capitulo4_Quebrada_de_Tarapaca.unity";

        /// <summary>Ancho del fondo de la quebrada (m), altura de la pampa y distancia horizontal de cada ladera.</summary>
        private const float FloorHalfWidth = 40f;
        private const float PampaHeight = 40f;
        private const float SlopeRun = 70f;
        private const float Length = 320f;
        /// <summary>Cartuchos de Chassepot al empezar: «cada cartucho disparado cuenta».</summary>
        private const int PlayerCartridges = 10;

        private static readonly Color Earth = new Color(0.66f, 0.55f, 0.42f);
        private static readonly Color Adobe = new Color(0.72f, 0.6f, 0.46f);
        private static readonly Color Stone = new Color(0.55f, 0.52f, 0.47f);
        private static readonly Color DryWood = new Color(0.42f, 0.34f, 0.24f);
        private static readonly Color DryLeaves = new Color(0.55f, 0.53f, 0.36f);
        private static readonly Color Bronze = new Color(0.35f, 0.3f, 0.22f);

        [MenuItem("Pacífico/Capítulos/Capítulo 4: Quebrada de Tarapacá")]
        public static void Build()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            BuildScene();
            EditorUtility.DisplayDialog("Pacífico", "Escena creada en " + ScenePath +
                "\n\nWASD y ratón · clic derecho: encarar · clic: fuego · R: cargar · F: bayoneta · E: recoger un fusil o dar agua.", "Aceptar");
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

        private static WeaponDataSO LoadWeapon(string id)
        {
            var data = AssetDatabase.LoadAssetAtPath<WeaponDataSO>(ProjectPaths.WeaponData + "/Weapon_" + id + ".asset");
            if (data == null) throw new InvalidOperationException("No se generó el WeaponDataSO " + id + ".");
            return data;
        }

        private static Material Mat(string key, Color color) => PrototypeSceneKit.Material(key, color);

        private static GameObject Box(string name, Transform parent, Vector3 position, Vector3 size, Color color, float yaw = 0f, bool collider = true)
        {
            GameObject go = PrototypeSceneKit.Primitive(PrimitiveType.Cube, name, parent, position, size, Mat(name.Split('_')[0], color), collider);
            go.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
            return go;
        }

        internal static void BuildScene()
        {
            HistoricalDataAssetGenerator.GenerateAll();
            WeaponDataSO chassepot = LoadWeapon(WeaponCatalog.ChassepotId);
            WeaponDataSO comblain = LoadWeapon(WeaponCatalog.ComblainId);
            WeaponDataSO bayonet = LoadWeapon(WeaponCatalog.TriangularBayonetId);

            string guion = File.ReadAllText(Path.Combine(HistoricalDataAssetGenerator.RepositoryRoot, GuionQuotes.ScriptFile));
            string chapter = GuionQuotes.Section(guion, TarapacaChapter.ChapterHeading);
            TarapacaChapter.Build(GuionQuotes.Extract(chapter, TarapacaChapter.ChapterHeading)); // falla aquí si el guion cambió

            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            CreateLight();
            var world = new GameObject("Quebrada_de_Tarapaca").transform;
            CreateCanyon(world);
            Transform plaza = CreateVillage(world);
            CreateDryWillows(world);
            KruppGun[] guns = CreateKrupps(world, plaza.position);
            WoundedDrummer drummer = CreateRoadToArica(world, out Transform column);

            var baker = new GameObject("NavMesh_Tiempo_de_Ejecucion").AddComponent<NavMeshRuntimeBaker>();
            baker.transform.position = new Vector3(0f, PampaHeight * 0.5f, 0f);
            var bakerSo = new SerializedObject(baker);
            bakerSo.FindProperty("size").vector3Value = new Vector3(2f * (FloorHalfWidth + SlopeRun + 60f), PampaHeight + 40f, Length);
            bakerSo.ApplyModifiedPropertiesWithoutUndo();

            // Mariano Santos descansa al norte del pueblo, con su Chassepot y pocos cartuchos.
            FirstPersonController player = PisaguaPrototypeBuilder.CreatePlayer(chassepot, null, new Vector3(-18f, 0.05f, 55f), 180f, PlayerCartridges);
            player.gameObject.AddComponent<Combatant>().Configure(Faction.Peru, true, null, "Mariano Santos", 4f);
            var hud = new GameObject("HUD_Infanteria").AddComponent<InfantryHud>();
            hud.Player = player;
            hud.Rifle = player.GetComponent<RifleController>();
            hud.Melee = player.GetComponent<MeleeController>();
            PisaguaPrototypeBuilder.CreateSmoke(player.ViewCamera.transform);
            drummer.Configure(drummer.transform.Find("Figura"), player.transform);

            CreateCaceres(plaza.position);

            var allies = new List<Transform>();
            for (int i = 0; i < 6; i++) allies.Add(Marker("Zepita_" + (i + 1), new Vector3(-14f + (i % 3) * 2.5f, 0f, 50f + (i / 3) * 3f), 180f));
            var chile = new List<Transform>();
            for (int i = 0; i < 6; i++)
            {
                // En el borde de la pampa este, a lo largo de la quebrada: bajan por la ladera hacia el pueblo.
                chile.Add(Marker("Vanguardia_" + (i + 1), new Vector3(FloorHalfWidth + SlopeRun - 4f, PampaHeight, -50f + i * 20f), -90f));
            }
            var escort = new List<Transform>();
            for (int i = 0; i < 4; i++) escort.Add(Marker("Escolta_" + (i + 1), new Vector3(FloorHalfWidth + SlopeRun + 30f, PampaHeight, -24f + i * 16f), -90f));

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

            var director = new GameObject("Director_Capitulo4").AddComponent<TarapacaMissionDirector>();
            director.Configure(chapter, player, chassepot, comblain, bayonet, plaza, allies.ToArray(), chile.ToArray(), escort.ToArray(),
                               guns, drummer, column, chileLook, peruLook);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, ScenePath);
            PrototypeSceneKit.AddToBuildSettings(ScenePath);
            Debug.Log("[Pacífico] Capítulo 4 guardado en " + ScenePath);
        }

        private static Transform Marker(string name, Vector3 position, float heading)
        {
            var t = new GameObject("Puesto_" + name).transform;
            t.SetPositionAndRotation(position, Quaternion.Euler(0f, heading, 0f));
            return t;
        }

        private static void CreateLight()
        {
            var sun = new GameObject("Sol_de_Tarapaca").AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.intensity = 1.5f; // «el calor es abrasador»
            sun.color = new Color(1f, 0.93f, 0.8f);
            sun.shadows = LightShadows.Soft;
            sun.transform.rotation = Quaternion.Euler(28f, 60f, 0f); // sol de la mañana, bajo sobre la pampa este
            RenderSettings.ambientLight = new Color(0.55f, 0.5f, 0.44f);
            RenderSettings.fog = true;
            RenderSettings.fogColor = new Color(0.86f, 0.8f, 0.7f);
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogStartDistance = 150f;
            RenderSettings.fogEndDistance = 900f;
        }

        /// <summary>Fondo plano, dos laderas de ~30° (caminables para el NavMesh) y la pampa arriba.</summary>
        private static void CreateCanyon(Transform world)
        {
            Box("Fondo_Quebrada", world, new Vector3(0f, -0.5f, 0f), new Vector3(2f * FloorHalfWidth + 2f, 1f, Length), Earth);
            float slopeLength = Mathf.Sqrt(SlopeRun * SlopeRun + PampaHeight * PampaHeight);
            float angle = Mathf.Atan2(PampaHeight, SlopeRun) * Mathf.Rad2Deg;
            for (int side = -1; side <= 1; side += 2)
            {
                GameObject slope = PrototypeSceneKit.Primitive(PrimitiveType.Cube, side < 0 ? "Ladera_Oeste" : "Ladera_Este", world,
                    new Vector3(side * (FloorHalfWidth + SlopeRun * 0.5f), PampaHeight * 0.5f - 0.5f, 0f), new Vector3(slopeLength, 1f, Length), Mat("Ladera", Earth));
                slope.transform.rotation = Quaternion.Euler(0f, 0f, side * angle);
                float pampaX = side * (FloorHalfWidth + SlopeRun + 60f);
                Box(side < 0 ? "Pampa_Oeste" : "Pampa_Este", world, new Vector3(pampaX, PampaHeight - 0.5f, 0f), new Vector3(120f, 1f, Length), Earth);
            }
        }

        /// <summary>El pueblo: casas de adobe en manzanas, la plaza con la iglesia, corrales de pirca.</summary>
        private static Transform CreateVillage(Transform world)
        {
            var village = new GameObject("Pueblo_de_Tarapaca").transform;
            village.SetParent(world, false);
            var plaza = new GameObject("Plaza_de_la_Iglesia").transform;
            plaza.SetParent(village, false);
            plaza.position = Vector3.zero;

            // Iglesia al sur de la plaza, con su campanario.
            Box("Iglesia_Nave", village, new Vector3(0f, 3f, -20f), new Vector3(9f, 6f, 16f), Adobe);
            Box("Iglesia_Campanario", village, new Vector3(0f, 6f, -10.5f), new Vector3(4f, 12f, 4f), Adobe);
            Box("Tejado_Campanario", village, new Vector3(0f, 12.4f, -10.5f), new Vector3(4.6f, 0.8f, 4.6f), DryWood, collider: false);

            // Manzanas de casas alrededor de la plaza (calles de 5 m entre ellas); ninguna encima de la plaza.
            var rng = new System.Random(1879);
            for (int bx = -3; bx <= 3; bx++)
            {
                for (int bz = -5; bz <= 5; bz++)
                {
                    float x = bx * 10f, z = bz * 11f;
                    if (Mathf.Abs(x) < 14f && Mathf.Abs(z) < 14f) continue; // plaza
                    if (Mathf.Abs(x) < 7f && z < -12f && z > -30f) continue; // iglesia
                    if (rng.NextDouble() < 0.3) continue; // solares vacíos y corrales
                    float w = 5f + (float)rng.NextDouble() * 1.5f, d = 5f + (float)rng.NextDouble() * 2f, h = 2.8f + (float)rng.NextDouble() * 0.8f;
                    Box("Casa_" + bx + "_" + bz, village, new Vector3(x, h * 0.5f, z), new Vector3(w, h, d), Adobe);
                    Box("Techo_" + bx + "_" + bz, village, new Vector3(x, h + 0.1f, z), new Vector3(w + 0.4f, 0.2f, d + 0.4f), DryWood, collider: false);
                }
            }
            // Pircas: tapias de piedra a la altura del pecho (cubren de rodillas).
            for (int i = 0; i < 14; i++)
            {
                float x = -30f + (float)rng.NextDouble() * 60f, z = -60f + (float)rng.NextDouble() * 120f;
                if (Mathf.Abs(x) < 12f && Mathf.Abs(z) < 12f) continue;
                Box("Pirca_" + (i + 1), village, new Vector3(x, 0.55f, z), new Vector3(6f, 1.1f, 0.6f), Stone, yaw: (float)rng.NextDouble() * 180f);
            }
            return plaza;
        }

        /// <summary>Sauces secos junto al cauce sin agua, al oeste del pueblo.</summary>
        private static void CreateDryWillows(Transform world)
        {
            var rng = new System.Random(27);
            Box("Cauce_Seco", world, new Vector3(-34f, -0.45f, 0f), new Vector3(4f, 0.1f, Length), new Color(0.6f, 0.52f, 0.42f), collider: false);
            for (int i = 0; i < 16; i++)
            {
                float z = -140f + i * 18f + (float)rng.NextDouble() * 6f;
                float x = -36f + (float)rng.NextDouble() * 6f;
                PrototypeSceneKit.Primitive(PrimitiveType.Cylinder, "Sauce_Tronco_" + i, world, new Vector3(x, 2f, z), new Vector3(0.4f, 2f, 0.4f), Mat("Tronco", DryWood));
                PrototypeSceneKit.Primitive(PrimitiveType.Sphere, "Sauce_Copa_" + i, world, new Vector3(x, 4.6f, z), new Vector3(4f, 3f, 4f), Mat("Hojas_Secas", DryLeaves), keepCollider: false);
            }
        }

        /// <summary>Dos cañones Krupp de montaña en el borde de la pampa este, batiendo el pueblo.</summary>
        private static KruppGun[] CreateKrupps(Transform world, Vector3 village)
        {
            Material dust = Mat("Polvo", new Color(0.78f, 0.7f, 0.58f));
            var guns = new KruppGun[2];
            for (int i = 0; i < 2; i++)
            {
                var root = new GameObject("Krupp_" + (i + 1)).transform;
                root.SetParent(world, false);
                root.SetPositionAndRotation(new Vector3(FloorHalfWidth + SlopeRun + 10f, PampaHeight, i == 0 ? -25f : 25f), Quaternion.Euler(0f, -90f, 0f));
                PrototypeSceneKit.Primitive(PrimitiveType.Cube, "Cureña", root, new Vector3(0f, 0.55f, 0f), new Vector3(0.9f, 0.5f, 1.8f), Mat("Cureña", DryWood));
                for (int w = -1; w <= 1; w += 2)
                {
                    GameObject wheel = PrototypeSceneKit.Primitive(PrimitiveType.Cylinder, "Rueda", root, new Vector3(w * 0.6f, 0.5f, 0.2f), new Vector3(1f, 0.05f, 1f), Mat("Rueda", DryWood), keepCollider: false);
                    wheel.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
                }
                var barrelPivot = new GameObject("Tubo").transform;
                barrelPivot.SetParent(root, false);
                barrelPivot.localPosition = new Vector3(0f, 0.95f, 0f);
                GameObject barrel = PrototypeSceneKit.Primitive(PrimitiveType.Cylinder, "Tubo_Krupp", barrelPivot, new Vector3(0f, 0f, 0.6f), new Vector3(0.14f, 0.6f, 0.14f), Mat("Bronce", Bronze), keepCollider: false);
                barrel.transform.localRotation = Quaternion.Euler(84f, 0f, 0f);
                // Parapeto de sacos por delante.
                PrototypeSceneKit.Primitive(PrimitiveType.Cube, "Parapeto", root, new Vector3(0f, 0.45f, 2.2f), new Vector3(4f, 0.9f, 0.8f), Mat("Sacos", Earth));

                var gun = root.gameObject.AddComponent<KruppGun>();
                gun.Configure(barrelPivot, village, 35f, dust);
                guns[i] = gun;
            }
            return guns;
        }

        /// <summary>El camino al sur, hacia Arica: una tapia de barro con el tambor herido y, más allá, la columna.</summary>
        private static WoundedDrummer CreateRoadToArica(Transform world, out Transform column)
        {
            Box("Tapia_de_Barro", world, new Vector3(-8f, 1.1f, -96f), new Vector3(7f, 2.2f, 0.6f), Adobe);
            var root = new GameObject("Tambor_Herido").transform;
            root.SetParent(world, false);
            root.SetPositionAndRotation(new Vector3(-8f, 0f, -95.2f), Quaternion.Euler(0f, 180f, 0f));
            var figure = new GameObject("Figura").transform;
            figure.SetParent(root, false);
            // Sentado contra la tapia: torso, cabeza, piernas estiradas y el tambor a su lado.
            PrototypeSceneKit.Primitive(PrimitiveType.Capsule, "Torso", figure, new Vector3(0f, 0.55f, 0f), new Vector3(0.36f, 0.4f, 0.3f), Mat("Uniforme_Chile", new Color(0.14f, 0.17f, 0.3f)), keepCollider: false);
            PrototypeSceneKit.Primitive(PrimitiveType.Sphere, "Cabeza", figure, new Vector3(0f, 1.05f, 0.05f), Vector3.one * 0.2f, Mat("Piel", new Color(0.62f, 0.47f, 0.36f)), keepCollider: false);
            PrototypeSceneKit.Primitive(PrimitiveType.Cube, "Piernas", figure, new Vector3(0f, 0.12f, 0.45f), new Vector3(0.3f, 0.18f, 0.8f), Mat("Pantalon_Chile", new Color(0.55f, 0.18f, 0.14f)), keepCollider: false);
            GameObject drum = PrototypeSceneKit.Primitive(PrimitiveType.Cylinder, "Tambor", root, new Vector3(0.55f, 0.2f, 0.2f), new Vector3(0.4f, 0.18f, 0.4f), Mat("Tambor", new Color(0.7f, 0.15f, 0.12f)), keepCollider: false);
            drum.transform.localRotation = Quaternion.Euler(0f, 0f, 80f);
            var drummer = root.gameObject.AddComponent<WoundedDrummer>();

            column = new GameObject("Columna_hacia_Arica").transform;
            column.SetParent(world, false);
            column.position = new Vector3(-5f, 0f, -135f);
            // La columna: soldados en fila en el camino (decorado, esperan).
            Material uniform = Mat("Uniforme_Peru", new Color(0.82f, 0.8f, 0.72f));
            for (int i = 0; i < 12; i++)
            {
                PrototypeSceneKit.Primitive(PrimitiveType.Capsule, "Columna_" + (i + 1), column, new Vector3((i % 2) * 1.2f - 0.6f, 0.9f, -i * 1.4f), new Vector3(0.46f, 0.85f, 0.46f), uniform, keepCollider: false);
            }
            return drummer;
        }

        /// <summary>Cáceres a caballo cruza el pueblo arengando al Zepita.</summary>
        private static void CreateCaceres(Vector3 plaza)
        {
            var rider = new GameObject("Caceres_a_Caballo").transform;
            rider.SetPositionAndRotation(new Vector3(-10f, 0f, 62f), Quaternion.Euler(0f, 180f, 0f));
            PrototypeSceneKit.Primitive(PrimitiveType.Cube, "Caballo", rider, new Vector3(0f, 1.2f, 0f), new Vector3(0.6f, 0.8f, 2f), Mat("Caballo", new Color(0.3f, 0.2f, 0.13f)), keepCollider: false);
            PrototypeSceneKit.Primitive(PrimitiveType.Cube, "Cuello", rider, new Vector3(0f, 1.75f, 0.95f), new Vector3(0.35f, 0.8f, 0.35f), Mat("Caballo", new Color(0.3f, 0.2f, 0.13f)), keepCollider: false);
            for (int i = 0; i < 4; i++)
            {
                PrototypeSceneKit.Primitive(PrimitiveType.Cube, "Pata", rider, new Vector3(i % 2 == 0 ? -0.22f : 0.22f, 0.4f, i < 2 ? 0.8f : -0.8f), new Vector3(0.14f, 0.8f, 0.14f), Mat("Caballo", new Color(0.3f, 0.2f, 0.13f)), keepCollider: false);
            }
            PrototypeSceneKit.Primitive(PrimitiveType.Capsule, "Jinete", rider, new Vector3(0f, 2.25f, -0.1f), new Vector3(0.45f, 0.55f, 0.45f), Mat("Uniforme_Oficial_Peru", new Color(0.12f, 0.12f, 0.2f)), keepCollider: false);
            PrototypeSceneKit.Primitive(PrimitiveType.Sphere, "Cabeza_Jinete", rider, new Vector3(0f, 2.95f, -0.1f), Vector3.one * 0.24f, Mat("Piel", new Color(0.62f, 0.47f, 0.36f)), keepCollider: false);
            rider.gameObject.AddComponent<ScriptedRider>().Configure(new[]
            {
                new Vector3(-10f, 0f, 40f), plaza + new Vector3(-4f, 0f, 6f), plaza + new Vector3(6f, 0f, -4f), new Vector3(20f, 0f, -40f),
            }, 5f);
        }
    }
}
