using SesiVillage.UI;
using Unity.Cinemachine;
using UnityEngine;

namespace SesiVillage.Core
{
    /// <summary>
    /// 탐험 카메라(CM_Explore)를 다루는 창구.
    /// 플레이어를 순간이동시키는 곳(시작, 불러오기, 잠자기)에서 SnapBehind()를 불러
    /// 카메라를 플레이어 뒤로 즉시 옮긴다.
    /// </summary>
    
    [RequireComponent(typeof(CinemachineCamera))]
    [RequireComponent(typeof(CinemachineOrbitalFollow))]
    public class ExploreCamera : MonoBehaviour
    {
        private CinemachineCamera cinemachineCamera;
        private CinemachineOrbitalFollow orbitalFollow;
        // UI 열렸을 때 카메라 회전 막기용
        private CinemachineInputAxisController inputAxis;
        
        private void Awake()
        {
            cinemachineCamera = GetComponent<CinemachineCamera>();
            orbitalFollow = GetComponent<CinemachineOrbitalFollow>();
            
            inputAxis = GetComponent<CinemachineInputAxisController>();
        }
        
        private void Start()
        {
            if (UIState.Instance == null) return;
            UIState.Instance.Changed += OnUIChanged;
            OnUIChanged(UIState.Instance.IsOpen);
        }

        private void OnDestroy()
        {
            if (UIState.Instance != null) UIState.Instance.Changed -= OnUIChanged;
        }

        /// <summary>UI가 열리면 마우스로 카메라가 돌지 않게 한다</summary>
        private void OnUIChanged(bool uiOpen)
        {
            if (inputAxis != null) inputAxis.enabled = !uiOpen;
        }
        
        /// <summary>
        /// 카메라를 target의 뒤쪽으로 즉시 옮긴다 (부드럽게 날아가지 않고 순간이동).
        /// </summary>
        public void SnapBehind(Transform target)
        {
            // ① 가로 각도를 플레이어가 보는 방향(Y 회전)과 같게 → 카메라가 플레이어 등 뒤에 위치
            //    (Binding Mode가 World Space라서 이 값은 "세계 기준 각도")
            orbitalFollow.HorizontalAxis.Value = target.eulerAngles.y;

            // "이전 프레임 위치는 무시해라" → 원래 있던 곳(마을 중앙)에서 날아오지 않고 바로 그 자리로
            cinemachineCamera.PreviousStateIsValid = false;
        }
    }
}
