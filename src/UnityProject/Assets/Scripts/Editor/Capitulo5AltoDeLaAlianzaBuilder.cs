using System;
using System.IO;
using Pacifico.Campaign;
using Pacifico.Core.Campaign;
using Pacifico.Core.Common;
using Pacifico.Core.Tactics;
using Pacifico.Core.Weapons;
using Pacifico.Data;
using Pacifico.Effects;
using Pacifico.Tactics;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using T = Pacifico.EditorTools.TacnaPrototypeBuilder;

namespace Pacifico.EditorTools
{
    /// <summary>
    /// Escena jugable del Capítulo 5, «El trueno de Intiorko» (ROADMAP 6.3): la meseta del Alto de la Alianza con la
    /// zanja y los parapetos de la línea aliada (el terreno del prototipo de Tacna), la reserva boliviana y los
    /// Colorados detrás de la trinchera, la batería Krupp chilena al sur, dos cañones aliados en la izquierda (que
    /// caerán en manos chilenas) y la retaguardia junto al depósito. Las oleadas chilenas las crea el director.
    /// Modo batch:
    /// <c>Unity -batchmode -quit -projectPath src/UnityProject -executeMethod Pacifico.EditorTools.Capitulo5AltoDeLaAlianzaBuilder.RunBatch</c>
    /// </summary>
    public static class Capitulo5AltoDeLaAlianzaBuilder
    {
        public const string ScenePath = ProjectPaths.Scenes + "/Capitulo5_Alto_de_la_Alianza.unity";

        private static readonly Color Bronze = new Color(0.35f, 0.3f, 0.22f);
        private static readonly Color Wood = new Color(0.42f, 0.32f, 0.2f);
        private static readonly Color ReserveCoat = new Color(0.3f, 0.32f, 0.36f);

        [MenuItem("Pacífico/Capítulos/Capítulo 5: Alto de la Alianza")]
        public static void Build()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            BuildScene();
            EditorUtility.DisplayDialog("Pacífico", "Escena creada en " + ScenePath +
                "\n\nClic o recuadro: seleccionar · clic derecho: mover o atacar · 1/2: línea o guerrilla · H: alto · " +
                "C: ¡a la carga! (cuando el mando la ordena) · WASD, Q/E y rueda: cámara.", "Aceptar");
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

        internal static void BuildScene()
        {
            HistoricalDataAssetGenerator.GenerateAll();
            WeaponDataSO remington = T.LoadWeapon(WeaponCatalog.RemingtonId);
            WeaponDataSO comblain = T.LoadWeapon(WeaponCatalog.ComblainId);
            WeaponDataSO winchester = T.LoadWeapon(WeaponCatalog.WinchesterId);

            string guion = File.ReadAllText(Path.Combine(HistoricalDataAssetGenerator.RepositoryRoot, GuionQuotes.ScriptFile));
            string chapter = GuionQuotes.Section(guion, AltoDeLaAlianzaChapter.ChapterHeading);
            AltoDeLaAlianzaChapter.Build(GuionQuotes.Extract(chapter, AltoDeLaAlianzaChapter.ChapterHeading)); // falla aquí si el guion cambió

            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            T.CreateEnvironment();
            RenderSettings.fogDensity = 0.0045f; // camanchaca densa al alba
            Terrain terrain = T.CreateTerrain();
            T.CreateDefences(terrain);
            T.CreateLogistics(terrain);
            new GameObject("NavMesh_Tiempo_de_Ejecucion").AddComponent<NavMeshRuntimeBaker>();
            CreateSmoke();

            // La niebla se levanta cuando el guion da paso al avance chileno.
            var climate = new GameObject("Clima_Camanchaca").AddComponent<BattlefieldClimate>();
            var climateSo = new SerializedObject(climate);
            climateSo.FindProperty("clearingSeconds").floatValue = AltoDeLaAlianzaChapter.FogSeconds + 20f;
            climateSo.FindProperty("morningFogDensity").floatValue = 0.0045f;
            climateSo.ApplyModifiedPropertiesWithoutUndo();

            Camera camera = T.CreateCamera();
            new GameObject("Mando_del_Jugador").AddComponent<RtsCommander>().ViewCamera = camera;

            // El jugador: tres compañías de Colorados y la reserva boliviana, detrás de la zanja (a unos 50 m de ella).
            Material red = PrototypeSceneKit.Material("Uniforme_Colorados", T.ColoradoRed);
            Material white = PrototypeSceneKit.Material("Correaje_Colorados", T.ColoradoTrim);
            Material grey = PrototypeSceneKit.Material("Uniforme_Reserva_Boliviana", ReserveCoat);
            SquadController[] player =
            {
                T.Squad(terrain, "Colorados · 1.ª Compañía", Faction.Bolivia, remington, 12, FormationType.Line, true, new Vector3(-40f, 0f, 140f), 180f, red, white),
                T.Squad(terrain, "Colorados · 2.ª Compañía", Faction.Bolivia, remington, 12, FormationType.Line, true, new Vector3(0f, 0f, 140f), 180f, red, white),
                T.Squad(terrain, "Colorados · Cazadores", Faction.Bolivia, remington, 8, FormationType.Skirmish, true, new Vector3(45f, 0f, 145f), 180f, red, white),
                T.Squad(terrain, "Reserva boliviana", Faction.Bolivia, remington, 12, FormationType.Line, true, new Vector3(20f, 0f, 175f), 180f, grey, white),
            };

            Material dust = PrototypeSceneKit.Material("Polvo", new Color(0.8f, 0.72f, 0.58f));
            SquadArtillery[] chileanBattery =
            {
                Gun(terrain, "Krupp_Chileno_1", Faction.Chile, new Vector3(-20f, 0f, -250f), 0f, dust),
                Gun(terrain, "Krupp_Chileno_2", Faction.Chile, new Vector3(20f, 0f, -250f), 0f, dust),
            };
            SquadArtillery[] leftGuns =
            {
                Gun(terrain, "Canon_Aliado_Izquierda_1", Faction.Bolivia, new Vector3(-150f, 0f, 70f), 180f, dust),
                Gun(terrain, "Canon_Aliado_Izquierda_2", Faction.Bolivia, new Vector3(-125f, 0f, 60f), 180f, dust),
            };

            Transform[] wave =
            {
                Marker(terrain, "Oleada_1", new Vector3(-90f, 0f, -210f), 0f), Marker(terrain, "Oleada_2", new Vector3(-30f, 0f, -215f), 0f),
                Marker(terrain, "Oleada_3", new Vector3(30f, 0f, -215f), 0f), Marker(terrain, "Oleada_4", new Vector3(90f, 0f, -210f), 0f),
            };
            Transform[] guards =
            {
                Marker(terrain, "Guardia_Canones_1", new Vector3(-150f, 0f, 55f), 0f), Marker(terrain, "Guardia_Canones_2", new Vector3(-122f, 0f, 45f), 0f),
            };
            Transform[] horse =
            {
                Marker(terrain, "Caballeria_1", new Vector3(-235f, 0f, 60f), 90f), Marker(terrain, "Caballeria_2", new Vector3(-235f, 0f, 130f), 90f),
            };
            Transform[] reserve =
            {
                Marker(terrain, "Reserva_1", new Vector3(-60f, 0f, -200f), 0f), Marker(terrain, "Reserva_2", new Vector3(0f, 0f, -200f), 0f),
                Marker(terrain, "Reserva_3", new Vector3(60f, 0f, -200f), 0f),
            };
            Transform rear = Marker(terrain, "Retaguardia", new Vector3(20f, 0f, 225f), 0f);

            var chile = new AltoDeLaAlianzaDirector.Uniform
            {
                coat = PrototypeSceneKit.Material("Uniforme_Chile", T.ChileBlue),
                trim = PrototypeSceneKit.Material("Quepis_Chile", T.ChileTrim),
            };
            var director = new GameObject("Director_Capitulo5").AddComponent<AltoDeLaAlianzaDirector>();
            director.Configure(chapter, player, comblain, winchester, chile, chileanBattery, leftGuns, wave, guards, horse, reserve, rear);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, ScenePath);
            PrototypeSceneKit.AddToBuildSettings(ScenePath);
            Debug.Log("[Pacífico] Capítulo 5 guardado en " + ScenePath);
        }

