using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using Pacifico.Core.Collectibles;
using Pacifico.Core.Ships;
using Pacifico.Core.Weapons;
using Pacifico.Runtime.Data;
using UnityEngine;

namespace Pacifico.Tests
{
    /// <summary>
    /// Comprueba que los ScriptableObjects de datos se instancian, conservan
    /// los datos del catálogo y solo usan campos que Unity sabe serializar.
    /// </summary>
    public class DataAssetSerializationTests
    {
        [TestCase(typeof(ShipDataSO))]
        [TestCase(typeof(WeaponDataSO))]
        [TestCase(typeof(CollectibleDataSO))]
        public void CamposSonSerializablesPorUnity(Type assetType)
        {
            var problems = new List<string>();
            CheckSerializableFields(assetType, problems, 0);
            CollectionAssert.IsEmpty(problems, string.Join("\n", problems));
        }

        [Test]
        public void ShipDataSO_ConservaLosDatosDelCatalogo()
        {
            foreach (var original in HistoricalShips.All)
            {
                var asset = ScriptableObject.CreateInstance<ShipDataSO>();
                asset.ApplySpec(original);
                var copy = asset.ToSpec();

                Assert.AreEqual(original.Id, copy.Id);
                Assert.AreEqual(original.DisplayName, copy.DisplayName);
                Assert.AreEqual(original.Faction, copy.Faction);
                Assert.AreEqual(original.Type, copy.Type);
                Assert.AreEqual(original.Hull, copy.Hull);
                Assert.AreEqual(original.DisplacementTons, copy.DisplacementTons);
                Assert.AreEqual(original.MaxSpeedKnots, copy.MaxSpeedKnots);
                Assert.AreEqual(original.Crew, copy.Crew);
                Assert.AreEqual(original.HasRam, copy.HasRam);
                Assert.AreEqual(original.HullIntegrity, copy.HullIntegrity);
                Assert.AreEqual(original.HistoricalNote, copy.HistoricalNote);
                CollectionAssert.AreEqual(
                    original.Armor.Select(a => (a.zone, a.thicknessInches)).ToList(),
                    copy.Armor.Select(a => (a.zone, a.thicknessInches)).ToList());
                CollectionAssert.AreEqual(
                    original.Guns.Select(g => (g.gunName, g.count, g.projectileLbs, g.caliberInches, g.muzzleVelocityMs, g.mount, g.reloadSeconds)).ToList(),
                    copy.Guns.Select(g => (g.gunName, g.count, g.projectileLbs, g.caliberInches, g.muzzleVelocityMs, g.mount, g.reloadSeconds)).ToList());
                Assert.AreEqual(original.Turret == null, copy.Turret == null, original.Id);
                if (original.Turret != null)
                {
                    Assert.AreEqual(original.Turret.traverseDegreesPerSecond, copy.Turret.traverseDegreesPerSecond);
                    Assert.AreEqual(original.Turret.maxElevationDegrees, copy.Turret.maxElevationDegrees);
                    Assert.AreEqual(original.Turret.muzzleVelocityMs, copy.Turret.muzzleVelocityMs);
                    CollectionAssert.AreEqual(
                        original.Turret.blindSectors.Select(b => (b.centerDegrees, b.halfWidthDegrees, b.reason)).ToList(),
                        copy.Turret.blindSectors.Select(b => (b.centerDegrees, b.halfWidthDegrees, b.reason)).ToList());
                    Assert.AreNotSame(original.Turret, copy.Turret);
                }
                CollectionAssert.IsEmpty(ShipValidator.Validate(copy), original.Id);
            }
        }

        [Test]
        public void ShipDataSO_NoComparteInstanciasConElCatalogo()
        {
            var asset = ScriptableObject.CreateInstance<ShipDataSO>();
            asset.ApplySpec(HistoricalShips.Huascar);
            asset.ToSpec().Armor[0].thicknessInches = 99f;
            asset.ToSpec().Guns[0].count = 99;

            Assert.AreEqual(4.5f, HistoricalShips.Huascar.ArmorInches(ArmorZone.BeltMidship), 1e-4f);
            Assert.AreEqual(2, HistoricalShips.Huascar.Guns[0].count);
            Assert.AreEqual(4.5f, asset.ToSpec().ArmorInches(ArmorZone.BeltMidship), 1e-4f);
        }

