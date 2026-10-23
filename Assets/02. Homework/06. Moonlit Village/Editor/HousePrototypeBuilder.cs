using System.Collections.Generic;
using System.IO;
using SesiVillage.Core;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace MoonlitVillage.EditorTools
{
    /// <summary>
    /// 실내 공간감 시험용 (2026-10-22): 설날집(초가집)을 크기별로 나란히 지어 비교한다.
    /// 젤다 방식(지붕 유지 + 건물을 실제보다 크게 + 카메라가 천장 아래로)을 정하기 전에 느낌을 보기 위한 도구.
    /// - Village 씬과 기존 머티리얼은 건드리지 않는다 (머티리얼은 읽기만 하고, 지붕 메시는 Generated/Prototype에 따로 저장)
    /// - 방 넓이·깊이·벽 높이만 배율로 키우고, 기단·계단·창 높이·가구는 사람 크기 그대로 둔다 ("거인 세계" 느낌 방지)
    /// - 천장에 콜라이더를 넣어 탐험 카메라(Deoccluder)가 천장 아래에 머물게 한다
    /// </summary>
    public static class HousePrototypeBuilder
    {
        const string ScenePath = "Assets/Scenes/Test_HousePrototype.unity";
        const string GenDir = "Assets/02. Homework/06. Moonlit Village/Generated";
        const string MeshDir = GenDir + "/Prototype";

        // ── 비교할 크기: 이 표의 숫자만 바꾸고 메뉴를 다시 누르면 된다 ──
        // roomScale = 방 넓이·깊이·벽 높이 배율 (원래 설날집 9.5 × 6m, 벽 2.8m)
        // doorW / doorH = 문 너비·높이 (캐릭터 키 1.85m 기준. 카메라가 같이 통과하므로 몸보다 조금 크게)
        struct Variant
        {
            public string label; public float roomScale, doorW, doorH;
            public Variant(string label, float roomScale, float doorW, float doorH)
            { this.label = label; this.roomScale = roomScale; this.doorW = doorW; this.doorH = doorH; }
        }
        static readonly Variant[] Variants =
        {
            // 2026-10-23: 1차 비교(1.0 / 1.3 / 1.5)에서 1.5가 가장 좋고 더 커도 된다는 의견 → 1.5 이상으로 2차 비교
            new Variant("x1.5", 1.5f, 2.0f, 2.6f),
            new Variant("x1.7", 1.7f, 2.2f, 2.7f),
            new Variant("x2.0", 2.0f, 2.4f, 2.8f),
        };

        const float BaseW = 9.5f, BaseD = 6f, BaseWallH = 2.8f, BaseRoofH = 2.3f;
        const float T = 0.3f, PlinthH = 0.35f;   // 벽 두께, 기단 높이 (배율 미적용)
        const float HouseGap = 10f;               // 집 사이 간격

        class Mats { public Material plinth, plank, plaster, darkWood, paperWindow, thatch, jar, fence; }

        [MenuItem("Moonlit Village/Build House Prototype (실내 크기 비교)")]
        public static void Build()
        {
            if (File.Exists(ScenePath) &&
                !EditorUtility.DisplayDialog("실내 크기 비교", "시험 씬을 다시 만듭니다. (" + ScenePath + ")\nVillage 씬은 바뀌지 않습니다.", "확인", "취소"))
                return;
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            var M = LoadMaterials();
            if (M == null) return;
            EnsureFolder(MeshDir);

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            BuildLighting();

            // 집을 왼쪽부터 x축으로 나란히 놓는다 (정면 = -z)
            var root = new GameObject("Houses").transform;
            float left = 0f, maxD = 0f;
            for (int i = 0; i < Variants.Length; i++)
            {
                float w = BaseW * Variants[i].roomScale;
                BuildHouse(M, root, Variants[i], left + w / 2f, i);
                left += w + HouseGap;
                maxD = Mathf.Max(maxD, BaseD * Variants[i].roomScale);
            }
            float totalW = left - HouseGap;
            float centerX = totalW / 2f;

            // 바닥 (윗면 y = 0)
            Prim(PrimitiveType.Cube, "Ground", null, new Vector3(centerX, -0.5f, 0f), new Vector3(totalW + 60f, 1f, maxD + 60f), M.fence);

            // 시작 위치: 집들 앞쪽에서 집을 바라본다 (+Z)
            var spawn = new GameObject("PlayerSpawnPoint");
            spawn.transform.position = new Vector3(centerX, 0f, -maxD / 2f - 10f);
            spawn.AddComponent<PlayerSpawnPoint>();

            AssignCollisionLayers();
            EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.SaveAssets();
            Debug.Log("[HousePrototype] 생성 완료: " + ScenePath +
                      "\n테스트: Build Profiles 씬 목록에 추가 → Core 씬의 GameBootstrap > Village Scene Name을 Test_HousePrototype으로 바꾸고 Play (끝나면 Village로 되돌릴 것)");
        }

        static void BuildHouse(Mats M, Transform parent, Variant v, float cx, int index)
        {
            float W = BaseW * v.roomScale, D = BaseD * v.roomScale, WallH = BaseWallH * v.roomScale;
            var root = new GameObject("House " + v.label).transform;
            root.SetParent(parent);
            root.position = new Vector3(cx, 0f, 0f);

            float floorY = PlinthH;
            float frontZ = -D / 2f, backZ = D / 2f;

            // 기단·마루·계단 (계단·기단 높이는 사람 크기 그대로)
            Prim(PrimitiveType.Cube, "Floor", root, new Vector3(cx, floorY - PlinthH / 2f, 0f), new Vector3(W + 0.8f, PlinthH, D + 0.8f), M.plinth);
            Prim(PrimitiveType.Cube, "FloorBoards", root, new Vector3(cx, floorY + 0.02f, 0f), new Vector3(W - T, 0.04f, D - T), M.plank, false);
            Prim(PrimitiveType.Cube, "Step", root, new Vector3(cx, 0.12f, frontZ - 0.9f), new Vector3(2.4f, 0.24f, 1.0f), M.plinth);

            // 벽: 앞(문), 뒤, 양옆
            WallX(root, "Wall_Front", cx, frontZ, W, floorY, WallH, M.plaster, v.doorW, v.doorH);
            WallX(root, "Wall_Back", cx, backZ, W, floorY, WallH, M.plaster);
            foreach (int sgn in new[] { -1, 1 })
                Prim(PrimitiveType.Cube, "Wall_Side", root, new Vector3(cx + sgn * W / 2f, floorY + WallH / 2f, 0f), new Vector3(T, WallH, D), M.plaster);

            // 기둥·문틀·창 (장식, 충돌 없음). 창은 바닥에서 같은 높이(사람 기준)
            float wallY = floorY + WallH / 2f;
            foreach (int sgn in new[] { -1, 1 })
            {
                Prim(PrimitiveType.Cube, "CornerPost", root, new Vector3(cx + sgn * W / 2f, wallY, frontZ - 0.12f), new Vector3(0.3f, WallH, 0.3f), M.darkWood, false);
                Prim(PrimitiveType.Cube, "CornerPostBack", root, new Vector3(cx + sgn * W / 2f, wallY, backZ + 0.12f), new Vector3(0.3f, WallH, 0.3f), M.darkWood, false);
                Prim(PrimitiveType.Cube, "DoorFrame", root, new Vector3(cx + sgn * (v.doorW / 2f + 0.08f), floorY + v.doorH / 2f, frontZ - 0.18f), new Vector3(0.16f, v.doorH, 0.12f), M.darkWood, false);
                Prim(PrimitiveType.Cube, "PaperWindow", root, new Vector3(cx + sgn * W * 0.32f, floorY + 1.55f, frontZ - 0.17f), new Vector3(1.6f, 1.1f, 0.04f), M.paperWindow, false);
                Prim(PrimitiveType.Cube, "PaperWindow_Inside", root, new Vector3(cx + sgn * W * 0.32f, floorY + 1.55f, frontZ + 0.17f), new Vector3(1.6f, 1.1f, 0.04f), M.paperWindow, false);
            }
            Prim(PrimitiveType.Cube, "Lintel", root, new Vector3(cx, floorY + v.doorH + 0.1f, frontZ - 0.18f), new Vector3(v.doorW + 0.6f, 0.2f, 0.16f), M.darkWood, false);

            // 지붕 (충돌 없음): 지붕 높이만 집 크기에 맞춰 키워서 납작해 보이지 않게
            float roofBase = floorY + WallH;
            float overhang = 1.3f;
            float rw = W + overhang * 2f + 0.4f, rd = D + overhang * 2f + 0.4f, rh = BaseRoofH * v.roomScale;
            // 처마 판: 벽 바깥으로 튀어나온 부분에만 (집 전체를 덮는 판이면 안에서 평평한 천장처럼 보임)
            Prim(PrimitiveType.Cube, "EaveBoard_Front", root, new Vector3(cx, roofBase + 0.23f, -D / 2f - overhang / 2f), new Vector3(W + overhang * 2f, 0.16f, overhang), M.darkWood, false);
            Prim(PrimitiveType.Cube, "EaveBoard_Back", root, new Vector3(cx, roofBase + 0.23f, D / 2f + overhang / 2f), new Vector3(W + overhang * 2f, 0.16f, overhang), M.darkWood, false);
            foreach (int sgn in new[] { -1, 1 })
                Prim(PrimitiveType.Cube, "EaveBoard_Side", root, new Vector3(cx + sgn * (W / 2f + overhang / 2f), roofBase + 0.23f, 0f), new Vector3(overhang, 0.16f, D), M.darkWood, false);
            // 바깥 지붕은 아랫면 없이 (아랫면이 있으면 방 안에서 평평한 천장처럼 보임)
            var roofMesh = HipRoofMesh(rw, rd, rh, "Proto_" + index + "_Thatch", false, false);
            var roof = new GameObject("ThatchRoof");
            roof.transform.SetParent(root);
            roof.transform.position = new Vector3(cx, roofBase + 0.31f, 0f);
            roof.AddComponent<MeshFilter>().sharedMesh = roofMesh;
            roof.AddComponent<MeshRenderer>().sharedMaterial = M.thatch;

            BuildRoofInterior(M, root, cx, roofBase, W, D, rh * D / rd, index);

            // 실내 소품: 크기는 사람 기준 그대로, 위치만 벽 쪽으로 (가운데는 비워 둠)
            float inW = W / 2f - T, inD = D / 2f - T;
            Prim(PrimitiveType.Cube, "LowTable", root, new Vector3(cx - inW + 1.2f, floorY + 0.25f, inD - 1.0f), new Vector3(1.4f, 0.5f, 0.9f), M.plank);
            Prim(PrimitiveType.Cube, "Cabinet", root, new Vector3(cx, floorY + 0.8f, inD - 0.3f), new Vector3(1.4f, 1.6f, 0.5f), M.darkWood);
            Prim(PrimitiveType.Cube, "SleepingMat", root, new Vector3(cx + inW - 1.2f, floorY + 0.05f, inD - 1.2f), new Vector3(1.0f, 0.1f, 2.0f), M.paperWindow);
            for (int i = 0; i < 3; i++)
                Prim(PrimitiveType.Sphere, "Jar", root, new Vector3(cx + inW - 0.6f, floorY + 0.5f, -inD + 0.8f + i * 1.1f), new Vector3(0.85f, 0.95f, 0.85f), M.jar);

            // 실내 등: 방이 커진 만큼 범위도 키움
            var lightGo = new GameObject("PointLight_House");
            lightGo.transform.SetParent(root);
            lightGo.transform.position = new Vector3(cx, floorY + WallH * 0.8f, 0f);
            var l = lightGo.AddComponent<Light>();
            l.type = LightType.Point; l.color = new Color(1f, 0.64f, 0.36f); l.intensity = 4f; l.range = 7f * v.roomScale; l.shadows = LightShadows.None;

            // 크기 표시 (문 위 글자, 게임 화면에도 보임)
            var label = new GameObject("Label " + v.label);
            label.transform.SetParent(root);
            label.transform.position = new Vector3(cx, roofBase + rh + 1.2f, frontZ);
            // 기본 폰트에 한글이 없을 수 있어서 영문·숫자로만 표시
            var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            var tm = label.AddComponent<TextMesh>();
            tm.font = font;
            label.GetComponent<MeshRenderer>().sharedMaterial = font.material;
            tm.text = "x" + v.roomScale.ToString("0.0") + "\nroom " + W.ToString("0.0") + " x " + D.ToString("0.0") + "m, wall " + WallH.ToString("0.0") + "m\ndoor " + v.doorW.ToString("0.0") + " x " + v.doorH.ToString("0.0") + "m";
            tm.anchor = TextAnchor.LowerCenter; tm.alignment = TextAlignment.Center;
            tm.characterSize = 0.12f; tm.fontSize = 48; tm.color = Color.white;
        }

        // 지붕 안쪽 (2026-10-23): 평평한 천장 대신 바깥 지붕 모양(모임지붕)을 안에서도 보이게 하고 나무 뼈대를 드러낸다.
        // - 안쪽 지붕면: 바깥 지붕과 같은 기울기로 벽 위에 얹은 모임지붕 (면이 안쪽을 향함). 콜라이더가 있어 카메라가 지붕면 아래에 머문다
        // - 뼈대(충돌 없음): 벽 위 도리, 가로지르는 보, 동자주(짧은 기둥), 꼭대기 종도리, 서까래, 모서리 추녀
        static void BuildRoofInterior(Mats M, Transform root, float cx, float baseY, float W, float D, float h, int index)
        {
            var group = new GameObject("RoofInterior").transform;
            group.SetParent(root);

            // 안쪽 지붕면 (판자). SuperCharacterController가 MeshCollider를 검사하려면 BSPTree가 필요하다 (TechNotes 6-6)
            var inner = HipRoofMesh(W, D, h, "Proto_" + index + "_RoofInner", true, false);
            var surf = new GameObject("RoofInner");
            surf.transform.SetParent(group);
            surf.transform.position = new Vector3(cx, baseY, 0f);
            surf.AddComponent<MeshFilter>().sharedMesh = inner;
            surf.AddComponent<MeshRenderer>().sharedMaterial = M.plank;
            surf.AddComponent<MeshCollider>().sharedMesh = inner;
            surf.AddComponent<BSPTree>();

            float hw = W / 2f, hd = D / 2f, hr = Mathf.Max(0.5f, hw - hd);  // hr = 종도리(꼭대기) 길이의 절반
            float topY = baseY + h;
            const float drop = 0.12f;  // 뼈대를 지붕면보다 살짝 아래에 둬서 판자에 묻히지 않게

            // 벽 위 도리 (네 벽을 따라)
            Beam(group, "Plate", new Vector3(cx - hw, baseY, -hd), new Vector3(cx + hw, baseY, -hd), 0.28f, M.darkWood);
            Beam(group, "Plate", new Vector3(cx - hw, baseY, hd), new Vector3(cx + hw, baseY, hd), 0.28f, M.darkWood);
            Beam(group, "Plate", new Vector3(cx - hw, baseY, -hd), new Vector3(cx - hw, baseY, hd), 0.28f, M.darkWood);
            Beam(group, "Plate", new Vector3(cx + hw, baseY, -hd), new Vector3(cx + hw, baseY, hd), 0.28f, M.darkWood);

            // 꼭대기 종도리
            Beam(group, "RidgeBeam", new Vector3(cx - hr, topY - drop, 0f), new Vector3(cx + hr, topY - drop, 0f), 0.3f, M.darkWood);

            // 보 + 동자주: 앞뒤 벽을 잇는 보를 약 3m 간격으로, 종도리 구간에는 보 위에 동자주를 세워 종도리를 받친다
            int beamCount = Mathf.Max(2, Mathf.RoundToInt(W / 3f));
            for (int i = 1; i < beamCount; i++)
            {
                float x = cx - hw + W * i / beamCount;
                Beam(group, "TieBeam", new Vector3(x, baseY + 0.2f, -hd), new Vector3(x, baseY + 0.2f, hd), 0.32f, M.darkWood);
                if (Mathf.Abs(x - cx) <= hr)
                    Beam(group, "KingPost", new Vector3(x, baseY + 0.2f, 0f), new Vector3(x, topY - drop, 0f), 0.22f, M.darkWood);
            }

            // 서까래: 앞뒤 경사면을 따라 약 0.9m 간격 (종도리 구간)
            int rafterCount = Mathf.Max(2, Mathf.RoundToInt(hr * 2f / 0.9f));
            for (int i = 0; i <= rafterCount; i++)
            {
                float x = cx - hr + hr * 2f * i / rafterCount;
                foreach (int sgn in new[] { -1, 1 })
                    Beam(group, "Rafter", new Vector3(x, baseY - drop, sgn * hd), new Vector3(x, topY - drop, 0f), 0.12f, M.darkWood);
            }
            // 양옆 삼각형 면의 서까래 (종도리 끝으로 모임)
            foreach (int sx in new[] { -1, 1 })
            {
                int sideCount = Mathf.Max(2, Mathf.RoundToInt(D / 0.9f));
                for (int i = 1; i < sideCount; i++)
                {
                    float z = -hd + D * i / sideCount;
                    Beam(group, "Rafter_Side", new Vector3(cx + sx * hw, baseY - drop, z), new Vector3(cx + sx * hr, topY - drop, 0f), 0.12f, M.darkWood);
                }
                // 모서리 추녀 (네 모서리에서 종도리 끝으로)
                foreach (int sz in new[] { -1, 1 })
                    Beam(group, "HipRafter", new Vector3(cx + sx * hw, baseY - drop, sz * hd), new Vector3(cx + sx * hr, topY - drop, 0f), 0.2f, M.darkWood);
            }
        }

        // a에서 b까지 이어지는 각재 (충돌 없음)
        static void Beam(Transform root, string name, Vector3 a, Vector3 b, float thickness, Material m)
        {
            Vector3 dir = b - a;
            var g = Prim(PrimitiveType.Cube, name, root, (a + b) / 2f, new Vector3(thickness, thickness, dir.magnitude), m, false);
            // 거의 수직이면 LookRotation의 위쪽 기준을 바꿔서 회전이 뒤틀리지 않게
            Vector3 up = Mathf.Abs(Vector3.Dot(dir.normalized, Vector3.up)) > 0.99f ? Vector3.forward : Vector3.up;
            g.transform.rotation = Quaternion.LookRotation(dir, up);
        }

        // ── 도구 ────────────────────────────────────────────────

        static Mats LoadMaterials()
        {
            var M = new Mats
            {
                plinth = LoadMat("House_Plinth"), plank = LoadMat("Wood_Plank"), plaster = LoadMat("Wall_Plaster"),
                darkWood = LoadMat("Wood_Dark"), paperWindow = LoadMat("Window_Paper"), thatch = LoadMat("Thatch_Yellow"),
                jar = LoadMat("Clay_Jar"), fence = LoadMat("House_FenceStone"),
            };
            if (M.plinth == null || M.plank == null || M.plaster == null || M.darkWood == null ||
                M.paperWindow == null || M.thatch == null || M.jar == null || M.fence == null)
            {
                Debug.LogError("[HousePrototype] Generated 폴더의 머티리얼을 찾지 못했습니다: " + GenDir);
                return null;
            }
            return M;
        }

        static Material LoadMat(string name) => AssetDatabase.LoadAssetAtPath<Material>(GenDir + "/M_" + name + ".mat");

        static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            AssetDatabase.CreateFolder(Path.GetDirectoryName(path).Replace('\\', '/'), Path.GetFileName(path));
        }

        static void BuildLighting()
        {
            // 크기 비교가 목적이라 밝은 저녁 빛으로 (Village의 밤 조명과는 다름)
            var sun = new GameObject("Directional Light").AddComponent<Light>();
            sun.type = LightType.Directional; sun.intensity = 1.1f; sun.color = new Color(1f, 0.93f, 0.82f); sun.shadows = LightShadows.Soft;
            sun.transform.rotation = Quaternion.Euler(45f, -35f, 0f);
            RenderSettings.skybox = AssetDatabase.GetBuiltinExtraResource<Material>("Default-Skybox.mat");
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.42f, 0.44f, 0.50f);
        }

        static GameObject Prim(PrimitiveType type, string name, Transform parent, Vector3 pos, Vector3 scale, Material m, bool collider = true)
        {
            var g = GameObject.CreatePrimitive(type);
            g.name = name;
            if (parent != null) g.transform.SetParent(parent);
            g.transform.position = pos;
            g.transform.localScale = scale;
            g.GetComponent<MeshRenderer>().sharedMaterial = m;
            if (!collider) Object.DestroyImmediate(g.GetComponent<Collider>());
            return g;
        }

        // x 방향 벽. gapW > 0이면 가운데에 문 구멍을 남긴다.
        static void WallX(Transform root, string name, float cx, float z, float width, float floorY, float h, Material m, float gapW = 0f, float gapH = 0f)
        {
            float wallY = floorY + h / 2f;
            if (gapW <= 0f)
            {
                Prim(PrimitiveType.Cube, name, root, new Vector3(cx, wallY, z), new Vector3(width, h, T), m);
                return;
            }
            float sideW = (width - gapW) / 2f;
            Prim(PrimitiveType.Cube, name + "_L", root, new Vector3(cx - gapW / 2f - sideW / 2f, wallY, z), new Vector3(sideW, h, T), m);
            Prim(PrimitiveType.Cube, name + "_R", root, new Vector3(cx + gapW / 2f + sideW / 2f, wallY, z), new Vector3(sideW, h, T), m);
            Prim(PrimitiveType.Cube, name + "_Top", root, new Vector3(cx, floorY + gapH + (h - gapH) / 2f, z), new Vector3(gapW, h - gapH, T), m);
        }

        // 모임지붕(초가 지붕) 메시. VillageBuilder.HipRoof와 같은 모양.
        // inward = false: 바깥 지붕 (면이 바깥을 향함) / true: 안쪽 지붕면 (면이 방 안을 향함)
        // withBottom = 아랫면(바닥 판)을 넣을지
        static Mesh HipRoofMesh(float width, float depth, float h, string name, bool inward = false, bool withBottom = true)
        {
            float sign = inward ? -1f : 1f;
            float hw = width / 2f, hd = depth / 2f, hr = Mathf.Max(0.5f, hw - hd);
            Vector3 A = new Vector3(-hw, 0, -hd), B = new Vector3(hw, 0, -hd), C = new Vector3(hw, 0, hd), D = new Vector3(-hw, 0, hd);
            Vector3 R0 = new Vector3(-hr, h, 0), R1 = new Vector3(hr, h, 0);
            var v = new List<Vector3>(); var n = new List<Vector3>(); var uv = new List<Vector2>(); var t = new List<int>();
            void Tri(Vector3 a, Vector3 b, Vector3 c, Vector3 want)
            {
                Vector3 nn = Vector3.Cross(b - a, c - a).normalized;
                if (Vector3.Dot(nn, want) < 0f) { var tmp = b; b = c; c = tmp; nn = -nn; }
                int i = v.Count;
                foreach (var p in new[] { a, b, c }) { v.Add(p); n.Add(nn); uv.Add(Mathf.Abs(nn.y) > 0.5f ? new Vector2(p.x, p.z) * 0.2f : new Vector2(p.x + p.z, p.y) * 0.2f); }
                t.Add(i); t.Add(i + 1); t.Add(i + 2);
            }
            void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector3 want) { Tri(a, b, c, want); Tri(a, c, d, want); }
            Quad(A, R0, R1, B, sign * new Vector3(0, hd, -h));
            Quad(C, R1, R0, D, sign * new Vector3(0, hd, h));
            Tri(D, R0, A, sign * new Vector3(-h, hw, 0));
            Tri(B, R1, C, sign * new Vector3(h, hw, 0));
            if (!inward && withBottom) Quad(A, B, C, D, Vector3.down);

            var mesh = new Mesh { name = name };
            mesh.SetVertices(v); mesh.SetNormals(n); mesh.SetUVs(0, uv); mesh.SetTriangles(t, 0);
            mesh.RecalculateBounds();
            string path = MeshDir + "/Mesh_" + name + ".asset";
            AssetDatabase.DeleteAsset(path);
            AssetDatabase.CreateAsset(mesh, path);
            return mesh;
        }

        // 부딪히는 콜라이더를 Walkable 레이어로 (주인공 SuperCharacterController와 카메라 Deoccluder가 이 레이어만 검사)
        static void AssignCollisionLayers()
        {
            int walkable = LayerMask.NameToLayer("Walkable");
            if (walkable < 0) { Debug.LogError("[HousePrototype] 'Walkable' 레이어가 없습니다."); return; }
            foreach (var col in Object.FindObjectsByType<Collider>(FindObjectsSortMode.None))
                if (!col.isTrigger) col.gameObject.layer = walkable;
        }
    }
}
