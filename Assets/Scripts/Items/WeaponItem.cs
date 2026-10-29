using UnityEngine;

namespace SesiVillage.Items
{
    /// <summary>
    /// 무기 아이템. 자리는 항상 Weapon. 모든 무기가 양손검 동작을 쓰고 모양만 바뀐다.
    /// 모양·색 칸은 4단계(WeaponAppearance)에서 추가한다
    /// </summary>
    [CreateAssetMenu(fileName = "Item_", menuName = "SesiVillage/Weapon Item")]
    public class WeaponItem : EquipmentItem
    {
        public override ItemType Type => ItemType.Weapon;
        public override EquipSlot Slot => EquipSlot.Weapon;
    }
}