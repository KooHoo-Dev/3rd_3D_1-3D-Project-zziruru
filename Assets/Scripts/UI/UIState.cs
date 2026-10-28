using System;
using System.Collections.Generic;
using UnityEngine;

namespace SesiVillage.UI
{
    /// <summary>
    /// 지금 UI(대화창·인벤토리·메뉴)가 하나라도 열려 있는지 관리한다. Core 씬 Systems에 하나만 둔다.
    /// 누가 열었는지(owner)를 모아 두어, 여러 UI가 겹쳐 열려도 마지막 하나가 닫힐 때만 잠금이 풀린다.
    /// 커서·주인공 입력·카메라·상호작용은 Changed를 듣고 스스로 잠그고 푼다.
    /// </summary>
    public class UIState : MonoBehaviour
    {
        public static UIState Instance { get; private set; }
        private readonly HashSet<object> owners = new HashSet<object>();

        /// <summary>UI가 하나라도 열려 있나</summary>
        public bool IsOpen => owners.Count > 0;

        /// <summary>열림 ↔ 닫힘이 바뀔 때만 (true = 처음 하나가 열림, false = 마지막 하나가 닫힘)</summary>
        public event Action<bool> Changed;
        /// <summary>마지막 UI가 닫힌 프레임 (그 프레임의 입력이 게임으로 새지 않게)</summary>
        public int ClosedFrame { get; private set; } = -1;
        
        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Debug.LogError("[UIState] 이미 있음 — 하나만 두세요", this);
                Destroy(this);
                return;
            }
            Instance = this;
        }
        
        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }
        
        /// <summary>UI를 열 때 부른다. owner = 연 쪽 (보통 this)</summary>
        public void Open(object owner)
        {
            bool wasOpen = IsOpen;
            if (owners.Add(owner) && !wasOpen)
                Changed?.Invoke(true);
        }

        /// <summary>UI를 닫을 때 부른다. Open 때와 같은 owner를 넘긴다</summary>
        public void Close(object owner)
        {
            if (owners.Remove(owner) && !IsOpen)
            {
                ClosedFrame = Time.frameCount;   // 닫힌 프레임을 기억
                Changed?.Invoke(false);
            }
        }
    }
}
