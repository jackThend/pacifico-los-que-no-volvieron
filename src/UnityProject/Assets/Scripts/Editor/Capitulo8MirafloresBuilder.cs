using System;
using System.Collections.Generic;
using System.IO;
using Pacifico.Campaign;
using Pacifico.Core.Campaign;
using Pacifico.Core.Common;
using Pacifico.Core.Weapons;
using Pacifico.Data;
using Pacifico.Infantry;
using Pacifico.Narrative;
using Pacifico.Tactics;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Pacifico.EditorTools
{
    /// <summary>
    /// Escena jugable del Capítulo 8, «Los que no volvieron» (ROADMAP 6.5), y del epílogo «La memoria rota»: los jardines
    /// de Miraflores con tapias y casas señoriales derruidas, olivos y acequias; el Reducto N.º 3 (parapetos de tierra,
    /// dos piezas y la bandera); el sitio contra el parapeto donde Abraham Quiroz se sienta; y el mosaico de retratos de
    /// época del Archivo para el epílogo. El prólogo del corresponsal se muestra sobre la lámina del Reducto N.º 3.
    /// Modo batch:
    /// <c>Unity -batchmode -quit -projectPath src/UnityProject -executeMethod Pacifico.EditorTools.Capitulo8MirafloresBuilder.RunBatch</c>
    /// </summary>
    public static class Capitulo8MirafloresBuilder
    {
        public const string ScenePath = ProjectPaths.Scenes + "/Capitulo8_Reductos_de_Miraflores.unity";
        private const string PrologueImage = "Archivo_Historico/03_Lugares_y_Campos_de_Batalla/09_Reducto_Numero_3_Miraflores_1881.jpg";
        private const int PlayerCartridges = 60;

        private static readonly Vector3 RedoubtCenter = new Vector3(0f, 0f, 110f);
        private static readonly Color Earth = new Color(0.6f, 0.5f, 0.38f);
        private static readonly Color Garden = new Color(0.42f, 0.46f, 0.3f);
        private static readonly Color Adobe = new Color(0.74f, 0.63f, 0.5f);
        private static readonly Color Olive = new Color(0.36f, 0.42f, 0.28f);
        private static readonly Color Bark = new Color(0.35f, 0.28f, 0.2f);
        private static readonly Color Bronze = new Color(0.35f, 0.3f, 0.22f);

        [MenuItem("Pacífico/Capítulos/Capítulo 8: Reductos de Miraflores")]
        public static void Build()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            BuildScene();
            EditorUtility.DisplayDialog("Pacífico", "Escena creada en " + ScenePath +
                "\n\nWASD y ratón · clic derecho: encarar · clic: fuego · R: cargar · F/G: bayoneta y corvo · E: sentarse o recoger un fusil.", "Aceptar");
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

        private static Material Mat(string key, Color color) => PrototypeSceneKit.Material(key, color);

        private static WeaponDataSO LoadWeapon(string id)
        {
            var data = AssetDatabase.LoadAssetAtPath<WeaponDataSO>(ProjectPaths.WeaponData + "/Weapon_" + id + ".asset");
            if (data == null) throw new InvalidOperationException("No se generó el WeaponDataSO " + id + ".");
            return data;
        }

        internal static void BuildScene()
        {
            HistoricalDataAssetGenerator.GenerateAll();
            WeaponDataSO comblain = LoadWeapon(WeaponCatalog.ComblainId);
            WeaponDataSO remington = LoadWeapon(WeaponCatalog.RemingtonId);
            WeaponDataSO corvo = LoadWeapon(WeaponCatalog.CorvoId);
            WeaponDataSO bayonet = LoadWeapon(WeaponCatalog.TriangularBayonetId);
            string root = HistoricalDataAssetGenerator.RepositoryRoot;

            string guion = File.ReadAllText(Path.Combine(root, GuionQuotes.ScriptFile));
            string chapter = GuionQuotes.Section(guion, MirafloresChapter.ChapterHeading);
            string epilogue = GuionQuotes.Section(guion, MemoriaRotaEpilogue.SectionHeading);
            MirafloresChapter.Build(GuionQuotes.Extract(chapter, MirafloresChapter.ChapterHeading)); // fallan aquí si el guion cambió
            MemoriaRotaEpilogue.Parse(epilogue);
            Texture2D prologue = HistoricalDataAssetGenerator.ImportArchiveTexture(root, PrologueImage);
            var mosaic = new List<Texture2D>();
            foreach (string path in MemoriaRotaEpilogue.MosaicImages) mosaic.Add(HistoricalDataAssetGenerator.ImportArchiveTexture(root, path));

            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            CreateLight();
            var world = new GameObject("Miraflores").transform;
            PrototypeSceneKit.Primitive(PrimitiveType.Cube, "Suelo", world, new Vector3(0f, -0.5f, 30f), new Vector3(240f, 1f, 240f), Mat("Tierra", Earth));
            List<Transform> gardenPosts = CreateGardens(world);
            List<Transform> redoubtPosts = CreateRedoubt(world, out KruppGun[] guns, out Transform flag, out Transform seatPoint);

            var baker = new GameObject("NavMesh_Tiempo_de_Ejecucion").AddComponent<NavMeshRuntimeBaker>();
            baker.transform.position = new Vector3(0f, 10f, 30f);
            var bakerSo = new SerializedObject(baker);
            bakerSo.FindProperty("size").vector3Value = new Vector3(240f, 40f, 240f);
            bakerSo.ApplyModifiedPropertiesWithoutUndo();

            FirstPersonController player = PisaguaPrototypeBuilder.CreatePlayer(comblain, corvo, new Vector3(0f, 0.05f, -62f), 0f, PlayerCartridges);
            player.gameObject.AddComponent<Combatant>().Configure(Faction.Chile, true, null, "Abraham Quiroz", 4f);
            var hud = new GameObject("HUD_Infanteria").AddComponent<InfantryHud>();
            hud.Player = player;
            hud.Rifle = player.GetComponent<RifleController>();
            hud.Melee = player.GetComponent<MeleeController>();
            PisaguaPrototypeBuilder.CreateSmoke(player.ViewCamera.transform);

            var seat = seatPoint.gameObject.AddComponent<InteractionPoint>();
            seat.Configure("Sentarte contra el parapeto", player.transform);
            GameObject boy = CreateBoy();

            var shells = new GameObject("Bombardeo").AddComponent<AmbientBombardment>();
            shells.transform.position = new Vector3(0f, 0f, 40f);
            shells.Configure(new Vector3(170f, 0f, 110f), Mat("Polvo", new Color(0.78f, 0.7f, 0.58f)));

            var epiloguePlayer = new GameObject("Epilogo_La_Memoria_Rota").AddComponent<EpiloguePlayer>();
            epiloguePlayer.Mosaic = mosaic.ToArray();

            var squad = new List<Transform>();
            for (int i = 0; i < 5; i++) squad.Add(Marker("Escuadra_" + (i + 1), new Vector3(-8f + i * 4f, 0f, -58f - (i % 2) * 3f), 0f));

            var chileLook = new SoldierLook
            {
                uniform = Mat("Uniforme_Chile", new Color(0.14f, 0.17f, 0.3f)), trousers = Mat("Pantalon_Chile", new Color(0.55f, 0.18f, 0.14f)),
                skin = Mat("Piel", new Color(0.62f, 0.47f, 0.36f)), kepi = Mat("Quepis_Chile", new Color(0.12f, 0.14f, 0.25f)),
            };
            // Los defensores de Lima: ropa civil (levitas, chalecos), no uniformes.
            var civilLook = new SoldierLook
            {
                uniform = Mat("Levita", new Color(0.18f, 0.17f, 0.16f)), trousers = Mat("Pantalon_Civil", new Color(0.35f, 0.33f, 0.3f)),
                skin = Mat("Piel", new Color(0.62f, 0.47f, 0.36f)), kepi = Mat("Sombrero", new Color(0.25f, 0.22f, 0.18f)),
            };

            var director = new GameObject("Director_Capitulo8").AddComponent<MirafloresMissionDirector>();
            director.Configure(chapter, epilogue, player, comblain, remington, bayonet, prologue, squad.ToArray(), gardenPosts.ToArray(),
                               redoubtPosts.ToArray(), Marker("Reducto_3", RedoubtCenter, 0f), guns, shells, seat, boy, flag, epiloguePlayer,
                               chileLook, civilLook);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, ScenePath);
            PrototypeSceneKit.AddToBuildSettings(ScenePath);
            Debug.Log("[Pacífico] Capítulo 8 guardado en " + ScenePath);
        }

        private static Transform Marker(string name, Vector3 position, float heading)
        {
            var t = new GameObject("Puesto_" + name).transform;
            t.SetPositionAndRotation(position, Quaternion.Euler(0f, heading, 0f));
            return t;
        }

        /// <summary>Mediodía de verano en la costa de Lima: sol alto y bruma cálida.</summary>
        private static void CreateLight()
        {
            var sun = new GameObject("Sol_de_Enero").AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.intensity = 1.25f;
            sun.color = new Color(1f, 0.94f, 0.82f);
            sun.shadows = LightShadows.Soft;
            sun.transform.rotation = Quaternion.Euler(55f, 150f, 0f);
            RenderSettings.ambientLight = new Color(0.5f, 0.48f, 0.44f);
            RenderSettings.fog = true;
            RenderSettings.fogColor = new Color(0.82f, 0.8f, 0.74f);
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogStartDistance = 90f;
            RenderSettings.fogEndDistance = 500f;
        }

        /// <summary>Jardines de casas señoriales: tapias derruidas, restos de casas, olivos y acequias; puestos de los reservistas.</summary>
        private static List<Transform> CreateGardens(Transform world)
        {
            var rng = new System.Random(1881);
            for (int i = 0; i < 22; i++)
            {
                float x = -70f + (float)rng.NextDouble() * 140f, z = -40f + (float)rng.NextDouble() * 120f;
                float length = 6f + (float)rng.NextDouble() * 10f, height = 1.2f + (float)rng.NextDouble() * 1.4f;
                GameObject wall = PrototypeSceneKit.Primitive(PrimitiveType.Cube, "Tapia_" + (i + 1), world, new Vector3(x, height * 0.5f, z), new Vector3(length, height, 0.7f), Mat("Adobe", Adobe));
                wall.transform.rotation = Quaternion.Euler(0f, rng.Next(0, 2) * 90f + (float)rng.NextDouble() * 10f, 0f);
            }
            // Casas señoriales destrozadas por la metralla: dos muros en esquina sin techo.
            for (int i = 0; i < 5; i++)
            {
                float x = -60f + i * 30f, z = 45f + (i % 2) * 20f;
                PrototypeSceneKit.Primitive(PrimitiveType.Cube, "Casa_Muro_A_" + i, world, new Vector3(x, 2f, z), new Vector3(10f, 4f, 0.6f), Mat("Adobe", Adobe));
                PrototypeSceneKit.Primitive(PrimitiveType.Cube, "Casa_Muro_B_" + i, world, new Vector3(x - 5f, 1.5f, z - 4f), new Vector3(0.6f, 3f, 8f), Mat("Adobe", Adobe));
            }
            // Acequias: franjas de tierra húmeda (visual) y jardines.
            for (int i = 0; i < 3; i++)
            {
                PrototypeSceneKit.Primitive(PrimitiveType.Cube, "Acequia_" + (i + 1), world, new Vector3(0f, -0.45f, -20f + i * 35f), new Vector3(200f, 0.1f, 1.5f), Mat("Acequia", new Color(0.3f, 0.26f, 0.2f)), keepCollider: false);
                PrototypeSceneKit.Primitive(PrimitiveType.Cube, "Jardin_" + (i + 1), world, new Vector3(-40f + i * 40f, -0.44f, 10f + i * 10f), new Vector3(25f, 0.1f, 18f), Mat("Jardin", Garden), keepCollider: false);
            }
            for (int i = 0; i < 12; i++)
            {
                float x = -65f + (float)rng.NextDouble() * 130f, z = -30f + (float)rng.NextDouble() * 110f;
                PrototypeSceneKit.Primitive(PrimitiveType.Cylinder, "Olivo_Tronco_" + i, world, new Vector3(x, 1.5f, z), new Vector3(0.45f, 1.5f, 0.45f), Mat("Corteza", Bark));
                PrototypeSceneKit.Primitive(PrimitiveType.Sphere, "Olivo_Copa_" + i, world, new Vector3(x, 3.6f, z), new Vector3(4f, 2.6f, 4f), Mat("Olivo", Olive), keepCollider: false);
            }
            var posts = new List<Transform>();
            for (int i = 0; i < 8; i++)
            {
                float x = -50f + i * 14f, z = 20f + (i % 3) * 18f;
                posts.Add(Marker("Reservista_" + (i + 1), new Vector3(x, 0f, z), 180f));
                PrototypeSceneKit.Primitive(PrimitiveType.Cube, "Tapia_Defensa_" + (i + 1), world, new Vector3(x, 0.6f, z - 1.2f), new Vector3(4f, 1.2f, 0.6f), Mat("Adobe", Adobe));
            }
            return posts;
        }

        /// <summary>Reducto N.º 3: recinto de parapetos de tierra con entrada al sur, dos piezas, la bandera y un olivo partido.</summary>
        private static List<Transform> CreateRedoubt(Transform world, out KruppGun[] guns, out Transform flag, out Transform seat)
        {
            Vector3 c = RedoubtCenter;
            Material earth = Mat("Parapeto_Tierra", Earth * 0.9f);
            const float w = 40f, d = 24f, h = 2.2f, t = 3f;
            PrototypeSceneKit.Primitive(PrimitiveType.Cube, "Reducto_Norte", world, c + new Vector3(0f, h * 0.5f, d * 0.5f), new Vector3(w, h, t), earth);
            PrototypeSceneKit.Primitive(PrimitiveType.Cube, "Reducto_Oeste", world, c + new Vector3(-w * 0.5f, h * 0.5f, 0f), new Vector3(t, h, d), earth);
            PrototypeSceneKit.Primitive(PrimitiveType.Cube, "Reducto_Este", world, c + new Vector3(w * 0.5f, h * 0.5f, 0f), new Vector3(t, h, d), earth);
            // Frente sur con la entrada (hueco de 6 m en el centro): por aquí se asalta.
            PrototypeSceneKit.Primitive(PrimitiveType.Cube, "Reducto_Sur_Oeste", world, c + new Vector3(-(w + 6f) * 0.25f, h * 0.5f, -d * 0.5f), new Vector3((w - 6f) * 0.5f, h, t), earth);
            PrototypeSceneKit.Primitive(PrimitiveType.Cube, "Reducto_Sur_Este", world, c + new Vector3((w + 6f) * 0.25f, h * 0.5f, -d * 0.5f), new Vector3((w - 6f) * 0.5f, h, t), earth);

            var posts = new List<Transform>();
            for (int i = 0; i < MirafloresChapter.RedoubtDefenders; i++)
            {
                posts.Add(Marker("Defensor_Reducto_" + (i + 1), c + new Vector3(-12f + i * 5f, 0f, -d * 0.5f + 3f + (i % 2) * 4f), 180f));
            }

            guns = new KruppGun[MirafloresChapter.RedoubtGuns];
            Material dust = Mat("Polvo", new Color(0.78f, 0.7f, 0.58f));
            for (int i = 0; i < guns.Length; i++)
            {
                var gun = new GameObject("Pieza_del_Reducto_" + (i + 1)).transform;
                gun.SetPositionAndRotation(c + new Vector3(i == 0 ? -14f : 14f, 0f, d * 0.5f - 4f), Quaternion.Euler(0f, 180f, 0f));
                PrototypeSceneKit.Primitive(PrimitiveType.Cube, "Cureña", gun, new Vector3(0f, 0.55f, 0f), new Vector3(0.9f, 0.5f, 1.8f), Mat("Cureña", Bark));
                var barrel = new GameObject("Tubo").transform;
                barrel.SetParent(gun, false);
                barrel.localPosition = new Vector3(0f, 0.95f, 0f);
                GameObject tube = PrototypeSceneKit.Primitive(PrimitiveType.Cylinder, "Tubo_Pieza", barrel, new Vector3(0f, 0f, 0.6f), new Vector3(0.16f, 0.65f, 0.16f), Mat("Bronce", Bronze), keepCollider: false);
                tube.transform.localRotation = Quaternion.Euler(84f, 0f, 0f);
                KruppGun k = gun.gameObject.AddComponent<KruppGun>();
                k.Configure(barrel, new Vector3(0f, 0f, 20f), 45f, dust, Faction.Peru);
                guns[i] = k;
            }

            // Bandera sobre el parapeto norte (el pivote en la base: cae al rotarlo).
            flag = new GameObject("Bandera_del_Reducto").transform;
            flag.position = c + new Vector3(0f, h, d * 0.5f);
            PrototypeSceneKit.Primitive(PrimitiveType.Cylinder, "Asta", flag, new Vector3(0f, 3f, 0f), new Vector3(0.1f, 3f, 0.1f), Mat("Cureña", Bark), keepCollider: false);
            for (int i = 0; i < 3; i++)
            {
                Color color = i == 1 ? Color.white : new Color(0.78f, 0.1f, 0.12f);
                PrototypeSceneKit.Primitive(PrimitiveType.Cube, "Pano_" + (i + 1), flag, new Vector3(0.5f + i * 0.5f, 5.4f, 0f), new Vector3(0.5f, 1f, 0.03f),
                    Mat(i == 1 ? "Bandera_Blanco" : "Bandera_Rojo", color), keepCollider: false);
            }

            // Olivo partido por un cañonazo y el sitio contra el parapeto sur (por dentro), mirando al olivo.
            PrototypeSceneKit.Primitive(PrimitiveType.Cylinder, "Olivo_Partido", world, c + new Vector3(10f, 1f, 2f), new Vector3(0.5f, 1f, 0.5f), Mat("Corteza", Bark));
            GameObject split = PrototypeSceneKit.Primitive(PrimitiveType.Cylinder, "Olivo_Rama_Caida", world, c + new Vector3(11.5f, 0.4f, 3.5f), new Vector3(0.35f, 1.6f, 0.35f), Mat("Corteza", Bark), keepCollider: false);
            split.transform.rotation = Quaternion.Euler(0f, 30f, 80f);
            PrototypeSceneKit.Primitive(PrimitiveType.Sphere, "Olivo_Copa_Rota", world, c + new Vector3(12.5f, 0.8f, 4.5f), new Vector3(2.4f, 1.4f, 2.4f), Mat("Olivo", Olive), keepCollider: false);
            seat = new GameObject("Sitio_Contra_el_Parapeto").transform;
            seat.SetPositionAndRotation(c + new Vector3(8f, 0f, -d * 0.5f + t * 0.5f + 0.6f), Quaternion.Euler(0f, 0f, 0f));
            return posts;
        }

        /// <summary>El muchacho voluntario (inactivo hasta el momento del rostro del enemigo): no es un combatiente.</summary>
        private static GameObject CreateBoy()
        {
            var boy = new GameObject("Muchacho_Voluntario");
            PrototypeSceneKit.Primitive(PrimitiveType.Capsule, "Cuerpo", boy.transform, new Vector3(0f, 0.35f, 0f), new Vector3(0.36f, 0.35f, 0.36f), Mat("Levita", new Color(0.18f, 0.17f, 0.16f)), keepCollider: false);
            PrototypeSceneKit.Primitive(PrimitiveType.Sphere, "Cabeza", boy.transform, new Vector3(0f, 0.85f, 0f), Vector3.one * 0.2f, Mat("Piel", new Color(0.62f, 0.47f, 0.36f)), keepCollider: false);
            GameObject rifle = PrototypeSceneKit.Primitive(PrimitiveType.Cube, "Fusil_Descargado", boy.transform, new Vector3(0.1f, 0.5f, 0.15f), new Vector3(0.05f, 0.05f, 1.2f), Mat("Corteza", Bark), keepCollider: false);
            rifle.transform.localRotation = Quaternion.Euler(-70f, 0f, 0f);
            boy.SetActive(false);
            return boy;
        }
    }
}
