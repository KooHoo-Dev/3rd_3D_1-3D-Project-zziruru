using System.Text.RegularExpressions;
using UnityEngine;

namespace SesiVillage.Items
{
    /// <summary>아이템 종류. 인벤토리에서 "입기"·"먹기" 같은 동작을 정할 때 쓴다.</summary>
    public enum ItemType
    {
        Food = 0,       // 먹을 것
        Outfit = 1,     // 옷 (설빔) — 입을 수 있음
        Material = 2,   // 재료 (김장 재료, 가죽 등)
        Weapon = 3,     // 무기 (목도)
    }
    
    /// <summary>인벤토리 위쪽 탭. 탭마다 칸 목록이 따로 있다.</summary>
    public enum InventoryTab
    {
        Weapon,     // 무기
        Outfit,     // 옷
        Item,       // 아이템·재료 (먹을 것, 재료)
    }
    
    /// <summary>
    /// 아이템 정의 (게임 중에 바뀌지 않는 정보). 몇 개 가지고 있는지는 인벤토리가 따로 관리한다.
    /// 직접 만들 수 없다 — 종류별 자식 클래스로 만든다: FoodItem, MaterialItem, OutfitItem, WeaponItem.
    /// 종류(Type)는 클래스가 정한다
    /// </summary>
    public abstract class ItemDefinition: ScriptableObject
    {
        public const int CommonMaxStack = 99;
        private static readonly Regex IdRule = new Regex("^[a-z0-9_]+$");
        
        [Tooltip("영문 소문자·숫자·밑줄만. 저장 파일·대화(Yarn)에서 이 이름으로 찾는다. 만든 뒤에는 바꾸지 말 것")]
        [SerializeField] private string id;
        [SerializeField] private string displayName;
        [TextArea(2, 4)]
        [SerializeField] private string description;
        [SerializeField] private Sprite icon;
        [Tooltip("퀘스트 물건인가. 체크하면 한 칸 최대 개수를 아래 값으로 따로 정한다")]
        [SerializeField] private bool isQuestItem;
        [Tooltip("퀘스트 물건일 때만 사용: 한 칸에 몇 개까지 쌓이는가. 아니면 종류로 정해짐 (무기·옷 1, 먹을 것·재료 99)")]
        [Min(1)]
        [SerializeField] private int questMaxStack = 1;
        
        public string Id => id;
        public string DisplayName => displayName;
        public string Description => description;
        public Sprite Icon => icon;
        public bool IsQuestItem => isQuestItem;
        
        /// <summary>아이템 종류. 자식 클래스가 반드시 답한다 (FoodItem → Food)</summary>
        public abstract ItemType Type { get; }
        
        /// <summary>한 칸에 몇 개까지 쌓이는가. 퀘스트 물건은 에셋 값, 아니면 종류로 정해진다</summary>
        public int MaxStack
        {
            get
            {
                if (isQuestItem) return questMaxStack;
                return Type switch
                {
                    ItemType.Weapon or ItemType.Outfit => 1,
                    ItemType.Food or ItemType.Material => CommonMaxStack,
                    _ => 1,      // 목록에 없는 번호(지워진 종류가 남은 에셋 등) — 안전하게 1
                };
            }
        }
        
        /// <summary>인벤토리의 어느 탭에 들어가는가</summary>
        public InventoryTab Tab => Type switch
        {
            ItemType.Weapon => InventoryTab.Weapon,
            ItemType.Outfit => InventoryTab.Outfit,
            _ => InventoryTab.Item,     // Food, Material
        };
        
#if UNITY_EDITOR
        // Inspector에서 값을 바꿀 때마다 불림 → ID 규칙을 어기면 바로 경고
        // protected virtual: 자식이 검사를 더할 수 있게. 자식은 override하고 base.OnValidate()를 꼭 부를 것
        protected virtual void OnValidate()
        {
            if (string.IsNullOrEmpty(id))
                Debug.LogWarning($"[Item] {name}: ID가 비어 있음", this);
            else if (!IdRule.IsMatch(id))
                Debug.LogWarning($"[Item] {name}: ID '{id}'는 영문 소문자·숫자·밑줄만 쓸 수 있음", this);
        }
#endif
    }
}