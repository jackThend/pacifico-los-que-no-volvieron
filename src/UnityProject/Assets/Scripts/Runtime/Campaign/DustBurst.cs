using System;
using Pacifico.Core.Campaign;
using Pacifico.Infantry;
using UnityEngine;

namespace Pacifico.Campaign
{
    /// <summary>Tierra levantada por una granada: una esfera que crece y se desvanece.</summary>
    public sealed class DustBurst : MonoBehaviour
    {
        private float _age;
        private float _size;

        public static void Spawn(Vector3 position, Material material, float size)
        {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            go.name = "Polvo_Granada";
            Destroy(go.GetComponent<Collider>());
            if (material != null) go.GetComponent<Renderer>().sharedMaterial = material;
            go.transform.position = position;
            go.transform.localScale = Vector3.one * 0.1f;
            go.AddComponent<DustBurst>()._size = size;
            Audio.GameAudio.Play(Audio.Sfx.Explosion, position, Mathf.Clamp(size / 12f, 0.3f, 1f));
        }

        private void Update()
        {
            _age += Time.deltaTime;
            float t = _age / 1.2f;
            transform.localScale = Vector3.one * Mathf.Lerp(0.5f, _size, 1f - (1f - Mathf.Min(1f, t * 2f)) * (1f - Mathf.Min(1f, t * 2f)));
            transform.position += Vector3.up * Time.deltaTime * 1.5f;
            if (t >= 1f) Destroy(gameObject);
        }
    }
}
