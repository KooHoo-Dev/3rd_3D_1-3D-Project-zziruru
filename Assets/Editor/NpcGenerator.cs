using System.Collections.Generic;
using System.IO;
using System.Linq;
using ithappy.Creative_Characters_FREE.CharacterCustomizationTool.Editor.Character;
using ithappy.Creative_Characters_FREE.CharacterCustomizationTool.Editor.FaceEditor;
using ithappy.Creative_Characters_FREE.CharacterCustomizationTool.Editor.MaterialManagement;
using SesiVillage.Interaction;
using SesiVillage.NPC;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace SesiVillage.EditorTools
{
    /// <summary>
    /// 1차 범위 NPC를 한 번에 만든다. 메뉴: SesiVillage → NPC 만들기·갱신
    ///  1) NPC_Idle Animator를 대기 동작 3개(숨쉬기·둘러보기·편히 서기)로 다시 만든다 (전환은 NpcIdleVariety가 함)
    ///  2) NPC 프리팹이 없으면 Creative Characters 커스터마이징의 Randomize로 모습을 뽑아 만든다 (있으면 모습은 그대로)
    ///  3) 모든 NPC 프리팹에 같은 부품을 맞춘다: 대기 동작, 대화(NpcInteractable), 자리 찾기(NpcPlacement), 트리거, Interactable 레이어
    ///  4) Village_GamePlay 씬에 없으면 놓고, Village 씬의 NpcSpot 위치로 옮긴 뒤 저장한다
    /// 모습을 다시 뽑고 싶으면 그 NPC 프리팹을 지우고 다시 실행한다.
    /// </summary>
    public static class NpcGenerator
    {
        private const string PrefabFolder = "Assets/Prefabs/NPC";
        private const string IdleControllerPath = "Assets/Animations/NPC/NPC_Idle.controller";
        private const string IdleClipFolder = "Assets/External Assets/ithappy/Creative_Characters_FREE/Animations/Other_Animations/";
        private const string GameplayScenePath = "Assets/Scenes/Village_GamePlay.unity";
        private const string VillageScenePath = "Assets/Scenes/Village.unity";

        private static readonly string[] IdleStates = { "Idle_Breathing", "Idle_Look_Around", "Idle_Relaxed" };

        // 마을에 어울리지 않는 파츠 (탈 인형, 광대 코, 젖꼭지, 헤드폰) → 뽑히면 다시 뽑는다
        private static readonly string[] BannedMeshKeywords = { "Mascot", "Costume", "Clown", "Pacifier", "Headphones" };

        private struct NpcDef
        {
            public string prefabName, npcId, displayName, spotId;
            public NpcDef(string prefabName, string npcId, string displayName, string spotId)
            {
                this.prefabName = prefabName; this.npcId = npcId; this.displayName = displayName; this.spotId = spotId;
            }
        }

        // 1차 범위(김장절·동지·섣달·설날) NPC. npcId는 퀘스트의 Giver Npc Id·Yarn 노드 이름, spotId는 VillageBuilder의 NpcSpot ID
        private static readonly NpcDef[] Npcs =
        {
            new NpcDef("NPC_Chief",          "chief",         "촌장",   "chief"),
            new NpcDef("NPC_Mokwonji",       "mokwonji",      "목원지", "chief_wife"),
            new NpcDef("NPC_Chilseongdang",  "chilseongdang", "칠성당", "shrine"),
            new NpcDef("NPC_Jeongeoeon",     "jeongeoeon",    "정어언", "seollal"),
            new NpcDef("NPC_Odanmi",         "odanmi",        "오단미", "dano"),
            new NpcDef("NPC_Baeknayeon",     "baeknayeon",    "백나연", "gimjang"),
            new NpcDef("NPC_Baeksuin",       "baeksuin",      "백수인", "dongji"),
            new NpcDef("NPC_Baekjuyeon",     "baekjuyeon",    "백주연", "chilseok"),
            new NpcDef("NPC_Oseonneung",     "oseonneung",    "오선릉", "seotdal"),
            new NpcDef("NPC_Baeknamsin",     "baeknamsin",    "백남신", "junggu"),
        };

        [MenuItem("SesiVillage/NPC 만들기·갱신")]
        public static void Generate()
        {
            // 씬을 열고 저장하므로, 저장하지 않은 변경이 있으면 먼저 물어본다
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            Directory.CreateDirectory(PrefabFolder);
            var controller = BuildIdleController();

            var prefabs = new List<(GameObject prefab, NpcDef def)>();
            foreach (var def in Npcs)
            {
                string path = $"{PrefabFolder}/{def.prefabName}.prefab";
                if (!File.Exists(path)) CreateRandomCharacterPrefab(path);
                ConfigurePrefab(path, def, controller);
                prefabs.Add((AssetDatabase.LoadAssetAtPath<GameObject>(path), def));
            }

            PlaceInGameplayScene(prefabs);
            AssetDatabase.SaveAssets();
            Debug.Log($"[NpcGenerator] NPC {prefabs.Count}명 만들기·갱신 완료");
        }

        /* ───────── 1) 대기 Animator ───────── */

        // 상태 3개만 두고 화살표는 없다. 언제 무엇으로 넘어갈지는 NpcIdleVariety가 정한다
        private static AnimatorController BuildIdleController()
        {
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(IdleControllerPath)
                             ?? AnimatorController.CreateAnimatorControllerAtPath(IdleControllerPath);
            var stateMachine = controller.layers[0].stateMachine;
            foreach (var child in stateMachine.states) stateMachine.RemoveState(child.state);

            for (int i = 0; i < IdleStates.Length; i++)
            {
                var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(IdleClipFolder + IdleStates[i] + ".anim");
                if (clip == null) { Debug.LogError($"[NpcGenerator] 클립 없음: {IdleStates[i]}"); continue; }
                var state = stateMachine.AddState(IdleStates[i], new Vector3(300f, 60f * i, 0f));
                state.motion = clip;
                if (i == 0) stateMachine.defaultState = state;
            }
            EditorUtility.SetDirty(controller);
            return controller;
        }

        /* ───────── 2) 무작위 모습 ───────── */

        // 커스터마이징 창의 Randomize + Save Prefab과 같은 일을 한다 (이동 컴포넌트는 붙이지 않음)
        private static void CreateRandomCharacterPrefab(string path)
        {
            var customizable = new CustomizableCharacter(SlotLibraryLoader.LoadSlotLibrary());
            for (int attempt = 0; attempt < 50; attempt++)
            {
                customizable.Randomize();
                if (!UsesBannedPart(customizable)) break;
            }

            var character = customizable.InstantiateCharacter();
            var material = new MaterialProvider().MainColor;
            foreach (var slot in customizable.Slots.Where(s => s.IsEnabled))
            {
                foreach (var (type, mesh) in slot.Meshes)
                {
                    var child = character.transform.Cast<Transform>().First(t => t.name.StartsWith(type.ToString()));
                    if (!child.TryGetComponent<SkinnedMeshRenderer>(out var renderer)) continue;
                    renderer.sharedMesh = mesh;
                    renderer.sharedMaterial = material;
                    renderer.localBounds = mesh.bounds;
                }
            }
            FaceLoader.AddFaces(character);

            PrefabUtility.SaveAsPrefabAsset(character, path);
            Object.DestroyImmediate(character);
        }

        private static bool UsesBannedPart(CustomizableCharacter customizable)
        {
            return customizable.Slots
                .Where(s => s.IsEnabled)
                .SelectMany(s => s.Meshes)
                .Any(m => m.Item2 != null && BannedMeshKeywords.Any(k => m.Item2.name.Contains(k)));
        }

        /* ───────── 3) 부품 맞추기 ───────── */

        private static void ConfigurePrefab(string path, NpcDef def, AnimatorController controller)
        {
            var root = PrefabUtility.LoadPrefabContents(path);
            root.name = def.prefabName;
            root.layer = LayerMask.NameToLayer("Interactable");

            var animator = root.GetComponent<Animator>();
            animator.runtimeAnimatorController = controller;
            animator.applyRootMotion = false;

            // 커스터마이징 창이 붙이는 이동 부품과 이전 대기 스크립트(NpcIdleOffset, 삭제됨)는 뺀다
            RemoveComponent<CharacterController>(root);
            RemoveComponent<ithappy.Creative_Characters_FREE.Controller.CharacterMover>(root);
            RemoveComponent<ithappy.Creative_Characters_FREE.Controller.MovePlayerInput>(root);
            GameObjectUtility.RemoveMonoBehavioursWithMissingScript(root);   // 지운 NpcIdleOffset 자리
            GetOrAdd<NpcIdleVariety>(root);

            // E 상호작용 판정용 트리거 (PlayerInteractor가 Interactable 레이어의 트리거도 찾음)
            var capsule = GetOrAdd<CapsuleCollider>(root);
            capsule.isTrigger = true;
            capsule.center = new Vector3(0f, 0.9f, 0f);
            capsule.height = 1.8f;
            capsule.radius = 0.4f;

            var interactable = new SerializedObject(GetOrAdd<NpcInteractable>(root));
            interactable.FindProperty("npcId").stringValue = def.npcId;
            interactable.FindProperty("displayName").stringValue = def.displayName;
            interactable.ApplyModifiedPropertiesWithoutUndo();

            var placement = new SerializedObject(GetOrAdd<NpcPlacement>(root));
            placement.FindProperty("spotId").stringValue = def.spotId;
            placement.ApplyModifiedPropertiesWithoutUndo();

            PrefabUtility.SaveAsPrefabAsset(root, path);
            PrefabUtility.UnloadPrefabContents(root);
        }

        private static T GetOrAdd<T>(GameObject go) where T : Component
        {
            return go.TryGetComponent<T>(out var c) ? c : go.AddComponent<T>();
        }

        private static void RemoveComponent<T>(GameObject go) where T : Component
        {
            if (go.TryGetComponent<T>(out var c)) Object.DestroyImmediate(c, true);
        }

        /* ───────── 4) 씬에 놓기 ───────── */

        private static void PlaceInGameplayScene(List<(GameObject prefab, NpcDef def)> prefabs)
        {
            // 열려 있지 않은 씬은 잠깐 열었다가 끝나면 닫는다 (자리를 찾으려면 Village도 열려 있어야 함)
            var gameplay = OpenAdditive(GameplayScenePath, out bool openedGameplay);
            OpenAdditive(VillageScenePath, out bool openedVillage);
            var village = SceneManager.GetSceneByPath(VillageScenePath);

            var existing = gameplay.GetRootGameObjects()
                .Where(go => PrefabUtility.IsAnyPrefabInstanceRoot(go))
                .GroupBy(go => PrefabUtility.GetCorrespondingObjectFromSource(go))   // 같은 NPC가 둘 놓여 있어도 첫 번째만
                .ToDictionary(g => g.Key, g => g.First());

            var missingSpots = new List<string>();
            foreach (var (prefab, def) in prefabs)
            {
                if (!existing.TryGetValue(prefab, out var instance))
                    instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, gameplay);
                if (!instance.GetComponent<NpcPlacement>().SnapToSpot()) missingSpots.Add(def.spotId);
            }

            EditorSceneManager.MarkSceneDirty(gameplay);
            EditorSceneManager.SaveScene(gameplay);

            if (openedVillage) EditorSceneManager.CloseScene(village, true);
            if (openedGameplay) EditorSceneManager.CloseScene(gameplay, true);

            if (missingSpots.Count > 0)
                Debug.LogWarning("[NpcGenerator] 자리를 못 찾은 NPC: " + string.Join(", ", missingSpots) +
                                 " → Moonlit Village/Build Scene으로 맵을 다시 만든 뒤 이 메뉴를 다시 실행하세요");
        }

        private static Scene OpenAdditive(string path, out bool opened)
        {
            var scene = SceneManager.GetSceneByPath(path);
            opened = !scene.isLoaded;
            return opened ? EditorSceneManager.OpenScene(path, OpenSceneMode.Additive) : scene;
        }
    }
}
