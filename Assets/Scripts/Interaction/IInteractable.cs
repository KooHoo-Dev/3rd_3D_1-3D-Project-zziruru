using UnityEngine;

namespace SesiVillage.Interaction
{
    /// <summary>
    /// E 키로 상호작용할 수 있는 것 (NPC, 이불, 문…).
    /// PlayerInteractor는 상대가 무엇인지 모르고 이 약속만 보고 다룬다.
    /// </summary>
    public interface IInteractable
    {
        /// <summary>안내 문구에 쓸 동사 (예: "대화하기", "잠자기") → "E 대화하기"</summary>
        string Prompt { get; }

        /// <summary>지금 상호작용할 수 있는가 (예: 이미 대화 중이면 false)</summary>
        bool CanInteract { get; }

        /// <summary>E를 눌렀을 때. interactor = 누른 쪽(주인공)</summary>
        void Interact(GameObject interactor);
    }
}
