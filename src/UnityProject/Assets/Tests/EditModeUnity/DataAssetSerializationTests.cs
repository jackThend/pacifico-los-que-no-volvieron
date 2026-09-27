using System.Collections;
using System.Reflection;
using NUnit.Framework;
using Pacifico.Core.Naval;
using Pacifico.Core.Weapons;
using Pacifico.Data;
using UnityEngine;

namespace Pacifico.Tests.Unity
{
    /// <summary>
    /// ROADMAP 1.1/1.2: los ScriptableObjects de datos se instancian, se serializan con el
    /// serializador de Unity y vuelven a producir exactamente la especificación del catálogo.
    /// </summary>
    public class DataAssetSerializationTests
    {
        [Test]
        public void WeaponDataSO_IdaYVueltaPorElSerializadorDeUnity_ConservaLosDatos()
        {
            foreach (var spec in WeaponCatalog.All())
            {
                var asset = ScriptableObject.CreateInstance<WeaponDataSO>();
                var restored = ScriptableObject.CreateInstance<WeaponDataSO>();
                try
                {
                    asset.CopyFrom(spec);
                    JsonUtility.FromJsonOverwrite(JsonUtility.ToJson(asset), restored);

                    AssertSameProperties(spec, restored.ToSpec(), spec.Id);
                    Assert.That(restored.Validate().IsValid, Is.True, restored.Validate().ToString());
                }
                finally
                {
                    Object.DestroyImmediate(asset);
                    Object.DestroyImmediate(restored);
                }
            }
        }

        [Test]
        public void ShipDataSO_IdaYVueltaPorElSerializadorDeUnity_ConservaLosDatos()
        {
            foreach (var spec in ShipCatalog.All())
            {
                var asset = ScriptableObject.CreateInstance<ShipDataSO>();
                var restored = ScriptableObject.CreateInstance<ShipDataSO>();
                try
                {
                    asset.CopyFrom(spec);
                    JsonUtility.FromJsonOverwrite(JsonUtility.ToJson(asset), restored);

                    ShipSpec roundTrip = restored.ToSpec();
                    AssertSameProperties(spec, roundTrip, spec.Id);
                    Assert.That(roundTrip.Guns, Has.Count.EqualTo(spec.Guns.Count), spec.Id);
                    for (int i = 0; i < spec.Guns.Count; i++)
                    {
                        AssertSameProperties(spec.Guns[i], roundTrip.Guns[i], spec.Id + ".Guns[" + i + "]");
                    }
                    Assert.That(roundTrip.Turret == null, Is.EqualTo(spec.Turret == null), spec.Id + ".Turret");
                    Assert.That(restored.Validate().IsValid, Is.True, restored.Validate().ToString());
                }
                finally
                {
                    Object.DestroyImmediate(asset);
                    Object.DestroyImmediate(restored);
                }
            }
        }

        /// <summary>Compara propiedades públicas escalares y colecciones (una capa de profundidad).</summary>
        internal static void AssertSameProperties(object expected, object actual, string context)
        {
            foreach (PropertyInfo property in expected.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance))
            {
                if (!property.CanRead || property.GetIndexParameters().Length > 0) continue;
                object a = property.GetValue(expected);
                object b = property.GetValue(actual);
                string where = context + "." + property.Name;

                if (a is string || a == null || a.GetType().IsPrimitive || a.GetType().IsEnum)
                {
                    Assert.That(b, Is.EqualTo(a), where);
                }
                else if (a is IEnumerable enumerable)
                {
                    // Las listas de objetos (p. ej. Guns) se comparan elemento a elemento en cada prueba.
                    if (IsListOfScalars(enumerable)) Assert.That((IEnumerable)b, Is.EqualTo(enumerable), where);
                }
                else
                {
                    // Objeto anidado (p. ej. HistoricalSource): compara sus propiedades.
                    AssertSameProperties(a, b, where);
                }
            }
        }

        private static bool IsListOfScalars(IEnumerable items)
        {
            foreach (object item in items)
            {
                if (item == null) continue;
                System.Type t = item.GetType();
                return t.IsPrimitive || t.IsEnum || item is string;
            }
            return true;
        }
    }
}
