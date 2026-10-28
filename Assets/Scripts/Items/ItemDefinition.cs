using System;
using System.Collections.Generic;
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
    /// 옷을 입는 자리. 자리마다 한 벌씩 따로 입는다 (상의를 바꿔도 하의는 그대로).
    /// 한 벌 옷(FullBody, 원피스·설빔처럼 위아래가 붙은 옷)은 상의·하의 자리를 함께 차지한다.
    /// </summary>
    public enum OutfitSlot
    {
        Top = 0,        // 상의 (겉옷)
        Bottom = 1,     // 하의 (바지)
        Hat = 2,        // 모자
        Shoes = 3,      // 신발
        FullBody = 4,   // 한 벌 옷 — 상의·하의 자리를 함께 씀
    }

    /// <summary>옷을 입으면 캐릭터의 한 부위가 어떻게 바뀌는가 (Outfit 아이템만 씀)</summary>
    [Serializable]
    public class OutfitPart
    {
        [Tooltip("캐릭터 모델에서 바꿀 부위 오브젝트 이름 (Outerwear, Pants, Hairstyle, Shoes …)")]
        public string partName;
        [Tooltip("바꿀 모양 (Creative Characters의 Meshes 폴더 FBX 안 메시). 비우면 모양은 그대로")]
        public Mesh mesh;
        [Tooltip("켜면 아래 색을 곱해서 입힘")]
        public bool changeColor;
        public Color color = Color.white;
    }

    /// <summary>
    /// 무기 모습 (Weapon 아이템만 씀). 무기는 모두 양손검 동작을 쓰고(기획 6장 "무기 교체 범위"), 모양만 바뀐다.
    /// 칼 오브젝트(손·등) 안에 이 모양을 그린다 — 위치·회전·크기는 원래 칼 모양 기준
    /// </summary>
    [Serializable]
    public class WeaponLook
    {
        [Tooltip("무기 모양. 비우면 원래 칼 모양 그대로 (목도)")]
        public Mesh mesh;
        [Tooltip("모양에 입힐 재질. 비우면 원래 칼 재질")]
        public Material material;
        [Tooltip("켜면 아래 색을 곱해서 입힘")]
        public bool changeColor;
        public Color color = Color.white;
        [Tooltip("원래 칼 기준 위치 (모양을 바꿀 때만)")]
        public Vector3 position;
        [Tooltip("원래 칼 기준 회전 (모양을 바꿀 때만)")]
        public Vector3 rotation;
        [Tooltip("원래 칼 기준 크기 (모양을 바꿀 때만)")]
        public Vector3 scale = Vector3.one;
    }

    /// <summary>
    /// 아이템 정의 (게임 중에 바뀌지 않는 정보). 몇 개 가지고 있는지는 인벤토리가 따로 관리한다.
    /// </summary>
    [CreateAssetMenu(fileName = "Item_", menuName = "SesiVillage/Item Definition")]
    public class ItemDefinition: ScriptableObject
    {
        public const int CommonMaxStack = 99;
        private static readonly Regex IdRule = new Regex("^[a-z0-9_]+$");
        
        [Tooltip("영문 소문자·숫자·밑줄만. 저장 파일·대화(Yarn)에서 이 이름으로 찾는다. 만든 뒤에는 바꾸지 말 것")]
        [SerializeField] private string id;
        [SerializeField] private string displayName;
        [TextArea(2, 4)]
        [SerializeField] private string description;
        [SerializeField] private Sprite icon;
        [SerializeField] private ItemType type = ItemType.Food;
        [Tooltip("퀘스트 물건인가. 체크하면 한 칸 최대 개수를 아래 값으로 따로 정한다")]
        [SerializeField] private bool isQuestItem;
        [Tooltip("퀘스트 물건일 때만 사용: 한 칸에 몇 개까지 쌓이는가. 아니면 종류로 정해짐 (무기·옷 1, 먹을 것·재료 99)")]
        [Min(1)]
        [SerializeField] private int questMaxStack = 1;

        [Header("옷 (종류가 Outfit일 때만)")]
        [Tooltip("입는 자리. 같은 자리의 옷끼리만 바뀐다 (한 벌 옷은 상의·하의를 함께 벗김)")]
        [SerializeField] private OutfitSlot outfitSlot = OutfitSlot.Top;
        [Tooltip("입으면 바뀌는 부위들")]
        [SerializeField] private List<OutfitPart> outfitParts = new List<OutfitPart>();

        [Header("무기 (종류가 Weapon일 때만)")]
        [SerializeField] private WeaponLook weaponLook = new WeaponLook();
        
        public string Id => id;
        public string DisplayName => displayName;
        public string Description => description;
        public Sprite Icon => icon;
        public ItemType Type => type;
        public bool IsQuestItem => isQuestItem;
        public OutfitSlot OutfitSlot => outfitSlot;
        public IReadOnlyList<OutfitPart> OutfitParts => outfitParts;
        public WeaponLook WeaponLook => weaponLook;
        
        /// <summary>한 칸에 몇 개까지 쌓이는가. 퀘스트 물건은 에셋 값, 아니면 종류로 정해진다</summary>
        public int MaxStack
        {
            get
            {
                if (isQuestItem) return questMaxStack;
                return type switch
                {
                    ItemType.Weapon or ItemType.Outfit => 1,
                    ItemType.Food or ItemType.Material => CommonMaxStack,
                    _ => 1,      // 목록에 없는 번호(지워진 종류가 남은 에셋 등) — 안전하게 1
                };
            }
        }
        
        /// <summary>인벤토리의 어느 탭에 들어가는가</summary>
        public InventoryTab Tab => type switch
        {
            ItemType.Weapon => InventoryTab.Weapon,
            ItemType.Outfit => InventoryTab.Outfit,
            _ => InventoryTab.Item,     // Food, Material
        };
        
#if UNITY_EDITOR
        // Inspector에서 값을 바꿀 때마다 불림 → ID 규칙을 어기면 바로 경고
        private void OnValidate()
        {
            if (string.IsNullOrEmpty(id))
                Debug.LogWarning($"[Item] {name}: ID가 비어 있음", this);
            else if (!IdRule.IsMatch(id))
                Debug.LogWarning($"[Item] {name}: ID '{id}'는 영문 소문자·숫자·밑줄만 쓸 수 있음", this);
            
            if (!System.Enum.IsDefined(typeof(ItemType), type))
                Debug.LogWarning($"[Item] {name}: 종류(Type)가 정해지지 않음 — 다시 고를 것", this);
        }
#endif
    }
}