        [Test]
        public void WeaponDataSO_ConservaLosDatosDelCatalogo()
        {
            foreach (var original in HistoricalWeapons.All)
            {
                var asset = ScriptableObject.CreateInstance<WeaponDataSO>();
                asset.ApplySpec(original);
                var copy = asset.ToSpec();

                Assert.AreEqual(original.Id, copy.Id);
                Assert.AreEqual(original.ReloadSeconds, copy.ReloadSeconds);
                Assert.AreEqual(original.Damage, copy.Damage);
                Assert.AreEqual(original.SpreadDegrees, copy.SpreadDegrees);
                CollectionAssert.AreEqual(original.Factions, copy.Factions);
                CollectionAssert.IsEmpty(WeaponValidator.Validate(copy), original.Id);
            }
        }

        [Test]
        public void CollectibleDataSO_ConservaLosDatosImportados()
        {
            var original = new CollectibleSpec("c", "Carta", CollectibleType.Letter, Pacifico.Core.Faction.Peru,
                "Grau", "Carmela", "Pisagua", "2 de junio de 1879", 1879, 1, "Texto\n\nFin", "Contexto",
                "Fuente", "doc.md", "img.jpg", "vo_c", new[] { "nota 1", "nota 2" });
            var asset = ScriptableObject.CreateInstance<CollectibleDataSO>();
            asset.ApplySpec(original);
            var copy = asset.ToSpec();

            Assert.AreEqual(original.Id, copy.Id);
            Assert.AreEqual(original.Title, copy.Title);
            Assert.AreEqual(original.Type, copy.Type);
            Assert.AreEqual(original.Faction, copy.Faction);
            Assert.AreEqual(original.Sender, copy.Sender);
            Assert.AreEqual(original.Recipient, copy.Recipient);
            Assert.AreEqual(original.Location, copy.Location);
            Assert.AreEqual(original.DateText, copy.DateText);
            Assert.AreEqual(original.Year, copy.Year);
            Assert.AreEqual(original.Chapter, copy.Chapter);
            Assert.AreEqual(original.Transcription, copy.Transcription);
            Assert.AreEqual(original.Context, copy.Context);
            Assert.AreEqual(original.SourceReference, copy.SourceReference);
            Assert.AreEqual(original.SourceDocumentPath, copy.SourceDocumentPath);
            Assert.AreEqual(original.FacsimileImagePath, copy.FacsimileImagePath);
            Assert.AreEqual(original.NarrationKey, copy.NarrationKey);
            CollectionAssert.AreEqual(original.GameNotes, copy.GameNotes);
        }

        // Reglas del serializador de Unity: campos de instancia públicos o con
        // [SerializeField], no readonly, de tipo primitivo, enum, string,
        // referencia a UnityEngine.Object o clase/struct [Serializable]
        // (recursivo), o array de éstos.
        private static void CheckSerializableFields(Type type, List<string> problems, int depth)
        {
            if (depth > 4) return;
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;
            foreach (var field in type.GetFields(flags))
            {
                if (field.Name.Contains("k__BackingField")) continue;
                var serialized = field.IsPublic || field.GetCustomAttribute<SerializeField>() != null;
                if (!serialized)
                {
                    problems.Add($"{type.Name}.{field.Name}: campo privado sin [SerializeField] (no se guardaría).");
                    continue;
                }
                if (field.IsInitOnly) problems.Add($"{type.Name}.{field.Name}: readonly no es serializable.");

                var fieldType = field.FieldType.IsArray ? field.FieldType.GetElementType() : field.FieldType;
                if (fieldType.IsPrimitive || fieldType.IsEnum || fieldType == typeof(string)) continue;
                // Referencias a assets (Texture2D, AudioClip, GameObject...) las serializa Unity por GUID.
                if (typeof(UnityEngine.Object).IsAssignableFrom(fieldType)) continue;
                if (fieldType.IsGenericType || fieldType.IsInterface || fieldType.IsAbstract)
                {
                    problems.Add($"{type.Name}.{field.Name}: tipo {fieldType.Name} no serializable.");
                    continue;
                }
                if (!fieldType.IsDefined(typeof(SerializableAttribute), false))
                {
                    problems.Add($"{type.Name}.{field.Name}: {fieldType.Name} no está marcado [Serializable].");
                    continue;
                }
                CheckSerializableFields(fieldType, problems, depth + 1);
            }
        }
    }
}
