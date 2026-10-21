using UnityEngine;

namespace MoonlitVillage
{
    [ExecuteAlways]
    [RequireComponent(typeof(ParticleSystem))]
    public class SnowFollowCamera : MonoBehaviour
    {
        public Camera target;
        public float heightAboveCamera = 14f;
        public float forwardOffset = 12f;

        public bool useZone = false;
        public Vector3 zoneCenter;
        public float zoneRadius = 30f;
        public float zoneFade = 15f;
        public float fullRate = 500f;

        ParticleSystem ps;

        void OnEnable() { ps = GetComponent<ParticleSystem>(); }

        void LateUpdate()
        {
            var cam = target != null ? target : Camera.main;
            if (cam == null) return;
            if (ps == null) ps = GetComponent<ParticleSystem>();

            Vector3 fwd = cam.transform.forward; fwd.y = 0f;
            if (fwd.sqrMagnitude < 0.001f) fwd = Vector3.forward;
            fwd.Normalize();
            transform.position = cam.transform.position + Vector3.up * heightAboveCamera + fwd * forwardOffset;
            transform.rotation = Quaternion.identity;

            var em = ps.emission;
            float rate = fullRate;
            if (useZone)
            {
                Vector3 p = cam.transform.position; p.y = zoneCenter.y;
                float d = Vector3.Distance(p, zoneCenter);
                rate = fullRate * Mathf.Clamp01(1f - (d - zoneRadius) / Mathf.Max(0.01f, zoneFade));
            }
            em.rateOverTime = rate;
        }
    }
}
