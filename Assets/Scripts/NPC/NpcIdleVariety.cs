using System;
using UnityEngine;
using Random = UnityEngine.Random;

namespace SesiVillage.NPC
{
    /// <summary>
    /// NPC 대기 동작 여러 개를 무작위로 번갈아 재생한다 (숨쉬기 → 둘러보기 → 편히 서기 …).
    /// Animator에는 상태만 두고 화살표는 없다 — 언제 무엇으로 넘어갈지는 이 스크립트가 정한다.
    /// 시작 동작과 첫 전환 시각도 NPC마다 무작위라서 마을 사람들이 동시에 움직이지 않는다.
    /// </summary>
    [RequireComponent(typeof(Animator))]
    public class NpcIdleVariety : MonoBehaviour
    {
        [Serializable]
        private struct IdleMotion
        {
            [Tooltip("Animator 창의 상태 이름")]
            public string stateName;
            [Tooltip("이 동작을 재생하는 시간 범위 (초). 반복이 꺼진 클립은 '클립 길이 - 전환 시간'보다 짧게")]
            public float minSeconds;
            public float maxSeconds;
        }

        [SerializeField] private IdleMotion[] motions =
        {
            new IdleMotion { stateName = "Idle_Breathing",   minSeconds = 3f,   maxSeconds = 4.5f },   // 클립 9.5초, 거의 멈춰 있어서 짧게
            new IdleMotion { stateName = "Idle_Look_Around", minSeconds = 6.5f, maxSeconds = 7f },     // 클립 7.5초
            new IdleMotion { stateName = "Idle_Relaxed",     minSeconds = 2.2f, maxSeconds = 2.4f },   // 클립 2.8초
        };
        [Tooltip("다음 동작으로 섞이며 넘어가는 시간 (초)")]
        [SerializeField] private float crossFadeSeconds = 0.4f;

        private Animator animator;
        private int current = -1;
        private float timeLeft;

        private void Start()
        {
            animator = GetComponent<Animator>();
            if (motions.Length == 0) { enabled = false; return; }

            // 첫 동작은 섞지 않고 바로 시작, 첫 전환 시각은 앞당겨서 NPC마다 어긋나게 한다
            PlayNext(crossFade: false);
            timeLeft *= Random.Range(0.2f, 1f);
        }

        private void Update()
        {
            timeLeft -= Time.deltaTime;
            if (timeLeft <= 0f) PlayNext(crossFade: true);
        }

        private void PlayNext(bool crossFade)
        {
            // 방금 한 동작은 빼고 고른다 (동작이 하나뿐이면 그것을 반복)
            int next = Random.Range(0, motions.Length);
            if (motions.Length > 1 && next == current)
                next = (next + Random.Range(1, motions.Length)) % motions.Length;
            current = next;

            var motion = motions[current];
            if (crossFade) animator.CrossFadeInFixedTime(motion.stateName, crossFadeSeconds);
            else animator.Play(motion.stateName, 0, 0f);
            timeLeft = Random.Range(motion.minSeconds, motion.maxSeconds);
        }
    }
}
