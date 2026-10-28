using System;
using System.Collections.Generic;

namespace SesiVillage.UI
{
    /// <summary>알림 한 건의 요청. key가 같으면 (잠깐 사이에 다시 오면) 한 장으로 합쳐진다</summary>
    public class ToastRequest
    {
        public readonly string key;      // 합치기 기준 (예: "get:weed" — 같은 아이템을 또 얻으면 ×2로 합침)
        public readonly string message;  // "잡초를 얻었습니다"
        public readonly int amount;      // 개수 (1이면 개수 표시 안 함)
        public readonly UnityEngine.Sprite icon;

        public ToastRequest(string key, string message, int amount = 1, UnityEngine.Sprite icon = null)
        {
            this.key = key; this.message = message; this.amount = amount; this.icon = icon;
        }
    }

    /// <summary>화면에 떠 있거나 기다리는 알림 한 장 (큐가 관리하는 상태)</summary>
    public class Toast
    {
        public int uid;
        public ToastRequest req;
        public int amount;
        public float duration;      // 초
        public float remaining;     // 초
        public float lastMergeAt;   // 마지막으로 합쳐진 시각 — 합치기 창 판정

        public float Remaining01 => duration > 0f ? Math.Max(0f, remaining / duration) : 0f;
    }

    [Serializable]
    public class QueueConfig
    {
        [UnityEngine.Tooltip("한 번에 보이는 최대 장수")]
        public int maxVisible = 3;
        [UnityEngine.Tooltip("한 장이 떠 있는 시간 (초)")]
        public float displaySeconds = 3f;
        [UnityEngine.Tooltip("이 시간 안에 같은 key가 또 오면 한 장으로 합침 (0 = 합치지 않음)")]
        public float mergeWindowSeconds = 1.5f;
        [UnityEngine.Tooltip("여러 장이 한꺼번에 와도 이 간격으로 한 장씩 올림 (슉, 슉). 0 = 한꺼번에")]
        public float showInterval = 0.25f;
    }

    /// <summary>
    /// 알림 큐 — 화면(UGUI)과 분리된 순수 C# 로직 (MonoBehaviour 아님). 수업 UI 갤러리 NotificationQueue를 줄인 것.
    ///   Enqueue(req)        알림 1건. 같은 key가 떠 있거나 기다리는 중이면 개수만 합치고, 아니면 대기열 끝에 넣는다 (FIFO)
    ///   Tick(dt, frozen)    시간 진행. 표시 시간이 끝난 장을 내리고, 빈 자리에 대기열 앞에서부터 한 장씩 간격을 두고 올린다
    /// 뷰(ToastStackView)는 OnShow / OnUpdate / OnHide / OnChanged만 듣고 그린다. 시간 계산은 뷰에서 하지 않는다.
    /// (갤러리 원본의 희귀도·우선순위·밀어내기는 우리 게임에 필요 없어서 뺐다)
    /// </summary>
    public class NotificationQueue
    {
        public readonly List<Toast> active = new List<Toast>();    // 지금 보이는 것
        public readonly List<Toast> pending = new List<Toast>();   // 자리가 나기를 기다리는 것 (들어온 순서)

        public event Action<Toast> OnShow;     // 새 장이 화면에 올라감
        public event Action<Toast> OnUpdate;   // 떠 있는 장에 개수가 합쳐짐
        public event Action<Toast> OnHide;     // 시간이 다 되어 내려감
        public event Action OnChanged;         // 무엇이든 바뀜 ("+N 대기" 갱신용)

        public QueueConfig config;
        private float now;
        private int nextUid = 1;
        private float lastShowAt = float.NegativeInfinity;   // 마지막으로 한 장을 올린 시각

        public NotificationQueue(QueueConfig config) { this.config = config; }

        public void Enqueue(ToastRequest req)
        {
            if (config.mergeWindowSeconds > 0f)
            {
                // (1) 떠 있는 같은 key — 합치기 창 안이면 개수만 올리고 표시 시간을 다시 채운다
                var live = active.Find(t => t.req.key == req.key && now - t.lastMergeAt <= config.mergeWindowSeconds);
                if (live != null)
                {
                    live.amount += req.amount;
                    live.remaining = live.duration;
                    live.lastMergeAt = now;
                    OnUpdate?.Invoke(live);
                    OnChanged?.Invoke();
                    return;
                }
                // (2) 기다리는 같은 key — 아직 안 보였으니 개수만 합친다
                var waiting = pending.Find(t => t.req.key == req.key);
                if (waiting != null)
                {
                    waiting.amount += req.amount;
                    waiting.lastMergeAt = now;
                    OnChanged?.Invoke();
                    return;
                }
            }
            // (3) 새 요청 — 대기열 끝에 (보이는 건 다음 Tick에서)
            float d = config.displaySeconds;
            pending.Add(new Toast { uid = nextUid++, req = req, amount = req.amount, duration = d, remaining = d, lastMergeAt = now });
            OnChanged?.Invoke();
        }

        /// <param name="dt">초. 일시정지 중 멈추려면 Time.deltaTime, 계속 흐르게 하려면 Time.unscaledDeltaTime</param>
        /// <param name="frozen">true면 표시 시간이 줄지 않는다 (예: 대화 중에는 알림을 붙잡아 둠)</param>
        public void Tick(float dt, bool frozen)
        {
            now += dt;
            if (!frozen)
            {
                // 뒤에서부터 지운다 → 지워도 아직 안 본 칸의 번호가 밀리지 않음
                for (int i = active.Count - 1; i >= 0; i--)
                {
                    var t = active[i];
                    t.remaining -= dt;
                    if (t.remaining > 0f) continue;
                    active.RemoveAt(i);
                    OnHide?.Invoke(t);
                    OnChanged?.Invoke();
                }
            }
            // 빈 자리를 대기열 앞에서부터 채운다. 한 Tick에 다 올리지 않고 showInterval마다 한 장씩
            // (같은 프레임에 3장이 와도 0초, 0.25초, 0.5초에 차례로 들어옴 → 눈이 하나씩 따라 읽을 수 있음)
            while (pending.Count > 0 && active.Count < config.maxVisible && now - lastShowAt >= config.showInterval)
            {
                lastShowAt = now;
                var t = pending[0];
                pending.RemoveAt(0);
                // 남은 시간은 다시 채울 필요 없음: 기다리는 동안 깎이지 않고, 화면에 나온 카드가 줄로 돌아오는 일(밀어내기)도 없음
                t.lastMergeAt = now;   // 합치기 창(1.5초)은 화면에 나온 순간부터 셈
                active.Add(t);
                OnShow?.Invoke(t);
                OnChanged?.Invoke();
            }
        }

        public void Clear()
        {
            foreach (var t in active) OnHide?.Invoke(t);
            active.Clear();
            pending.Clear();
            OnChanged?.Invoke();
        }
    }
}