        private static float Ground(Terrain terrain, Vector3 p) => terrain.SampleHeight(p) + terrain.transform.position.y;

        private static Transform Marker(Terrain terrain, string name, Vector3 position, float heading)
        {
            var t = new GameObject("Puesto_" + name).transform;
            position.y = Ground(terrain, position);
            t.SetPositionAndRotation(position, Quaternion.Euler(0f, heading, 0f));
            return t;
        }

        /// <summary>Cañón de montaña: cureña, ruedas y tubo de bronce.</summary>
        private static SquadArtillery Gun(Terrain terrain, string name, Faction owner, Vector3 position, float heading, Material dust)
        {
            var root = new GameObject(name).transform;
            position.y = Ground(terrain, position);
            root.SetPositionAndRotation(position, Quaternion.Euler(0f, heading, 0f));
            PrototypeSceneKit.Primitive(PrimitiveType.Cube, "Cureña", root, new Vector3(0f, 0.55f, 0f), new Vector3(0.9f, 0.5f, 1.8f), PrototypeSceneKit.Material("Cureña", Wood));
            for (int w = -1; w <= 1; w += 2)
            {
                GameObject wheel = PrototypeSceneKit.Primitive(PrimitiveType.Cylinder, "Rueda", root, new Vector3(w * 0.6f, 0.5f, 0.2f), new Vector3(1f, 0.05f, 1f), PrototypeSceneKit.Material("Rueda", Wood), keepCollider: false);
                wheel.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
            }
            var barrelPivot = new GameObject("Tubo").transform;
            barrelPivot.SetParent(root, false);
            barrelPivot.localPosition = new Vector3(0f, 0.95f, 0f);
            GameObject barrel = PrototypeSceneKit.Primitive(PrimitiveType.Cylinder, "Tubo_Krupp", barrelPivot, new Vector3(0f, 0f, 0.6f), new Vector3(0.14f, 0.6f, 0.14f), PrototypeSceneKit.Material("Bronce", Bronze), keepCollider: false);
            barrel.transform.localRotation = Quaternion.Euler(84f, 0f, 0f);
            var gun = root.gameObject.AddComponent<SquadArtillery>();
            gun.Configure(owner, barrelPivot, dust);
            return gun;
        }

        private static void CreateSmoke()
        {
            var smoke = new GameObject("Humo_Polvora_Negra");
            smoke.AddComponent<ParticleSystem>();
            BlackPowderSmoke component = smoke.AddComponent<BlackPowderSmoke>();
            var so = new SerializedObject(component);
            so.FindProperty("maxPuffs").intValue = 160;
            so.FindProperty("muzzleFlash").boolValue = false;
            so.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
