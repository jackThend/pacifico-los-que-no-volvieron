using UnityEngine;

namespace Pacifico.Infantry
{
    /// <summary>
    /// Silueta de la línea de tiro (prototipo de Pisagua): registra cada impacto, cuánto se desvió del centro y a
    /// qué distancia, y parpadea al recibirlo. Sirve para practicar con el alza: a 400 m con el alza en 200 se ve
    /// que la bala impacta baja.
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public sealed class ShootingTarget : MonoBehaviour, IBulletTarget
    {
        [SerializeField] private Color hitColor = new Color(0.85f, 0.2f, 0.1f);
        [SerializeField] private float flashSeconds = 0.35f;

        private Renderer _renderer;
        private MaterialPropertyBlock _block;
        private float _flashUntil;
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int ColorId = Shader.PropertyToID("_Color");

        public int Hits { get; private set; }

        /// <summary>Último impacto en cualquier blanco (para el HUD).</summary>
        public static ShootingTarget LastHitTarget { get; private set; }
        public float LastDistanceM { get; private set; }
        /// <summary>Desviación del último impacto respecto al centro del blanco (m): x = derecha, y = arriba.</summary>
        public Vector2 LastOffsetM { get; private set; }

        public void ReceiveBullet(BulletHit hit)
        {
            Hits++;
            Vector3 local = hit.Point - GetComponent<Collider>().bounds.center;
            LastOffsetM = new Vector2(Vector3.Dot(local, transform.right), local.y);
            LastDistanceM = hit.DistanceTravelled;
            LastHitTarget = this;
            _flashUntil = Time.time + flashSeconds;
            SetColor(hitColor, true);
        }

        private void Update()
        {
            if (_flashUntil > 0f && Time.time >= _flashUntil)
            {
                _flashUntil = 0f;
                SetColor(default, false);
            }
        }

        private void SetColor(Color color, bool on)
        {
            if (_renderer == null) _renderer = GetComponent<Renderer>();
            if (_renderer == null) return;
            if (_block == null) _block = new MaterialPropertyBlock();
            if (!on)
            {
                _renderer.SetPropertyBlock(null);
                return;
            }
            _block.SetColor(BaseColorId, color);
            _block.SetColor(ColorId, color);
            _renderer.SetPropertyBlock(_block);
        }

        /// <summary>Descripción corta del último impacto: «400 m · 32 cm bajo · 5 cm dcha.».</summary>
        public static string DescribeLastHit()
        {
            if (LastHitTarget == null) return string.Empty;
            Vector2 o = LastHitTarget.LastOffsetM;
            string vertical = Mathf.Abs(o.y) < 0.02f ? "al centro" : (Mathf.Abs(o.y) * 100f).ToString("0") + " cm " + (o.y > 0f ? "alto" : "bajo");
            string lateral = Mathf.Abs(o.x) < 0.02f ? string.Empty : " · " + (Mathf.Abs(o.x) * 100f).ToString("0") + " cm " + (o.x > 0f ? "dcha." : "izda.");
            return LastHitTarget.LastDistanceM.ToString("0") + " m · " + vertical + lateral;
        }
    }
}
