using UnityEngine;

namespace SesiVillage.Items
{
    /// <summary>
    /// 먹을 것 (떡국, 팥죽 …).
    /// 나중에 "먹기" 작업 때: 체력 회복량 칸 + 먹기 동작이 이 클래스에 들어간다
    /// </summary>
    [CreateAssetMenu(fileName = "Item_", menuName = "SesiVillage/Food Item")]
    public class FoodItem : ItemDefinition
    {
        public override ItemType Type => ItemType.Food;
    }
}