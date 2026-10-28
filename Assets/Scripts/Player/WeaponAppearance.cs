using SesiVillage.Items;
using UnityEngine;

namespace SesiVillage.Player
{
    /// <summary>
    /// 칼 오브젝트(손에 든 칼·등에 멘 칼)에 지금 든 무기의 모습을 입힌다. PlayerWeapon.Changed를 듣는다.
    /// 주인공(손·등 칼 2개)과 인벤토리 미리보기(등 칼 1개)에 똑같이 붙이면 함께 바뀐다 — OutfitAppearance와 같은 구조.
    ///  · 칼 오브젝트 자체는 건드리지 않는다: Explosive가 손 칼을 켜고 끄고(SetActive), BackSwordDisplay가 등 칼을 켜고 끄기 때문
    ///  · 모양이 다른 무기: 칼 오브젝트의 원래 MeshRenderer를 끄고, 자식 "WeaponVisual"에 새 모양을 그린다 (위치·회전·크기는 아이템 값)
    ///  · 모양이 같은 무기(목도·철검): 원래 칼을 그대로 보이고 색만 MaterialPropertyBlock으로 곱한다
    /// </summary>
    public class WeaponAppearance : MonoBehaviour
    {
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        [Tooltip("모습을 바꿀 칼 오브젝트들 (주인공: 2Hand-Sword, 2Hand-Sword-Back)")]
        [SerializeField] private Transform[] swords;

        private MeshRenderer[] originals;
        private MeshRenderer[] visuals;
        private MaterialPropertyBlock block;
        private PlayerWeapon weapon;

        private void Awake()
        {
            block = new MaterialPropertyBlock();
            originals = new MeshRenderer[swords.Length];
            visuals = new MeshRenderer[swords.Length];
            for (int i = 0; i < swords.Length; i++)
            {
                if (swords[i] == null) { Debug.LogWarning($"[WeaponAppearance] 칼 {i}번 칸이 비어 있음", this); continue; }
                originals[i] = swords[i].GetComponent<MeshRenderer>();
                visuals[i] = CreateVisual(swords[i]);
            }
        }

        // Start: PlayerWeapon.Instance는 Awake에서 정해지므로 그 뒤에 찾는다
        private void Start()
        {
            weapon = PlayerWeapon.Instance;
            if (weapon == null) { Debug.LogWarning("[WeaponAppearance] PlayerWeapon 없음 — 무기 모양이 바뀌지 않음", this); return; }
            weapon.Changed += Apply;
            Apply(weapon.Equipped);
        }

        private void OnDestroy()
        {
            if (weapon != null) weapon.Changed -= Apply;
        }

        public void Apply(ItemDefinition item)
        {
            var look = item != null ? item.WeaponLook : null;
            bool newShape = look != null && look.mesh != null;

            for (int i = 0; i < swords.Length; i++)
            {
                if (swords[i] == null) continue;
                var original = originals[i];
                var visual = visuals[i];

                if (original != null) original.enabled = !newShape;
                visual.gameObject.SetActive(newShape);

                MeshRenderer shown = newShape ? visual : original;
                if (newShape)
                {
                    visual.GetComponent<MeshFilter>().sharedMesh = look.mesh;
                    visual.sharedMaterial = look.material != null ? look.material : original != null ? original.sharedMaterial : null;
                    visual.transform.localPosition = look.position;
                    visual.transform.localRotation = Quaternion.Euler(look.rotation);
                    visual.transform.localScale = look.scale;
                }

                if (original != null) original.SetPropertyBlock(null);
                if (shown != null && look != null && look.changeColor)
                {
                    block.Clear();
                    block.SetColor(BaseColorId, look.color);
                    shown.SetPropertyBlock(block);
                }
                else if (shown != null) shown.SetPropertyBlock(null);
            }
        }

        // 칼 오브젝트 아래에 새 모양을 그릴 자식을 만든다 (레이어는 칼과 같게 — 미리보기 카메라가 레이어로 골라 그림)
        private static MeshRenderer CreateVisual(Transform sword)
        {
            var go = new GameObject("WeaponVisual");
            go.layer = sword.gameObject.layer;
            go.transform.SetParent(sword, false);
            go.AddComponent<MeshFilter>();
            var r = go.AddComponent<MeshRenderer>();
            go.SetActive(false);
            return r;
        }
    }
}
