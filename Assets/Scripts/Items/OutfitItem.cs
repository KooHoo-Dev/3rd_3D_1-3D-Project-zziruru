using System;
using System.Collections.Generic;
using UnityEngine;

namespace SesiVillage.Items
{
    /// <summary>옷을 입으면 캐릭터의 한 부위가 어떻게 바뀌는가</summary>
    [Serializable]
    public class OutfitPart
    {
        [Tooltip("캐릭터 모델에서 바꿀 부위 오브젝트 이름 (Outerwear, Pants, Hat, Shoes …)")]
        [SerializeField] private string partName;
        [Tooltip("바꿀 모양 (Creative Characters Meshes 폴더의 FBX 안 메시). 비우면 모양은 그대로")]
        [SerializeField] private Mesh mesh;
        [Tooltip("켜면 아래 색을 곱해서 입힘")]
        [SerializeField] private bool changeColor;
        [SerializeField] private Color color = Color.white;

        public string PartName => partName;
        public Mesh Mesh => mesh;
        public bool ChangeColor => changeColor;
        public Color Color => color;
    }

    /// <summary>
    /// 옷 아이템. 입는 자리 하나 + 입으면 바뀌는 부위들.
    /// 한 벌 옷(설빔)은 FullBody 자리 — 상의·하의를 함께 막는 규칙은 PlayerEquipment가 맡는다 (데이터는 자리만 표시)
    /// </summary>
    [CreateAssetMenu(fileName = "Item_", menuName = "SesiVillage/Outfit Item")]
    public class OutfitItem : EquipmentItem
    {
        [Header("옷")]
        [Tooltip("입는 자리. 같은 자리의 옷끼리 바뀐다")]
        [SerializeField] private EquipSlot slot = EquipSlot.Top;
        [Tooltip("입으면 바뀌는 부위들")]
        [SerializeField] private List<OutfitPart> parts = new List<OutfitPart>();

        public override ItemType Type => ItemType.Outfit;
        public override EquipSlot Slot => slot;
        /// <summary>읽기 전용으로 내보낸다 — 밖에서 Add·Remove로 에셋 데이터를 바꾸지 못하게</summary>
        public IReadOnlyList<OutfitPart> Parts => parts;

#if UNITY_EDITOR
        protected override void OnValidate()
        {
            base.OnValidate();   // ID 검사는 부모가

            if (slot == EquipSlot.Weapon)
                Debug.LogWarning($"[Item] {name}: 옷인데 자리가 Weapon — 무기는 Weapon Item으로 만드세요", this);

            for (int i = 0; i < parts.Count; i++)
                if (parts[i] != null && string.IsNullOrEmpty(parts[i].PartName))
                    Debug.LogWarning($"[Item] {name}: 부위 {i}번의 이름이 비어 있음", this);
        }
#endif
    }
}