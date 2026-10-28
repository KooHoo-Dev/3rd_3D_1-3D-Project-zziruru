namespace SesiVillage.UI
{
    /// <summary>
    /// 받침에 따라 조사를 고른다: "떡국을" / "잡초를", "설빔이" / "고사반이".
    /// 한글 음절(가~힣)은 (글자 코드 - 0xAC00) % 28 이 0이면 받침 없음 (유니코드 한글 음절 = 초성×588 + 중성×28 + 종성).
    /// </summary>
    public static class KoreanJosa
    {
        /// <summary>마지막 글자에 받침이 있는가 (한글이 아니면 false)</summary>
        public static bool HasFinalConsonant(string word)
        {
            if (string.IsNullOrEmpty(word)) return false;
            char last = word[word.Length - 1];
            if (last < '가' || last > '힣') return false;
            return (last - '가') % 28 != 0;
        }

        /// <summary>을/를 — "떡국을", "잡초를"</summary>
        public static string EulReul(string word) => word + (HasFinalConsonant(word) ? "을" : "를");

        /// <summary>이/가 — "설빔이", "잡초가"</summary>
        public static string IGa(string word) => word + (HasFinalConsonant(word) ? "이" : "가");
    }
}
