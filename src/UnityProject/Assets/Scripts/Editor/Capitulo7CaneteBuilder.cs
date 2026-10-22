using System;
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
    /// Escena jugable del Capítulo 7, «Cadenas rotas» (ROADMAP 6.9): de noche, una senda de 4 m entre los cañaverales de
    /// Cañete con cinco torpedos de tierra, la acequia y el campo de tiro al salir de la caña, y la hacienda fortificada
    /// con su portón de madera. La misión la dirige <see cref="CaneteMissionDirector"/> con el guion de
    /// <see cref="CaneteChapter"/>. Greyboxing con primitivas (AGENTS.md §4.A). Modo batch:
    /// <c>Unity -batchmode -quit -projectPath src/UnityProject -executeMethod Pacifico.EditorTools.Capitulo7CaneteBuilder.RunBatch</c>
    /// </summary>
    public static class Capitulo7CaneteBuilder
    {
        public const string ScenePath = ProjectPaths.Scenes + "/Capitulo7_Canaverales_de_Canete.unity";
        internal const string PlateImage = "Archivo_Historico/02_Fotos_Soldados_y_Personajes/09_Culi_Chino_Esclavizado_Peru_1881.jpg";
        internal const string PlateCaption = "Culí chino encadenado en una hacienda azucarera del Perú, hacia 1881. Fotografía de época. Archivo Histórico.";

        /// <summary>Senda: 4 m de ancho a lo largo de z, de 0 a <see cref="TrailEndZ"/>.</summary>
        private const float TrailHalfWidth = 2.2f;
        private const float TrailEndZ = 122f;
        private static readonly float[] MineZ = { 18f, 38f, 58f, 78f, 98f };
        private const float HaciendaZ = 235f;

        private static readonly Color Earth = new Color(0.32f, 0.27f, 0.2f);
        private static readonly Color Cane = new Color(0.36f, 0.42f, 0.2f);
        private static readonly Color Adobe = new Color(0.62f, 0.55f, 0.44f);
        private static readonly Color Wood = new Color(0.36f, 0.24f, 0.14f);
        private static readonly Color Wire = new Color(0.85f, 0.82f, 0.6f);

        [MenuItem("Pacífico/Capítulos/Capítulo 7: Cañaverales de Cañete")]
        public static void Build()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            BuildScene();
            EditorUtility.DisplayDialog("Pacífico", "Escena creada en " + ScenePath +
                "\n\nC: agacharse (descubre los alambres) · E mantenido: cortar el alambre, cargar al herido." +
                "\nG: tajo de machete · E: recoger la Winchester y la dinamita, poner la carga.", "Aceptar");
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

        private static GameObject Box(string name, Transform parent, Vector3 position, Vector3 size, Material material, bool collider = true) =>
            PrototypeSceneKit.Primitive(PrimitiveType.Cube, name, parent, position, size, material, collider);

        internal static void BuildScene()
        {
            HistoricalDataAssetGenerator.GenerateAll();
            WeaponDataSO machete = LoadWeapon(WeaponCatalog.MacheteId);
            WeaponDataSO winchester = LoadWeapon(WeaponCatalog.WinchesterId);
            WeaponDataSO comblain = LoadWeapon(WeaponCatalog.ComblainId);
            WeaponDataSO remington = LoadWeapon(WeaponCatalog.RemingtonId);
            WeaponDataSO bayonet = LoadWeapon(WeaponCatalog.TriangularBayonetId);

            string root = HistoricalDataAssetGenerator.RepositoryRoot;
            string guion = File.ReadAllText(Path.Combine(root, GuionQuotes.ScriptFile));
            string chapter = GuionQuotes.Section(guion, CaneteChapter.ChapterHeading);
            CaneteChapter.Build(GuionQuotes.Extract(chapter, CaneteChapter.ChapterHeading)); // falla aquí si el guion cambió

            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            Light moon = CreateNight();
            var world = new GameObject("Valle_de_Canete").transform;
            Material earth = Mat("Tierra_Canete", Earth);
            Material blast = Mat("Humo_Explosion", new Color(0.18f, 0.16f, 0.15f));
            Box("Suelo", world, new Vector3(0f, -0.5f, 120f), new Vector3(300f, 1f, 360f), earth);
            CreateCaneField(world);
            TripwireMine[] mines = CreateTrailMines(world, blast);
            TripwireMine patrolMine = Mine(world, "Torpedo_de_la_Patrulla", new Vector3(22f, 0f, 168f), 3.5f, blast);
            Transform safe = CreateAcequia(world, earth);
            WoundedSoldier wounded = CreateWounded(world, safe);
            CreateHacienda(world, out GameObject gate, out Transform courtyard, out Transform[] posts, out Transform[] finishers);

            var baker = new GameObject("NavMesh_Tiempo_de_Ejecucion").AddComponent<NavMeshRuntimeBaker>();
            baker.transform.position = new Vector3(0f, 5f, 120f);
            var bakerSo = new SerializedObject(baker);
            bakerSo.FindProperty("size").vector3Value = new Vector3(300f, 30f, 360f);
            bakerSo.ApplyModifiedPropertiesWithoutUndo();

            // Quintín Quintana, con el machete, al principio de la senda.
            FirstPersonController player = PisaguaPrototypeBuilder.CreatePlayer(machete, null, new Vector3(0f, 0.05f, 2f), 0f, 0);
            player.gameObject.AddComponent<Combatant>().Configure(Faction.Chile, true, null, "Quintín Quintana", 4f);
            var hud = new GameObject("HUD_Infanteria").AddComponent<InfantryHud>();
            hud.Player = player;
            hud.Rifle = player.GetComponent<RifleController>();
            hud.Melee = player.GetComponent<MeleeController>();
            PisaguaPrototypeBuilder.CreateSmoke(player.ViewCamera.transform);

            Transform winchesterSpot = Marker("Winchester", new Vector3(-13f, 0.05f, 133f), 0f);
            InteractionPoint dynamite = Interaction(world, "Cajon_de_Dinamita", new Vector3(-23f, 0.4f, 133f), Mat("Cajon", Wood));
            InteractionPoint charge = Interaction(world, "Carga_del_Porton", new Vector3(0f, 0.4f, HaciendaZ - 16.8f), Mat("Cajon", Wood));
            var allies = new Transform[6];
            for (int i = 0; i < allies.Length; i++) allies[i] = Marker("Aliado_" + (i + 1), new Vector3(-21f + (i % 3) * 3f, 0f, 126f + (i / 3) * 3f), 0f);

            Material skin = Mat("Piel", new Color(0.62f, 0.47f, 0.36f));
            var chileLook = new SoldierLook
            {
                uniform = Mat("Uniforme_Chile", new Color(0.14f, 0.17f, 0.3f)), trousers = Mat("Pantalon_Chile", new Color(0.55f, 0.18f, 0.14f)),
                skin = skin, kepi = Mat("Quepis_Chile", new Color(0.12f, 0.14f, 0.25f)),
            };
            var culiLook = new SoldierLook
            {
                uniform = Mat("Blusa_Culi", new Color(0.42f, 0.4f, 0.36f)), trousers = Mat("Pantalon_Culi", new Color(0.3f, 0.28f, 0.25f)),
                skin = skin, kepi = Mat("Sombrero_de_Paja", new Color(0.66f, 0.56f, 0.36f)),
            };
            var peruLook = new SoldierLook
            {
                uniform = Mat("Uniforme_Peru", new Color(0.82f, 0.8f, 0.72f)), trousers = Mat("Pantalon_Peru", new Color(0.78f, 0.76f, 0.68f)),
                skin = skin, kepi = Mat("Quepis_Peru", new Color(0.35f, 0.12f, 0.1f)),
            };
            Texture2D plate = HistoricalDataAssetGenerator.ImportArchiveTexture(root, PlateImage);

            var director = new GameObject("Director_Capitulo7").AddComponent<CaneteMissionDirector>();
            director.Configure(chapter, player, plate, PlateCaption, mines, patrolMine, wounded, finishers, posts, winchesterSpot, dynamite, charge,
                               gate, courtyard, allies, blast, moon, winchester, comblain, remington, bayonet, chileLook, culiLook, peruLook);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, ScenePath);
            PrototypeSceneKit.AddToBuildSettings(ScenePath);
            Debug.Log("[Pacífico] Capítulo 7 guardado en " + ScenePath);
        }

        private static Transform Marker(string name, Vector3 position, float heading)
        {
            var t = new GameObject("Puesto_" + name).transform;
            t.SetPositionAndRotation(position, Quaternion.Euler(0f, heading, 0f));
            return t;
        }

        /// <summary>Noche de luna: luz fría y débil, niebla oscura.</summary>
        private static Light CreateNight()
        {
            var moon = new GameObject("Luna").AddComponent<Light>();
            moon.type = LightType.Directional;
            moon.intensity = 0.25f;
            moon.color = new Color(0.55f, 0.62f, 0.85f);
            moon.shadows = LightShadows.Soft;
            moon.transform.rotation = Quaternion.Euler(35f, -30f, 0f);
            RenderSettings.ambientLight = new Color(0.08f, 0.09f, 0.13f);
            RenderSettings.fog = true;
            RenderSettings.fogColor = new Color(0.05f, 0.06f, 0.09f);
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogStartDistance = 25f;
            RenderSettings.fogEndDistance = 220f;
            return moon;
        }

        /// <summary>
        /// Cañaverales a ambos lados de la senda: hileras macizas de 3 m con rendijas de 0,4 m (no se pasa entre ellas, así
        /// que los torpedos no se pueden rodear) y que tapan la vista como la caña de 2,8 m.
        /// </summary>
        private static void CreateCaneField(Transform world)
        {
            var cane = new GameObject("Canaverales").transform;
            cane.SetParent(world, false);
            Material material = Mat("Cana", Cane);
            for (int side = -1; side <= 1; side += 2)
            {
                for (int i = 0; i < 9; i++)
                {
                    float x = side * (TrailHalfWidth + 0.3f + 1.5f + i * 3.4f);
                    Box("Hilera_de_Cana", cane, new Vector3(x, 1.4f, TrailEndZ * 0.5f), new Vector3(3f, 2.8f, TrailEndZ), material);
                }
            }
        }

        private static TripwireMine[] CreateTrailMines(Transform world, Material blast)
        {
            var mines = new TripwireMine[MineZ.Length];
            for (int i = 0; i < MineZ.Length; i++) mines[i] = Mine(world, "Torpedo_de_Tierra_" + (i + 1), new Vector3(0f, 0f, MineZ[i]), TrailHalfWidth + 0.3f, blast); // de caña a caña
            return mines;
        }

        /// <summary>Caja de dinamita enterrada con un alambre entre dos estacas (el alambre solo se ve al descubrirlo).</summary>
        private static TripwireMine Mine(Transform world, string name, Vector3 centre, float halfWidth, Material blast)
        {
            var root = new GameObject(name).transform;
            root.SetParent(world, false);
            root.position = centre;
            Material wood = Mat("Estaca", Wood);
            Box("Estaca_A", root, new Vector3(-halfWidth, 0.2f, 0f), new Vector3(0.06f, 0.4f, 0.06f), wood, false);
            Box("Estaca_B", root, new Vector3(halfWidth, 0.2f, 0f), new Vector3(0.06f, 0.4f, 0.06f), wood, false);
            Box("Caja_de_Dinamita", root, new Vector3(0f, -0.1f, 0.4f), new Vector3(0.5f, 0.25f, 0.35f), wood, false);
            GameObject wire = Box("Alambre", root, new Vector3(0f, 0.15f, 0f), new Vector3(halfWidth * 2f, 0.02f, 0.02f), Mat("Alambre", Wire), false);
            GameObject marker = Box("Cana_Senal", root, new Vector3(0.6f, 0.9f, 0.6f), new Vector3(0.05f, 1.8f, 0.05f), Mat("Cana", Cane), false);
            var mine = root.gameObject.AddComponent<TripwireMine>();
            mine.Configure(centre + new Vector3(-halfWidth, 0f, 0f), centre + new Vector3(halfWidth, 0f, 0f), wire.GetComponent<Renderer>(), blast, marker);
            return mine;
        }

        /// <summary>La acequia al salir del cañaveral: dos bordos de tierra que cubren de la hacienda; su fondo es el punto seguro.</summary>
        private static Transform CreateAcequia(Transform world, Material earth)
        {
            var acequia = new GameObject("Acequia").transform;
            acequia.SetParent(world, false);
            Box("Bordo_Norte", acequia, new Vector3(-18f, 0.5f, 141f), new Vector3(16f, 1f, 1.2f), earth);
            Box("Bordo_Este", acequia, new Vector3(-9.5f, 0.5f, 133f), new Vector3(1.2f, 1f, 14f), earth);
            var safe = new GameObject("Fondo_de_la_Acequia").transform;
            safe.SetParent(acequia, false);
            safe.position = new Vector3(-17f, 0f, 136f);
            return safe;
        }

        private static WoundedSoldier CreateWounded(Transform world, Transform safe)
        {
            var root = new GameObject("Soldado_Herido").transform;
            root.SetParent(world, false);
            root.position = new Vector3(14f, 0f, 162f);
            var body = new GameObject("Cuerpo_Tendido").transform;
            body.SetParent(root, false);
            body.localPosition = new Vector3(0f, 0.25f, 0f);
            body.localRotation = Quaternion.Euler(0f, 0f, 90f);
            PrototypeSceneKit.Primitive(PrimitiveType.Capsule, "Cuerpo", body, Vector3.zero, new Vector3(0.45f, 0.85f, 0.45f), Mat("Uniforme_Chile", new Color(0.14f, 0.17f, 0.3f)), keepCollider: false);
            PrototypeSceneKit.Primitive(PrimitiveType.Sphere, "Cabeza", body, new Vector3(0f, 0.95f, 0f), Vector3.one * 0.24f, Mat("Piel", new Color(0.62f, 0.47f, 0.36f)), keepCollider: false);
            body.gameObject.SetActive(false);
            var wounded = root.gameObject.AddComponent<WoundedSoldier>();
            wounded.Configure(body, safe);
            return wounded;
        }

        /// <summary>
        /// La hacienda fortificada: tapias de adobe de 3,2 m con banquetas de tirador detrás del muro sur, el portón de madera
        /// en el centro y el patio.
        /// </summary>
        private static void CreateHacienda(Transform world, out GameObject gate, out Transform courtyard, out Transform[] posts, out Transform[] finishers)
        {
            var hacienda = new GameObject("Hacienda_Fortificada").transform;
            hacienda.SetParent(world, false);
            Material adobe = Mat("Adobe", Adobe);
            const float halfX = 22f, halfZ = 15f, height = 3.2f;
            float south = HaciendaZ - halfZ;
            Box("Tapia_Sur_Oeste", hacienda, new Vector3(-(halfX + 2.5f) * 0.5f, height * 0.5f, south), new Vector3(halfX - 2.5f, height, 1f), adobe);
            Box("Tapia_Sur_Este", hacienda, new Vector3((halfX + 2.5f) * 0.5f, height * 0.5f, south), new Vector3(halfX - 2.5f, height, 1f), adobe);
            Box("Tapia_Norte", hacienda, new Vector3(0f, height * 0.5f, HaciendaZ + halfZ), new Vector3(2f * halfX, height, 1f), adobe);
            Box("Tapia_Oeste", hacienda, new Vector3(-halfX, height * 0.5f, HaciendaZ), new Vector3(1f, height, 2f * halfZ), adobe);
            Box("Tapia_Este", hacienda, new Vector3(halfX, height * 0.5f, HaciendaZ), new Vector3(1f, height, 2f * halfZ), adobe);
            gate = Box("Porton_de_Madera", hacienda, new Vector3(0f, height * 0.5f, south), new Vector3(5f, height, 0.5f), Mat("Porton", Wood));
            Box("Casa_Hacienda", hacienda, new Vector3(0f, 2.5f, HaciendaZ + 9f), new Vector3(20f, 5f, 8f), adobe);

            // Banquetas de tirador: los defensores asoman por encima de la tapia sur.
            float[] postX = { -16f, -10f, -6f, 6f, 10f, 16f };
            posts = new Transform[postX.Length + 2];
            for (int i = 0; i < postX.Length; i++)
            {
                Box("Banqueta", hacienda, new Vector3(postX[i], 1f, south + 1.4f), new Vector3(3f, 2f, 1.6f), adobe);
                posts[i] = Marker("Fusilero_" + (i + 1), new Vector3(postX[i], 2.05f, south + 1.4f), 180f);
            }
            posts[postX.Length] = Marker("Patio_1", new Vector3(-8f, 0f, HaciendaZ + 2f), 180f);
            posts[postX.Length + 1] = Marker("Patio_2", new Vector3(8f, 0f, HaciendaZ + 2f), 180f);
            courtyard = Marker("Patio", new Vector3(0f, 0f, HaciendaZ), 0f);
            finishers = new[] { Marker("Tirador_1", new Vector3(-3f, 0f, south - 3f), 180f), Marker("Tirador_2", new Vector3(3f, 0f, south - 3f), 180f) };
        }

        private static InteractionPoint Interaction(Transform world, string name, Vector3 position, Material material)
        {
            GameObject crate = Box(name, world, position, new Vector3(0.6f, 0.5f, 0.45f), material, false);
            return crate.AddComponent<InteractionPoint>();
        }
    }
}
