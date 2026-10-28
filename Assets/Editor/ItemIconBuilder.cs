using System.Collections.Generic;
using System.IO;
using SesiVillage.Items;
using UnityEditor;
using UnityEngine;

namespace SesiVillage.EditorTools
{
    /// <summary>
    /// 메뉴 SesiVillage → Items → Make Item Icons. 옷·무기 아이템의 실제 모양(메시)을 사진 찍어 인벤토리 아이콘으로 만든다.
    ///  · 옷: 그 옷이 바꾸는 부위 메시들을 같은 색으로 함께 찍음 (설빔이면 겉옷 + 바지). 비스듬한 앞모습
    ///  · 무기: 무기 모양을 대각선으로 눕혀 찍음 (모양을 안 바꾸는 무기는 원래 칼 모양 + 색)
    ///  · 배경은 투명 (단색 배경으로 찍은 뒤 그 색을 지움) → Assets/Textures/ItemIcons/Icon_<id>.png, Sprite로 가져와 아이템 Icon 칸에 넣음
    ///  · 손으로 넣은 아이콘(이 폴더 밖의 그림)은 덮어쓰지 않는다. 메시가 없는 아이템(떡국·잡초)은 건너뜀 → 첫 글자 그대로
    /// 에디터 미리보기용 PreviewRenderUtility를 쓴다 — 씬과 따로 떨어진 작은 무대라 열린 씬을 건드리지 않음
    /// </summary>
    public static class ItemIconBuilder
    {
        private const string IconFolder = "Assets/Textures/ItemIcons";
        private const string CharacterMaterialPath = "Assets/External Assets/ithappy/Creative_Characters_FREE/Materials/Color.mat";
        private const string SwordFbxPath = "Assets/External Assets/ExplosiveLLC/RPG Character Mecanim Animation Pack FREE/Models/Weapons/2Hand-Sword.FBX";
        private const int Size = 256;
        private const float Fov = 30f;
        private static readonly Color KeyColor = new Color32(255, 0, 255, 255);   // 지울 배경색 (모델에 안 나오는 자홍)
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        private struct Piece
        {
            public Mesh mesh; public Material material; public Matrix4x4 matrix; public Color? color;
        }

        [MenuItem("SesiVillage/Items/Make Item Icons (옷·무기 모습으로)")]
        public static void MakeAll()
        {
            var characterMaterial = AssetDatabase.LoadAssetAtPath<Material>(CharacterMaterialPath);
            var swordModel = AssetDatabase.LoadAssetAtPath<GameObject>(SwordFbxPath);
            var swordFilter = swordModel != null ? swordModel.GetComponentInChildren<MeshFilter>() : null;
            var swordRenderer = swordModel != null ? swordModel.GetComponentInChildren<MeshRenderer>() : null;
            EnsureFolder(IconFolder);

            int made = 0;
            foreach (var guid in AssetDatabase.FindAssets("t:ItemDefinition"))
            {
                var item = AssetDatabase.LoadAssetAtPath<ItemDefinition>(AssetDatabase.GUIDToAssetPath(guid));
                if (item == null || !CanOverwrite(item)) continue;

                List<Piece> pieces;
                bool diagonal;
                if (item.Type == ItemType.Outfit) { pieces = OutfitPieces(item, characterMaterial); diagonal = false; }
                else if (item.Type == ItemType.Weapon)
                {
                    pieces = WeaponPieces(item, swordFilter != null ? swordFilter.sharedMesh : null,
                                          swordRenderer != null ? swordRenderer.sharedMaterial : null);
                    diagonal = true;
                }
                else continue;
                if (pieces.Count == 0) continue;

                var sprite = Shoot(item.Id, pieces, diagonal);
                if (sprite == null) continue;
                var so = new SerializedObject(item);
                so.FindProperty("icon").objectReferenceValue = sprite;
                so.ApplyModifiedPropertiesWithoutUndo();
                made++;
            }
            AssetDatabase.SaveAssets();
            Debug.Log($"[ItemIconBuilder] 아이콘 {made}개 만듦 → {IconFolder}");
        }

        // 아이콘이 비었거나 이 도구가 만든 것만 다시 만든다
        private static bool CanOverwrite(ItemDefinition item) =>
            item.Icon == null || AssetDatabase.GetAssetPath(item.Icon).StartsWith(IconFolder);

        private static List<Piece> OutfitPieces(ItemDefinition item, Material material)
        {
            var list = new List<Piece>();
            foreach (var part in item.OutfitParts)
            {
                if (part.mesh == null) continue;   // 색만 바꾸는 부위는 모양이 없어 찍을 수 없음
                list.Add(new Piece { mesh = part.mesh, material = material, matrix = Matrix4x4.identity,
                                     color = part.changeColor ? part.color : (Color?)null });
            }
            return list;
        }

