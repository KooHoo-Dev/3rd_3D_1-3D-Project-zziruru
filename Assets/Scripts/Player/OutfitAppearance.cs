using System.Collections.Generic;
using SesiVillage.Items;
using UnityEngine;

namespace SesiVillage.Player
{
    /// <summary>
    /// 캐릭터 모델에 옷 모습을 입힌다. 모델 맨 위(부위 오브젝트 Outerwear·Pants…를 자식으로 가진 곳)에 붙인다.
    /// PlayerOutfit.Changed를 듣고 바꾸므로, 주인공 모델과 인벤토리 미리보기 모델에 똑같이 붙이면 둘이 함께 바뀐다.
    ///  · 모양: SkinnedMeshRenderer.sharedMesh 교체 (Creative Characters 커스터마이징 창과 같은 방식 — 부위가 같은 뼈대를 씀)
    ///  · 색: MaterialPropertyBlock으로 이 렌더러에만 _BaseColor를 곱함 → 재질 파일(모든 캐릭터가 같이 씀)은 그대로
    ///  · 옷을 바꿀 때마다 처음 모습으로 되돌린 뒤 입은 옷 전부(상의·하의·모자…)를 다시 입힌다 → 이전 옷의 흔적이 남지 않음
    /// </summary>
    public class OutfitAppearance : MonoBehaviour
    {
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        private readonly Dictionary<string, SkinnedMeshRenderer> parts = new Dictionary<string, SkinnedMeshRenderer>();
        private readonly Dictionary<SkinnedMeshRenderer, Mesh> originalMeshes = new Dictionary<SkinnedMeshRenderer, Mesh>();
        private MaterialPropertyBlock block;
        private PlayerOutfit outfit;

        private void Awake()
        {
            block = new MaterialPropertyBlock();
            // 부위 이름 → 렌더러, 그리고 처음 모양을 기억 (벗을 때 되돌리려고)
            foreach (var r in GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                parts[r.gameObject.name] = r;
                originalMeshes[r] = r.sharedMesh;
            }
        }

        // Start: PlayerOutfit.Instance는 Awake에서 정해지므로 그 뒤에 찾는다
        private void Start()
        {
            outfit = PlayerOutfit.Instance;
            if (outfit == null) { Debug.LogWarning("[OutfitAppearance] PlayerOutfit 없음 — 옷이 바뀌지 않음", this); return; }
            outfit.Changed += Apply;
            Apply();   // 시작할 때 이미 입은 옷이 있으면 (불러오기 등) 바로 반영
        }

        private void OnDestroy()
        {
            if (outfit != null) outfit.Changed -= Apply;
        }

        public void Apply()
        {
            // 1) 처음 모습으로
            foreach (var pair in originalMeshes)
            {
                SetMesh(pair.Key, pair.Value);
                pair.Key.SetPropertyBlock(null);   // 색 덮어쓰기 지우기
            }
            if (outfit == null) return;
            // 2) 입은 옷마다 그 옷의 부위들을 입힘 (자리가 겹치는 옷은 PlayerOutfit이 미리 벗겨 둠)
            foreach (var worn in outfit.Worn)
            foreach (var part in worn.OutfitParts)
            {
                if (!parts.TryGetValue(part.partName, out var r))
                {
                    Debug.LogWarning($"[OutfitAppearance] '{part.partName}' 부위가 모델에 없음 ({worn.DisplayName})", this);
                    continue;
                }
                if (part.mesh != null) SetMesh(r, part.mesh);
                if (part.changeColor)
                {
                    block.Clear();
                    block.SetColor(BaseColorId, part.color);
                    r.SetPropertyBlock(block);
                }
            }
        }

        private static void SetMesh(SkinnedMeshRenderer r, Mesh mesh)
        {
            if (r.sharedMesh == mesh) return;
            r.sharedMesh = mesh;
            // 화면 밖 판정용 상자도 새 모양에 맞춤 (안 하면 긴 옷의 끝이 카메라 가장자리에서 사라질 수 있음)
            if (mesh != null) r.localBounds = mesh.bounds;
        }
    }
}
