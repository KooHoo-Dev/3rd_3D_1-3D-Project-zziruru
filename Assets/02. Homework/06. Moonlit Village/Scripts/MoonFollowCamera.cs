using UnityEngine;

namespace MoonlitVillage
{
    [ExecuteAlways]
    public class MoonFollowCamera : MonoBehaviour
    {
        public Camera target;

        void LateUpdate() { Sync(); }

        public void Sync()
        {
            var cam = target != null ? target : Camera.main;
            if (cam == null) return;
            transform.position = cam.transform.position;
        }
    }
}
