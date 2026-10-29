namespace SesiVillage.Items
{
    /// <summary>
    /// 장비를 입거나 드는 자리. 자리마다 하나씩.
    /// </summary>
    public enum EquipSlot
    {
        Top = 0,        // 상의
        Bottom = 1,     // 하의
        Hat = 2,        // 모자
        Shoes = 3,      // 신발
        FullBody = 4,   // 한 벌 옷 (설빔) — 상의·하의 자리를 함께 차지
        Weapon = 5,     // 무기
    }

    /// <summary>
    /// 입거나 들 수 있는 아이템의 공통 부모. 직접 만들 수 없다 (옷 또는 무기 중 하나).
    /// 장비 시스템(PlayerEquipment)은 옷·무기를 구분하지 않고 이 타입과 Slot만 보고 다룬다.
    /// Type은 여기서 정하지 않는다 — 옷·무기가 각자 답한다
    /// </summary>
    public abstract class EquipmentItem : ItemDefinition
    {
        /// <summary>이 장비가 들어가는 자리. 자식이 반드시 정한다</summary>
        public abstract EquipSlot Slot { get; }
    }
}