        private static List<Piece> WeaponPieces(ItemDefinition item, Mesh swordMesh, Material swordMaterial)
        {
            var look = item.WeaponLook;
            var mesh = look.mesh != null ? look.mesh : swordMesh;
            var material = look.material != null ? look.material : swordMaterial;
            var list = new List<Piece>();
            if (mesh == null || material == null) return list;
            // 가장 긴 축 → 화면 대각선(↗), 두 번째로 긴 축(칼날 폭) → 그와 수직인 화면 방향 → 칼날 넓은 면이 보임
            Vector3 size = mesh.bounds.size;
            int a = LongestAxis(size);
            int b = SecondAxis(size, a);
            var from = Quaternion.LookRotation(Axis(a), Axis(b));
            var to = Quaternion.LookRotation(new Vector3(1f, 1f, 0f).normalized, new Vector3(-1f, 1f, 0f).normalized);
            var rot = to * Quaternion.Inverse(from);
            list.Add(new Piece { mesh = mesh, material = material, matrix = Matrix4x4.Rotate(rot) * Matrix4x4.Translate(-mesh.bounds.center),
                                 color = look.changeColor ? look.color : (Color?)null });
            return list;
        }

        private static Sprite Shoot(string id, List<Piece> pieces, bool diagonal)
        {
            // 모든 조각을 감싸는 상자 → 카메라가 그 상자를 감싼 구가 화면에 꽉 차는 거리에 섬
            var bounds = new Bounds();
            bool any = false;
            foreach (var p in pieces)
            {
                var mb = p.mesh.bounds;
                for (int i = 0; i < 8; i++)
                {
                    var corner = mb.center + Vector3.Scale(mb.extents, new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
                    var w = p.matrix.MultiplyPoint3x4(corner);
                    if (!any) { bounds = new Bounds(w, Vector3.zero); any = true; } else bounds.Encapsulate(w);
                }
            }
            // 대각선으로 눕힌 무기는 상자의 대각선이 화면 대각선과 겹치므로 조금 더 가까이
            float radius = (diagonal ? new Vector2(bounds.extents.x, bounds.extents.y).magnitude * 0.75f : bounds.extents.magnitude) * 1.08f;
            float dist = radius / Mathf.Sin(Fov * 0.5f * Mathf.Deg2Rad);

            var pru = new PreviewRenderUtility();
            try
            {
                var cam = pru.camera;
                cam.fieldOfView = Fov;
                cam.clearFlags = CameraClearFlags.SolidColor;
                cam.backgroundColor = KeyColor;
                cam.nearClipPlane = 0.01f;
                cam.farClipPlane = dist + radius * 4f;
                // 캐릭터 옷은 +z가 앞 → 앞에서 보되 옷은 살짝 비스듬히(3/4), 무기는 정면
                var rot = Quaternion.Euler(diagonal ? 0f : 10f, diagonal ? 180f : 205f, 0f);
                cam.transform.rotation = rot;
                cam.transform.position = bounds.center - rot * Vector3.forward * dist;

                pru.lights[0].intensity = 1.2f;
                pru.lights[0].transform.rotation = Quaternion.Euler(40f, 160f, 0f);
                pru.lights[1].intensity = 0.6f;
                pru.ambientColor = new Color(0.35f, 0.35f, 0.35f);

                pru.BeginStaticPreview(new Rect(0, 0, Size, Size));
                var block = new MaterialPropertyBlock();
                foreach (var p in pieces)
                {
                    block.Clear();
                    if (p.color.HasValue) block.SetColor(BaseColorId, p.color.Value);
                    for (int s = 0; s < p.mesh.subMeshCount; s++)
                        pru.DrawMesh(p.mesh, p.matrix, p.material, s, p.color.HasValue ? block : null);
                }
                pru.Render(true, false);   // true = URP로 그림, false = FOV를 자동으로 바꾸지 않음
                var shot = pru.EndStaticPreview();
                return SaveAsSprite(id, shot);
            }
            finally { pru.Cleanup(); }
        }

        // 배경색(모서리 픽셀)과 거의 같은 픽셀을 투명하게 만들고 PNG로 저장 → Sprite로 가져옴
        private static Sprite SaveAsSprite(string id, Texture2D shot)
        {
            var pixels = shot.GetPixels();
            var key = pixels[0];
            for (int i = 0; i < pixels.Length; i++)
            {
                var c = pixels[i];
                if (Mathf.Abs(c.r - key.r) + Mathf.Abs(c.g - key.g) + Mathf.Abs(c.b - key.b) < 0.08f) pixels[i] = Color.clear;
            }
            var tex = new Texture2D(shot.width, shot.height, TextureFormat.RGBA32, false);
            tex.SetPixels(pixels);
            tex.Apply();

            string path = $"{IconFolder}/Icon_{id}.png";
            File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(path);

            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }

        private static int LongestAxis(Vector3 v) => v.x >= v.y && v.x >= v.z ? 0 : v.y >= v.z ? 1 : 2;
        private static int SecondAxis(Vector3 v, int longest)
        {
            int best = -1;
            for (int i = 0; i < 3; i++)
                if (i != longest && (best < 0 || v[i] > v[best])) best = i;
            return best;
        }
        private static Vector3 Axis(int i) => i == 0 ? Vector3.right : i == 1 ? Vector3.up : Vector3.forward;

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            int slash = path.LastIndexOf('/');
            EnsureFolder(path.Substring(0, slash));
            AssetDatabase.CreateFolder(path.Substring(0, slash), path.Substring(slash + 1));
        }
    }
}
