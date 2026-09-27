using System.Collections.Generic;

namespace Pacifico.Core.Common
{
    /// <summary>
    /// Acumula errores de validación de datos (armas, buques, coleccionables) con mensajes legibles
    /// que el editor de Unity muestra en consola y que las pruebas pueden inspeccionar.
    /// </summary>
    public sealed class ValidationResult
    {
        private readonly List<string> _errors = new List<string>();
        private readonly string _subject;

        public ValidationResult(string subject)
        {
            _subject = subject;
        }

        public bool IsValid => _errors.Count == 0;

        public IReadOnlyList<string> Errors => _errors;

        public void Require(bool condition, string message)
        {
            if (!condition) _errors.Add(_subject + ": " + message);
        }

        public void RequirePositive(float value, string field)
        {
            Require(value > 0f, field + " debe ser > 0 (valor: " + value + ")");
        }

        public void RequireNonNegative(float value, string field)
        {
            Require(value >= 0f, field + " no puede ser negativo (valor: " + value + ")");
        }

        public void RequireNotEmpty(string value, string field)
        {
            Require(!string.IsNullOrWhiteSpace(value), field + " no puede estar vacío");
        }

        public void Merge(ValidationResult other)
        {
            _errors.AddRange(other._errors);
        }

        public override string ToString() => IsValid ? _subject + ": OK" : string.Join("\n", _errors);
    }
}
