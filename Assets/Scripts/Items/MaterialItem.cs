using UnityEngine;

namespace SesiVillage.Items
{
    /// <summary>재료 (잡초, 김장 재료, 가죽 …). 모으거나 NPC에게 건네는 용도</summary>
    [CreateAssetMenu(fileName = "Item_", menuName = "SesiVillage/Material Item")]
    public class MaterialItem : ItemDefinition
    {
        public override ItemType Type => ItemType.Material;
    }
}