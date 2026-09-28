using Pacifico.Core;
using Pacifico.Core.Collectibles;
using UnityEngine;

namespace Pacifico.Runtime.Data
{
    /// <summary>
    /// Coleccionable de "La Memoria Rota": carta, fotografía o documento con
    /// su transcripción, facsímil 3D y narración. Los textos se importan desde
    /// Archivo_Historico (menú Pacífico/Datos/Importar cartas históricas);
    /// las referencias a textura, modelo y audio se asignan en el editor.
    /// </summary>
    [CreateAssetMenu(fileName = "Collectible_", menuName = "Pacífico/Datos/Coleccionable", order = 2)]
    public sealed class CollectibleDataSO : ScriptableObject
    {
        [Header("Identidad")]
        [SerializeField] private string id;
        [SerializeField] private string title;
        [SerializeField] private CollectibleType collectibleType;
        [SerializeField] private Faction faction;
        [SerializeField, Range(1, 8)] private int chapter = 1;

        [Header("Documento")]
        [SerializeField] private string sender;
        [SerializeField] private string recipient;
        [SerializeField] private string location;
        [SerializeField] private string dateText;
        [SerializeField] private int year;
        [SerializeField, TextArea(8, 30)] private string transcription;
        [SerializeField, TextArea] private string context;
        [SerializeField, TextArea] private string sourceReference;
        [SerializeField, TextArea] private string[] gameNotes = new string[0];

        [Header("Origen en el repositorio")]
        [SerializeField] private string sourceDocumentPath;
        [SerializeField] private string facsimileImagePath;

        [Header("Presentación")]
        [SerializeField, Tooltip("Imagen del manuscrito o daguerrotipo para el visor.")] private Texture2D facsimileTexture;
        [SerializeField, Tooltip("Modelo 3D del objeto (carta doblada, relicario, libreta).")] private GameObject facsimileModel;
        [SerializeField, Tooltip("Voz en off para [Escuchar Carta].")] private AudioClip narrationClip;
        [SerializeField] private string narrationKey;

        public string Id => id;
        public string Title => title;
        public string Transcription => transcription;
        public Texture2D FacsimileTexture => facsimileTexture;
        public GameObject FacsimileModel => facsimileModel;
        public AudioClip NarrationClip => narrationClip;

        public CollectibleSpec ToSpec()
        {
            return new CollectibleSpec(id, title, collectibleType, faction, sender, recipient, location,
                dateText, year, chapter, transcription, context, sourceReference, sourceDocumentPath,
                facsimileImagePath, narrationKey, (string[])gameNotes.Clone());
        }

        /// <summary>Copia los datos textuales; conserva textura, modelo y audio ya asignados.</summary>
        public void ApplySpec(CollectibleSpec spec)
        {
            id = spec.Id;
            title = spec.Title;
            collectibleType = spec.Type;
            faction = spec.Faction;
            chapter = spec.Chapter;
            sender = spec.Sender;
            recipient = spec.Recipient;
            location = spec.Location;
            dateText = spec.DateText;
            year = spec.Year;
            transcription = spec.Transcription;
            context = spec.Context;
            sourceReference = spec.SourceReference;
            gameNotes = new string[spec.GameNotes.Count];
            for (var i = 0; i < gameNotes.Length; i++) gameNotes[i] = spec.GameNotes[i];
            sourceDocumentPath = spec.SourceDocumentPath;
            facsimileImagePath = spec.FacsimileImagePath;
            narrationKey = spec.NarrationKey;
        }

        private void OnValidate()
        {
            foreach (var error in CollectibleValidator.Validate(ToSpec()))
            {
                Debug.LogWarning($"[CollectibleDataSO] {name}: {error}", this);
            }
        }
    }
}
