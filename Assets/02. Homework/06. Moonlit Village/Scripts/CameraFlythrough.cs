using System.Collections.Generic;
using UnityEngine;

namespace MoonlitVillage
{
    // Moves the camera along a list of waypoints when the scene is played.
    // Each segment eases in and out; the camera smoothly turns toward each look target.
    public class CameraFlythrough : MonoBehaviour
    {
        [System.Serializable]
        public class Waypoint
        {
            public Vector3 position;
            public Vector3 lookAt;
            public float travelSeconds = 8f;
            public float holdSeconds = 1f;
            public float fieldOfView = 55f;
        }

        public List<Waypoint> waypoints = new List<Waypoint>();
        public float startDelay = 1.5f;
        public bool loop = false;
        public bool playOnStart = true;

        Camera cam;
        int index;
        float t;
        bool holding;
        float holdT;
        bool running;

        void Start()
        {
            cam = GetComponent<Camera>();
            if (waypoints.Count > 0) Apply(waypoints[0], waypoints[0], 0f);
            running = playOnStart;
            t = -startDelay;
        }

        void Update()
        {
            if (!running || waypoints.Count < 2) return;
            if (holding)
            {
                holdT += Time.deltaTime;
                if (holdT >= waypoints[index].holdSeconds) { holding = false; holdT = 0f; }
                return;
            }
            var a = waypoints[index];
            var b = waypoints[(index + 1) % waypoints.Count];
            t += Time.deltaTime;
            float u = Mathf.Clamp01(t / Mathf.Max(0.01f, b.travelSeconds));
            float e = Mathf.SmoothStep(0f, 1f, u);
            Apply(a, b, e);
            if (u >= 1f)
            {
                index++;
                t = 0f;
                if (index >= waypoints.Count - 1)
                {
                    if (loop) index = 0; else { running = false; return; }
                }
                holding = waypoints[index].holdSeconds > 0f;
            }
        }

        void Apply(Waypoint a, Waypoint b, float e)
        {
            Vector3 pos = Vector3.Lerp(a.position, b.position, e);
            Vector3 look = Vector3.Lerp(a.lookAt, b.lookAt, e);
            transform.position = pos;
            Vector3 dir = look - pos;
            if (dir.sqrMagnitude > 0.0001f) transform.rotation = Quaternion.LookRotation(dir, Vector3.up);
            if (cam != null) cam.fieldOfView = Mathf.Lerp(a.fieldOfView, b.fieldOfView, e);
        }

        void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(1f, 0.85f, 0.3f, 0.9f);
            for (int i = 0; i < waypoints.Count; i++)
            {
                Gizmos.DrawSphere(waypoints[i].position, 0.4f);
                if (i + 1 < waypoints.Count) Gizmos.DrawLine(waypoints[i].position, waypoints[i + 1].position);
                Gizmos.DrawLine(waypoints[i].position, waypoints[i].lookAt);
            }
        }
    }
}
