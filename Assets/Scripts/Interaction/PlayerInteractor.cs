using SesiVillage.UI;
using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace SesiVillage.Interaction
{
    /// <summary>
    /// 주인공 주변에서 상호작용할 대상 하나를 고르고(가깝고 앞에 있는 것), E를 누르면 실행한다.
    /// 고른 대상이 바뀌면 FocusChanged를 알린다 → "E 대화하기" 안내 UI가 듣는다.
    /// </summary>
    public class PlayerInteractor : MonoBehaviour
    {
        [Tooltip("이 거리 안의 대상만 찾음 (m)")]
        [SerializeField] private float range = 2.5f;
        [Tooltip("상호작용 대상 레이어 (Interactable)")]
        [SerializeField] private LayerMask interactableLayers;
        [Tooltip("앞쪽 판정: 1 = 정면만, 0 = 옆까지, -1 = 뒤까지 전부")]
        [Range(-1f, 1f)]
        [SerializeField] private float minFacingDot = 0.2f;

        private readonly Collider[] hits = new Collider[16];
        private InputAction interactAction;
        
        /// <summary>지금 고른 대상 (없으면 null)</summary>
        public IInteractable Current { get; private set; }

        /// <summary>고른 대상이 바뀌었을 때 (새 대상, 없으면 null)</summary>
        public event Action<IInteractable> FocusChanged;
        
        private void Awake()
        {
            // Project Settings → Input System Package에 등록된 프로젝트 공용 입력에서 Player/Interact(E)를 찾는다
            interactAction = InputSystem.actions.FindAction("Player/Interact", throwIfNotFound: true);
        }
        
        private void Update()
        {
            // UI가 열려 있으면 고르지도 누르지도 않는다 (E는 대화창의 "다음 대사"로 쓰임)
            if (UIState.Instance != null && UIState.Instance.IsOpen)
            {
                if (Current != null) { Current = null; FocusChanged?.Invoke(null); }   // 안내 숨기기
                return;
            }

            var best = FindBest();
            if (best != Current)
            {
                Current = best;
                FocusChanged?.Invoke(Current);
                Debug.Log($"[PlayerInteractor] 대상: {(Current as Component)?.name ?? "없음"}");   // 확인용, 2단계에서 지움
            }

            if (Current != null && interactAction.WasPressedThisFrame())
                Current.Interact(gameObject);
        }
        
        /// <summary>범위 안에서 "앞에 있을수록, 가까울수록" 점수가 높은 대상 하나</summary>
        private IInteractable FindBest()
        {
            Vector3 origin = transform.position;
            int count = Physics.OverlapSphereNonAlloc(origin, range, hits, interactableLayers, QueryTriggerInteraction.Collide);

            IInteractable best = null;
            float bestScore = float.MinValue;
            for (int i = 0; i < count; i++)
            {
                // 콜라이더가 자식에 있어도 찾도록 부모 방향으로
                var candidate = hits[i].GetComponentInParent<IInteractable>();
                if (candidate == null || !candidate.CanInteract) continue;

                // 높이 차이는 무시하고 바닥 평면에서 방향과 거리를 잰다
                Vector3 toTarget = hits[i].transform.position - origin;
                toTarget.y = 0f;
                float distance = toTarget.magnitude;
                // distance 가 거의 0이면 0으로 나누는 것 방지 하기 위해 정면에 있는 걸(1f)로 치자 
                float facing = distance > 0.01f ? Vector3.Dot(transform.forward, toTarget / distance) : 1f;
                if (facing < minFacingDot) continue;   // 등 뒤에 있는 것은 제외

                float score = facing - distance / range;
                if (score > bestScore)
                {
                    bestScore = score;
                    best = candidate;
                }
            }
            return best;
        }
        
        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(0.2f, 0.8f, 1f, 0.5f);
            Gizmos.DrawWireSphere(transform.position, range);
        }
    }
}
