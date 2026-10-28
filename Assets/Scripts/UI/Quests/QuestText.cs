using System.Text;
using SesiVillage.Items;
using SesiVillage.Quests;

namespace SesiVillage.UI
{
    /// <summary>퀘스트 목록·HUD가 같은 문장을 쓰도록 모은 곳</summary>
    public static class QuestText
    {
        private const string Dim = "#9A8C78";     // 끝낸 목표
        private const string Need = "#C8B89A";    // 아직 모자람
        private const string Enough = "#8FD39A";  // 다 모음

        /// <summary>
        /// 목표 줄들. 끝낸 목표는 √(흐리게), 남은 목표는 · (✓는 한글 폰트·대체 폰트 둘 다 없어서 □로 나옴, √는 대체 폰트 LiberationSans에 있음)
        /// showCounts = true면 목표 뒤에 필요한 것을 붙인다 (2026-10-28 사용자 결정: 퀘스트 상세에서는 보임)
        ///   · 1개만 필요하면 이름만        "— 떡국"
        ///   · 2개 이상이면 가진 개수 / 총개수 "— 잡초 3 / 5"   (아이템이 없는 처치·대화 목표는 "3 / 5")
        /// </summary>
        public static string Objectives(QuestDefinition quest, QuestState state, bool showCounts)
        {
            var sb = new StringBuilder();
            var objectives = quest.Objectives;
            for (int i = 0; i < objectives.Count; i++)
            {
                if (i > 0) sb.Append('\n');
                var o = objectives[i];
                bool done = IsObjectiveDone(quest, state, i);
                string text = string.IsNullOrEmpty(o.Description) ? "(목표 설명 없음)" : o.Description;

                if (done) sb.Append($"<color={Dim}>√ {text}</color>");
                else
                {
                    sb.Append("· ").Append(text);
                    if (showCounts) sb.Append(Requirement(quest, i));
                }
            }
            if (state == QuestState.Completed) sb.Append("\n<color=#E8C36A>보상을 받으러 가자 (가방을 비우고)</color>");
            return sb.ToString();
        }

        /// <summary>이 목표를 끝냈나. 퀘스트를 보고했으면 전부 끝, 전달 목표는 건넸으면 끝 (모으기는 보고할 때 판단하므로 그 전엔 안 끝남)</summary>
        private static bool IsObjectiveDone(QuestDefinition quest, QuestState state, int index)
        {
            if (state == QuestState.Completed || state == QuestState.Rewarded) return true;
            var o = quest.Objectives[index];
            return o.Type == ObjectiveType.Deliver && QuestManager.Instance != null
                   && QuestManager.Instance.GetCount(quest.Id, index) >= o.RequiredCount;
        }

        /// <summary>"— 떡국" / "— 잡초 3 / 5" / "— 3 / 5"</summary>
        private static string Requirement(QuestDefinition quest, int index)
        {
            var o = quest.Objectives[index];
            string item = o.Item != null ? o.Item.DisplayName : null;

            if (o.RequiredCount <= 1)
                return item != null ? $"  <color={Need}>— {item}</color>" : "";

            int have = CurrentCount(quest, index);
            string color = have >= o.RequiredCount ? Enough : Need;
            string label = item != null ? item + " " : "";
            return $"  <color={color}>— {label}{have} / {o.RequiredCount}</color>";
        }

        /// <summary>
        /// 지금 개수. 아이템이 걸린 목표(모으기·전달)는 "가방에 가진 개수", 처치·대화는 QuestManager가 센 개수.
        /// 가진 개수는 필요한 수보다 많을 수 있다 (7 / 5 — 남는 건 플레이어 것)
        /// </summary>
        private static int CurrentCount(QuestDefinition quest, int index)
        {
            var o = quest.Objectives[index];
            if (o.Item != null && Inventory.Instance != null) return Inventory.Instance.Count(o.Item);
            return QuestManager.Instance != null ? QuestManager.Instance.GetCount(quest.Id, index) : 0;
        }
    }
}
