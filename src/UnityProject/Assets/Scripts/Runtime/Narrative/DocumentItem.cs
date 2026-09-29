using Pacifico.Core.Narrative;
using Pacifico.Data;
using Pacifico.Input;
using UnityEngine;

namespace Pacifico.Narrative
{
    /// <summary>
    /// Un documento que se puede examinar (ROADMAP 5.1): un coleccionable de «La Memoria Rota» o una imagen del
    /// Archivo Histórico (fotografía, plano). Al hacer clic sobre él se abre en el <see cref="DocumentViewer"/>.
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public sealed class DocumentItem : MonoBehaviour
    {
        [SerializeField] private CollectibleDataSO collectible;
        [Header("Sin coleccionable")]
        [SerializeField] private string title = string.Empty;
        [SerializeField] private string subtitle = string.Empty;
        [SerializeField] private Texture2D image;
        [SerializeField] private DocumentForm form = DocumentForm.Photograph;
        [SerializeField, TextArea(2, 8)] private string transcription = string.Empty;
        [SerializeField] private string reverseText = string.Empty;

        public void Configure(CollectibleDataSO data)
        {
            collectible = data;
        }

        public void Configure(string itemTitle, string itemSubtitle, Texture2D texture, DocumentForm itemForm, string text, string reverse)
        {
            collectible = null;
            title = itemTitle;
            subtitle = itemSubtitle;
            image = texture;
            form = itemForm;
            transcription = text;
            reverseText = reverse;
        }

        public DocumentSpec ToSpec()
        {
            if (collectible != null) return DocumentSpec.From(collectible);
            return new DocumentSpec
            {
                Title = title,
                Subtitle = subtitle,
                Facsimile = image,
                Form = form,
                Transcription = transcription,
                ReverseText = reverseText,
            };
        }
    }

    /// <summary>
    /// Mesa de trabajo del archivo: clic sobre un documento para examinarlo. Con <see cref="openOnStart"/> abre el
    /// primero al empezar (la verificación de ROADMAP 5.1 abre la carta de Grau).
    /// </summary>
    public sealed class DocumentDesk : MonoBehaviour
    {
        [SerializeField] private Camera viewCamera;
        [SerializeField] private DocumentViewer viewer;
        [SerializeField] private DocumentItem openOnStart;

        public void Configure(Camera cameraComponent, DocumentViewer documentViewer, DocumentItem first)
        {
            viewCamera = cameraComponent;
            viewer = documentViewer;
            openOnStart = first;
        }

        private void Start()
        {
            if (openOnStart != null && viewer != null) viewer.Open(openOnStart.ToSpec());
        }

        private void Update()
        {
            if (viewer == null || viewer.IsOpen || viewCamera == null) return;
            if (!GameInput.MousePressed(0)) return;
            Ray ray = viewCamera.ScreenPointToRay(GameInput.MousePosition);
            if (!Physics.Raycast(ray, out RaycastHit hit, 20f)) return;
            DocumentItem item = hit.collider.GetComponentInParent<DocumentItem>();
            if (item != null) viewer.Open(item.ToSpec());
        }

        private void OnGUI()
        {
            if (viewer == null || viewer.IsOpen) return;
            GUI.Label(new Rect(20f, 20f, 700f, 24f), "Haz clic en un documento para examinarlo.");
        }
    }
}
