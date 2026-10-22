namespace Pacifico
{
    /// <summary>
    /// Identificador entero de un objeto de Unity, válido mientras exista, para semillas aleatorias y claves de diccionario.
    /// Unity 6.4+ sustituye <c>GetInstanceID()</c> (obsoleto con error en 6.6) por <c>GetEntityId()</c>, de 64 bits;
    /// esta envoltura compila igual en 6000.0 y en las versiones nuevas.
    /// </summary>
    public static class UnityObjectIds
    {
        public static int StableId(this UnityEngine.Object obj)
        {
#if UNITY_6000_4_OR_NEWER
            return obj.GetEntityId().GetHashCode();
#else
            return obj.GetInstanceID();
#endif
        }
    }
}
