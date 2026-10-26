using System.Collections.Generic;
using UnityEngine;

namespace SesiVillage.Items
{
    /// <summary>
    /// 모든 아이템 정의 목록. 게임 중에는 ID로 바로 찾을 수 있게 Dictionary로 바꿔서 쓴다.
    /// </summary>
    [CreateAssetMenu(fileName = "ItemDatabase", menuName = "SesiVillage/Item Database")]
    public class ItemDatabase : ScriptableObject
    {
        [SerializeField] private List<ItemDefinition> items = new List<ItemDefinition>();
        
        // 게임 중에 만들어 쓰는 찾기용 표 (저장하지 않음)
        [System.NonSerialized] private Dictionary<string, ItemDefinition> byId;
        
        public IReadOnlyList<ItemDefinition> All => items;
        
        /// <summary>ID로 찾기. 없으면 false (없을 수도 있는 경우에 씀)</summary>
        public bool TryGet(string id, out ItemDefinition item)
        {
            if (byId == null) Build();
            return byId.TryGetValue(id, out item);
        }
        
        /// <summary>ID로 찾기. 반드시 있어야 하는 경우에 씀 → 없으면 에러</summary>
        public ItemDefinition Get(string id)
        {
            if (TryGet(id, out var item)) return item;
            Debug.LogError($"[ItemDatabase] 없는 아이템 ID: '{id}'", this);
            return null;
        }
        
        private void Build()
        {
            byId = new Dictionary<string, ItemDefinition>(items.Count);
            foreach (var item in items)
            {
                if (item == null) continue;
                if (byId.ContainsKey(item.Id))
                {
                    Debug.LogError($"[ItemDatabase] ID 중복: '{item.Id}' ({byId[item.Id].name}, {item.name})", item);
                    continue;
                }
                byId.Add(item.Id, item);
            }
        }
        
#if UNITY_EDITOR
        // 목록을 Inspector에서 바꾸면 다음에 찾을 때 표를 다시 만든다
        private void OnValidate() => byId = null;

        [ContextMenu("프로젝트의 모든 아이템 모으기")]
        private void CollectAll()
        {
            items.Clear();
            foreach (var guid in UnityEditor.AssetDatabase.FindAssets("t:ItemDefinition"))
            {
                string path = UnityEditor.AssetDatabase.GUIDToAssetPath(guid);
                items.Add(UnityEditor.AssetDatabase.LoadAssetAtPath<ItemDefinition>(path));
            }
            UnityEditor.EditorUtility.SetDirty(this);   // "바뀌었음" 표시 → Save Project 때 파일에 저장됨
            byId = null;
            Build();   // 중복 ID가 있으면 바로 에러로 알려 줌
            Debug.Log($"[ItemDatabase] 아이템 {items.Count}개 모음", this);
        }
#endif
    }
}
