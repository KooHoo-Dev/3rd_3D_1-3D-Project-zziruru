using System.Collections.Generic;
using System.Linq;
using SesiVillage.Items;
using SesiVillage.Player;
using SesiVillage.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace SesiVillage.EditorTools
{
    /// <summary>
    /// 메뉴 SesiVillage → Outfit → Set Up Outfit (Core 씬을 열고 실행). 착용 시스템(옷·무기)에 필요한 에디터 세팅을 한 번에 한다:
    ///  1) 예시 옷 4벌·무기 3자루 만들기 (목도는 Assets/Data/Items, 나머지는 Samples) + 설빔을 한 벌 옷으로
    ///  2) ItemDatabase에 등록, 아이콘 찍기(ItemIconBuilder), 시작 인벤토리에 넣기 (바꿔 입어 보기용 — 제출 전에 예시는 시작 인벤토리에서 빼기)
    ///  3) Systems에 PlayerOutfit·PlayerWeapon, Player 프리팹에 OutfitAppearance(모델)·WeaponAppearance(손·등 칼)
    ///  4) 미리보기 무대 PreviewStage (0, -500, 0): 캐릭터(+등 칼) + 몸 전체가 들어오게 맞춘 카메라(RenderTexture에 그림) + 조명
    ///  5) 인벤토리 화면의 PreviewImage·CharacterPreviewDrag·previewCamera 연결
    /// 몇 번 눌러도 결과가 같다 (있으면 고치고, 무대는 지우고 다시 만듦). Build Game UI를 다시 누르면 5)만 자동으로 다시 연결된다.
    /// </summary>
    public static class OutfitSetupBuilder
    {
        private const string MeshRoot = "Assets/External Assets/ithappy/Creative_Characters_FREE/Meshes/";
        private const string CharacterPrefabPath = "Assets/External Assets/ithappy/Creative_Characters_FREE/Saved_Characters/Character 1.prefab";
        private const string SwordFbxPath = "Assets/External Assets/ExplosiveLLC/RPG Character Mecanim Animation Pack FREE/Models/Weapons/2Hand-Sword.FBX";
        private const string StickFbxPath = "Assets/External Assets/Cartoon Nature - LowPoly Asset Pack/Prefabs/Sticks/Stick_Brown_3.fbx";
        private const string PlayerPrefabPath = "Assets/Prefabs/Player/Player.prefab";
        private const string IdleControllerPath = "Assets/Animations/NPC/NPC_Idle.controller";
        private const string ItemsFolder = "Assets/Data/Items";
        private const string SamplesFolder = "Assets/Data/Items/Samples";
        private const string DatabasePath = "Assets/Data/ItemDatabase.asset";
        private const string RenderTextureFolder = "Assets/Textures";
        private const string RenderTexturePath = "Assets/Textures/RT_CharacterPreview.renderTexture";
        private const string PreviewLayerName = "Character Preview";
        private const string StageName = "PreviewStage";
        private const string HandSwordName = "2Hand-Sword";
        private const string BackSwordName = "2Hand-Sword-Back";

        private const float FrameMargin = 1.3f;   // 미리보기에서 캐릭터 위아래로 남길 여백 (1.3 = 키의 30%)
        private const float PreviewFov = 30f;
        private const float PreviewAspect = 512f / 864f;

        // 부위 하나: (부위 이름, 메시 FBX 경로(MeshRoot 기준, null = 모양 그대로), 색(null = 색 그대로))
        private struct Part
        {
            public string name; public string fbx; public Color? color;
            public Part(string name, string fbx, Color? color) { this.name = name; this.fbx = fbx; this.color = color; }
        }

        [MenuItem("SesiVillage/Outfit/Set Up Outfit (예시 옷·무기·미리보기 무대·연결)")]
        public static void SetUp()
        {
            int layer = LayerMask.NameToLayer(PreviewLayerName);
            if (layer < 0) { Debug.LogError($"[OutfitSetup] 레이어 '{PreviewLayerName}'가 없음 — Tags and Layers에서 만드세요"); return; }
            var inventory = Object.FindFirstObjectByType<Inventory>(FindObjectsInactive.Include);
            if (inventory == null) { Debug.LogError("[OutfitSetup] 씬에 Inventory가 없음 — Core 씬을 열고 실행하세요"); return; }

            var items = new List<ItemDefinition>();
            items.AddRange(CreateWeapons());        // 목도가 무기 탭 첫 칸 → 시작할 때 드는 무기
            items.AddRange(CreateSampleOutfits());
            FillSeolbim();
            RegisterInDatabase();
            ItemIconBuilder.MakeAll();              // 옷·무기 모양을 찍어 아이콘으로
            AddStartingItems(inventory, items);
            AddSystem<PlayerOutfit>("PlayerOutfit", inventory);
            AddSystem<PlayerWeapon>("PlayerWeapon", inventory);
            SetUpPlayerPrefab();
            var rt = CreateRenderTexture();
            var stage = BuildStage(layer, rt);
            ExcludeLayerFromMainCamera(layer);
            WirePreviewUI();

            EditorSceneManager.MarkSceneDirty(inventory.gameObject.scene);
            Selection.activeGameObject = stage;
            Debug.Log("[OutfitSetup] 완료 — 씬을 저장하세요 (Ctrl+S)");
        }

        /* ───────── 1) 아이템 ───────── */

        private static ItemDefinition[] CreateSampleOutfits()
        {
            EnsureFolder(SamplesFolder);
            return new[]
            {
                SaveOutfit("Item_Sample_TravelerCoat", "sample_traveler_coat", "나그네 겉옷",
                    "(시험용) 먼 길을 걸어온 나그네의 겉옷.", OutfitSlot.Top,
                    new Part("Outerwear", "Outwear/Outwear_004.fbx", null)),
                SaveOutfit("Item_Sample_PaddedCoat", "sample_padded_coat", "솜누비 저고리",
                    "(시험용) 솜을 두어 누빈 따뜻한 겨울 윗옷.", OutfitSlot.Top,
                    new Part("Outerwear", "Outwear/Outwear_050.fbx", new Color32(150, 110, 70, 255))),
                SaveOutfit("Item_Sample_Pants", "sample_pants", "무명 바지",
                    "(시험용) 무명으로 지은 긴 바지.", OutfitSlot.Bottom,
                    new Part("Pants", "Pants/Pants_009.fbx", new Color32(200, 190, 170, 255))),
                SaveOutfit("Item_Sample_WinterHat", "sample_winter_hat", "털모자",
                    "(시험용) 귀까지 덮는 겨울 모자.", OutfitSlot.Hat,
                    new Part("Hat", "Hat Single/Hat_Single_008.fbx", null)),
            };
        }

        // 설빔 = 한 벌 옷 (다홍 겉옷 + 남색 바지). 부위가 1개 이하(처음 만든 값)일 때만 채운다 → 손으로 고친 값은 덮어쓰지 않음
        private static void FillSeolbim()
        {
            var seolbim = AssetDatabase.LoadAssetAtPath<ItemDefinition>(ItemsFolder + "/Item_Seolbim.asset");
            if (seolbim == null) { Debug.LogWarning("[OutfitSetup] Item_Seolbim이 없어 건너뜀"); return; }
            if (seolbim.OutfitParts.Count > 1) return;
            var so = new SerializedObject(seolbim);
            so.FindProperty("outfitSlot").enumValueIndex = (int)OutfitSlot.FullBody;
            WriteParts(so,
                new Part("Outerwear", "Outwear/Outwear_043.fbx", new Color32(220, 60, 60, 255)),
                new Part("Pants", "Pants/Pants_010.fbx", new Color32(60, 70, 110, 255)));
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static ItemDefinition SaveOutfit(string fileName, string id, string displayName, string description, OutfitSlot slot, params Part[] parts)
        {
            var so = SaveItem($"{SamplesFolder}/{fileName}.asset", id, displayName, description, ItemType.Outfit, out var item);
            so.FindProperty("outfitSlot").enumValueIndex = (int)slot;
            WriteParts(so, parts);
            so.ApplyModifiedPropertiesWithoutUndo();
            return item;
        }

        private static void WriteParts(SerializedObject so, params Part[] parts)
        {
            var list = so.FindProperty("outfitParts");
            list.arraySize = parts.Length;
            for (int i = 0; i < parts.Length; i++)
            {
                var el = list.GetArrayElementAtIndex(i);
                el.FindPropertyRelative("partName").stringValue = parts[i].name;
                el.FindPropertyRelative("mesh").objectReferenceValue = parts[i].fbx != null ? LoadMesh(MeshRoot + parts[i].fbx) : null;
                el.FindPropertyRelative("changeColor").boolValue = parts[i].color.HasValue;
                el.FindPropertyRelative("color").colorValue = parts[i].color ?? Color.white;
            }
        }

        private static ItemDefinition[] CreateWeapons()
        {
            EnsureFolder(SamplesFolder);
            // 원래 칼 텍스처가 쇠 색이라: 철검 = 그대로, 목도 = 나무색을 곱함
            var mokdo = SaveWeapon($"{ItemsFolder}/Item_Mokdo.asset", "mokdo", "목도",
                "나무를 깎아 만든 칼. 처음부터 들고 있던 무기.", null, null, new Color32(185, 130, 80, 255));
            var iron = SaveWeapon($"{SamplesFolder}/Item_Sample_IronSword.asset", "sample_iron_sword", "철검",
                "(시험용) 대장간에서 벼린 쇠칼.", null, null, null);

            // 나무 막대기: 칼과 모양이 달라서 원래 칼 크기·방향에 맞춰 놓는다 (자동 계산 — 어긋나면 아이템 Weapon Look 값을 손으로 조절)
            var stickMesh = LoadMesh(StickFbxPath);
            var stickModel = AssetDatabase.LoadAssetAtPath<GameObject>(StickFbxPath);
            var stickRenderer = stickModel != null ? stickModel.GetComponentInChildren<MeshRenderer>() : null;
            var stickMaterial = stickRenderer != null ? stickRenderer.sharedMaterial : null;
            var stick = SaveWeapon($"{SamplesFolder}/Item_Sample_Stick.asset", "sample_stick", "나무 막대기",
                "(시험용) 길에서 주운 튼튼한 막대기.", stickMesh, stickMaterial, null);
            FitToSword(stick, stickMesh, LoadMesh(SwordFbxPath));

            return new[] { mokdo, iron, stick };
        }

        private static ItemDefinition SaveWeapon(string path, string id, string displayName, string description, Mesh mesh, Material material, Color? color)
        {
            var so = SaveItem(path, id, displayName, description, ItemType.Weapon, out var item);
            var look = so.FindProperty("weaponLook");
            look.FindPropertyRelative("mesh").objectReferenceValue = mesh;
            look.FindPropertyRelative("material").objectReferenceValue = material;
            look.FindPropertyRelative("changeColor").boolValue = color.HasValue;
            look.FindPropertyRelative("color").colorValue = color ?? Color.white;
            so.ApplyModifiedPropertiesWithoutUndo();
            return item;
        }

        // 가장 긴 축끼리 맞추고, 길이를 칼만큼 늘이고, 가운데를 맞춘다 → 손잡이 쪽 끝이 대략 칼 손잡이 자리에 옴
        private static void FitToSword(ItemDefinition item, Mesh mesh, Mesh sword)
        {
            if (item == null || mesh == null || sword == null) return;
            Bounds m = mesh.bounds, s = sword.bounds;
            int ma = LongestAxis(m.size), sa = LongestAxis(s.size);
            var rot = Quaternion.FromToRotation(Axis(ma), Axis(sa));
            float k = s.size[sa] / m.size[ma] * 0.95f;
            Vector3 pos = s.center - rot * (m.center * k);

            var so = new SerializedObject(item);
            var look = so.FindProperty("weaponLook");
            look.FindPropertyRelative("position").vector3Value = pos;
            look.FindPropertyRelative("rotation").vector3Value = rot.eulerAngles;
            look.FindPropertyRelative("scale").vector3Value = Vector3.one * k;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static int LongestAxis(Vector3 v) => v.x >= v.y && v.x >= v.z ? 0 : v.y >= v.z ? 1 : 2;
        private static Vector3 Axis(int i) => i == 0 ? Vector3.right : i == 1 ? Vector3.up : Vector3.forward;

        // 있으면 열고, 없으면 만든다. 공통 칸(id·이름·설명·종류)을 채운 SerializedObject를 돌려줌 (Apply는 부르는 쪽이)
        private static SerializedObject SaveItem(string path, string id, string displayName, string description, ItemType type, out ItemDefinition item)
        {
            item = AssetDatabase.LoadAssetAtPath<ItemDefinition>(path);
            if (item == null)
            {
                item = ScriptableObject.CreateInstance<ItemDefinition>();
                AssetDatabase.CreateAsset(item, path);
            }
            var so = new SerializedObject(item);
            so.FindProperty("id").stringValue = id;
            so.FindProperty("displayName").stringValue = displayName;
            so.FindProperty("description").stringValue = description;
            so.FindProperty("type").enumValueIndex = (int)type;
            so.FindProperty("isQuestItem").boolValue = false;
            return so;
        }

        // FBX 안에 들어 있는 메시(하위 에셋)를 꺼낸다
        private static Mesh LoadMesh(string path)
        {
            var mesh = AssetDatabase.LoadAllAssetsAtPath(path).OfType<Mesh>().FirstOrDefault();
            if (mesh == null) Debug.LogWarning($"[OutfitSetup] 메시를 찾지 못함: {path}");
            return mesh;
        }

        private static void RegisterInDatabase()
        {
            var db = AssetDatabase.LoadAssetAtPath<ItemDatabase>(DatabasePath);
            if (db == null) { Debug.LogWarning($"[OutfitSetup] {DatabasePath} 없음 — ItemDatabase 등록 건너뜀"); return; }
            var so = new SerializedObject(db);
            var list = so.FindProperty("items");
            foreach (var guid in AssetDatabase.FindAssets("t:ItemDefinition"))
            {
                var item = AssetDatabase.LoadAssetAtPath<ItemDefinition>(AssetDatabase.GUIDToAssetPath(guid));
                if (db.All.Contains(item)) continue;
                list.arraySize++;
                list.GetArrayElementAtIndex(list.arraySize - 1).objectReferenceValue = item;
            }
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void AddStartingItems(Inventory inventory, IEnumerable<ItemDefinition> items)
        {
            var so = new SerializedObject(inventory);
            var list = so.FindProperty("startingItems");
            foreach (var item in items)
            {
                bool has = false;
                for (int i = 0; i < list.arraySize; i++)
                    if (list.GetArrayElementAtIndex(i).FindPropertyRelative("item").objectReferenceValue == item) has = true;
                if (has) continue;
                list.arraySize++;
                var el = list.GetArrayElementAtIndex(list.arraySize - 1);
                el.FindPropertyRelative("item").objectReferenceValue = item;
                el.FindPropertyRelative("count").intValue = 1;
            }
            so.ApplyModifiedProperties();
        }

        /* ───────── 3) 시스템·프리팹 ───────── */

        // Inventory와 같은 부모(Systems) 아래에 이름 붙인 오브젝트로 두고 Inventory를 연결
        private static void AddSystem<T>(string name, Inventory inventory) where T : MonoBehaviour
        {
            var system = Object.FindFirstObjectByType<T>(FindObjectsInactive.Include);
            if (system == null)
            {
                var go = new GameObject(name);
                Undo.RegisterCreatedObjectUndo(go, name);
                go.transform.SetParent(inventory.transform.parent, false);
                system = go.AddComponent<T>();
            }
            Wire(system, ("inventory", inventory));
        }

        private static void SetUpPlayerPrefab()
        {
            var root = PrefabUtility.LoadPrefabContents(PlayerPrefabPath);
            try
            {
                var model = FindModelRoot(root.transform);
                if (model == null) Debug.LogError("[OutfitSetup] Player 프리팹에서 'Outerwear' 부위를 가진 모델을 찾지 못함");
                else if (model.GetComponent<OutfitAppearance>() == null) model.gameObject.AddComponent<OutfitAppearance>();

                var hand = FindDeep(root.transform, HandSwordName);
                var back = FindDeep(root.transform, BackSwordName);
                if (hand == null || back == null) Debug.LogError("[OutfitSetup] Player 프리팹에서 손·등 칼을 찾지 못함");
                else
                {
                    // ?? 대신 TryGetComponent: 에디터에서 GetComponent는 "없음"을 진짜 null이 아닌 가짜 객체로 돌려줄 수 있음
                    if (!root.TryGetComponent<WeaponAppearance>(out var weapon)) weapon = root.AddComponent<WeaponAppearance>();
                    WireArray(weapon, "swords", hand, back);
                }
                PrefabUtility.SaveAsPrefabAsset(root, PlayerPrefabPath);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }

        // 부위 오브젝트(Outerwear)의 부모 = 캐릭터 모델 맨 위
        private static Transform FindModelRoot(Transform root) => FindDeep(root, "Outerwear")?.parent;

        private static Transform FindDeep(Transform root, string name) =>
            root.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == name);

        /* ───────── 4) 미리보기 무대 ───────── */

        private static RenderTexture CreateRenderTexture()
        {
            var rt = AssetDatabase.LoadAssetAtPath<RenderTexture>(RenderTexturePath);
            if (rt != null) return rt;
            EnsureFolder(RenderTextureFolder);
            // 미리보기 칸 380×640 비율. 깊이 버퍼 24비트(앞뒤 가리기), 안티에일리어싱 4배
            rt = new RenderTexture(512, 864, 24) { name = "RT_CharacterPreview", antiAliasing = 4 };
            AssetDatabase.CreateAsset(rt, RenderTexturePath);
            return rt;
        }

        private static GameObject BuildStage(int layer, RenderTexture rt)
        {
            var old = GameObject.Find(StageName);
            if (old != null) Undo.DestroyObjectImmediate(old);

            var stage = new GameObject(StageName);
            Undo.RegisterCreatedObjectUndo(stage, StageName);
            stage.transform.position = new Vector3(0f, -500f, 0f);   // 마을에서 멀리 → 메인 카메라·마을 그림자와 섞이지 않음
            stage.layer = layer;

            // 캐릭터: 주인공과 같은 모델 + 숨쉬는 대기 동작 + 옷 입히기
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(CharacterPrefabPath);
            var character = (GameObject)PrefabUtility.InstantiatePrefab(prefab, stage.transform);
            character.name = "PreviewCharacter";
            character.transform.localPosition = Vector3.zero;
            character.transform.localRotation = Quaternion.identity;
            // 커스터마이징 창이 붙인 이동 부품을 뺀다 (NpcGenerator와 같음). 남겨 두면 Play 때 중력으로 카메라 밖으로 떨어지고 WASD에 걷는다
            // 부품끼리 서로 필요로 하므로(RequireComponent) 입력 → 이동 → CharacterController 순서로 지움
            RemoveComponent<ithappy.Creative_Characters_FREE.Controller.MovePlayerInput>(character);
            RemoveComponent<ithappy.Creative_Characters_FREE.Controller.CharacterMover>(character);
            RemoveComponent<CharacterController>(character);

            var animator = character.GetComponentInChildren<Animator>();
            if (animator != null)
            {
                animator.runtimeAnimatorController = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(IdleControllerPath);
                animator.applyRootMotion = false;
            }
            var model = FindModelRoot(character.transform) ?? character.transform;
            if (model.GetComponent<OutfitAppearance>() == null) model.gameObject.AddComponent<OutfitAppearance>();
            AddBackSword(character);
            foreach (var t in character.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = layer;

            // 카메라: 캐릭터 정면에서, 몸 전체 + 위아래 여백이 들어오는 거리
            var camGo = new GameObject("PreviewCamera");
            camGo.transform.SetParent(stage.transform, false);
            camGo.layer = layer;
            var cam = camGo.AddComponent<Camera>();
            cam.fieldOfView = PreviewFov;
            cam.nearClipPlane = 0.1f;
            cam.cullingMask = 1 << layer;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color32(35, 27, 20, 255);
            cam.targetTexture = rt;
            cam.enabled = false;   // InventoryScreen이 열릴 때만 켠다 (AudioListener는 안 붙임 → 두 개 경고 없음)
            FrameCharacter(cam, stage.transform, character);

            // 조명: 미리보기 레이어만 비춘다
            var lightGo = new GameObject("PreviewLight");
            lightGo.transform.SetParent(stage.transform, false);
            lightGo.transform.localPosition = new Vector3(0.8f, 1.6f, 1.8f);
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Point;
            light.range = 6f;
            light.intensity = 3f;
            light.color = new Color32(255, 240, 220, 255);
            light.cullingMask = 1 << layer;

            return stage;
        }

        // 주인공 프리팹의 등 칼을 같은 이름의 뼈에 복사하고 WeaponAppearance로 무기 모양을 따라가게 한다
        private static void AddBackSword(GameObject character)
        {
            var player = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefabPath);
            var source = player != null ? FindDeep(player.transform, BackSwordName) : null;
            var bone = source != null ? FindDeep(character.transform, source.parent.name) : null;
            if (bone == null) { Debug.LogWarning("[OutfitSetup] 미리보기에 등 칼을 붙일 뼈를 찾지 못함 — 칼 없이 진행"); return; }

            var sword = Object.Instantiate(source.gameObject, bone);
            sword.name = BackSwordName;
            sword.transform.localPosition = source.localPosition;
            sword.transform.localRotation = source.localRotation;
            sword.transform.localScale = source.localScale;
            sword.SetActive(true);

            var weapon = character.AddComponent<WeaponAppearance>();
            WireArray(weapon, "swords", sword.transform);
        }

        // 메시 상자로 키를 재서, 위아래 여백(FrameMargin)을 두고 몸 전체가 들어오는 거리에 카메라를 둔다
        private static void FrameCharacter(Camera cam, Transform stage, GameObject character)
        {
            bool any = false;
            var b = new Bounds();
            foreach (var r in character.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                if (r.sharedMesh == null || r.sharedMesh.vertexCount == 0 || !r.gameObject.activeInHierarchy) continue;
                var mb = r.sharedMesh.bounds;
                // 메시 상자의 꼭짓점 8개를 무대 기준 좌표로 옮겨 합친다
                for (int i = 0; i < 8; i++)
                {
                    var corner = mb.center + Vector3.Scale(mb.extents, new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
                    var p = stage.InverseTransformPoint(r.transform.TransformPoint(corner));
                    if (!any) { b = new Bounds(p, Vector3.zero); any = true; }
                    else b.Encapsulate(p);
                }
            }
            if (!any) { b = new Bounds(new Vector3(0f, 0.9f, 0f), new Vector3(0.6f, 1.8f, 0.4f)); }

            float halfTan = Mathf.Tan(PreviewFov * 0.5f * Mathf.Deg2Rad);
            float needH = b.size.y * FrameMargin;
            float needW = b.size.x * FrameMargin;
            float dist = Mathf.Max(needH / 2f / halfTan, needW / 2f / (halfTan * PreviewAspect));

            cam.transform.localPosition = new Vector3(b.center.x, b.center.y, b.center.z + dist);
            cam.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
            cam.farClipPlane = dist + b.size.z + 5f;
            Debug.Log($"[OutfitSetup] 미리보기 캐릭터 키 {b.size.y:0.00}m → 카메라 거리 {dist:0.00}m (여백은 FrameMargin)");
        }

        private static void ExcludeLayerFromMainCamera(int layer)
        {
            var main = Camera.main;
            if (main == null) { Debug.LogWarning("[OutfitSetup] Main Camera를 찾지 못함 — Culling Mask에서 Character Preview를 직접 끄세요"); return; }
            Undo.RecordObject(main, "Culling Mask");
            main.cullingMask &= ~(1 << layer);
        }

        /* ───────── 5) 인벤토리 화면 연결 (Build Game UI가 끝날 때도 부름) ───────── */

        public static void WirePreviewUI()
        {
            var stage = GameObject.Find(StageName);
            var screen = Object.FindFirstObjectByType<InventoryScreen>(FindObjectsInactive.Include);
            if (stage == null || screen == null) return;   // 무대나 화면이 아직 없으면 할 일 없음

            var cam = stage.GetComponentInChildren<Camera>(true);
            var character = stage.transform.Find("PreviewCharacter");
            var image = screen.GetComponentsInChildren<RawImage>(true).FirstOrDefault(r => r.name == "PreviewImage");
            if (image == null) { Debug.LogWarning("[OutfitSetup] PreviewImage가 없음 — Build Game UI를 먼저 실행하세요"); return; }

            image.texture = cam != null ? cam.targetTexture : null;
            EditorUtility.SetDirty(image);
            if (!image.TryGetComponent<CharacterPreviewDrag>(out var drag)) drag = image.gameObject.AddComponent<CharacterPreviewDrag>();
            Wire(drag, ("target", character));
            Wire(screen, ("previewCamera", cam));
        }

        /* ───────── 도우미 ───────── */

        private static void Wire(Object target, params (string field, Object value)[] pairs)
        {
            var so = new SerializedObject(target);
            foreach (var (field, value) in pairs)
            {
                var p = so.FindProperty(field);
                if (p == null) { Debug.LogError($"[OutfitSetup] {target.GetType().Name}에 '{field}' 칸이 없음"); continue; }
                p.objectReferenceValue = value;
            }
            so.ApplyModifiedProperties();
        }

        private static void WireArray(Object target, string field, params Object[] values)
        {
            var so = new SerializedObject(target);
            var p = so.FindProperty(field);
            p.arraySize = values.Length;
            for (int i = 0; i < values.Length; i++) p.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void RemoveComponent<T>(GameObject go) where T : Component
        {
            if (go.TryGetComponent<T>(out var c)) Object.DestroyImmediate(c);
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            int slash = path.LastIndexOf('/');
            EnsureFolder(path.Substring(0, slash));
            AssetDatabase.CreateFolder(path.Substring(0, slash), path.Substring(slash + 1));
        }
    }
}
