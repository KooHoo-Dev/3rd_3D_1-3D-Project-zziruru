using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace MoonlitVillage.EditorTools
{
    public static class VillageBuilder
    {
        const string Root = "Assets/02. Homework/06. Moonlit Village";
        const string Gen = Root + "/Generated";
        // 2026-10-22: 세시마을 기본 맵을 마지막으로 생성할 위치. 이후에는 이 씬을 에디터에서 직접 편집한다.
        const string ScenePath = "Assets/Scenes/Village.unity";
        const string CaptureDir = "Reference/Moonlit Village/Captures";
        const string NightSkyPath = "Assets/External Assets/Fantasy Skybox FREE/Cubemaps/Classic/FS000_Night_02_Moonless.mat";

        // ── 맵 크기 (2026-10-23) ──
        // MapScale = 마을 배치(가로 x·z) 배율. 높이와 사람 크기 물건(문·계단·가구·울타리·등롱·나무·다리 폭)은 키우지 않는다.
        // BuildingScale = 들어갈 수 있는 건물의 방 크기 배율 (Docs/TechNotes 3-5 "실내 크기 비교" 결과)
        // 2026-10-23: 처음엔 배치도 1.5배로 키웠으나 이동이 지루해져 배치만 1.2배로 줄임 (건물은 1.5배 유지).
        //             좁아져서 겹치는 집(김장·동지, 칠석·섣달)은 원작 지도 방향대로 조금 옮김 (Houses 목록)
        const float MapScale = 1.2f;
        // 지붕 묶음 이름. (2026-10-23 젤다식 실내로 바뀌어 지붕을 숨기지 않음. 위에서 실내를 찍는 캡처 도구만 이 이름을 씀)
        const string RoofGroupName = "Roof";
        const float BuildingScale = 1.5f;
        static float L(float v) => v * MapScale;                                     // 배치 거리 → 키운 맵 거리
        static Vector2 L(float x, float z) => new Vector2(x, z) * MapScale;           // 배치 좌표 → 키운 맵 좌표
        static Vector3 Circle(float x, float z, float r) => new Vector3(x, z, r) * MapScale; // (x, z, 반지름) 원

        // 지형 범위 (2026-10-23 경계 작업): 마을 북쪽 바로 뒤에서 지형이 끝나던 문제 → 놀 수 있는 구역 + 경계 절벽 + 절벽 위 숲이 들어가게 넓힘.
        // 배치 배율과 상관없이 고정 (1.2배 기준으로 정함)
        const float TerrainSize = 640f;
        const float TerrainMinX = -320f, TerrainMinZ = -440f;
        const float TerrainMaxX = TerrainMinX + TerrainSize, TerrainMaxZ = TerrainMinZ + TerrainSize;
        const float TerrainHeight = 60f;
        const float Ground = 4f;
        const float WaterDrop = 0.8f;
        const float PondDepth = 2.4f;
        static float WaterY => Ground - WaterDrop;

        const float BridgeLength = 14f * MapScale;   // 해자를 건너는 길이라 해자와 함께 키움
        const float BridgeWidth = 5f;
        const float DeckRise = 1.4f;
        const float ArchRadius = 4.0f * MapScale;
        const float ArchHeight = 1.8f;
        const float RampLen = 5.5f;
        const float MoatWidth = 8f * MapScale;
        static float DeckY => Ground + DeckRise;
        static readonly Vector3 BridgeWest = new Vector3(-20f, 0f, 9f) * MapScale;
        static readonly Vector3 BridgeEast = new Vector3(24f, 0f, -2f) * MapScale;

        // ── 세시마을 배치 (2026-10-22, 원작 세시마을 지도 기준. Docs/Map/목표배치도_초안.html 참고) ──
        // 마을 집들이 들어갈 자리를 만들기 위해 남쪽 지형(묘지·연꽃 호수·달맞이다리·달맞이고개)을 통째로 남쪽으로 옮긴다.
        const float SouthShift = -40f * MapScale;
        internal const float VillageRadius = 92f * MapScale;   // 마을 평지 반지름
        const float RingRoadRadius = 52f * MapScale;  // 해자를 도는 순환 길 반지름

        const float WoodBridgeX = 0f, WoodBridgeZ0 = -144f * MapScale + SouthShift, WoodBridgeZ1 = -196f * MapScale + SouthShift, WoodBridgeWidth = 4.2f;
        static readonly Vector2 LakeCenter = new Vector2(0f, L(-170f) + SouthShift);

        static readonly Vector2 PlateauCenter = new Vector2(L(70f), L(-146f) + SouthShift);
        const float PlateauRadius = 22f * MapScale, PlateauSlope = 30f * MapScale, PlateauRise = 18f;  // 높이(Rise)는 그대로

        internal static readonly Vector2 GraveCenter = new Vector2(0f, L(-92f) + SouthShift);
        internal const float GraveRadius = 46f * MapScale;
        const float GraveLoopRadius = 27f * MapScale;
        const float GraveClearing = 13f * MapScale;

        // 촌장집(기존 기와집)은 섬 위 (0, 16), 칠성당은 그 앞 (2, -3)
        static readonly Vector2 ChiefHousePos = L(0f, 16f);
        static readonly Vector2 ShrinePos = L(2f, -3f);

        // DongjiCompound: 들어갈 수 있는 동지집 + 안쪽 동지창고 (원작: 동지집 안으로 들어가 위쪽 방이 동지창고)
        enum HouseStyle { OpenDoorway, DoubleDoor, Plain, DongjiCompound }

        // 초가집 목록. 모든 집은 마을 중심(0,0)을 바라보도록 회전한다. extension = 확장 명절용 빈 집(들어갈 수 없음)
        struct VillageHouse
        {
            public string id, label; public Vector2 pos; public bool yellowThatch, gourds, extension; public HouseStyle style;
            public VillageHouse(string id, string label, float x, float z, bool yellow, bool gourds, HouseStyle style, bool extension)
            { this.id = id; this.label = label; pos = L(x, z); yellowThatch = yellow; this.gourds = gourds; this.style = style; this.extension = extension; }
        }
        static readonly VillageHouse[] Houses =
        {
            new VillageHouse("Seollal",   "설날집", -66f,  14f, true,  false, HouseStyle.OpenDoorway, false),
            new VillageHouse("Chuseok",   "추석집", -76f, -12f, false, true,  HouseStyle.DoubleDoor,  true),
            new VillageHouse("Chilseok",  "칠석집",  40f, -46f, true,  true,  HouseStyle.DoubleDoor,  false),
            new VillageHouse("Dano",      "단오집", -40f,  66f, false, false, HouseStyle.Plain,       false),
            new VillageHouse("Gimjang",   "김장집",  42f,  76f, true,  true,  HouseStyle.Plain,       false),
            new VillageHouse("Dongji",    "동지집",  14f,  62f, false, false, HouseStyle.DongjiCompound, false),
            new VillageHouse("Junghwa",   "중화집",  51f,  37f, true,  false, HouseStyle.Plain,       true),
            new VillageHouse("Junggu",    "중구집", -76f, -62f, true,  false, HouseStyle.DoubleDoor,  false),
            new VillageHouse("Samjit",    "삼짇집", -34f, -66f, false, false, HouseStyle.Plain,       true),
            new VillageHouse("Yudu",      "유두집",  16f, -70f, true,  true,  HouseStyle.Plain,       true),
            new VillageHouse("Seotdal",   "섣달집",  62f, -66f, false, false, HouseStyle.DoubleDoor,  false),
        };

        // 집 정면 방향(마을 중심 쪽) 단위 벡터와 회전 각도
        static Vector2 HouseFacing(Vector2 p) => (-p).normalized;
        static float HouseYaw(Vector2 p) => Mathf.Atan2(p.x, p.y) * Mathf.Rad2Deg;
        static bool NearAnyHouse(Vector2 p, float dist)
        {
            if (Vector2.Distance(p, ChiefHousePos) < dist) return true;
            foreach (var h in Houses) if (Vector2.Distance(p, h.pos) < dist) return true;
            return false;
        }

        // 동쪽 대문 = 신부여성서쪽에서 마을로 들어오는 출발 지점
        static readonly Vector2 GatePos = L(90f, 3f);

        static readonly Vector3 MoonDir = new Vector3(0.55f, 0.42f, -0.72f).normalized;

        static System.Random rng = new System.Random(20260919);
        static float R(float a, float b) => a + (float)rng.NextDouble() * (b - a);

        [MenuItem("Moonlit Village/Build Scene")]
        public static void Build()
        {
            bool confirm = EditorUtility.DisplayDialog(
                "맵 재생성", "맵을 재생성 하시겠습니까? 현재 작업사항이 전부 지워집니다!",
                "확인",
                "취소"
            );

            if (!confirm) return;

            // 새 씬을 만들면 지금 열린 씬이 닫히므로, 저장하지 않은 변경이 있으면 먼저 물어본다.
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            rng = new System.Random(20260919);
            EnsureFolders();
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var M = CreateMaterials();
            BuildTerrain(M);
            BuildWater(M);
            BuildBridge(M, BridgeWest, "Bridge_West");
            BuildBridge(M, BridgeEast, "Bridge_East");
            BuildHouse(M);
            BuildShrine(M);
            BuildVillageHouses(M);
            BuildEastGate(M);
            BuildWoodBridge(M);
            BuildGraveyard(M);
            BuildPlateau(M);
            BuildShoreStones(M);
            BuildVegetation(M);
            BuildBoundaryForest();
            BuildOuterTerrain(M);
            BuildExitBarriers(M);
            BuildLanterns(M);
            BuildLighting(M);
            BuildCamera();
            BuildSnow(M);
            BuildGameplay();
            AssignCollisionLayers();

            EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.SaveAssets();
            Debug.Log("[MoonlitVillage] scene built: " + ScenePath);
        }


        public static void BuildAndCapture() { Build(); CaptureAllViews(); }

        // TreeComboPreview(나무 조합 비교 도구)도 같은 시점으로 찍도록 internal
        internal static (string name, Vector3 pos, Vector3 look, float fov)[] Views()
        {
            float pt = Ground + PlateauRise;
            Vector3 pc = new Vector3(PlateauCenter.x, pt, PlateauCenter.y);
            Vector3 G2(Vector2 p, float y) => new Vector3(p.x, y, p.y);
            Vector2 seollal = Houses[0].pos, chilseok = Houses[2].pos;
            float s = SouthShift;
            // 가로(x·z)는 맵 배율로, 높이는 그대로 (위에서 찍는 높이만 맵 크기에 맞춰 키움)
            Vector3 P(float x, float y, float z) => new Vector3(L(x), y, L(z));
            return new[]
            {
                ("village_overview", P(0f, 95f * MapScale, -130f),  new Vector3(0f, Ground, 0f), 55f),
                ("village_topdown",  P(0f, 190f * MapScale, 0.01f), new Vector3(0f, Ground, 0f), 60f),
                ("island_chief",     P(3f, 7f, -16f),    P(1f, Ground + 2.5f, 8f), 55f),
                ("west_bridge",      P(-34f, 9f, -8f),   P(-20f, Ground + 1f, 9f), 45f),
                ("east_gate",        new Vector3(GatePos.x + 16f, 6f, GatePos.y - 9f), new Vector3(GatePos.x, Ground + 3f, GatePos.y), 50f),
                ("enter_from_east",  new Vector3(GatePos.x + 30f, Ground + 2.2f, GatePos.y), new Vector3(0f, Ground + 3f, 0f), 60f),
                ("north_houses",     P(0f, 14f, 20f),    P(5f, Ground + 2f, 66f), 60f),
                ("seollal_house",    G2(seollal + HouseFacing(seollal) * L(18f), 6.5f), G2(seollal, Ground + 2.5f), 50f),
                ("chilseok_house",   G2(chilseok + HouseFacing(chilseok) * L(18f), 6.5f), G2(chilseok, Ground + 2.5f), 50f),
                ("dongji_compound",  G2(Houses[5].pos + HouseFacing(Houses[5].pos) * L(14f), 9f), G2(Houses[5].pos - HouseFacing(Houses[5].pos) * L(3f), Ground + 1f), 55f),
                ("south_houses",     P(0f, 14f, -30f),   P(0f, Ground + 2f, -66f), 65f),
                ("graveyard",        new Vector3(L(26f), 14f, L(-60f) + s),  G2(GraveCenter, Ground), 50f),
                ("graveyard_air",    new Vector3(0f, 70f * MapScale, L(-40f) + s),   new Vector3(0f, Ground, L(-96f) + s), 55f),
                ("moon_bridge",      new Vector3(L(-13f), 8f, L(-154f) + s), new Vector3(L(2f), Ground + 1f, L(-176f) + s), 50f),
                ("moon_bridge_walk", new Vector3(0f, Ground + 2f, L(-148f) + s), new Vector3(0f, Ground + 1.5f, L(-194f) + s), 55f),
                ("hill_moon",        pc + new Vector3(L(-8f), 2.2f, L(6f)), pc + new Vector3(L(-8f), 2.2f, L(6f)) + MoonDir * 100f + Vector3.down * 20f, 55f),
                ("hill_top",         pc + new Vector3(L(-30f), 14f, L(22f)), pc + Vector3.up * 2f, 50f),
                ("whole_map",        new Vector3(L(-190f), 150f * MapScale, L(-40f)), new Vector3(L(20f), Ground, L(-100f) + s), 60f),
            };
        }

        [MenuItem("Moonlit Village/Capture All Views")]
        public static void CaptureAllViews()
        {
            // 마을 비행 카메라는 평소 꺼 두므로(Core 씬 카메라를 씀) 꺼진 카메라도 찾는다
            var cam = Camera.main;
            if (cam == null)
                foreach (var c in UnityEngine.Object.FindObjectsByType<Camera>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                    if (c.CompareTag("MainCamera")) { cam = c; break; }
            if (cam == null) { Debug.LogError("[MoonlitVillage] no main camera"); return; }
            var savedPos = cam.transform.position; var savedRot = cam.transform.rotation; float savedFov = cam.fieldOfView;
            int i = 1;
            foreach (var v in Views())
            {
                cam.transform.position = v.pos; cam.transform.LookAt(v.look); cam.fieldOfView = v.fov;
                CaptureTo(cam, CaptureDir + "/view_" + (i++).ToString("00") + "_" + v.name + ".png");
            }
            // 실내 확인용: 지붕 묶음을 잠시 숨기고 위에서 찍는다
            var roofs = new List<GameObject>();
            foreach (var tr in UnityEngine.Object.FindObjectsByType<Transform>(FindObjectsSortMode.None))
                if (tr.name == RoofGroupName && tr.gameObject.activeSelf) roofs.Add(tr.gameObject);
            foreach (var r in roofs) r.SetActive(false);
            foreach (var (name, center, height) in new[] { ("roofless_chief", new Vector3(ChiefHousePos.x, Ground, ChiefHousePos.y - L(2f)), L(26f)),
                                                           ("roofless_dongji", new Vector3(Houses[5].pos.x, Ground, Houses[5].pos.y + L(3f)), L(30f)),
                                                           ("roofless_village", new Vector3(0f, Ground, 0f), L(150f)) })
            {
                cam.transform.position = center + new Vector3(0f, height, -height * 0.35f); cam.transform.LookAt(center); cam.fieldOfView = 55f;
                CaptureTo(cam, CaptureDir + "/view_" + (i++).ToString("00") + "_" + name + ".png");
            }
            foreach (var r in roofs) r.SetActive(true);
            cam.transform.position = savedPos; cam.transform.rotation = savedRot; cam.fieldOfView = savedFov;
        }

        [MenuItem("Moonlit Village/Debug Dump Terrain Splat")]
        public static void DebugDumpSplat()
        {
            var terrain = UnityEngine.Object.FindFirstObjectByType<Terrain>();
            if (terrain == null) { Debug.LogError("[MoonlitVillage] no terrain"); return; }
            var td = terrain.terrainData;
            Debug.Log("[MoonlitVillage] layers=" + td.alphamapLayers + " res=" + td.alphamapResolution + " names=" + string.Join(",", Array.ConvertAll(td.terrainLayers, l => l == null ? "null" : l.name)) + " material=" + (terrain.materialTemplate ? terrain.materialTemplate.shader.name : "none"));
            int r = td.alphamapResolution;
            var a = td.GetAlphamaps(0, 0, r, r);
            var tex = new Texture2D(r, r, TextureFormat.RGB24, false);
            float sumDirt = 0, sumPav = 0;
            for (int z = 0; z < r; z++)
                for (int x = 0; x < r; x++)
                {
                    float d = td.alphamapLayers > 1 ? a[z, x, 1] : 0f, p = td.alphamapLayers > 2 ? a[z, x, 2] : 0f;
                    sumDirt += d; sumPav += p;
                    tex.SetPixel(x, z, new Color(d, p, a[z, x, 0]));
                }
            tex.Apply();
            Directory.CreateDirectory(CaptureDir);
            File.WriteAllBytes(CaptureDir + "/debug_splat.png", tex.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(tex);
            Debug.Log("[MoonlitVillage] splat dumped. dirt sum=" + sumDirt + " paving sum=" + sumPav);
        }

        [MenuItem("Moonlit Village/Capture Screenshot (Main Camera)")]
        public static void Capture()
        {
            var cam = Camera.main;
            if (cam == null) { Debug.LogError("[MoonlitVillage] no main camera"); return; }
            CaptureTo(cam, CaptureDir + "/main_camera.png");
        }

        internal static void CaptureTo(Camera cam, string path)
        {
            var moonFollow = UnityEngine.Object.FindFirstObjectByType<MoonlitVillage.MoonFollowCamera>();
            if (moonFollow != null) moonFollow.Sync();
            foreach (var ps in UnityEngine.Object.FindObjectsByType<ParticleSystem>(FindObjectsSortMode.None))
            {
                var follow = ps.GetComponent<MoonlitVillage.SnowFollowCamera>();
                if (follow != null) follow.SendMessage("LateUpdate");
                ps.Simulate(12f, true, true);
            }
            int w = 1920, h = 1080;
            var rt = new RenderTexture(w, h, 24);
            cam.targetTexture = rt;
            cam.Render();
            RenderTexture.active = rt;
            var tex = new Texture2D(w, h, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, w, h), 0, 0);
            tex.Apply();
            cam.targetTexture = null;
            RenderTexture.active = null;
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllBytes(path, tex.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(tex);
            rt.Release();
            Debug.Log("[MoonlitVillage] capture saved: " + path);
        }

        static readonly Vector3[] IslandCircles =
        {
            Circle( 0f,  4f, 17f), Circle(-9f, -3f, 10f), Circle(10f, -3f, 10f), Circle( 0f, 16f, 11f),
        };
        static readonly Vector3[] LobeCircles =
        {
            Circle(-26f,-18f, 15f), Circle( 0f,-26f, 10f), Circle( 26f, 16f, 11f), Circle( 30f,-16f, 10f), Circle(-28f, 22f, 9f),
        };
        static readonly Vector3[] GravePondCircles =
        {
            Circle(0f, -92f - 40f, 5.5f), Circle(2.5f, -98f - 40f, 4.5f), Circle(-2f, -87f - 40f, 3.5f),   // -40 = 원래 SouthShift
        };
        static readonly Vector3[] MoonLakeCircles =
        {
            Circle(0f, -170f - 40f, 20f), Circle(-17f, -163f - 40f, 12f), Circle(18f, -177f - 40f, 12f),
            Circle(-9f, -185f - 40f, 8f), Circle(14f, -156f - 40f, 8f),
        };

        // ── 플레이 구역 경계 (2026-10-23) ──
        // 놀 수 있는 구역(마을·묘지·호수·달 언덕·안쪽 길)에서 RidgeStart 넘게 떨어진 곳은 가파른 숲 절벽으로 올려서 막는다 (보이지 않는 벽 없이).
        // 출구 길(북·서·동)은 좁은 골짜기로 절벽을 뚫고 지형 끝까지 이어지고, 골짜기 입구를 장승·금줄(충돌 있음)로 막는다.
        const float RidgeStart = 18f * MapScale;   // 놀 수 있는 구역 끝에서 절벽이 시작되는 거리
        const float RidgeRise = 22f;               // 절벽 높이
        const float RidgeWidth = 7f;               // 절벽이 올라가는 가로 거리 → 평균 약 72° (노이즈로 가장 완만해지는 곳도 약 66°)
        const float GorgeHalf = 9f;                // 출구 골짜기 바닥 반폭 (길 중심에서)
        // 주인공이 오를 수 있는 최대 경사 (지형의 SuperCollisionType). 연못 둑(최대 약 54°)보다 크고 절벽(평균 약 68°)보다 작게
        const float TerrainSlopeLimit = 60f;

        // 출구 길: 북(세시호수·약수터), 서(세시논밭·명경사), 동(신부여성서쪽, 출발점). 나중에 지역을 확장하면 금줄을 걷어 길을 연다
        static readonly Vector2[][] ExitPaths =
        {
            new[] { new Vector2(0f, RingRoadRadius), L(-4f, 70f), new Vector2(L(-4f), TerrainMaxZ) },
            new[] { L(-45f, 26f), L(-100f, 40f), new Vector2(TerrainMinX, L(40f)) },
            new[] { new Vector2(RingRoadRadius, 0f), new Vector2(GatePos.x, GatePos.y), new Vector2(TerrainMaxX, L(6f)) },
        };

        static readonly Vector2[][] Paths = BuildPathList();

        // 흙길 목록. 지형을 평평하게 만들고(ZoneDistance) 흙을 칠하는(InDirtPath) 기준이 된다.
        static Vector2[][] BuildPathList()
        {
            var list = new List<Vector2[]>();
            float gz = GraveCenter.y, s = SouthShift;

            // 해자를 도는 순환 길 (12각형)
            var ring = new Vector2[13];
            for (int i = 0; i <= 12; i++)
            {
                float a = i * 30f * Mathf.Deg2Rad;
                ring[i] = new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * RingRoadRadius;
            }
            list.Add(ring);

            // 돌다리와 순환 길 연결
            list.Add(new[] { new Vector2(BridgeWest.x - BridgeLength / 2f - RampLen, BridgeWest.z), new Vector2(L(-50f), BridgeWest.z) });
            list.Add(new[] { new Vector2(BridgeEast.x + BridgeLength / 2f + RampLen, BridgeEast.z), new Vector2(RingRoadRadius, BridgeEast.z) });

            // 출구: 북(세시호수·약수터, 나중), 서(세시논밭·명경사, 나중), 동(신부여성서쪽, 출발점), 남(묘지)
            list.AddRange(ExitPaths);
            list.Add(new[] { new Vector2(0f, -RingRoadRadius), L(-4f, -80f), new Vector2(0f, gz + GraveLoopRadius) });

            // 집 앞마당 → 순환 길
            foreach (var h in Houses)
            {
                Vector2 dir = h.pos.normalized;
                list.Add(new[] { h.pos + HouseFacing(h.pos) * L(5f), dir * RingRoadRadius });
            }

            // 묘지 순환 길과 갈림길, 달맞이다리, 달맞이고개 길 (기존 배치를 남쪽으로 옮긴 것)
            list.Add(new[] { new Vector2(0f, gz + GraveLoopRadius), new Vector2(-GraveLoopRadius, gz), new Vector2(L(-4f), gz - GraveLoopRadius), new Vector2(GraveLoopRadius, gz + L(2f)), new Vector2(0f, gz + GraveLoopRadius) });
            list.Add(new[] { new Vector2(-GraveLoopRadius, gz), new Vector2(-GraveLoopRadius - L(16f), gz - L(12f)) });
            list.Add(new[] { new Vector2(GraveLoopRadius, gz + L(2f)), new Vector2(GraveLoopRadius + L(18f), gz + L(12f)) });
            list.Add(new[] { new Vector2(L(-4f), gz - GraveLoopRadius), new Vector2(0f, L(-140f) + s) });
            list.Add(new[] { new Vector2(0f, L(-198f) + s), new Vector2(L(16f), L(-206f) + s), new Vector2(L(36f), L(-202f) + s), new Vector2(L(50f), L(-186f) + s), new Vector2(L(60f), L(-166f) + s), new Vector2(L(68f), L(-152f) + s) });
            return list.ToArray();
        }

        static float CirclesSdf(Vector3[] circles, float x, float z)
        {
            float d = float.MaxValue;
            foreach (var c in circles)
                d = Mathf.Min(d, Vector2.Distance(new Vector2(x, z), new Vector2(c.x, c.y)) - c.z);
            return d;
        }

        static float SegDist(Vector2 p, Vector2 a, Vector2 b)
        {
            Vector2 ab = b - a; float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / Mathf.Max(0.0001f, ab.sqrMagnitude));
            return Vector2.Distance(p, a + ab * t);
        }

        static float PathDistance(float x, float z)
        {
            var p = new Vector2(x, z); float d = float.MaxValue;
            foreach (var path in Paths)
                for (int i = 0; i < path.Length - 1; i++) d = Mathf.Min(d, SegDist(p, path[i], path[i + 1]));
            return d;
        }

        static float PolylineDistance(Vector2[] path, Vector2 p)
        {
            float d = float.MaxValue;
            for (int i = 0; i < path.Length - 1; i++) d = Mathf.Min(d, SegDist(p, path[i], path[i + 1]));
            return d;
        }

        // 놀 수 있는 구역에서 얼마나 떨어졌는가 (음수 = 안쪽). ZoneDistance와 같지만 출구 길은 뺀다 (출구는 골짜기로 따로 계산)
        static float PlayZoneDistance(float x, float z)
        {
            var p = new Vector2(x, z);
            float d = p.magnitude - VillageRadius;
            d = Mathf.Min(d, Vector2.Distance(p, GraveCenter) - (GraveRadius + L(6f)));
            d = Mathf.Min(d, Vector2.Distance(p, LakeCenter) - L(36f));
            d = Mathf.Min(d, Vector2.Distance(p, PlateauCenter) - (PlateauRadius + PlateauSlope + L(4f)));
            foreach (var h in Houses) d = Mathf.Min(d, Vector2.Distance(p, h.pos) - L(16f));
            foreach (var path in Paths)
                if (Array.IndexOf(ExitPaths, path) < 0) d = Mathf.Min(d, PolylineDistance(path, p) - L(7f));
            return d;
        }

        // 경계 절벽까지 남은 거리. 0 이하 = 평지·언덕(갈 수 있음), 0 ~ RidgeWidth = 절벽 면, 그 너머 = 절벽 위
        static float BoundaryDistance(float x, float z)
        {
            var p = new Vector2(x, z);
            // 경계선이 원처럼 매끈하지 않게. ⚠ 주파수를 높이면 노이즈 기울기가 거리 기울기를 깎아 절벽이 완만해짐 (2026-10-23 올라가짐 → 0.03 → 0.012)
            float noise = (Mathf.PerlinNoise(x * 0.012f + 40f, z * 0.012f + 90f) - 0.5f) * L(8f);
            float d = PlayZoneDistance(x, z) - RidgeStart + noise;
            foreach (var path in ExitPaths) d = Mathf.Min(d, PolylineDistance(path, p) - GorgeHalf);
            return d;
        }

        static float BridgeProximity(float x, float z)
        {
            var p = new Vector2(x, z);
            float d = Mathf.Min(Vector2.Distance(p, new Vector2(BridgeWest.x, BridgeWest.z)), Vector2.Distance(p, new Vector2(BridgeEast.x, BridgeEast.z)));
            d = Mathf.Min(d, Vector2.Distance(p, new Vector2(WoodBridgeX, WoodBridgeZ0)));
            d = Mathf.Min(d, Vector2.Distance(p, new Vector2(WoodBridgeX, WoodBridgeZ1)));
            return Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(L(9f), L(20f), d));
        }

        static void Warp(float x, float z, out float wx, out float wz)
        {
            // 물가 굴곡: 맵을 키운 만큼 굴곡도 크고 완만하게 (진폭 ×, 주파수 ÷ MapScale)
            float amp = L(6.5f) * BridgeProximity(x, z);
            float f = 0.045f / MapScale;
            wx = x + (Mathf.PerlinNoise(x * f + 11.3f, z * f + 5.7f) - 0.5f) * 2f * amp;
            wz = z + (Mathf.PerlinNoise(x * f + 41.9f, z * f + 23.1f) - 0.5f) * 2f * amp;
        }

        static float IslandSdf(float x, float z)
        {
            Warp(x, z, out float wx, out float wz);
            float k = BridgeProximity(x, z);
            return CirclesSdf(IslandCircles, wx, wz) + (Mathf.PerlinNoise(x * 0.16f + 3f, z * 0.16f + 9f) - 0.5f) * 2.2f * k;
        }

        static float WaterSdf(float x, float z)
        {
            Warp(x, z, out float wx, out float wz);
            float k = BridgeProximity(x, z);
            float fine = (Mathf.PerlinNoise(x * 0.12f + 7f, z * 0.12f + 2f) - 0.5f) * 3f * k;
            float island = IslandSdf(x, z);
            float islandWater = Mathf.Max(Mathf.Min(island - MoatWidth, CirclesSdf(LobeCircles, wx, wz) + fine), -island);
            float gravePond = CirclesSdf(GravePondCircles, wx, wz) + fine * 0.5f;
            float moonLake = CirclesSdf(MoonLakeCircles, wx, wz) + fine;
            float sdf = Mathf.Min(islandWater, Mathf.Min(gravePond, moonLake));
            float w = ApproachMask(x, z);
            if (w > 0f) sdf = Mathf.Lerp(sdf, Mathf.Max(sdf, 2.5f), w);
            return sdf;
        }

        static float ApproachMask(float x, float z)
        {
            float best = 0f;
            foreach (var b in new[] { BridgeWest, BridgeEast })
            {
                float ax = Mathf.Abs(x - b.x), az = Mathf.Abs(z - b.z);
                if (ax < ArchRadius * 0.95f) continue;
                float halfX = BridgeLength / 2f + RampLen + 2.5f, halfZ = BridgeWidth / 2f + 2.5f;
                float dx = halfX - ax, dz = halfZ - az;
                float inside = Mathf.Min(dx, dz);
                float wgt = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0f, 2.5f, inside));
                wgt *= Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(ArchRadius * 0.95f, ArchRadius * 0.95f + 1.5f, ax));
                best = Mathf.Max(best, wgt);
            }
            return best;
        }

        static float ZoneDistance(float x, float z)
        {
            var p = new Vector2(x, z);
            float d = Vector2.Distance(p, Vector2.zero) - VillageRadius;
            d = Mathf.Min(d, Vector2.Distance(p, GraveCenter) - (GraveRadius + L(6f)));
            d = Mathf.Min(d, Vector2.Distance(p, LakeCenter) - L(36f));
            d = Mathf.Min(d, Vector2.Distance(p, PlateauCenter) - (PlateauRadius + PlateauSlope + L(4f)));
            d = Mathf.Min(d, PathDistance(x, z) - L(7f));
            foreach (var h in Houses) d = Mathf.Min(d, Vector2.Distance(p, h.pos) - L(16f));
            return d;
        }

        static float PlateauK(float x, float z)
        {
            float dist = Vector2.Distance(new Vector2(x, z), PlateauCenter);
            float edgeNoise = (Mathf.PerlinNoise(x * 0.06f / MapScale + 300f, z * 0.06f / MapScale + 120f) - 0.5f) * L(6f);
            return Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(PlateauRadius + PlateauSlope + edgeNoise, PlateauRadius + edgeNoise, dist));
        }

        static float TerrainHeightAt(float x, float z)
        {
            float sdf = WaterSdf(x, z);
            // 물가 둑 (2026-10-23 수정): 3.5m → 7m 폭으로 완만하게. 경사 제한 60°(TerrainSlopeLimit)에서 연못에 빠져도 걸어 나올 수 있게.
            // 물가 굴곡(Warp)이 거리 기울기를 최대 약 1.9배 키워서, 3.5m 폭이면 둑이 60°를 넘었음 (사용자 확인). 7m면 최대 약 52°
            // 물가선 위치(t = 0.75가 되는 sdf ≈ -0.32)는 예전과 같게 유지 → 연잎·물가 돌 위치 그대로
            float t = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(-4.87f, 2.13f, sdf));
            float bottom = WaterY - PondDepth;
            float h = Mathf.Lerp(bottom, Ground, t);
            h += (Mathf.PerlinNoise(x * 0.05f + 3f, z * 0.05f + 7f) - 0.5f) * 0.35f * t;

            float hillK = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0f, L(40f), ZoneDistance(x, z)));
            float hf = 1f / MapScale;   // 언덕 모양도 맵 크기에 맞춰 넓게 (높이는 그대로)
            float hill = Mathf.PerlinNoise(x * 0.02f * hf + 100f, z * 0.02f * hf + 50f) * 0.7f + Mathf.PerlinNoise(x * 0.06f * hf + 200f, z * 0.06f * hf + 80f) * 0.3f;
            h += (hill * 14f + 3f) * hillK;

            float pk = PlateauK(x, z);
            h += PlateauRise * pk + (Mathf.PerlinNoise(x * 0.08f + 500f, z * 0.08f + 20f) - 0.5f) * 0.6f * pk;

            // 경계 절벽 (+ 절벽 위는 울퉁불퉁하게)
            float bk = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0f, RidgeWidth, BoundaryDistance(x, z)));
            h += RidgeRise * bk + (Mathf.PerlinNoise(x * 0.05f + 700f, z * 0.05f + 300f) - 0.5f) * 6f * bk;
            return h;
        }

        static float PlateauTopY(float x, float z) => TerrainHeightAt(x, z);

        static bool InDirtPath(float x, float z)
        {
            float n = (Mathf.PerlinNoise(x * 0.25f, z * 0.25f) - 0.5f) * 1.6f;
            if (PathDistance(x, z) < 3.2f + n) return true;   // 길 폭은 사람 기준 그대로 (다리 폭 5m와 맞춤)
            if (Vector2.Distance(new Vector2(x, z), GraveCenter) < GraveClearing + (Mathf.PerlinNoise(x * 0.12f + 9f, z * 0.12f + 4f) - 0.5f) * 5f) return true;
            // 집 앞마당: 집 정면 쪽으로 조금 나간 원
            foreach (var h in Houses)
            {
                Vector2 c = h.pos + HouseFacing(h.pos) * L(5f);
                float r = Vector2.Distance(new Vector2(x, z), c) / L(9f);
                if (r + (Mathf.PerlinNoise(x * 0.15f + 50f, z * 0.15f + 70f) - 0.5f) * 0.5f < 1f) return true;
            }
            return false;
        }

        class Mats
        {
            public Material stone, deck, railing, water, shore, lilyPad, lotus, bamboo, deadWood, moon, halo, lantern, lanternPost;
            public Material brick, roof, redWood, eave, windowDark, windowLattice, plinth, fence, inscribedStone, plank, mound, marker, snow;
            public Material thatchYellow, thatchOrange, plaster, darkWood, paperWindow, jar, pumpkin, gourdVine;
            public Material blueRoof, signBoard, totemWood, sunflowerPetal, sunflowerCenter;
            public TerrainLayer grass, dirt, paving;
        }

        static void EnsureFolders()
        {
            if (!AssetDatabase.IsValidFolder(Root)) AssetDatabase.CreateFolder("Assets/02. Homework", "06. Moonlit Village");
            if (!AssetDatabase.IsValidFolder(Gen)) AssetDatabase.CreateFolder(Root, "Generated");
            if (!AssetDatabase.IsValidFolder("Assets/Scenes")) AssetDatabase.CreateFolder("Assets", "Scenes");
        }

        static Texture2D NoiseTexture(string name, Color baseCol, float amp, float scale, Func<int, int, float> extra = null)
        {
            int s = 256;
            var tex = new Texture2D(s, s, TextureFormat.RGB24, true);
            for (int y = 0; y < s; y++)
                for (int x = 0; x < s; x++)
                {
                    float n = Mathf.PerlinNoise(x * scale, y * scale) * 0.6f + Mathf.PerlinNoise(x * scale * 3.1f, y * scale * 3.1f) * 0.4f;
                    float v = 1f + (n - 0.5f) * amp;
                    if (extra != null) v *= extra(x, y);
                    tex.SetPixel(x, y, new Color(baseCol.r * v, baseCol.g * v, baseCol.b * v));
                }
            tex.Apply();
            string path = Gen + "/T_" + name + ".png";
            File.WriteAllBytes(path, tex.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        static Texture2D SoftCircleTexture()
        {
            int s = 128;
            var tex = new Texture2D(s, s, TextureFormat.RGBA32, false);
            for (int y = 0; y < s; y++)
                for (int x = 0; x < s; x++)
                {
                    float dx = (x + 0.5f) / s - 0.5f, dy = (y + 0.5f) / s - 0.5f;
                    float r = Mathf.Sqrt(dx * dx + dy * dy) * 2f;
                    float a = Mathf.Clamp01(1f - Mathf.SmoothStep(0.3f, 1f, r));
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
                }
            tex.Apply();
            string path = Gen + "/T_SoftCircle.png";
            File.WriteAllBytes(path, tex.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            var imp = AssetImporter.GetAtPath(path) as TextureImporter;
            if (imp != null) { imp.alphaIsTransparency = true; imp.mipmapEnabled = true; imp.SaveAndReimport(); }
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        static Material LitMat(string name, Color c, float smooth = 0.2f, Texture2D tex = null, Vector2? tiling = null)
        {
            var m = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            m.SetColor("_BaseColor", c);
            m.SetFloat("_Smoothness", smooth);
            m.SetFloat("_Metallic", 0f);
            if (tex != null) { m.SetTexture("_BaseMap", tex); if (tiling.HasValue) m.SetTextureScale("_BaseMap", tiling.Value); }
            AssetDatabase.CreateAsset(m, Gen + "/M_" + name + ".mat");
            return m;
        }

        static void MakeTransparent(Material m)
        {
            m.SetFloat("_Surface", 1f);
            m.SetFloat("_Blend", 0f);
            m.SetOverrideTag("RenderType", "Transparent");
            m.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
            m.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
            m.SetInt("_ZWrite", 0);
            m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            m.renderQueue = (int)RenderQueue.Transparent;
        }

        static void MakeEmissive(Material m, Color e)
        {
            m.EnableKeyword("_EMISSION");
            m.SetColor("_EmissionColor", e);
            m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
        }

        static Mats CreateMaterials()
        {
            var M = new Mats();

            var tGrass = NoiseTexture("Grass", new Color(0.26f, 0.40f, 0.15f), 0.5f, 0.06f);
            var tDirt = NoiseTexture("Dirt", new Color(0.52f, 0.36f, 0.21f), 0.45f, 0.05f);
            var tStone = NoiseTexture("StoneRough", new Color(0.52f, 0.52f, 0.54f), 0.55f, 0.04f);
            var tTile = NoiseTexture("DeckTile", new Color(0.58f, 0.58f, 0.60f), 0.25f, 0.03f, (x, y) =>
            {
                int gx = x % 64, gy = y % 32;
                bool grout = gx < 3 || gy < 3;
                float tileShade = 0.85f + 0.15f * Mathf.PerlinNoise((x / 64) * 0.7f, (y / 32) * 0.7f);
                return grout ? 0.45f : tileShade;
            });
            var tPaving = NoiseTexture("Paving", new Color(0.62f, 0.60f, 0.56f), 0.35f, 0.05f, (x, y) =>
            {
                int cx = x / 32, cy = y / 32;
                float jx = Mathf.PerlinNoise(cx * 3.7f, cy * 1.3f) * 10f, jy = Mathf.PerlinNoise(cx * 1.1f, cy * 5.3f) * 10f;
                int gx = (int)(x + jx) % 32, gy = (int)(y + jy) % 32;
                bool grout = gx < 3 || gy < 3;
                float shade = 0.8f + 0.2f * Mathf.PerlinNoise(cx * 0.9f, cy * 0.9f);
                return grout ? 0.5f : shade;
            });
            var tPlank = NoiseTexture("Plank", new Color(0.55f, 0.42f, 0.28f), 0.4f, 0.02f, (x, y) =>
            {
                int gy = y % 40; return gy < 3 ? 0.55f : 1f;
            });
            var tBrick = NoiseTexture("Brick", new Color(0.74f, 0.70f, 0.64f), 0.2f, 0.05f, (x, y) =>
            {
                int row = y / 24; int ox = (row % 2 == 0) ? 0 : 32;
                int gx = (x + ox) % 64, gy = y % 24;
                bool mortar = gx < 3 || gy < 3;
                float shade = 0.85f + 0.15f * Mathf.PerlinNoise(((x + ox) / 64) * 1.3f, row * 1.3f);
                return mortar ? 0.62f : shade;
            });
            var tRoof = NoiseTexture("RoofTile", new Color(0.62f, 0.24f, 0.14f), 0.3f, 0.04f, (x, y) =>
            {
                int gy = y % 32; int gx = x % 16;
                return (gy < 4 ? 0.55f : 1f) * (gx < 2 ? 0.8f : 1f);
            });

            M.grass = new TerrainLayer { diffuseTexture = tGrass, tileSize = new Vector2(4, 4) };
            AssetDatabase.CreateAsset(M.grass, Gen + "/TL_Grass.terrainlayer");
            M.dirt = new TerrainLayer { diffuseTexture = tDirt, tileSize = new Vector2(4, 4) };
            AssetDatabase.CreateAsset(M.dirt, Gen + "/TL_Dirt.terrainlayer");
            M.paving = new TerrainLayer { diffuseTexture = tPaving, tileSize = new Vector2(6, 6) };
            AssetDatabase.CreateAsset(M.paving, Gen + "/TL_Paving.terrainlayer");

            M.stone = LitMat("Bridge_Stone", Color.white, 0.15f, tStone, new Vector2(2, 1));
            M.deck = LitMat("Bridge_DeckTile", Color.white, 0.2f, tTile, new Vector2(4, 2));
            M.railing = LitMat("Bridge_Railing_White", new Color(0.92f, 0.92f, 0.94f), 0.35f);
            M.shore = LitMat("Shore_Stone", new Color(0.78f, 0.72f, 0.62f), 0.1f, tStone, new Vector2(1, 1));
            M.plank = LitMat("Wood_Plank", Color.white, 0.15f, tPlank, new Vector2(1, 1));

            M.water = LitMat("Water_Basic", new Color(0.06f, 0.13f, 0.34f, 0.86f), 0.95f);
            MakeTransparent(M.water);

            M.lilyPad = LitMat("PH_LilyPad", new Color(0.18f, 0.45f, 0.2f), 0.3f);
            M.lotus = LitMat("PH_Lotus", new Color(0.95f, 0.5f, 0.7f), 0.3f);
            M.bamboo = LitMat("PH_Bamboo", new Color(0.45f, 0.62f, 0.3f), 0.35f);
            M.deadWood = LitMat("PH_DeadWood", new Color(0.42f, 0.33f, 0.24f), 0.1f);
            M.mound = LitMat("Grave_Mound", new Color(0.32f, 0.48f, 0.2f), 0.1f, tGrass, new Vector2(2, 2));
            M.marker = LitMat("Grave_Marker", new Color(0.45f, 0.46f, 0.48f), 0.1f, tStone, new Vector2(1, 2));

            var soft = SoftCircleTexture();
            var moonShader = Shader.Find("MoonlitVillage/MoonUnlit");
            if (moonShader == null) moonShader = Shader.Find("Universal Render Pipeline/Unlit");
            M.moon = new Material(moonShader);
            M.moon.SetColor("_BaseColor", new Color(1f, 0.97f, 0.86f, 1f));
            AssetDatabase.CreateAsset(M.moon, Gen + "/M_Moon.mat");
            M.halo = new Material(moonShader);
            M.halo.SetTexture("_BaseMap", soft);
            M.halo.SetColor("_BaseColor", new Color(0.85f, 0.9f, 1f, 0.35f));
            AssetDatabase.CreateAsset(M.halo, Gen + "/M_MoonHalo.mat");

            M.lantern = LitMat("Lantern_Paper", new Color(1f, 0.85f, 0.6f), 0.2f);
            MakeEmissive(M.lantern, new Color(1f, 0.55f, 0.2f) * 2.5f);
            M.lanternPost = LitMat("Lantern_Post", new Color(0.3f, 0.22f, 0.15f), 0.1f);

            M.brick = LitMat("House_Brick", Color.white, 0.1f, tBrick, new Vector2(3, 1.2f));
            M.roof = LitMat("House_RoofTile", Color.white, 0.25f, tRoof, new Vector2(6, 2));
            M.redWood = LitMat("House_RedWood", new Color(0.72f, 0.24f, 0.14f), 0.3f);
            M.eave = LitMat("House_Eave_Orange", new Color(0.85f, 0.46f, 0.16f), 0.25f);
            M.windowDark = LitMat("House_WindowDark", new Color(0.12f, 0.14f, 0.2f), 0.6f);
            M.windowLattice = LitMat("House_WindowLattice", new Color(0.45f, 0.72f, 0.7f), 0.5f);
            MakeEmissive(M.windowLattice, new Color(0.4f, 0.8f, 0.75f) * 0.35f);
            M.plinth = LitMat("House_Plinth", new Color(0.5f, 0.5f, 0.52f), 0.1f, tStone, new Vector2(2, 0.5f));
            M.fence = LitMat("House_FenceStone", new Color(0.58f, 0.56f, 0.52f), 0.1f, tStone, new Vector2(2, 0.5f));
            M.inscribedStone = LitMat("Inscribed_Stone", new Color(0.55f, 0.53f, 0.48f), 0.15f, tStone, new Vector2(1, 1));

            Func<int, int, float> thatchStreak = (x, y) => (x % 5 == 0 ? 0.78f : 1f) * (0.9f + 0.1f * Mathf.PerlinNoise(x * 0.3f, y * 0.02f));
            var tThatchY = NoiseTexture("ThatchYellow", new Color(0.80f, 0.64f, 0.30f), 0.35f, 0.08f, thatchStreak);
            var tThatchO = NoiseTexture("ThatchOrange", new Color(0.74f, 0.48f, 0.22f), 0.35f, 0.08f, thatchStreak);
            var tPlaster = NoiseTexture("Plaster", new Color(0.86f, 0.81f, 0.70f), 0.18f, 0.05f);
            M.thatchYellow = LitMat("Thatch_Yellow", Color.white, 0.05f, tThatchY, new Vector2(3, 3));
            M.thatchOrange = LitMat("Thatch_Orange", Color.white, 0.05f, tThatchO, new Vector2(3, 3));
            M.plaster = LitMat("Wall_Plaster", Color.white, 0.1f, tPlaster, new Vector2(2, 1));
            M.darkWood = LitMat("Wood_Dark", new Color(0.30f, 0.21f, 0.14f), 0.15f, tPlank, new Vector2(1, 1));
            M.paperWindow = LitMat("Window_Paper", new Color(1f, 0.92f, 0.78f), 0.2f);
            MakeEmissive(M.paperWindow, new Color(1f, 0.72f, 0.42f) * 1.6f);
            M.jar = LitMat("Clay_Jar", new Color(0.30f, 0.20f, 0.14f), 0.7f);
            M.pumpkin = LitMat("Pumpkin", new Color(0.88f, 0.48f, 0.15f), 0.3f);
            M.gourdVine = LitMat("Gourd_Vine", new Color(0.35f, 0.55f, 0.22f), 0.2f);


            var tRoofBlue = NoiseTexture("RoofTileBlue", new Color(0.30f, 0.36f, 0.66f), 0.3f, 0.04f, (x, y) =>
            {
                int gy = y % 32; int gx = x % 16;
                return (gy < 4 ? 0.55f : 1f) * (gx < 2 ? 0.8f : 1f);
            });
            M.blueRoof = LitMat("Gate_RoofBlue", Color.white, 0.3f, tRoofBlue, new Vector2(4, 2));
            M.signBoard = LitMat("Gate_SignBoard", new Color(0.9f, 0.88f, 0.8f), 0.2f);
            M.totemWood = LitMat("Totem_Wood", new Color(0.46f, 0.32f, 0.18f), 0.15f, tPlank, new Vector2(1, 1));
            M.sunflowerPetal = LitMat("Sunflower_Petal", new Color(0.95f, 0.78f, 0.2f), 0.3f);
            M.sunflowerCenter = LitMat("Sunflower_Center", new Color(0.3f, 0.2f, 0.1f), 0.2f);

            M.snow = SnowMaterial(soft);
            return M;
        }

        static Material SnowMaterial(Texture2D tex)
        {
            var m = new Material(Shader.Find("Universal Render Pipeline/Particles/Unlit"));
            m.SetTexture("_BaseMap", tex);
            m.SetColor("_BaseColor", new Color(1f, 1f, 1f, 0.9f));
            m.SetFloat("_Surface", 1f);
            m.SetFloat("_Blend", 0f);
            m.SetOverrideTag("RenderType", "Transparent");
            m.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
            m.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
            m.SetInt("_ZWrite", 0);
            m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            m.renderQueue = (int)RenderQueue.Transparent;
            AssetDatabase.CreateAsset(m, Gen + "/M_Snow_Particle.mat");
            return m;
        }

        static void BuildTerrain(Mats M)
        {
            var td = new TerrainData();
            td.heightmapResolution = 1025;
            td.alphamapResolution = 1024;
            td.size = new Vector3(TerrainSize, TerrainHeight, TerrainSize);
            AssetDatabase.DeleteAsset(Gen + "/TD_MoonlitVillage.asset");
            AssetDatabase.CreateAsset(td, Gen + "/TD_MoonlitVillage.asset");
            td.terrainLayers = new[] { M.grass, M.dirt, M.paving };

            int res = td.heightmapResolution;
            var heights = new float[res, res];
            for (int iz = 0; iz < res; iz++)
                for (int ix = 0; ix < res; ix++)
                {
                    float x = TerrainMinX + ix / (float)(res - 1) * TerrainSize;
                    float z = TerrainMinZ + iz / (float)(res - 1) * TerrainSize;
                    heights[iz, ix] = Mathf.Clamp01(TerrainHeightAt(x, z) / TerrainHeight);
                }
            td.SetHeights(0, 0, heights);

            int ar = td.alphamapResolution;
            var alpha = new float[ar, ar, 3];
            for (int iz = 0; iz < ar; iz++)
                for (int ix = 0; ix < ar; ix++)
                {
                    float x = TerrainMinX + ix / (float)(ar - 1) * TerrainSize;
                    float z = TerrainMinZ + iz / (float)(ar - 1) * TerrainSize;
                    float sdf = WaterSdf(x, z);
                    float dirt = 0f, paving = 0f;
                    if (sdf < 0.2f) dirt = Mathf.Clamp01(Mathf.InverseLerp(0.2f, -0.8f, sdf));
                    if (InDirtPath(x, z)) dirt = 1f;
                    float pd = Vector2.Distance(new Vector2(x, z), PlateauCenter);
                    float pn = (Mathf.PerlinNoise(x * 0.1f + 900f, z * 0.1f + 30f) - 0.5f) * 5f;
                    if (pd < L(15f) + pn) paving = Mathf.Clamp01(Mathf.InverseLerp(L(15f) + pn, L(11f) + pn, pd));
                    float grass = Mathf.Clamp01(1f - dirt - paving);
                    alpha[iz, ix, 0] = grass; alpha[iz, ix, 1] = dirt; alpha[iz, ix, 2] = paving;
                }
            td.SetAlphamaps(0, 0, alpha);
            EditorUtility.SetDirty(td);
            AssetDatabase.SaveAssets();

            var check = td.GetAlphamaps(0, 0, ar, ar);
            float dirtSum = 0f, pavSum = 0f;
            for (int a = 0; a < ar; a++) for (int b = 0; b < ar; b++) { dirtSum += check[a, b, 1]; pavSum += check[a, b, 2]; }
            Debug.Log("[MoonlitVillage] alphamap check after save: dirt sum=" + dirtSum + " paving sum=" + pavSum + " layers=" + td.alphamapLayers);

            var go = Terrain.CreateTerrainGameObject(td);
            go.name = "Terrain";
            go.transform.position = new Vector3(TerrainMinX, 0f, TerrainMinZ);
            var t = go.GetComponent<Terrain>();
            t.heightmapPixelError = 3f;
            t.shadowCastingMode = ShadowCastingMode.TwoSided;

            // 주인공 이동 시스템(SuperCharacterController)은 밟은 콜라이더의 SuperCollisionType에서 오를 수 있는 경사를 읽는다 (없으면 기본 80°)
            // StandAngle = 서 있을 수 있는 최대 경사. 기본 80°면 절벽 위에서도 "땅에 서 있음"이 되어 점프로 계속 올라갈 수 있음 → 경사 제한과 같게
            var slope = go.AddComponent<SuperCollisionType>();
            slope.SlopeLimit = TerrainSlopeLimit;
            slope.StandAngle = TerrainSlopeLimit;
        }

        static void BuildWater(Mats M)
        {
            var root = new GameObject("Water");
            var p = GameObject.CreatePrimitive(PrimitiveType.Plane);
            p.name = "WaterSurface";
            p.transform.SetParent(root.transform);
            p.transform.position = new Vector3(TerrainMinX + TerrainSize / 2f, WaterY, TerrainMinZ + TerrainSize / 2f);
            p.transform.localScale = new Vector3(TerrainSize / 10f, 1f, TerrainSize / 10f);
            p.GetComponent<MeshRenderer>().sharedMaterial = M.water;
            p.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;
            UnityEngine.Object.DestroyImmediate(p.GetComponent<Collider>());
        }

        static GameObject Prim(PrimitiveType type, string name, Transform parent, Vector3 pos, Vector3 scale, Material m, Quaternion? rot = null, bool collider = true)
        {
            var g = GameObject.CreatePrimitive(type);
            g.name = name;
            g.transform.SetParent(parent);
            g.transform.position = pos;
            g.transform.rotation = rot ?? Quaternion.identity;
            g.transform.localScale = scale;
            g.GetComponent<MeshRenderer>().sharedMaterial = m;
            if (!collider) UnityEngine.Object.DestroyImmediate(g.GetComponent<Collider>());
            return g;
        }

        class MeshBuilder
        {
            readonly List<Vector3> v = new List<Vector3>(); readonly List<Vector3> n = new List<Vector3>();
            readonly List<Vector2> uv = new List<Vector2>(); readonly List<int> t = new List<int>();
            readonly float uvScale;
            public MeshBuilder(float uvScale = 0.25f) { this.uvScale = uvScale; }
            Vector2 UV(Vector3 p, Vector3 nn)
            {
                Vector3 a = new Vector3(Mathf.Abs(nn.x), Mathf.Abs(nn.y), Mathf.Abs(nn.z));
                if (a.y >= a.x && a.y >= a.z) return new Vector2(p.x, p.z) * uvScale;
                if (a.x >= a.z) return new Vector2(p.z, p.y) * uvScale;
                return new Vector2(p.x, p.y) * uvScale;
            }
            public void Tri(Vector3 a, Vector3 b, Vector3 c, Vector3 want)
            {
                Vector3 nn = Vector3.Cross(b - a, c - a).normalized;
                if (Vector3.Dot(nn, want) < 0f) { var tmp = b; b = c; c = tmp; nn = -nn; }
                int i = v.Count;
                v.Add(a); v.Add(b); v.Add(c); n.Add(nn); n.Add(nn); n.Add(nn);
                uv.Add(UV(a, nn)); uv.Add(UV(b, nn)); uv.Add(UV(c, nn));
                t.Add(i); t.Add(i + 1); t.Add(i + 2);
            }
            public void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector3 want) { Tri(a, b, c, want); Tri(a, c, d, want); }
            public Mesh ToMesh(string name)
            {
                var m = new Mesh { name = name };
                m.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
                m.SetVertices(v); m.SetNormals(n); m.SetUVs(0, uv); m.SetTriangles(t, 0);
                m.RecalculateBounds();
                return m;
            }
        }

        static GameObject MeshObj(string name, Transform parent, Mesh mesh, Material mat, Vector3 pos, Quaternion rot)
        {
            var g = new GameObject(name);
            g.transform.SetParent(parent);
            g.transform.position = pos;
            g.transform.rotation = rot;
            g.AddComponent<MeshFilter>().sharedMesh = mesh;
            g.AddComponent<MeshRenderer>().sharedMaterial = mat;
            g.AddComponent<MeshCollider>().sharedMesh = mesh;
            return g;
        }

        static Mesh NGonPrism(int sides, float radius, float depth, float rotDeg, string name)
        {
            var mb = new MeshBuilder(0.3f);
            var pts = new Vector3[sides];
            for (int i = 0; i < sides; i++)
            {
                float a = (rotDeg + 360f * i / sides) * Mathf.Deg2Rad;
                pts[i] = new Vector3(Mathf.Cos(a) * radius, Mathf.Sin(a) * radius, 0);
            }
            float hz = depth / 2f;
            for (int i = 0; i < sides; i++)
            {
                var p0 = pts[i]; var p1 = pts[(i + 1) % sides];
                mb.Tri(new Vector3(0, 0, -hz), p0 + Vector3.back * hz, p1 + Vector3.back * hz, Vector3.back);
                mb.Tri(new Vector3(0, 0, hz), p0 + Vector3.forward * hz, p1 + Vector3.forward * hz, Vector3.forward);
                Vector3 side = new Vector3((p0.x + p1.x) / 2f, (p0.y + p1.y) / 2f, 0).normalized;
                mb.Quad(p0 + Vector3.back * hz, p0 + Vector3.forward * hz, p1 + Vector3.forward * hz, p1 + Vector3.back * hz, side);
            }
            var m = mb.ToMesh(name);
            AssetDatabase.CreateAsset(m, Gen + "/Mesh_" + name + ".asset");
            return m;
        }

        // withBottom: 아랫면(바닥 판)을 넣을지. 들어갈 수 있는 건물은 아랫면이 방 안에서 평평한 천장처럼 보이므로 뺀다.
        // inward: 면이 안쪽(방 안)을 향하는 지붕 (실내에서 보이는 안쪽 지붕면)
        static Mesh HipRoof(float width, float depth, float h, string name, bool withBottom = true, bool inward = false)
        {
            float hw = width / 2f, hd = depth / 2f;
            float hr = Mathf.Max(0.5f, hw - hd);
            float sign = inward ? -1f : 1f;
            var mb = new MeshBuilder(0.2f);
            Vector3 A = new Vector3(-hw, 0, -hd), B = new Vector3(hw, 0, -hd), C = new Vector3(hw, 0, hd), D = new Vector3(-hw, 0, hd);
            Vector3 R0 = new Vector3(-hr, h, 0), R1 = new Vector3(hr, h, 0);
            mb.Quad(A, R0, R1, B, sign * new Vector3(0, hd, -h));
            mb.Quad(C, R1, R0, D, sign * new Vector3(0, hd, h));
            mb.Tri(D, R0, A, sign * new Vector3(-h, hw, 0));
            mb.Tri(B, R1, C, sign * new Vector3(h, hw, 0));
            if (withBottom && !inward) mb.Quad(A, B, C, D, Vector3.down);
            var m = mb.ToMesh(name);
            AssetDatabase.CreateAsset(m, Gen + "/Mesh_" + name + ".asset");
            return m;
        }

        static Mesh WedgeMesh(float xi, float xo, float yBottom, float yTopOut, float yTopIn, float width, string name)
        {
            var mb = new MeshBuilder(0.25f);
            float hz = width / 2f;
            Vector3 A = new Vector3(xo, yBottom, -hz), B = new Vector3(xo, yTopOut, -hz), C = new Vector3(xi, yTopIn, -hz), D = new Vector3(xi, yBottom, -hz);
            Vector3 A2 = A + Vector3.forward * width, B2 = B + Vector3.forward * width, C2 = C + Vector3.forward * width, D2 = D + Vector3.forward * width;
            Vector3 slopeN = Vector3.Cross(Vector3.forward, (C - B)).normalized; if (slopeN.y < 0) slopeN = -slopeN;
            mb.Quad(B, C, C2, B2, slopeN);
            mb.Quad(A, B, C, D, Vector3.back);
            mb.Quad(D2, C2, B2, A2, Vector3.forward);
            mb.Quad(A, B, B2, A2, new Vector3(Mathf.Sign(xo - xi), 0, 0));
            mb.Quad(D, C, C2, D2, new Vector3(Mathf.Sign(xi - xo), 0, 0));
            var m = mb.ToMesh(name);
            AssetDatabase.CreateAsset(m, Gen + "/Mesh_" + name + ".asset");
            return m;
        }

        static Mesh BuildArchBridgeMesh(float length, float width, float yBottom, float yTop, float archCenterY, float r, float a, int segs, string name)
        {
            var mb = new MeshBuilder(0.25f);
            float hz = width / 2f;
            float BottomAt(float x) => Mathf.Abs(x) < r ? Mathf.Max(yBottom, archCenterY + a * Mathf.Sqrt(1f - (x * x) / (r * r))) : yBottom;
            float x0 = -length / 2f, dx = length / segs;
            for (int i = 0; i < segs; i++)
            {
                float xa = x0 + i * dx, xb = xa + dx;
                float ba = BottomAt(xa), bb = BottomAt(xb);
                mb.Quad(new Vector3(xa, ba, -hz), new Vector3(xa, yTop, -hz), new Vector3(xb, yTop, -hz), new Vector3(xb, bb, -hz), Vector3.back);
                mb.Quad(new Vector3(xb, bb, hz), new Vector3(xb, yTop, hz), new Vector3(xa, yTop, hz), new Vector3(xa, ba, hz), Vector3.forward);
                mb.Quad(new Vector3(xa, yTop, -hz), new Vector3(xa, yTop, hz), new Vector3(xb, yTop, hz), new Vector3(xb, yTop, -hz), Vector3.up);
                Vector3 tangent = new Vector3(xb - xa, bb - ba, 0).normalized;
                mb.Quad(new Vector3(xa, ba, hz), new Vector3(xa, ba, -hz), new Vector3(xb, bb, -hz), new Vector3(xb, bb, hz), new Vector3(tangent.y, -tangent.x, 0));
            }
            float xl = x0, xr = x0 + length;
            mb.Quad(new Vector3(xl, yBottom, hz), new Vector3(xl, yTop, hz), new Vector3(xl, yTop, -hz), new Vector3(xl, yBottom, -hz), Vector3.left);
            mb.Quad(new Vector3(xr, yBottom, -hz), new Vector3(xr, yTop, -hz), new Vector3(xr, yTop, hz), new Vector3(xr, yBottom, hz), Vector3.right);
            var m = mb.ToMesh(name);
            AssetDatabase.CreateAsset(m, Gen + "/Mesh_" + name + ".asset");
            return m;
        }

        static void BuildBridge(Mats M, Vector3 center, string name)
        {
            var root = new GameObject(name);
            var body = new GameObject("Body");
            body.transform.SetParent(root.transform);
            var mesh = BuildArchBridgeMesh(BridgeLength, BridgeWidth, WaterY - 2.0f, DeckY, WaterY, ArchRadius, ArchHeight, 48, name + "_Body");
            body.AddComponent<MeshFilter>().sharedMesh = mesh;
            body.AddComponent<MeshRenderer>().sharedMaterial = M.stone;
            body.AddComponent<MeshCollider>().sharedMesh = mesh;

            Prim(PrimitiveType.Cube, "DeckPaving", root.transform, new Vector3(0, DeckY + 0.03f, 0), new Vector3(BridgeLength, 0.06f, BridgeWidth), M.deck);

            for (int s = -1; s <= 1; s += 2)
            {
                float xi = s * (BridgeLength / 2f - 0.05f);
                float xo = s * (BridgeLength / 2f + RampLen);
                var wedge = WedgeMesh(xi, xo, Ground - 1.5f, Ground + 0.04f, DeckY + 0.06f, BridgeWidth, name + "_Ramp" + (s < 0 ? "W" : "E"));
                MeshObj("Ramp", root.transform, wedge, M.deck, Vector3.zero, Quaternion.identity);
                foreach (int side in new[] { -1, 1 })
                    Prim(PrimitiveType.Cube, "RampCurb", root.transform, new Vector3((xi + xo) / 2f, Ground + DeckRise / 2f + 0.1f, side * (BridgeWidth / 2f + 0.15f)),
                        new Vector3(RampLen + 0.4f, 0.3f, 0.3f), M.stone, Quaternion.Euler(0, 0, -s * Mathf.Atan2(DeckRise, RampLen) * Mathf.Rad2Deg), false);
            }

            var rails = new GameObject("Railings");
            rails.transform.SetParent(root.transform);
            float postStep = 1.5f;
            int postCount = Mathf.RoundToInt(BridgeLength / postStep) + 1;
            float half = BridgeWidth / 2f - 0.22f;
            for (int side = -1; side <= 1; side += 2)
            {
                float z = side * half;
                Prim(PrimitiveType.Cube, "TopRail", rails.transform, new Vector3(0, DeckY + 1.02f, z), new Vector3(BridgeLength + 0.3f, 0.14f, 0.2f), M.railing, null, false);
                Prim(PrimitiveType.Cube, "MidRail", rails.transform, new Vector3(0, DeckY + 0.52f, z), new Vector3(BridgeLength + 0.3f, 0.08f, 0.12f), M.railing, null, false);
                for (int i = 0; i < postCount; i++)
                {
                    float x = -BridgeLength / 2f + i * postStep;
                    Prim(PrimitiveType.Cube, "Post", rails.transform, new Vector3(x, DeckY + 0.6f, z), new Vector3(0.3f, 1.2f, 0.3f), M.railing, null, false);
                    Prim(PrimitiveType.Sphere, "PostBall", rails.transform, new Vector3(x, DeckY + 1.32f, z), new Vector3(0.36f, 0.36f, 0.36f), M.railing, null, false);
                    if (i < postCount - 1)
                        for (int b = 1; b <= 3; b++)
                            Prim(PrimitiveType.Cube, "Baluster", rails.transform, new Vector3(x + postStep * b / 4f, DeckY + 0.5f, z), new Vector3(0.09f, 0.9f, 0.09f), M.railing, null, false);
                }
            }
            root.transform.position = new Vector3(center.x, 0f, center.z);
        }

        // 촌장집: 섬 위의 기와집. 들어갈 수 있는 한옥 구조로, 왼쪽은 대청(촌장·촌장부인), 오른쪽은 사랑방(온돌방, 잠자는 곳).
        // (2026-10-22: 중국풍 정자(팔각 창·벽돌 벽)를 한옥풍(흰 회벽·나무 기둥·창살 종이창)으로 교체)
        static void BuildHouse(Mats M)
        {
            // 방 크기는 BuildingScale만큼 (2026-10-23 x1.5), 문·계단·창 높이·가구는 사람 크기
            const float W = 14f * BuildingScale, D = 7f * BuildingScale, WallH = 3.2f * BuildingScale, PlinthH = 0.6f, T = 0.3f;
            const float HallDoorW = 2.4f, DoorH = 2.7f, InnerDoorW = 1.8f, InnerDoorH = 2.5f;
            Vector3 H = new Vector3(ChiefHousePos.x, Ground, ChiefHousePos.y);
            float frontZ = H.z - D / 2f, backZ = H.z + D / 2f;
            float floorY = H.y + PlinthH;
            float wallY = floorY + WallH / 2f;
            float partitionX = H.x + 2f * BuildingScale;  // 대청(왼쪽) | 사랑방(오른쪽)
            float hallCX = (H.x - W / 2f + partitionX) / 2f;
            float roomCX = (partitionX + H.x + W / 2f) / 2f;

            var root = new GameObject("촌장집 (House_Chief)");
            root.transform.position = H;
            var t = root.transform;

            // 기단·마루 바닥·계단
            Prim(PrimitiveType.Cube, "Plinth", t, H + new Vector3(0, PlinthH / 2f - 0.15f, 0), new Vector3(W + 1.6f, PlinthH + 0.3f, D + 1.6f), M.plinth);
            Prim(PrimitiveType.Cube, "FloorBoards", t, new Vector3(H.x, floorY + 0.02f, H.z), new Vector3(W - T, 0.04f, D - T), M.plank, null, false);
            Prim(PrimitiveType.Cube, "DoorStep", t, new Vector3(hallCX, Ground + 0.2f, frontZ - 1.0f), new Vector3(2.6f, 0.4f, 1.4f), M.plinth);
            Prim(PrimitiveType.Cube, "DoorStep2", t, new Vector3(hallCX, Ground + 0.5f, frontZ - 0.5f), new Vector3(2.2f, 0.3f, 0.7f), M.plinth);

            // 벽: 앞(대청 문), 뒤, 양옆, 대청과 사랑방 사이 칸막이(문)
            WallX(t, "Wall_Front", H.x, frontZ, W, floorY, WallH, T, M.plaster, HallDoorW, DoorH, hallCX);
            WallX(t, "Wall_Back", H.x, backZ, W, floorY, WallH, T, M.plaster);
            foreach (int sgn in new[] { -1, 1 })
                WallZ(t, "Wall_Side", H.x + sgn * W / 2f, H.z, D, floorY, WallH, T, M.plaster);
            WallZ(t, "Wall_Partition", partitionX, H.z, D, floorY, WallH, T, M.plaster, InnerDoorW, InnerDoorH, H.z - 1.4f * BuildingScale);

            // 나무 기둥·인방·창살 종이창 (장식, 충돌 없음)
            int bays = 5;
            for (int i = 0; i <= bays; i++)
            {
                float x = H.x - W / 2f + W * i / bays;
                Prim(PrimitiveType.Cylinder, "WoodColumn", t, new Vector3(x, wallY, frontZ - 0.25f), new Vector3(0.38f, WallH / 2f, 0.38f), M.darkWood, null, false);
            }
            Prim(PrimitiveType.Cube, "Lintel", t, new Vector3(H.x, floorY + WallH - 0.2f, frontZ - 0.25f), new Vector3(W + 0.3f, 0.26f, 0.3f), M.darkWood, null, false);
            Prim(PrimitiveType.Cube, "BaseSill", t, new Vector3(H.x, floorY + 0.12f, frontZ - 0.2f), new Vector3(W + 0.2f, 0.24f, 0.24f), M.darkWood, null, false);
            for (int i = 0; i < bays; i++)
            {
                float x = H.x - W / 2f + W * (i + 0.5f) / bays;
                if (Mathf.Abs(x - hallCX) < HallDoorW) continue;   // 대청 문 자리는 비움
                Prim(PrimitiveType.Cube, "PaperWindow", t, new Vector3(x, floorY + 1.6f, frontZ - 0.17f), new Vector3(1.9f, 1.4f, 0.04f), M.paperWindow, null, false);
                for (int k = -1; k <= 1; k++)
                    Prim(PrimitiveType.Cube, "Lattice", t, new Vector3(x + k * 0.6f, floorY + 1.6f, frontZ - 0.2f), new Vector3(0.05f, 1.4f, 0.03f), M.darkWood, null, false);
                Prim(PrimitiveType.Cube, "Lattice", t, new Vector3(x, floorY + 1.6f, frontZ - 0.2f), new Vector3(1.9f, 0.05f, 0.03f), M.darkWood, null, false);
            }
            Prim(PrimitiveType.Cube, "SignBoard", t, new Vector3(hallCX, floorY + WallH - 0.55f, frontZ - 0.3f), new Vector3(1.8f, 0.5f, 0.06f), M.signBoard, null, false);

            // 대청: 촌장·촌장부인 자리, 낮은 상
            Prim(PrimitiveType.Cube, "LowTable", t, new Vector3(hallCX, floorY + 0.25f, H.z + 1.6f * BuildingScale), new Vector3(1.6f, 0.5f, 0.9f), M.plank);
            NpcSpotMarker(t, "chief", new Vector3(hallCX - 1.2f * BuildingScale, floorY, H.z + 1.5f * BuildingScale));
            NpcSpotMarker(t, "chief_wife", new Vector3(hallCX + 1.8f * BuildingScale, floorY, H.z + 1.0f * BuildingScale));
            JarStand(M, t, new Vector2(H.x - W / 2f + 2.4f, H.z + 2.2f * BuildingScale), 2, floorY);

            // 사랑방(온돌방): 이불과 잠자리 표시. 플레이어가 여기서 자고 시간대를 넘긴다
            float bedZ = H.z + 1.4f * BuildingScale;
            Prim(PrimitiveType.Cube, "Bedding_Mattress", t, new Vector3(roomCX + 0.4f, floorY + 0.08f, bedZ), new Vector3(1.4f, 0.16f, 2.2f), M.signBoard);
            Prim(PrimitiveType.Cube, "Bedding_Blanket", t, new Vector3(roomCX + 0.4f, floorY + 0.2f, bedZ - 0.3f), new Vector3(1.45f, 0.1f, 1.5f), M.redWood, null, false);
            Prim(PrimitiveType.Cube, "Bedding_Pillow", t, new Vector3(roomCX + 0.4f, floorY + 0.25f, bedZ + 0.85f), new Vector3(0.7f, 0.2f, 0.35f), M.eave, null, false);
            Marker(t, "SleepSpot", new Vector3(roomCX - 0.8f * BuildingScale, floorY, H.z + 0.6f * BuildingScale));

            // 담장은 두지 않는다 (2026-10-23): 1.2배 배치에서 앞마당 양옆 담장이 돌다리 끝(경사로)과 겹침.
            // 섬을 키우면 해자·다리·연못을 같이 옮겨야 하고, 담장이 다리에서 들어오는 길을 막아 동선에도 방해 → 제거

            // 기와 지붕 묶음 (충돌 없음, 들어가면 숨김)
            var roof = new GameObject(RoofGroupName);
            roof.transform.SetParent(t);
            float roofBase = floorY + WallH;
            float overhang = 1.4f;
            const float roofH = 2.6f * BuildingScale;
            float rd = D + overhang * 2f + 0.3f;
            EaveRing(roof.transform, new Vector3(H.x, roofBase + 0.12f, H.z), W, D, overhang, 0.24f, M.darkWood);
            RemoveCollider(MeshObj("Roof", roof.transform, HipRoof(W + overhang * 2f + 0.3f, rd, roofH, "HipRoof", false), M.roof, new Vector3(H.x, roofBase + 0.24f, H.z), Quaternion.identity));
            float ridgeLen = Mathf.Max(1f, (W + overhang * 2f) / 2f - (D + overhang * 2f) / 2f) * 2f;
            Prim(PrimitiveType.Cube, "RidgeBeam", roof.transform, new Vector3(H.x, roofBase + 0.24f + roofH + 0.02f, H.z), new Vector3(ridgeLen + 0.6f, 0.3f, 0.5f), M.windowDark, null, false);
            BuildRoofInterior(M, t, new Vector3(H.x, roofBase, H.z), W, D, roofH * D / rd, "HouseChief");

            RoomLight(t, "PointLight_Hall", new Vector3(hallCX, floorY + WallH * 0.75f, H.z), 6f, 10f * BuildingScale);
            RoomLight(t, "PointLight_Sarangbang", new Vector3(roomCX, floorY + WallH * 0.75f, H.z), 4f, 7f * BuildingScale);
            InteriorTrigger(t, "InteriorTrigger_Hall", new Vector3(hallCX, wallY, H.z), new Vector3(partitionX - (H.x - W / 2f) - T * 2f, WallH, D - T * 2f));
            InteriorTrigger(t, "InteriorTrigger_Sarangbang", new Vector3(roomCX, wallY, H.z), new Vector3((H.x + W / 2f) - partitionX - T * 2f, WallH, D - T * 2f));

            var yardTrees = new GameObject("YardPines"); yardTrees.transform.SetParent(t);
            PlaceTree(yardTrees.transform, new Vector2(H.x - 9.5f * BuildingScale, frontZ - 7f * BuildingScale), YardPineNames, 5.5f, 6.5f, trunkCollider: true);
            PlaceTree(yardTrees.transform, new Vector2(H.x + 8f * BuildingScale, frontZ - 8.5f * BuildingScale), YardPineNames, 6f, 7f, trunkCollider: true);
            Prim(PrimitiveType.Sphere, "InscribedStone", t, new Vector3(H.x + 9f * BuildingScale, Ground + 0.4f, frontZ - 4f * BuildingScale), new Vector3(2.2f, 1.3f, 1.5f), M.inscribedStone, Quaternion.Euler(0, 20, 0));
        }

        // 집 목록(Houses)대로 초가집을 세운다. 앞마당 소품까지 만든 뒤 집 전체를 마을 중심 쪽으로 돌린다.
        static void BuildVillageHouses(Mats M)
        {
            var root = new GameObject("Village_Houses");
            for (int i = 0; i < Houses.Length; i++)
            {
                var h = Houses[i];
                var thatch = h.yellowThatch ? M.thatchYellow : M.thatchOrange;
                // 1차 범위 집(확장 명절 집이 아닌 집)은 들어갈 수 있는 집으로 만든다 (젤다 방식 이어진 실내, 2026-10-22 결정)
                if (!h.extension)
                {
                    var enterable = BuildEnterableThatched(M, root.transform, h, thatch, h.style == HouseStyle.DongjiCompound);
                    BuildYardProps(M, enterable.transform, h.pos, i);
                    enterable.transform.rotation = Quaternion.Euler(0f, HouseYaw(h.pos), 0f);
                    continue;
                }
                var house = BuildThatchedHouse(M, root.transform, h.pos, h.label + " (House_" + h.id + ")", thatch, h.gourds,
                    h.style == HouseStyle.DoubleDoor, h.style == HouseStyle.OpenDoorway, "House_" + h.id);
                BuildYardProps(M, house.transform, h.pos, i);

                // 확장 명절용 빈 집 (속이 찬 겉모습만, 들어갈 수 없음): 실내 불빛을 꺼서 사람이 살지 않는 집으로 보이게 한다.
                if (h.extension)
                    foreach (var light in house.GetComponentsInChildren<Light>()) light.enabled = false;

                // 정면(-z)이 마을 중심을 보도록 집 전체를 회전 (자식들은 집 중심을 기준으로 함께 돈다)
                house.transform.rotation = Quaternion.Euler(0f, HouseYaw(h.pos), 0f);
            }
        }

        // 앞마당 소품. 회전 전 기준(정면 = -z)으로 배치한다. variant마다 조합을 바꿔 집들이 똑같아 보이지 않게 한다.
        // 집 중심에서 (dx, dz)만큼 떨어진 앞마당 위치. 집이 커진 만큼(BuildingScale) 바깥으로 밀어 낸다 (소품 크기는 그대로)
        static Vector2 Yard(Vector2 p, float dx, float dz) => p + new Vector2(dx, dz) * BuildingScale;

        static void BuildYardProps(Mats M, Transform house, Vector2 p, int variant)
        {
            switch (variant % 3)
            {
                case 0:
                    JarStand(M, house, Yard(p, 7.5f, -1f), 5);
                    Well(M, house, Yard(p, -8f, -6f));
                    break;
                case 1:
                    JarStand(M, house, Yard(p, -7.5f, -1f), 4);
                    Mortar(M, house, Yard(p, 6f, -6.5f));
                    WoodBench(M, house, Yard(p, -4f, -8f));
                    break;
                default:
                    for (int k = 0; k < 3; k++)
                    {
                        Vector2 q = Yard(p, 5f, -6f) + new Vector2(k * 2.2f, 0f);
                        Prim(PrimitiveType.Sphere, "HayStack", house, new Vector3(q.x, TerrainHeightAt(q.x, q.y) + 0.6f, q.y), new Vector3(1.8f, 1.5f, 1.8f), M.thatchYellow, null, false);
                    }
                    Well(M, house, Yard(p, -8f, -4f));
                    break;
            }
            var pines = new GameObject("YardPines"); pines.transform.SetParent(house);
            PlaceTree(pines.transform, Yard(p, variant % 2 == 0 ? 7f : -7f, 7f), YardPineNames, 5f, 7f, trunkCollider: true);
        }

        // assetId: 생성되는 지붕 메시 파일 이름에 쓸 영문 이름 (null이면 name 사용)
        static GameObject BuildThatchedHouse(Mats M, Transform parent, Vector2 pos2, string name, Material thatch, bool gourds, bool doubleDoor = false, bool openDoorway = false, string assetId = null)
        {
            // 겉모습만 있는 집도 마을 균형을 위해 같은 배율로 (문·창 높이는 사람 크기, 너비는 아래에서 상한을 둠)
            const float W = 9.5f * BuildingScale, D = 6f * BuildingScale, WallH = 2.6f * BuildingScale, PlinthH = 0.35f;
            Vector3 H = new Vector3(pos2.x, Ground, pos2.y);
            float frontZ = H.z - D / 2f;
            var root = new GameObject(name);
            root.transform.SetParent(parent);
            root.transform.position = H;

            Prim(PrimitiveType.Cube, "Plinth", root.transform, H + new Vector3(0, PlinthH / 2f - 0.1f, 0), new Vector3(W + 1.2f, PlinthH + 0.2f, D + 1.2f), M.plinth);
            float wallBase = H.y + PlinthH;
            Prim(PrimitiveType.Cube, "Walls", root.transform, new Vector3(H.x, wallBase + WallH / 2f, H.z), new Vector3(W, WallH, D), M.plaster);
            for (int i = 0; i <= 3; i++)
            {
                float x = H.x - W / 2f + W * i / 3f;
                Prim(PrimitiveType.Cube, "Post", root.transform, new Vector3(x, wallBase + WallH / 2f, frontZ - 0.12f), new Vector3(0.28f, WallH, 0.28f), M.darkWood);
            }
            foreach (int sgn in new[] { -1, 1 })
                Prim(PrimitiveType.Cube, "PostBack", root.transform, new Vector3(H.x + sgn * W / 2f, wallBase + WallH / 2f, H.z + D / 2f + 0.12f), new Vector3(0.28f, WallH, 0.28f), M.darkWood);
            Prim(PrimitiveType.Cube, "Lintel", root.transform, new Vector3(H.x, wallBase + WallH - 0.15f, frontZ - 0.12f), new Vector3(W + 0.2f, 0.22f, 0.26f), M.darkWood);
            Prim(PrimitiveType.Cube, "Sill", root.transform, new Vector3(H.x, wallBase + 0.12f, frontZ - 0.12f), new Vector3(W + 0.2f, 0.22f, 0.26f), M.darkWood);
            float bay = W / 3f;
            float doorwayW = Mathf.Min(bay - 0.2f, 2.4f), windowW = Mathf.Min(bay - 0.9f, 2.4f), smallWindowW = Mathf.Min(bay - 1.4f, 1.8f), doorW = Mathf.Min(bay - 1.1f, 1.6f);
            if (doubleDoor)
            {
                Prim(PrimitiveType.Cube, "StoneBand", root.transform, new Vector3(H.x, wallBase + 0.55f, H.z), new Vector3(W + 0.12f, 1.1f, D + 0.12f), M.fence);
            }
            for (int i = 0; i < 3; i++)
            {
                float x = H.x - W / 2f + bay * (i + 0.5f);
                if ((doubleDoor || openDoorway) && i == 1)
                {
                    if (openDoorway)
                    {
                        Prim(PrimitiveType.Cube, "DarkDoorway", root.transform, new Vector3(x, wallBase + 1.15f, frontZ - 0.04f), new Vector3(doorwayW, 2.3f, 0.06f), M.windowDark, null, false);
                        Prim(PrimitiveType.Cube, "DoorwayLintel", root.transform, new Vector3(x, wallBase + 2.4f, frontZ - 0.14f), new Vector3(doorwayW + 0.6f, 0.25f, 0.24f), M.darkWood, null, false);
                        foreach (int sgn in new[] { -1, 1 })
                            Prim(PrimitiveType.Capsule, "ThatchPillar", root.transform, new Vector3(x + sgn * (doorwayW / 2f + 0.25f), wallBase + WallH / 2f, frontZ - 0.25f), new Vector3(0.7f, WallH / 2f + 0.1f, 0.7f), thatch, null, false);
                    }
                    else
                    {
                        foreach (int sgn in new[] { -1, 1 })
                            Prim(PrimitiveType.Cube, "DoorLeaf", root.transform, new Vector3(x + sgn * 0.5f, wallBase + 1.1f, frontZ - 0.07f), new Vector3(0.94f, 2.0f, 0.08f), M.plank);
                        Prim(PrimitiveType.Cube, "DoorGap", root.transform, new Vector3(x, wallBase + 1.1f, frontZ - 0.09f), new Vector3(0.05f, 2.0f, 0.03f), M.windowDark, null, false);
                        Prim(PrimitiveType.Cube, "DoorFrame", root.transform, new Vector3(x, wallBase + 2.15f, frontZ - 0.1f), new Vector3(2.2f, 0.14f, 0.12f), M.darkWood, null, false);
                    }
                    continue;
                }
                if (openDoorway)
                {
                    Prim(PrimitiveType.Cube, "ThatchWallPanel", root.transform, new Vector3(x, wallBase + WallH / 2f, frontZ - 0.08f), new Vector3(bay - 0.6f, WallH - 0.3f, 0.1f), thatch, null, false);
                    Prim(PrimitiveType.Cube, "Vent", root.transform, new Vector3(x, wallBase + 1.9f, frontZ - 0.15f), new Vector3(0.7f, 0.35f, 0.06f), M.windowDark, null, false);
                    continue;
                }
                if (doubleDoor)
                {
                    Prim(PrimitiveType.Cube, "PaperWindowSmall", root.transform, new Vector3(x, wallBase + 1.85f, frontZ - 0.06f), new Vector3(smallWindowW, 0.8f, 0.06f), M.paperWindow);
                    Prim(PrimitiveType.Cube, "Lattice", root.transform, new Vector3(x, wallBase + 1.85f, frontZ - 0.1f), new Vector3(0.05f, 0.8f, 0.04f), M.darkWood, null, false);
                    Prim(PrimitiveType.Cube, "Lattice", root.transform, new Vector3(x, wallBase + 1.85f, frontZ - 0.1f), new Vector3(smallWindowW, 0.05f, 0.04f), M.darkWood, null, false);
                    continue;
                }
                if (i == 1)
                {
                    Prim(PrimitiveType.Cube, "PaperWindow", root.transform, new Vector3(x, wallBase + 1.55f, frontZ - 0.06f), new Vector3(windowW, 1.3f, 0.06f), M.paperWindow);
                    for (int k = -1; k <= 1; k++)
                        Prim(PrimitiveType.Cube, "Lattice", root.transform, new Vector3(x + k * windowW / 3.2f, wallBase + 1.55f, frontZ - 0.1f), new Vector3(0.05f, 1.3f, 0.04f), M.darkWood, null, false);
                    Prim(PrimitiveType.Cube, "Lattice", root.transform, new Vector3(x, wallBase + 1.55f, frontZ - 0.1f), new Vector3(windowW, 0.05f, 0.04f), M.darkWood, null, false);
                }
                else
                {
                    Prim(PrimitiveType.Cube, "Door", root.transform, new Vector3(x, wallBase + 1.1f, frontZ - 0.06f), new Vector3(doorW, 2.0f, 0.06f), M.paperWindow);
                    Prim(PrimitiveType.Cube, "DoorFrame", root.transform, new Vector3(x, wallBase + 1.1f, frontZ - 0.09f), new Vector3(0.06f, 2.0f, 0.05f), M.darkWood, null, false);
                }
            }
            if (!openDoorway) Prim(PrimitiveType.Cube, "Maru", root.transform, new Vector3(H.x, wallBase - 0.05f, frontZ - 0.7f), new Vector3(W - 0.4f, 0.16f, 1.3f), M.plank);
            float roofBase = wallBase + WallH;
            float overhang = 1.3f;
            Prim(PrimitiveType.Cube, "EaveBoard", root.transform, new Vector3(H.x, roofBase + 0.08f, H.z), new Vector3(W + overhang * 2f, 0.16f, D + overhang * 2f), M.darkWood);
            float rw = W + overhang * 2f + 0.4f, rd = D + overhang * 2f + 0.4f, rh = ThatchRoofH;
            var roofMesh = HipRoof(rw, rd, rh, (assetId ?? name.Replace(" ", "_")) + "_Thatch");
            MeshObj("ThatchRoof", root.transform, roofMesh, thatch, new Vector3(H.x, roofBase + 0.16f, H.z), Quaternion.identity);
            float ridgeLen = Mathf.Max(1f, rw / 2f - rd / 2f) * 2f;
            var ridge = Prim(PrimitiveType.Capsule, "ThatchRidge", root.transform, new Vector3(H.x, roofBase + 0.16f + rh - 0.1f, H.z), new Vector3(1.4f, ridgeLen / 2f + 0.6f, 1.4f), thatch, Quaternion.Euler(0, 0, 90), false);
            ridge.transform.localScale = new Vector3(0.9f, (ridgeLen + 1.0f) / 2f, 0.75f);
            foreach (int sgn in new[] { -1, 1 })
            {
                Prim(PrimitiveType.Capsule, "EaveRoll", root.transform, new Vector3(H.x, roofBase + 0.2f, H.z + sgn * rd / 2f), new Vector3(0.9f, rw / 2f, 0.9f), thatch, Quaternion.Euler(0, 0, 90), false);
                Prim(PrimitiveType.Capsule, "EaveRoll", root.transform, new Vector3(H.x + sgn * rw / 2f, roofBase + 0.2f, H.z), new Vector3(0.9f, rd / 2f, 0.9f), thatch, Quaternion.Euler(90, 0, 0), false);
            }
            if (openDoorway)
            {
                Prim(PrimitiveType.Sphere, "ThatchDome", root.transform, new Vector3(H.x, roofBase + 0.16f + rh * 0.35f, H.z), new Vector3(rw * 0.78f, rh * 1.5f, rd * 0.85f), thatch, null, false);
                for (int i = 0; i < 9; i++)
                {
                    float ax = R(-rw * 0.28f, rw * 0.28f), az = R(-rd * 0.28f, rd * 0.28f);
                    float ay = roofBase + 0.16f + rh * 0.35f + rh * 0.75f * Mathf.Sqrt(Mathf.Max(0f, 1f - (ax * ax) / (rw * rw * 0.15f) - (az * az) / (rd * rd * 0.18f)));
                    Prim(PrimitiveType.Sphere, "RoofGrass", root.transform, new Vector3(H.x + ax, ay - 0.15f, H.z + az), new Vector3(R(0.8f, 1.4f), R(0.25f, 0.4f), R(0.8f, 1.4f)), M.gourdVine, null, false);
                }
            }
            if (gourds)
            {
                var vine = new GameObject("GourdVine"); vine.transform.SetParent(root.transform);
                for (int i = 0; i < 7; i++)
                {
                    float x = H.x + R(-rw * 0.4f, rw * 0.4f), z = H.z + R(-rd * 0.25f, rd * 0.25f);
                    float y = roofBase + 0.16f + rh * (1f - Mathf.Abs(z - H.z) / (rd / 2f)) - 0.1f;
                    Prim(PrimitiveType.Sphere, "Gourd", vine.transform, new Vector3(x, y, z), Vector3.one * R(0.5f, 0.8f), i % 2 == 0 ? M.pumpkin : M.gourdVine, null, false);
                    Prim(PrimitiveType.Sphere, "Leaf", vine.transform, new Vector3(x + R(-0.6f, 0.6f), y + 0.15f, z + R(-0.4f, 0.4f)), new Vector3(0.7f, 0.12f, 0.7f), M.gourdVine, null, false);
                }
            }
            var pl = new GameObject("PointLight_Indoor");
            pl.transform.SetParent(root.transform);
            pl.transform.position = new Vector3(H.x, wallBase + 1.4f, frontZ - 0.6f);
            var l = pl.AddComponent<Light>();
            l.type = LightType.Point; l.color = new Color(1f, 0.68f, 0.38f); l.intensity = 7f; l.range = 9f; l.shadows = LightShadows.None;
            return root;
        }


        // 칠성당: 섬 위, 촌장집 앞의 작은 사당. 안으로 들어가 칠성당 NPC를 만난다 (설날: 떡국 → 설빔)
        static void BuildShrine(Mats M)
        {
            const float W = 5.5f * BuildingScale, D = 4.5f * BuildingScale, WallH = 2.6f * BuildingScale, PlinthH = 0.5f, T = 0.3f, DoorW = 2.0f, DoorH = 2.6f;
            Vector3 H = new Vector3(ShrinePos.x, Ground, ShrinePos.y);
            float frontZ = H.z - D / 2f, backZ = H.z + D / 2f;
            float floorY = H.y + PlinthH;
            float wallY = floorY + WallH / 2f;
            var root = new GameObject("칠성당 (Shrine_Chilseong)");
            root.transform.position = H;
            var t = root.transform;

            Prim(PrimitiveType.Cube, "Plinth", t, H + new Vector3(0, PlinthH / 2f - 0.1f, 0), new Vector3(W + 1.4f, PlinthH + 0.2f, D + 1.4f), M.plinth);
            Prim(PrimitiveType.Cube, "FloorBoards", t, new Vector3(H.x, floorY + 0.02f, H.z), new Vector3(W - T, 0.04f, D - T), M.plank, null, false);
            Prim(PrimitiveType.Cube, "Step", t, new Vector3(H.x, Ground + 0.15f, frontZ - 1.2f), new Vector3(2.4f, 0.3f, 1.0f), M.plinth);

            WallX(t, "Wall_Front", H.x, frontZ, W, floorY, WallH, T, M.plaster, DoorW, DoorH);
            WallX(t, "Wall_Back", H.x, backZ, W, floorY, WallH, T, M.plaster);
            foreach (int sgn in new[] { -1, 1 })
                WallZ(t, "Wall_Side", H.x + sgn * W / 2f, H.z, D, floorY, WallH, T, M.plaster);
            foreach (int sx in new[] { -1, 1 })
                foreach (float cz in new[] { frontZ - 0.15f, backZ + 0.15f })
                    Prim(PrimitiveType.Cylinder, "RedColumn", t, new Vector3(H.x + sx * (W / 2f), wallY, cz), new Vector3(0.36f, WallH / 2f, 0.36f), M.redWood, null, false);
            Prim(PrimitiveType.Cube, "SignBoard", t, new Vector3(H.x, floorY + WallH - 0.25f, frontZ - 0.18f), new Vector3(1.4f, 0.4f, 0.06f), M.signBoard, null, false);

            // 제단과 NPC 자리
            Prim(PrimitiveType.Cube, "Altar", t, new Vector3(H.x, floorY + 0.45f, backZ - 0.8f), new Vector3(2.4f, 0.9f, 0.8f), M.redWood);
            Prim(PrimitiveType.Cube, "AltarCloth", t, new Vector3(H.x, floorY + 0.92f, backZ - 0.8f), new Vector3(2.5f, 0.05f, 0.85f), M.signBoard, null, false);
            NpcSpotMarker(t, "shrine", new Vector3(H.x + 1.0f * BuildingScale, floorY, H.z));

            var roof = new GameObject(RoofGroupName);
            roof.transform.SetParent(t);
            float roofBase = floorY + WallH;
            const float roofH = 1.8f * BuildingScale;
            EaveRing(roof.transform, new Vector3(H.x, roofBase + 0.1f, H.z), W, D, 1.2f, 0.2f, M.eave);
            RemoveCollider(MeshObj("Roof", roof.transform, HipRoof(W + 2.7f, D + 2.7f, roofH, "ShrineRoof", false), M.roof, new Vector3(H.x, roofBase + 0.2f, H.z), Quaternion.identity));
            Prim(PrimitiveType.Cube, "RidgeBeam", roof.transform, new Vector3(H.x, roofBase + 0.2f + roofH, H.z), new Vector3(1.6f * BuildingScale, 0.25f, 0.4f), M.windowDark, null, false);
            BuildRoofInterior(M, t, new Vector3(H.x, roofBase, H.z), W, D, roofH * D / (D + 2.7f), "Shrine");

            RoomLight(t, "PointLight_Shrine", new Vector3(H.x, floorY + WallH * 0.75f, H.z), 4f, 6f * BuildingScale);
            InteriorTrigger(t, "InteriorTrigger_Shrine", new Vector3(H.x, wallY, H.z), new Vector3(W - T * 2f, WallH, D - T * 2f));
        }

        // ── 들어갈 수 있는 건물 공통 도구 (회전 전 기준: 정면 = -z) ──────────────────────────────

        // x 방향으로 뻗은 벽. gapW > 0이면 gapCenterX 위치에 문 구멍을 남긴다.
        static void WallX(Transform root, string name, float cx, float z, float width, float floorY, float h, float t, Material m,
                          float gapW = 0f, float gapH = 0f, float gapCenterX = float.NaN)
        {
            float wallY = floorY + h / 2f;
            if (gapW <= 0f)
            {
                Prim(PrimitiveType.Cube, name, root, new Vector3(cx, wallY, z), new Vector3(width, h, t), m);
                return;
            }
            float gx = float.IsNaN(gapCenterX) ? cx : gapCenterX;
            float x0 = cx - width / 2f, x1 = cx + width / 2f;
            float leftW = (gx - gapW / 2f) - x0, rightW = x1 - (gx + gapW / 2f);
            if (leftW > 0.01f) Prim(PrimitiveType.Cube, name + "_L", root, new Vector3(x0 + leftW / 2f, wallY, z), new Vector3(leftW, h, t), m);
            if (rightW > 0.01f) Prim(PrimitiveType.Cube, name + "_R", root, new Vector3(x1 - rightW / 2f, wallY, z), new Vector3(rightW, h, t), m);
            Prim(PrimitiveType.Cube, name + "_Top", root, new Vector3(gx, floorY + gapH + (h - gapH) / 2f, z), new Vector3(gapW, h - gapH, t), m);
        }

        // z 방향으로 뻗은 벽. gapW > 0이면 gapCenterZ 위치에 문 구멍을 남긴다.
        static void WallZ(Transform root, string name, float x, float cz, float depth, float floorY, float h, float t, Material m,
                          float gapW = 0f, float gapH = 0f, float gapCenterZ = float.NaN)
        {
            float wallY = floorY + h / 2f;
            if (gapW <= 0f)
            {
                Prim(PrimitiveType.Cube, name, root, new Vector3(x, wallY, cz), new Vector3(t, h, depth), m);
                return;
            }
            float gz = float.IsNaN(gapCenterZ) ? cz : gapCenterZ;
            float z0 = cz - depth / 2f, z1 = cz + depth / 2f;
            float aW = (gz - gapW / 2f) - z0, bW = z1 - (gz + gapW / 2f);
            if (aW > 0.01f) Prim(PrimitiveType.Cube, name + "_A", root, new Vector3(x, wallY, z0 + aW / 2f), new Vector3(t, h, aW), m);
            if (bW > 0.01f) Prim(PrimitiveType.Cube, name + "_B", root, new Vector3(x, wallY, z1 - bW / 2f), new Vector3(t, h, bW), m);
            Prim(PrimitiveType.Cube, name + "_Top", root, new Vector3(x, floorY + gapH + (h - gapH) / 2f, gz), new Vector3(t, h - gapH, gapW), m);
        }

        // 실내 감지 트리거. 지붕 숨기기·장소 이름 표시에 쓴다. 충돌에 쓰지 않으므로 Ignore Raycast 레이어.
        static void InteriorTrigger(Transform root, string name, Vector3 center, Vector3 size)
        {
            var g = new GameObject(name); g.transform.SetParent(root); g.transform.position = center;
            var bc = g.AddComponent<BoxCollider>(); bc.isTrigger = true; bc.size = size;
            g.layer = LayerMask.NameToLayer("Ignore Raycast");
        }

        // NPC가 설 자리·잠자리 같은 위치 표시 (빈 오브젝트). 방향은 문 쪽(-z)을 보게 한다.
        static void Marker(Transform root, string name, Vector3 pos)
        {
            var g = new GameObject(name); g.transform.SetParent(root);
            g.transform.position = pos; g.transform.rotation = Quaternion.Euler(0f, 180f, 0f);
        }

        // NPC 자리: Marker + NpcSpot(ID). Village_GamePlay 씬의 NPC(NpcPlacement)가 같은 ID로 찾아온다
        static void NpcSpotMarker(Transform root, string spotId, Vector3 pos)
        {
            var g = new GameObject("NPCSpot_" + spotId); g.transform.SetParent(root);
            g.transform.position = pos; g.transform.rotation = Quaternion.Euler(0f, 180f, 0f);
            g.AddComponent<SesiVillage.NPC.NpcSpot>().Init(spotId);
        }

        static void RoomLight(Transform root, string name, Vector3 pos, float intensity, float range = 8f)
        {
            var pl = new GameObject(name); pl.transform.SetParent(root); pl.transform.position = pos;
            var l = pl.AddComponent<Light>();
            l.type = LightType.Point; l.color = new Color(1f, 0.64f, 0.36f); l.intensity = intensity; l.range = range; l.shadows = LightShadows.None;
        }

        static void RemoveCollider(GameObject g) => UnityEngine.Object.DestroyImmediate(g.GetComponent<Collider>());

        // 처마 판: 벽 바깥으로 튀어나온 부분에만 둔다 (집 전체를 덮는 판이면 실내에서 평평한 천장처럼 보임)
        static void EaveRing(Transform parent, Vector3 center, float W, float D, float overhang, float thickness, Material m)
        {
            Prim(PrimitiveType.Cube, "EaveBoard_Front", parent, center + new Vector3(0f, 0f, -D / 2f - overhang / 2f), new Vector3(W + overhang * 2f, thickness, overhang), m, null, false);
            Prim(PrimitiveType.Cube, "EaveBoard_Back", parent, center + new Vector3(0f, 0f, D / 2f + overhang / 2f), new Vector3(W + overhang * 2f, thickness, overhang), m, null, false);
            foreach (int sgn in new[] { -1, 1 })
                Prim(PrimitiveType.Cube, "EaveBoard_Side", parent, center + new Vector3(sgn * (W / 2f + overhang / 2f), 0f, 0f), new Vector3(overhang, thickness, D), m, null, false);
        }

        // 지붕 안쪽 (2026-10-23, 젤다식 실내): 바깥 지붕과 같은 기울기의 모임지붕을 안쪽에서 보이게 하고 나무 뼈대를 드러낸다.
        // center = 벽 꼭대기 높이의 방 중심, W·D = 벽 바깥선 크기, h = 안쪽 지붕 높이
        // - 안쪽 지붕면: 판자, MeshCollider(카메라 Deoccluder가 지붕면 아래에 머물게) + BSPTree(주인공 SuperCharacterController용, TechNotes 6-6)
        // - 뼈대(충돌 없음): 벽 위 도리, 가로지르는 보, 동자주, 꼭대기 종도리, 서까래, 모서리 추녀
        static void BuildRoofInterior(Mats M, Transform parent, Vector3 center, float W, float D, float h, string assetId)
        {
            var group = new GameObject("RoofInterior").transform;
            group.SetParent(parent);

            var inner = HipRoof(W, D, h, assetId + "_RoofInner", false, true);
            var surf = MeshObj("RoofInner", group, inner, M.plank, center, Quaternion.identity);
            surf.AddComponent<BSPTree>();

            float cx = center.x, cz = center.z, baseY = center.y;
            float hw = W / 2f, hd = D / 2f, hr = Mathf.Max(0.5f, hw - hd);   // hr = 종도리 길이의 절반
            float topY = baseY + h;
            const float drop = 0.12f;   // 뼈대를 지붕면보다 살짝 아래에 둬서 판자에 묻히지 않게

            Beam(group, "Plate", new Vector3(cx - hw, baseY, cz - hd), new Vector3(cx + hw, baseY, cz - hd), 0.28f, M.darkWood);
            Beam(group, "Plate", new Vector3(cx - hw, baseY, cz + hd), new Vector3(cx + hw, baseY, cz + hd), 0.28f, M.darkWood);
            Beam(group, "Plate", new Vector3(cx - hw, baseY, cz - hd), new Vector3(cx - hw, baseY, cz + hd), 0.28f, M.darkWood);
            Beam(group, "Plate", new Vector3(cx + hw, baseY, cz - hd), new Vector3(cx + hw, baseY, cz + hd), 0.28f, M.darkWood);
            Beam(group, "RidgeBeam", new Vector3(cx - hr, topY - drop, cz), new Vector3(cx + hr, topY - drop, cz), 0.3f, M.darkWood);

            // 보 + 동자주: 앞뒤 벽을 잇는 보를 약 3m 간격으로, 종도리 구간에는 보 위에 동자주를 세워 종도리를 받친다
            int beamCount = Mathf.Max(2, Mathf.RoundToInt(W / 3f));
            for (int i = 1; i < beamCount; i++)
            {
                float x = cx - hw + W * i / beamCount;
                Beam(group, "TieBeam", new Vector3(x, baseY + 0.2f, cz - hd), new Vector3(x, baseY + 0.2f, cz + hd), 0.32f, M.darkWood);
                if (Mathf.Abs(x - cx) <= hr)
                    Beam(group, "KingPost", new Vector3(x, baseY + 0.2f, cz), new Vector3(x, topY - drop, cz), 0.22f, M.darkWood);
            }
            // 서까래: 앞뒤 경사면 (종도리 구간), 약 0.9m 간격
            int rafterCount = Mathf.Max(2, Mathf.RoundToInt(hr * 2f / 0.9f));
            for (int i = 0; i <= rafterCount; i++)
            {
                float x = cx - hr + hr * 2f * i / rafterCount;
                foreach (int sgn in new[] { -1, 1 })
                    Beam(group, "Rafter", new Vector3(x, baseY - drop, cz + sgn * hd), new Vector3(x, topY - drop, cz), 0.12f, M.darkWood);
            }
            // 양옆 삼각형 면의 서까래와 모서리 추녀 (종도리 끝으로 모임)
            foreach (int sx in new[] { -1, 1 })
            {
                int sideCount = Mathf.Max(2, Mathf.RoundToInt(D / 0.9f));
                for (int i = 1; i < sideCount; i++)
                {
                    float z = cz - hd + D * i / sideCount;
                    Beam(group, "Rafter_Side", new Vector3(cx + sx * hw, baseY - drop, z), new Vector3(cx + sx * hr, topY - drop, cz), 0.12f, M.darkWood);
                }
                foreach (int sz in new[] { -1, 1 })
                    Beam(group, "HipRafter", new Vector3(cx + sx * hw, baseY - drop, cz + sz * hd), new Vector3(cx + sx * hr, topY - drop, cz), 0.2f, M.darkWood);
            }
        }

        // a에서 b까지 이어지는 각재 (충돌 없음)
        static void Beam(Transform parent, string name, Vector3 a, Vector3 b, float thickness, Material m)
        {
            Vector3 dir = b - a;
            Vector3 up = Mathf.Abs(Vector3.Dot(dir.normalized, Vector3.up)) > 0.99f ? Vector3.forward : Vector3.up;   // 거의 수직이면 회전 기준을 바꿔 뒤틀림 방지
            Prim(PrimitiveType.Cube, name, parent, (a + b) / 2f, new Vector3(thickness, thickness, dir.magnitude), m, Quaternion.LookRotation(dir, up), false);
        }

        const float ThatchRoofH = 2.3f * BuildingScale;   // 초가 지붕 높이 (집 크기에 맞춰 키움)

        // 초가 지붕 묶음 (충돌 없음). 들어갈 수 있는 집용: 아랫면 없음 + 처마 판은 바깥 둘레에만
        static GameObject ThatchRoofGroup(Mats M, Transform root, Vector3 H, float W, float D, float roofBase, Material thatch, string assetId, bool gourds)
        {
            var roof = new GameObject(RoofGroupName);
            roof.transform.SetParent(root);
            float overhang = 1.3f;
            float rw = W + overhang * 2f + 0.4f, rd = D + overhang * 2f + 0.4f, rh = ThatchRoofH;
            EaveRing(roof.transform, new Vector3(H.x, roofBase + 0.08f, H.z), W, D, overhang, 0.16f, M.darkWood);
            RemoveCollider(MeshObj("ThatchRoof", roof.transform, HipRoof(rw, rd, rh, assetId + "_Thatch", false), thatch, new Vector3(H.x, roofBase + 0.16f, H.z), Quaternion.identity));
            float ridgeLen = Mathf.Max(1f, rw / 2f - rd / 2f) * 2f;
            var ridge = Prim(PrimitiveType.Capsule, "ThatchRidge", roof.transform, new Vector3(H.x, roofBase + 0.16f + rh - 0.1f, H.z), Vector3.one, thatch, Quaternion.Euler(0, 0, 90), false);
            ridge.transform.localScale = new Vector3(0.9f, (ridgeLen + 1.0f) / 2f, 0.75f);
            foreach (int sgn in new[] { -1, 1 })
            {
                Prim(PrimitiveType.Capsule, "EaveRoll", roof.transform, new Vector3(H.x, roofBase + 0.2f, H.z + sgn * rd / 2f), new Vector3(0.9f, rw / 2f, 0.9f), thatch, Quaternion.Euler(0, 0, 90), false);
                Prim(PrimitiveType.Capsule, "EaveRoll", roof.transform, new Vector3(H.x + sgn * rw / 2f, roofBase + 0.2f, H.z), new Vector3(0.9f, rd / 2f, 0.9f), thatch, Quaternion.Euler(90, 0, 0), false);
            }
            if (gourds)
            {
                for (int i = 0; i < 7; i++)
                {
                    float x = H.x + R(-rw * 0.4f, rw * 0.4f), z = H.z + R(-rd * 0.25f, rd * 0.25f);
                    float y = roofBase + 0.16f + rh * (1f - Mathf.Abs(z - H.z) / (rd / 2f)) - 0.1f;
                    Prim(PrimitiveType.Sphere, "Gourd", roof.transform, new Vector3(x, y, z), Vector3.one * R(0.5f, 0.8f), i % 2 == 0 ? M.pumpkin : M.gourdVine, null, false);
                    Prim(PrimitiveType.Sphere, "Leaf", roof.transform, new Vector3(x + R(-0.6f, 0.6f), y + 0.15f, z + R(-0.4f, 0.4f)), new Vector3(0.7f, 0.12f, 0.7f), M.gourdVine, null, false);
                }
            }
            return roof;
        }

        // 들어갈 수 있는 초가집 (젤다 방식: 문으로 걸어 들어감). withStorage = 동지집처럼 안쪽에 동지창고가 붙은 집
        static GameObject BuildEnterableThatched(Mats M, Transform parent, VillageHouse h, Material thatch, bool withStorage)
        {
            // 방 크기는 BuildingScale만큼 (2026-10-23 x1.5), 기단·계단·창 높이·가구는 사람 크기 그대로
            const float W = 9.5f * BuildingScale, D = 6f * BuildingScale, WallH = 2.8f * BuildingScale, T = 0.3f, PlinthH = 0.35f;
            const float SW = 7f * BuildingScale, SD = 5f * BuildingScale;   // 동지창고 크기
            const float DoorW = 2.0f, DoorH = 2.6f;    // 앞문 (카메라가 같이 지나가므로 몸보다 조금 크게, 시험 결과)
            const float InnerW = 1.8f, InnerH = 2.5f;  // 창고 문
            Vector3 H = new Vector3(h.pos.x, Ground, h.pos.y);
            var root = new GameObject(h.label + (withStorage ? " + 동지창고" : "") + " (House_" + h.id + ")");
            root.transform.SetParent(parent);
            root.transform.position = H;
            var t = root.transform;

            float floorY = H.y + PlinthH;
            float frontZ = H.z - D / 2f, backZ = H.z + D / 2f;

            // 바닥·계단
            Prim(PrimitiveType.Cube, "Floor", t, new Vector3(H.x, floorY - PlinthH / 2f, H.z), new Vector3(W + 0.8f, PlinthH, D + 0.8f), M.plinth);
            Prim(PrimitiveType.Cube, "FloorBoards", t, new Vector3(H.x, floorY + 0.02f, H.z), new Vector3(W - T, 0.04f, D - T), M.plank, null, false);
            Prim(PrimitiveType.Cube, "Step", t, new Vector3(H.x, Ground + 0.12f, frontZ - 0.9f), new Vector3(2.4f, 0.24f, 1.0f), M.plinth);

            // 벽: 앞(앞문), 뒤(동지집이면 창고 문), 양옆
            WallX(t, "Wall_Front", H.x, frontZ, W, floorY, WallH, T, M.plaster, DoorW, DoorH);
            if (withStorage) WallX(t, "Wall_Back", H.x, backZ, W, floorY, WallH, T, M.plaster, InnerW, InnerH);
            else WallX(t, "Wall_Back", H.x, backZ, W, floorY, WallH, T, M.plaster);
            foreach (int sgn in new[] { -1, 1 })
                WallZ(t, "Wall_Side", H.x + sgn * W / 2f, H.z, D, floorY, WallH, T, M.plaster);

            // 기둥·문틀·창 (장식, 충돌 없음)
            float wallY = floorY + WallH / 2f;
            foreach (int sgn in new[] { -1, 1 })
            {
                Prim(PrimitiveType.Cube, "CornerPost", t, new Vector3(H.x + sgn * W / 2f, wallY, frontZ - 0.12f), new Vector3(0.3f, WallH, 0.3f), M.darkWood, null, false);
                Prim(PrimitiveType.Cube, "CornerPostBack", t, new Vector3(H.x + sgn * W / 2f, wallY, backZ + 0.12f), new Vector3(0.3f, WallH, 0.3f), M.darkWood, null, false);
                Prim(PrimitiveType.Cube, "DoorFrame", t, new Vector3(H.x + sgn * (DoorW / 2f + 0.08f), floorY + DoorH / 2f, frontZ - 0.18f), new Vector3(0.16f, DoorH, 0.12f), M.darkWood, null, false);
                Prim(PrimitiveType.Cube, "PaperWindow", t, new Vector3(H.x + sgn * W * 0.32f, floorY + 1.55f, frontZ - 0.17f), new Vector3(1.6f, 1.1f, 0.04f), M.paperWindow, null, false);
                Prim(PrimitiveType.Cube, "WindowLattice", t, new Vector3(H.x + sgn * W * 0.32f, floorY + 1.55f, frontZ - 0.2f), new Vector3(0.05f, 1.1f, 0.03f), M.darkWood, null, false);
                Prim(PrimitiveType.Cube, "PaperWindow_Inside", t, new Vector3(H.x + sgn * W * 0.32f, floorY + 1.55f, frontZ + 0.17f), new Vector3(1.6f, 1.1f, 0.04f), M.paperWindow, null, false);
            }
            Prim(PrimitiveType.Cube, "Lintel", t, new Vector3(H.x, floorY + DoorH + 0.1f, frontZ - 0.18f), new Vector3(DoorW + 0.6f, 0.2f, 0.16f), M.darkWood, null, false);

            // 실내: 벽 쪽에만 소품, 가운데는 비워 둔다. NPC 자리 표시
            JarStand(M, t, new Vector2(H.x + W / 2f - 2.4f, H.z + 1.5f * BuildingScale), 3, floorY);
            Prim(PrimitiveType.Cube, "LowTable", t, new Vector3(H.x - W / 2f + 1.6f, floorY + 0.25f, H.z + 1.6f * BuildingScale), new Vector3(1.4f, 0.5f, 0.9f), M.plank);
            NpcSpotMarker(t, h.id.ToLowerInvariant(), new Vector3(H.x - 1.5f * BuildingScale, floorY, H.z + 1.4f * BuildingScale));

            if (withStorage)
            {
                float sz = backZ + SD / 2f, sBackZ = backZ + SD;
                Prim(PrimitiveType.Cube, "Floor_Storage", t, new Vector3(H.x, floorY - PlinthH / 2f, sz), new Vector3(SW + 0.8f, PlinthH, SD + 0.4f), M.plinth);
                Prim(PrimitiveType.Cube, "FloorBoards_Storage", t, new Vector3(H.x, floorY + 0.02f, sz), new Vector3(SW - T, 0.04f, SD - T), M.plank, null, false);
                WallX(t, "StorageWall_Back", H.x, sBackZ, SW, floorY, WallH, T, M.fence);
                foreach (int sgn in new[] { -1, 1 })
                    WallZ(t, "StorageWall_Side", H.x + sgn * SW / 2f, sz, SD, floorY, WallH, T, M.fence);

                // 창고 문: 잠긴 문 (열쇠가 있으면 이 오브젝트를 끄거나 옮겨서 연다)
                Prim(PrimitiveType.Cube, "Door_StorageLocked", t, new Vector3(H.x, floorY + InnerH / 2f, backZ - 0.05f), new Vector3(InnerW, InnerH, 0.12f), M.plank);
                Prim(PrimitiveType.Cube, "LockPlate", t, new Vector3(H.x + 0.45f, floorY + 1.1f, backZ - 0.13f), new Vector3(0.18f, 0.25f, 0.04f), M.windowDark, null, false);

                Prim(PrimitiveType.Cylinder, "Cauldron", t, new Vector3(H.x - W / 2f + 1.2f, floorY + 0.4f, H.z - 1.2f * BuildingScale), new Vector3(1.1f, 0.4f, 1.1f), M.windowDark);
                for (int i = 0; i < 4; i++)
                    Prim(PrimitiveType.Cylinder, "RiceSack", t, new Vector3(H.x - SW / 2f + 1.0f + i * 1.15f, floorY + 0.45f, sBackZ - 0.8f), new Vector3(0.9f, 0.45f, 0.9f), M.thatchYellow, Quaternion.Euler(0, 0, 90));

                var sroof = new GameObject(RoofGroupName);
                sroof.transform.SetParent(t);
                const float storageRoofH = 1.6f * BuildingScale;
                RemoveCollider(MeshObj("ThatchRoof_Storage", sroof.transform, HipRoof(SW + 1.6f, SD + 1.6f, storageRoofH, "Storage_" + h.id + "_Thatch", false), thatch, new Vector3(H.x, floorY + WallH - 0.2f, sz + 0.3f), Quaternion.identity));
                BuildRoofInterior(M, t, new Vector3(H.x, floorY + WallH, sz), SW, SD, storageRoofH * SD / (SD + 1.6f) - 0.2f, "Storage_" + h.id);
                RoomLight(t, "PointLight_Storage", new Vector3(H.x, floorY + WallH * 0.75f, sz), 3f, 8f * BuildingScale);
                InteriorTrigger(t, "InteriorTrigger_Storage", new Vector3(H.x, wallY, sz), new Vector3(SW - T * 2f, WallH, SD - T * 2f));
            }

            ThatchRoofGroup(M, t, H, W, D, floorY + WallH, thatch, "House_" + h.id, h.gourds);
            float rd = D + 1.3f * 2f + 0.4f;   // ThatchRoofGroup의 바깥 지붕 깊이 (같은 기울기로 안쪽 지붕 높이를 구함)
            BuildRoofInterior(M, t, new Vector3(H.x, floorY + WallH, H.z), W, D, ThatchRoofH * D / rd, "House_" + h.id);
            RoomLight(t, "PointLight_House", new Vector3(H.x, floorY + WallH * 0.75f, H.z), 6f, 8f * BuildingScale);
            InteriorTrigger(t, "InteriorTrigger_House", new Vector3(H.x, wallY, H.z), new Vector3(W - T * 2f, WallH, D - T * 2f));
            return root;
        }

        // 마지막 단계: 땅·벽처럼 부딪혀야 하는 콜라이더를 Walkable 레이어로 지정한다.
        // (주인공의 SuperCharacterController는 Walkable 레이어만 땅·벽으로 인식한다. 트리거는 제외)
        static void AssignCollisionLayers()
        {
            int walkable = LayerMask.NameToLayer("Walkable");
            if (walkable < 0)
            {
                Debug.LogError("[MoonlitVillage] 'Walkable' 레이어가 없습니다. Tags and Layers에 추가한 뒤 다시 생성하세요.");
                return;
            }
            int count = 0;
            foreach (var col in UnityEngine.Object.FindObjectsByType<Collider>(FindObjectsSortMode.None))
            {
                if (col.isTrigger) continue;
                col.gameObject.layer = walkable;
                // 주인공 SuperCharacterController는 MeshCollider를 검사할 때 BSPTree가 필요하다 (없으면 에러, TechNotes 6-6)
                if (col is MeshCollider && col.GetComponent<BSPTree>() == null) col.gameObject.AddComponent<BSPTree>();
                count++;
            }
            Debug.Log("[MoonlitVillage] Walkable 레이어 지정: " + count + "개");
        }

        static void StoneWall(Mats M, Transform parent, Vector2 a, Vector2 b)
        {
            var root = new GameObject("StoneWall"); root.transform.SetParent(parent);
            float len = Vector2.Distance(a, b);
            Vector2 dir = (b - a).normalized;
            float yaw = Mathf.Atan2(dir.x, dir.y) * Mathf.Rad2Deg;
            Vector2 mid = (a + b) / 2f;
            float ym = TerrainHeightAt(mid.x, mid.y);
            Prim(PrimitiveType.Cube, "Wall", root.transform, new Vector3(mid.x, ym + 0.4f, mid.y), new Vector3(0.6f, 0.9f, len), M.fence, Quaternion.Euler(0, yaw, 0));
            int n = Mathf.Max(2, Mathf.RoundToInt(len / 0.7f));
            for (int i = 0; i < n; i++)
            {
                Vector2 q = a + dir * (len * (i + 0.5f) / n);
                float s = R(0.45f, 0.75f);
                Prim(PrimitiveType.Sphere, "CapStone", root.transform, new Vector3(q.x + R(-0.12f, 0.12f), TerrainHeightAt(q.x, q.y) + 0.85f, q.y + R(-0.12f, 0.12f)), new Vector3(s, s * 0.55f, s), M.shore, Quaternion.Euler(0, R(0, 360), 0), false);
            }
        }

        static void WoodBench(Mats M, Transform parent, Vector2 p)
        {
            float ty = TerrainHeightAt(p.x, p.y);
            var table = new GameObject("WoodBench"); table.transform.SetParent(parent);
            Prim(PrimitiveType.Cube, "Top", table.transform, new Vector3(p.x, ty + 0.5f, p.y), new Vector3(2.2f, 0.14f, 1.4f), M.plank);
            foreach (var o in new[] { new Vector2(-0.95f, -0.55f), new Vector2(0.95f, -0.55f), new Vector2(-0.95f, 0.55f), new Vector2(0.95f, 0.55f) })
                Prim(PrimitiveType.Cube, "Leg", table.transform, new Vector3(p.x + o.x, ty + 0.22f, p.y + o.y), new Vector3(0.14f, 0.45f, 0.14f), M.darkWood, null, false);
        }

        static void StoneSign(Mats M, Transform parent, Vector2 p)
        {
            float y = TerrainHeightAt(p.x, p.y);
            var root = new GameObject("StoneSign"); root.transform.SetParent(parent);
            Prim(PrimitiveType.Cube, "Base", root.transform, new Vector3(p.x, y + 0.15f, p.y), new Vector3(1.3f, 0.3f, 0.8f), M.marker);
            Prim(PrimitiveType.Cube, "Slab", root.transform, new Vector3(p.x, y + 0.3f + 0.9f, p.y), new Vector3(1.0f, 1.8f, 0.3f), M.marker);
            Prim(PrimitiveType.Cube, "Inscription", root.transform, new Vector3(p.x, y + 0.3f + 0.95f, p.y - 0.16f), new Vector3(0.6f, 1.3f, 0.02f), M.windowDark, null, false);
            Prim(PrimitiveType.Cube, "Cap", root.transform, new Vector3(p.x, y + 0.3f + 1.85f, p.y), new Vector3(1.2f, 0.16f, 0.5f), M.marker, null, false);
        }

        static void Mortar(Mats M, Transform parent, Vector2 p)
        {
            float y = TerrainHeightAt(p.x, p.y);
            var root = new GameObject("Mortar"); root.transform.SetParent(parent);
            Prim(PrimitiveType.Cylinder, "Body", root.transform, new Vector3(p.x, y + 0.45f, p.y), new Vector3(0.85f, 0.45f, 0.85f), M.plinth);
            Prim(PrimitiveType.Cylinder, "Hole", root.transform, new Vector3(p.x, y + 0.905f, p.y), new Vector3(0.5f, 0.01f, 0.5f), M.windowDark, null, false);
            Prim(PrimitiveType.Cylinder, "Pestle", root.transform, new Vector3(p.x + 0.15f, y + 1.1f, p.y), new Vector3(0.13f, 0.55f, 0.13f), M.totemWood, Quaternion.Euler(0, 0, -14f), false);
        }

        static void Well(Mats M, Transform parent, Vector2 p)
        {
            float y = TerrainHeightAt(p.x, p.y);
            var root = new GameObject("Well"); root.transform.SetParent(parent);
            Prim(PrimitiveType.Cylinder, "Ring", root.transform, new Vector3(p.x, y + 0.45f, p.y), new Vector3(2.0f, 0.45f, 2.0f), M.plinth);
            Prim(PrimitiveType.Cylinder, "Hole", root.transform, new Vector3(p.x, y + 0.92f, p.y), new Vector3(1.4f, 0.02f, 1.4f), M.windowDark, null, false);
            foreach (int sgn in new[] { -1, 1 })
                Prim(PrimitiveType.Cube, "Post", root.transform, new Vector3(p.x + sgn * 0.9f, y + 1.6f, p.y), new Vector3(0.16f, 1.6f, 0.16f), M.darkWood);
            Prim(PrimitiveType.Cylinder, "Beam", root.transform, new Vector3(p.x, y + 2.3f, p.y), new Vector3(0.14f, 1.0f, 0.14f), M.darkWood, Quaternion.Euler(0, 0, 90), false);
            Prim(PrimitiveType.Cube, "Bucket", root.transform, new Vector3(p.x, y + 1.6f, p.y), new Vector3(0.4f, 0.4f, 0.4f), M.darkWood, null, false);
        }

        // floorY: 실내 마루 위에 둘 때 마루 높이 (없으면 지형 높이)
        static void JarStand(Mats M, Transform parent, Vector2 p, int count, float? floorY = null)
        {
            float y = floorY ?? TerrainHeightAt(p.x, p.y);
            var root = new GameObject("JarStand"); root.transform.SetParent(parent);
            Prim(PrimitiveType.Cube, "Platform", root.transform, new Vector3(p.x, y + 0.12f, p.y), new Vector3(4.5f, 0.24f, 2.6f), M.plinth);
            for (int i = 0; i < count; i++)
            {
                float x = p.x - 1.7f + (i % 3) * 1.7f + R(-0.15f, 0.15f), z = p.y - 0.6f + (i / 3) * 1.2f + R(-0.1f, 0.1f);
                float s = R(0.75f, 1.05f);
                Prim(PrimitiveType.Sphere, "Jar", root.transform, new Vector3(x, y + 0.24f + s * 0.55f, z), new Vector3(s, s * 1.1f, s), M.jar, null, false);
                Prim(PrimitiveType.Cylinder, "JarLid", root.transform, new Vector3(x, y + 0.24f + s * 1.1f, z), new Vector3(s * 0.55f, 0.04f, s * 0.55f), M.jar, null, false);
            }
        }

        static void WoodFence(Mats M, Transform parent, Vector2 a, Vector2 b)
        {
            var root = new GameObject("WoodFence"); root.transform.SetParent(parent);
            float len = Vector2.Distance(a, b);
            Vector2 dir = (b - a).normalized;
            float yaw = Mathf.Atan2(dir.x, dir.y) * Mathf.Rad2Deg;
            int posts = Mathf.Max(2, Mathf.RoundToInt(len / 1.6f) + 1);
            for (int i = 0; i < posts; i++)
            {
                Vector2 q = a + dir * (len * i / (posts - 1));
                float y = TerrainHeightAt(q.x, q.y);
                Prim(PrimitiveType.Cube, "Post", root.transform, new Vector3(q.x, y + 0.6f, q.y), new Vector3(0.16f, 1.2f, 0.16f), M.darkWood, Quaternion.Euler(0, yaw, 0), false);
            }
            Vector2 mid = (a + b) / 2f;
            float ym = TerrainHeightAt(mid.x, mid.y);
            foreach (float h in new[] { 0.5f, 0.95f })
                Prim(PrimitiveType.Cube, "Rail", root.transform, new Vector3(mid.x, ym + h, mid.y), new Vector3(0.08f, 0.1f, len), M.darkWood, Quaternion.Euler(0, yaw, 0), false);
        }


        static void BuildEastGate(Mats M)
        {
            var root = new GameObject("East_Gate");
            Vector3 G = new Vector3(GatePos.x, TerrainHeightAt(GatePos.x, GatePos.y), GatePos.y);
            float halfSpan = 2.8f, pillarH = 4.2f;

            foreach (int sgn in new[] { -1, 1 })
            {
                Vector3 pb = new Vector3(G.x, 0, G.z + sgn * halfSpan); pb.y = TerrainHeightAt(pb.x, pb.z);
                Prim(PrimitiveType.Cube, "PillarBase", root.transform, pb + new Vector3(0, 0.25f, 0), new Vector3(1.1f, 0.5f, 1.1f), M.plinth);
                Prim(PrimitiveType.Cube, "Pillar", root.transform, pb + new Vector3(0, 0.5f + pillarH / 2f, 0), new Vector3(0.7f, pillarH, 0.7f), M.plinth);
                Prim(PrimitiveType.Cube, "PillarCap", root.transform, pb + new Vector3(0, 0.5f + pillarH + 0.12f, 0), new Vector3(0.95f, 0.24f, 0.95f), M.plinth);
            }
            float beamY = G.y + 0.5f + pillarH + 0.35f;
            Prim(PrimitiveType.Cube, "Beam", root.transform, new Vector3(G.x, beamY, G.z), new Vector3(0.5f, 0.35f, halfSpan * 2f + 1.6f), M.darkWood);
            Prim(PrimitiveType.Cube, "BeamUpper", root.transform, new Vector3(G.x, beamY + 0.9f, G.z), new Vector3(0.4f, 0.25f, halfSpan * 2f + 2.2f), M.darkWood);
            foreach (int sgn in new[] { -1, 1 })
                Prim(PrimitiveType.Cube, "Strut", root.transform, new Vector3(G.x, beamY + 0.45f, G.z + sgn * (halfSpan - 0.4f)), new Vector3(0.3f, 0.6f, 0.3f), M.darkWood, null, false);
            var roof = HipRoof(2.6f, halfSpan * 2f + 3.2f, 0.9f, "GateRoof");
            MeshObj("GateRoof", root.transform, roof, M.blueRoof, new Vector3(G.x, beamY + 1.05f, G.z), Quaternion.identity);
            Prim(PrimitiveType.Cube, "SignBoard", root.transform, new Vector3(G.x - 0.32f, beamY - 0.55f, G.z), new Vector3(0.08f, 0.7f, 2.4f), M.signBoard, null, false);
            Prim(PrimitiveType.Cube, "SignFrame", root.transform, new Vector3(G.x - 0.30f, beamY - 0.55f, G.z), new Vector3(0.06f, 0.82f, 2.55f), M.darkWood, null, false);

            var totems = new GameObject("TotemPole"); totems.transform.SetParent(root.transform);
            foreach (int sgn in new[] { -1, 1 })
                for (int i = 0; i < 2; i++)
                {
                    float x = G.x - 3.5f - i * 2.4f, z = G.z + sgn * (halfSpan + 1.2f + i * 0.4f);
                    TotemPole(M, totems.transform, new Vector2(x, z), 2.6f + R(0f, 0.6f), sgn > 0);
                }

            BambooFence(M, root.transform, new Vector2(G.x - 1f, G.z + halfSpan + 4f), new Vector2(G.x - 1f, G.z + halfSpan + 22f));
            BambooFence(M, root.transform, new Vector2(G.x - 1f, G.z - halfSpan - 4f), new Vector2(G.x - 1f, G.z - halfSpan - 10f));
            for (int i = 0; i < 7; i++)
            {
                float z = G.z + halfSpan + 6f + i * 2.1f + R(-0.4f, 0.4f), x = G.x - 2.4f + R(-0.4f, 0.4f);
                Sunflower(M, root.transform, new Vector2(x, z), R(1.6f, 2.3f));
            }
            foreach (var p in new[] { new Vector2(G.x - 9f, G.z - 7f), new Vector2(G.x - 14f, G.z + 9f) })
                SpikyPlant(M, root.transform, p);
        }

        static void TotemPole(Mats M, Transform parent, Vector2 p, float h, bool hat)
        {
            float y = TerrainHeightAt(p.x, p.y);
            var root = new GameObject("TotemPole"); root.transform.SetParent(parent);
            root.transform.position = new Vector3(p.x, y, p.y);
            root.transform.rotation = Quaternion.Euler(0, -90f + R(-8f, 8f), 0);
            var t = root.transform;
            var body = Prim(PrimitiveType.Cylinder, "Body", t, Vector3.zero, new Vector3(0.55f, h / 2f, 0.55f), M.totemWood, null, false);
            body.transform.localPosition = new Vector3(0, h / 2f - 0.2f, 0);
            var head = Prim(PrimitiveType.Cylinder, "Head", t, Vector3.zero, new Vector3(0.66f, 0.45f, 0.66f), M.totemWood, null, false);
            head.transform.localPosition = new Vector3(0, h - 0.4f, 0);
            foreach (int sgn in new[] { -1, 1 })
            {
                var eye = Prim(PrimitiveType.Sphere, "Eye", t, Vector3.zero, new Vector3(0.2f, 0.16f, 0.1f), M.windowDark, null, false);
                eye.transform.localPosition = new Vector3(sgn * 0.17f, h - 0.3f, 0.3f);
            }
            var nose = Prim(PrimitiveType.Cube, "Nose", t, Vector3.zero, new Vector3(0.14f, 0.32f, 0.16f), M.darkWood, null, false);
            nose.transform.localPosition = new Vector3(0, h - 0.55f, 0.33f);
            var mouth = Prim(PrimitiveType.Cube, "Mouth", t, Vector3.zero, new Vector3(0.42f, 0.14f, 0.08f), M.windowDark, null, false);
            mouth.transform.localPosition = new Vector3(0, h - 0.8f, 0.31f);
            for (int i = -1; i <= 1; i++)
            {
                var tooth = Prim(PrimitiveType.Cube, "Tooth", t, Vector3.zero, new Vector3(0.08f, 0.1f, 0.06f), M.signBoard, null, false);
                tooth.transform.localPosition = new Vector3(i * 0.13f, h - 0.78f, 0.34f);
            }
            if (hat)
            {
                var brim = Prim(PrimitiveType.Cylinder, "HatBrim", t, Vector3.zero, new Vector3(1.1f, 0.04f, 1.1f), M.windowDark, null, false);
                brim.transform.localPosition = new Vector3(0, h + 0.06f, 0);
                var crown = Prim(PrimitiveType.Cylinder, "HatCrown", t, Vector3.zero, new Vector3(0.5f, 0.22f, 0.5f), M.windowDark, null, false);
                crown.transform.localPosition = new Vector3(0, h + 0.3f, 0);
            }
            else
            {
                var top = Prim(PrimitiveType.Sphere, "TopKnot", t, Vector3.zero, new Vector3(0.5f, 0.35f, 0.5f), M.totemWood, null, false);
                top.transform.localPosition = new Vector3(0, h + 0.05f, 0);
            }
        }

        static void BambooFence(Mats M, Transform parent, Vector2 a, Vector2 b)
        {
            var root = new GameObject("BambooFence"); root.transform.SetParent(parent);
            float len = Vector2.Distance(a, b);
            Vector2 dir = (b - a).normalized;
            float yaw = Mathf.Atan2(dir.x, dir.y) * Mathf.Rad2Deg;
            int n = Mathf.Max(2, Mathf.RoundToInt(len / 0.22f));
            for (int i = 0; i < n; i++)
            {
                Vector2 q = a + dir * (len * i / (n - 1));
                float y = TerrainHeightAt(q.x, q.y);
                float h = R(1.7f, 2.0f);
                Prim(PrimitiveType.Cylinder, "Stalk", root.transform, new Vector3(q.x, y + h / 2f, q.y), new Vector3(0.09f, h / 2f, 0.09f), M.bamboo, Quaternion.Euler(R(-2, 2), 0, R(-2, 2)), false);
            }
            Vector2 mid = (a + b) / 2f;
            float ym = TerrainHeightAt(mid.x, mid.y);
            foreach (float h in new[] { 0.5f, 1.1f, 1.6f })
                Prim(PrimitiveType.Cylinder, "Rail", root.transform, new Vector3(mid.x, ym + h, mid.y), new Vector3(0.08f, len / 2f, 0.08f), M.totemWood, Quaternion.Euler(90, yaw, 0), false);
            var col = new GameObject("FenceCollider"); col.transform.SetParent(root.transform);
            col.transform.position = new Vector3(mid.x, ym + 1f, mid.y); col.transform.rotation = Quaternion.Euler(0, yaw, 0);
            var bc = col.AddComponent<BoxCollider>(); bc.size = new Vector3(0.3f, 2f, len);
        }

        static void Sunflower(Mats M, Transform parent, Vector2 p, float h)
        {
            float y = TerrainHeightAt(p.x, p.y);
            var root = new GameObject("Sunflower"); root.transform.SetParent(parent);
            Prim(PrimitiveType.Cylinder, "Stalk", root.transform, new Vector3(p.x, y + h / 2f, p.y), new Vector3(0.06f, h / 2f, 0.06f), M.bamboo, null, false);
            float yaw = R(-40f, 40f);
            var head = Prim(PrimitiveType.Cylinder, "Head", root.transform, new Vector3(p.x, y + h, p.y), new Vector3(0.55f, 0.03f, 0.55f), M.sunflowerPetal, Quaternion.Euler(90 + R(-15f, 15f), -90f + yaw, 0), false);
            var center = Prim(PrimitiveType.Cylinder, "Center", root.transform, head.transform.position, new Vector3(0.28f, 0.035f, 0.28f), M.sunflowerCenter, head.transform.rotation, false);
            center.transform.position += head.transform.up * -0.02f;
            for (int i = 0; i < 2; i++)
                Prim(PrimitiveType.Sphere, "Leaf", root.transform, new Vector3(p.x + R(-0.3f, 0.3f), y + h * R(0.35f, 0.7f), p.y + R(-0.3f, 0.3f)), new Vector3(0.5f, 0.08f, 0.3f), M.gourdVine, Quaternion.Euler(0, R(0, 360), R(-20, 20)), false);
        }

        static void SpikyPlant(Mats M, Transform parent, Vector2 p)
        {
            float y = TerrainHeightAt(p.x, p.y);
            var root = new GameObject("SpikyPlant"); root.transform.SetParent(parent);
            for (int i = 0; i < 12; i++)
            {
                float a = i * 30f + R(-8f, 8f), tilt = R(25f, 60f), len = R(0.9f, 1.5f);
                var q = Quaternion.Euler(0, a, 0) * Quaternion.Euler(tilt, 0, 0);
                Vector3 dir = q * Vector3.up;
                Prim(PrimitiveType.Cube, "Leaf", root.transform, new Vector3(p.x, y, p.y) + dir * len / 2f, new Vector3(0.14f, len, 0.05f), M.gourdVine, q, false);
            }
        }

        static void BuildWoodBridge(Mats M)
        {
            var root = new GameObject("Bridge_Wood");
            float deckY = Ground + 0.35f;
            float len = WoodBridgeZ0 - WoodBridgeZ1;
            float zc = (WoodBridgeZ0 + WoodBridgeZ1) / 2f;
            float hw = WoodBridgeWidth / 2f;

            foreach (int sgn in new[] { -1, 1 })
                Prim(PrimitiveType.Cube, "EdgeBeam", root.transform, new Vector3(WoodBridgeX + sgn * (hw - 0.15f), deckY - 0.22f, zc), new Vector3(0.3f, 0.32f, len + 0.6f), M.plank);
            for (float z = WoodBridgeZ1; z <= WoodBridgeZ0; z += 0.6f)
            {
                float w = R(0.5f, 0.56f);
                Prim(PrimitiveType.Cube, "Plank", root.transform, new Vector3(WoodBridgeX + R(-0.03f, 0.03f), deckY + R(-0.01f, 0.01f), z), new Vector3(WoodBridgeWidth, 0.1f, w), M.plank, Quaternion.Euler(0, R(-1f, 1f), 0), false);
            }
            var col = new GameObject("DeckCollider"); col.transform.SetParent(root.transform);
            col.transform.position = new Vector3(WoodBridgeX, deckY, zc);
            var bc = col.AddComponent<BoxCollider>(); bc.size = new Vector3(WoodBridgeWidth, 0.1f, len);
            for (float z = WoodBridgeZ1 + 2f; z <= WoodBridgeZ0 - 2f; z += 4f)
                foreach (int sgn in new[] { -1, 1 })
                {
                    float x = WoodBridgeX + sgn * (hw - 0.15f);
                    float bottom = Mathf.Min(TerrainHeightAt(x, z), WaterY - 0.5f) - 0.5f;
                    float top = deckY + 0.55f;
                    Prim(PrimitiveType.Cylinder, "Pile", root.transform, new Vector3(x, (bottom + top) / 2f, z), new Vector3(0.28f, (top - bottom) / 2f, 0.28f), M.deadWood, Quaternion.Euler(R(-2, 2), 0, R(-2, 2)));
                }
            foreach (int sgn in new[] { -1, 1 })
                Prim(PrimitiveType.Cylinder, "SideLog", root.transform, new Vector3(WoodBridgeX + sgn * (hw - 0.15f), deckY + 0.5f, zc), new Vector3(0.14f, len / 2f, 0.14f), M.deadWood, Quaternion.Euler(90, 0, 0), false);
            foreach (float z in new[] { WoodBridgeZ0 + 1.2f, WoodBridgeZ1 - 1.2f })
                Prim(PrimitiveType.Cube, "EndStep", root.transform, new Vector3(WoodBridgeX, Ground + 0.12f, z), new Vector3(WoodBridgeWidth + 1f, 0.3f, 2.4f), M.plinth);
        }

        static void BuildGraveyard(Mats M)
        {
            var root = new GameObject("Graveyard");
            var mounds = new GameObject("Mounds"); mounds.transform.SetParent(root.transform);
            var markers = new GameObject("Markers"); markers.transform.SetParent(root.transform);
            var placed = new List<Vector2>();
            int guard = 0;
            int graveCount = Mathf.RoundToInt(26 * MapScale * MapScale);   // 넓이가 MapScale²배라 개수도 맞춤
            while (placed.Count < graveCount && guard++ < 20000)
            {
                float x = GraveCenter.x + R(-GraveRadius, GraveRadius), z = GraveCenter.y + R(-GraveRadius + L(4f), GraveRadius - L(6f));
                if (Vector2.Distance(new Vector2(x, z), GraveCenter) > GraveRadius) continue;
                if (WaterSdf(x, z) < 4f) continue;
                if (PathDistance(x, z) < 5f) continue;
                if (Vector2.Distance(new Vector2(x, z), GraveCenter) < GraveClearing + 3f) continue;
                if (NearAnyHouse(new Vector2(x, z), L(18f))) continue;
                bool close = false; foreach (var q in placed) if (Vector2.Distance(q, new Vector2(x, z)) < 7f) { close = true; break; }
                if (close) continue;
                placed.Add(new Vector2(x, z));
                float y = TerrainHeightAt(x, z);
                float s = R(3.6f, 5.2f);
                Prim(PrimitiveType.Sphere, "Mound", mounds.transform, new Vector3(x, y - s * 0.12f, z), new Vector3(s, s * 0.5f, s * 0.9f), M.mound, Quaternion.Euler(0, R(0, 360), 0));
                float mx = x + R(-1f, 1f), mz = z - s * 0.6f;
                float my = TerrainHeightAt(mx, mz);
                Prim(PrimitiveType.Cube, "MarkerBase", markers.transform, new Vector3(mx, my + 0.15f, mz), new Vector3(1.1f, 0.3f, 0.7f), M.marker);
                Prim(PrimitiveType.Cube, "Marker", markers.transform, new Vector3(mx, my + 0.3f + 0.75f, mz), new Vector3(0.6f, 1.5f, 0.32f), M.marker, Quaternion.Euler(R(-3, 3), R(-8, 8), R(-3, 3)));
                Prim(PrimitiveType.Cube, "MarkerCap", markers.transform, new Vector3(mx, my + 0.3f + 1.55f, mz), new Vector3(0.8f, 0.14f, 0.5f), M.marker);
            }
            ScatterMarkers(M, markers.transform, Mathf.RoundToInt(12 * MapScale * MapScale));
            var big = new GameObject("BigTrees"); big.transform.SetParent(root.transform);
            foreach (var o in new[] { L(-8f, 6f), L(8f, 4f), L(-7f, -8f), L(8f, -10f) })
                PlaceTree(big.transform, GraveCenter + o, OakNames, 10f, 12f, trunkCollider: true);
            foreach (var off in new[] { L(6f, 26f), L(-30f, -4f), L(30f, 6f), L(2f, -34f), L(-16f, 16f), L(18f, -18f) })
            {
                Vector2 o = GraveCenter + off;
                float y = TerrainHeightAt(o.x, o.y);
                Prim(PrimitiveType.Cube, "StoneLantern", markers.transform, new Vector3(o.x, y + 0.9f, o.y), new Vector3(0.7f, 1.8f, 0.7f), M.marker);
                Prim(PrimitiveType.Cube, "StoneLanternCap", markers.transform, new Vector3(o.x, y + 1.95f, o.y), new Vector3(1.1f, 0.3f, 1.1f), M.marker);
            }
        }

        static void ScatterMarkers(Mats M, Transform parent, int count)
        {
            int guard = 0, n = 0;
            while (n < count && guard++ < 6000)
            {
                float x = GraveCenter.x + R(-GraveRadius, GraveRadius), z = GraveCenter.y + R(-GraveRadius, GraveRadius);
                var p = new Vector2(x, z);
                if (Vector2.Distance(p, GraveCenter) > GraveRadius || Vector2.Distance(p, GraveCenter) < GraveClearing + 2f) continue;
                if (WaterSdf(x, z) < 3f || PathDistance(x, z) < 3.5f || NearAnyHouse(p, L(18f))) continue;
                float y = TerrainHeightAt(x, z);
                Prim(PrimitiveType.Cube, "LoneMarkerBase", parent, new Vector3(x, y + 0.12f, z), new Vector3(0.9f, 0.24f, 0.6f), M.marker, Quaternion.Euler(0, R(0, 360), 0));
                Prim(PrimitiveType.Cube, "LoneMarker", parent, new Vector3(x, y + 0.24f + 0.6f, z), new Vector3(0.5f, 1.2f, 0.28f), M.marker, Quaternion.Euler(R(-4, 4), R(0, 360), R(-4, 4)));
                n++;
            }
        }

        static void BuildPlateau(Mats M)
        {
            var root = new GameObject("Moon_Hill");
            var dead = new GameObject("DeadTrees"); dead.transform.SetParent(root.transform);
            int n = 0, guard = 0;
            var placed = new List<Vector2>();
            int deadCount = Mathf.RoundToInt(14 * MapScale * MapScale);
            while (n < deadCount && guard++ < 8000)
            {
                float a = R(0f, Mathf.PI * 2f), r = R(L(10f), PlateauRadius + L(10f));
                float x = PlateauCenter.x + Mathf.Cos(a) * r, z = PlateauCenter.y + Mathf.Sin(a) * r;
                Vector2 dir = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
                if (Vector2.Dot(dir, new Vector2(MoonDir.x, MoonDir.z).normalized) > 0.35f) continue;
                if (PathDistance(x, z) < 4f) continue;
                bool close = false; foreach (var q in placed) if (Vector2.Distance(q, new Vector2(x, z)) < 6f) { close = true; break; }
                if (close) continue;
                placed.Add(new Vector2(x, z));
                DeadTree(dead.transform, new Vector3(x, TerrainHeightAt(x, z), z), R(6f, 9f), M);
                n++;
            }
            var rocks = new GameObject("EdgeRocks"); rocks.transform.SetParent(root.transform);
            for (int i = 0; i < Mathf.RoundToInt(26 * MapScale); i++)   // 둘레가 MapScale배
            {
                float a = R(0f, Mathf.PI * 2f), r = PlateauRadius + R(-2f, 3f);
                float x = PlateauCenter.x + Mathf.Cos(a) * r, z = PlateauCenter.y + Mathf.Sin(a) * r;
                if (PathDistance(x, z) < 4f) continue;
                float s = R(1.2f, 2.6f);
                Prim(PrimitiveType.Sphere, "Rock", rocks.transform, new Vector3(x, TerrainHeightAt(x, z) - s * 0.2f, z), new Vector3(s, s * 0.6f, s * R(0.7f, 1.2f)), M.shore, Quaternion.Euler(R(-15, 15), R(0, 360), R(-15, 15)), false);
            }
        }

        static void DeadTree(Transform parent, Vector3 basePos, float height, Mats M)
        {
            var root = new GameObject("DeadTree"); root.transform.SetParent(parent); root.transform.position = basePos;
            Branch(root.transform, basePos, Vector3.up, height, 0.35f, 0, M);
        }

        static void Branch(Transform parent, Vector3 start, Vector3 dir, float len, float radius, int depth, Mats M)
        {
            Vector3 end = start + dir * len;
            var c = Prim(PrimitiveType.Cylinder, "Branch" + depth, parent, (start + end) / 2f, new Vector3(radius, len / 2f, radius), M.deadWood, Quaternion.FromToRotation(Vector3.up, dir), depth == 0);
            if (depth >= 3) return;
            int count = depth == 0 ? 3 : 2;
            for (int i = 0; i < count; i++)
            {
                Vector3 nd = (dir + new Vector3(R(-0.9f, 0.9f), R(0.3f, 0.9f), R(-0.9f, 0.9f))).normalized;
                float startT = depth == 0 ? R(0.45f, 0.95f) : R(0.5f, 1f);
                Branch(parent, start + dir * len * startT, nd, len * R(0.45f, 0.65f), radius * 0.55f, depth + 1, M);
            }
        }

        static bool NearStoneBridge(float x, float z, float padX, float padZ)
        {
            foreach (var b in new[] { BridgeWest, BridgeEast })
                if (Mathf.Abs(x - b.x) < BridgeLength / 2f + padX && Mathf.Abs(z - b.z) < BridgeWidth / 2f + padZ) return true;
            return false;
        }
        static bool NearWoodBridge(float x, float z, float pad) => Mathf.Abs(x - WoodBridgeX) < WoodBridgeWidth / 2f + pad && z < WoodBridgeZ0 + 2f && z > WoodBridgeZ1 - 2f;

        static void BuildShoreStones(Mats M)
        {
            var root = new GameObject("ShoreStones");
            float step = 0.9f; int n = 0;
            for (float z = TerrainMinZ + 2; z < TerrainMinZ + TerrainSize - 2; z += step)
                for (float x = TerrainMinX + 2; x < TerrainMinX + TerrainSize - 2; x += step)
                {
                    float jx = x + R(-0.3f, 0.3f), jz = z + R(-0.3f, 0.3f);
                    float sdf = WaterSdf(jx, jz);
                    if (sdf < -0.15f || sdf > 0.75f) continue;
                    if (NearStoneBridge(jx, jz, RampLen + 0.8f, 0.6f) || NearWoodBridge(jx, jz, 0.4f)) continue;
                    float y = TerrainHeightAt(jx, jz);
                    float s = R(0.55f, 1.05f);
                    Prim(PrimitiveType.Sphere, "Stone", root.transform, new Vector3(jx, y - 0.1f, jz),
                        new Vector3(s * R(0.9f, 1.3f), s * 0.6f, s * R(0.9f, 1.3f)), M.shore, Quaternion.Euler(R(-10, 10), R(0, 360), R(-10, 10)), false);
                    n++;
                }
            Debug.Log("[MoonlitVillage] shore stones: " + n);
        }

        // ── 나무 (2026-10-23): 임시 원기둥+공 나무 → Cartoon Nature 프리팹 ──
        // 마을 안 = 참나무, 연못가 = 벚나무, 경계·바깥 = 가문비(상록), 앞마당 = 가는 가문비, 달 언덕 = 고목(DeadTree, 계절과 상관없이 앙상) (TechNotes 2-11)
        // 계절 표현은 나무를 바꾸지 않고 팔레트 텍스처만 바꾼다 (3단계)
        const string NaturePrefabs = "Assets/External Assets/Cartoon Nature - LowPoly Asset Pack/Prefabs/";
        static readonly string[] OakNames = { "Oak_Green_1", "Oak_Green_2", "Oak_DarkGreen_1", "Oak_DarkGreen_2" };
        static readonly string[] SakuraNames = { "Sakura_1", "Sakura_2", "Sakura_3", "Sakura_4" };
        static readonly string[] SpruceNames = { "Spruce_DarkGreen_1", "Spruce_DarkGreen_2", "Spruce_DarkGreen_3", "Spruce_DarkGreen_Thick_1", "Spruce_DarkGreen_Thick_2", "Spruce_DarkGreen_Thick_3", "Spruce_Green_1", "Spruce_Green_2", "Spruce_Green_3" };
        static readonly string[] YardPineNames = { "Spruce_DarkGreen_Thin_1", "Spruce_DarkGreen_Thin_2", "Spruce_DarkGreen_Thin_3" };

        static readonly Dictionary<string, GameObject> naturePrefabCache = new Dictionary<string, GameObject>();
        static readonly Dictionary<GameObject, (float height, float minY)> naturePrefabSize = new Dictionary<GameObject, (float, float)>();

        static GameObject NaturePrefab(string name)
        {
            if (naturePrefabCache.TryGetValue(name, out var cached) && cached != null) return cached;
            string folder = name.StartsWith("Spruce_Cold") ? "Spruces_Cold" : name.StartsWith("Spruce") ? "Spruces"
                          : name.StartsWith("Oak") ? "Oaks" : name.StartsWith("Sakura") ? "Sakuras" : "Acacias";
            var go = AssetDatabase.LoadAssetAtPath<GameObject>(NaturePrefabs + folder + "/" + name + ".fbx");
            if (go == null) Debug.LogWarning("[MoonlitVillage] 나무 프리팹 없음: " + folder + "/" + name);
            naturePrefabCache[name] = go;
            return go;
        }

        // 배율 1일 때 높이와, 기준점에서 바닥까지의 높이 차 (모델마다 기준점 위치가 달라도 땅에 맞게 심기 위해)
        static (float height, float minY) NaturePrefabSize(GameObject prefab)
        {
            if (naturePrefabSize.TryGetValue(prefab, out var size)) return size;
            var tmp = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            tmp.transform.position = Vector3.zero;
            var rs = tmp.GetComponentsInChildren<Renderer>();
            var bounds = rs.Length > 0 ? rs[0].bounds : new Bounds(Vector3.zero, Vector3.one);
            foreach (var r in rs) bounds.Encapsulate(r.bounds);
            UnityEngine.Object.DestroyImmediate(tmp);
            size = (Mathf.Max(0.01f, bounds.size.y), bounds.min.y);
            naturePrefabSize[prefab] = size;
            return size;
        }

        // 나무 하나 = 빈 오브젝트(위치·줄기 충돌) + 그 아래 나무 프리팹. 프리팹을 직접 돌리거나 키우면 충돌 모양까지 같이 틀어지므로 나눔
        // heightAt: 땅 높이 함수 (기본 = 마을 지형, 바깥 구릉은 OuterHeightAt)
        static GameObject PlaceTree(Transform parent, Vector2 p, string[] names, float minH, float maxH, bool castShadows = true, bool trunkCollider = false, Func<float, float, float> heightAt = null)
        {
            var prefab = NaturePrefab(names[rng.Next(names.Length)]);
            if (prefab == null) return null;
            var (height, minY) = NaturePrefabSize(prefab);
            float treeH = R(minH, maxH);
            float s = treeH / height;
            heightAt ??= TerrainHeightAt;
            float y = heightAt(p.x, p.y);

            // 경사면: 기준점 한 점만 땅에 맞추면 비탈 아래쪽에서 줄기 밑동(잘린 원기둥)이 떠 보임 (2026-10-23 사용자 발견)
            // → 비탈 아래쪽 땅이 낮아지는 만큼(줄기 반지름 × 기울기) 더 묻는다. 줄기 반지름은 나무 높이의 약 4%로 어림
            const float e = 0.5f;
            float gx = (heightAt(p.x + e, p.y) - heightAt(p.x - e, p.y)) / (2f * e);
            float gz = (heightAt(p.x, p.y + e) - heightAt(p.x, p.y - e)) / (2f * e);
            float sink = 0.15f + treeH * 0.04f * Mathf.Sqrt(gx * gx + gz * gz);

            var tree = new GameObject("Tree_" + prefab.name);
            tree.transform.SetParent(parent);
            tree.transform.position = new Vector3(p.x, y, p.y);
            var model = (GameObject)PrefabUtility.InstantiatePrefab(prefab, tree.transform);
            model.transform.rotation = Quaternion.Euler(0f, R(0f, 360f), 0f) * prefab.transform.rotation;
            model.transform.localScale = prefab.transform.localScale * s;
            model.transform.position = new Vector3(p.x, y - minY * s - sink, p.y);   // 뿌리가 땅에 묻히게
            if (!castShadows)
                foreach (var r in model.GetComponentsInChildren<Renderer>()) r.shadowCastingMode = ShadowCastingMode.Off;
            if (trunkCollider)
            {
                // 줄기만 막음 (가지·잎은 지나감). ⚠ 충돌 캡슐을 자식으로 올리고 center는 (0,0,0)으로 둔다:
                // Explosive SuperCollider의 캡슐 계산이 center 높이를 빼지 않아서, center를 올리면 밀어내는 방향이 틀어져 줄기 안에 갇힘 (2026-10-23 사용자 발견, TechNotes 6-9)
                var col = new GameObject("TrunkCollider");
                col.transform.SetParent(tree.transform, false);
                col.transform.localPosition = new Vector3(0f, 2f, 0f);
                var c = col.AddComponent<CapsuleCollider>();
                c.radius = Mathf.Max(0.3f, treeH * 0.035f);   // 보이는 줄기 굵기에 가깝게 (큰 나무는 줄기도 굵음)
                c.height = 4f;
            }
            return tree;
        }

        // 경계 숲: 절벽 아랫부분부터 절벽 위까지 가문비를 빽빽하게. 절벽 위 먼 곳은 성기게, 그림자 끔 (보이기만 하는 곳)
        static void BuildBoundaryForest()
        {
            var root = new GameObject("BoundaryForest");
            const float step = 6.5f;
            int n = 0;
            for (float z = TerrainMinZ + 3f; z < TerrainMaxZ - 3f; z += step)
                for (float x = TerrainMinX + 3f; x < TerrainMaxX - 3f; x += step)
                {
                    float px = x + R(-2.5f, 2.5f), pz = z + R(-2.5f, 2.5f);
                    float b = BoundaryDistance(px, pz);
                    if (b < -4f) continue;
                    bool face = b < 24f;
                    if (rng.NextDouble() > (face ? 0.85 : 0.22)) continue;
                    if (WaterSdf(px, pz) < 3f) continue;
                    if (PathDistance(px, pz) < 6f) continue;   // 출구 골짜기의 길은 비워 둠
                    if (NearAnyHouse(new Vector2(px, pz), L(16f))) continue;
                    PlaceTree(root.transform, new Vector2(px, pz), SpruceNames, 11f, 16f, castShadows: face && b < 10f);
                    n++;
                }
            Debug.Log("[MoonlitVillage] boundary forest: " + n);
        }

        // ── 바깥 구릉 (2026-10-23): 갈 수 없는 먼 땅 ──
        // 해상도가 낮은 큰 지형 하나를 마을 지형 아래에 깔고, 마을 지형 밖으로 낮은 언덕이 이어지게 한다.
        // 마을 지형 안쪽은 4m 아래로 숨기고, 마을 지형 가장자리에서는 그 높이에서 시작해 멀어질수록 구릉으로 바뀜 → 이음매가 생기지 않음
        // 충돌 없음, 그림자 끔. 먼 곳은 안개(끝 거리 L(260))에 덮여 하늘 지평선 색으로 녹아든다
        const float OuterSize = 2600f, OuterHeight = 160f;

        static float OuterHeightAt(float x, float z)
        {
            float cx = Mathf.Clamp(x, TerrainMinX + 0.5f, TerrainMaxX - 0.5f), cz = Mathf.Clamp(z, TerrainMinZ + 0.5f, TerrainMaxZ - 0.5f);
            float d = Vector2.Distance(new Vector2(x, z), new Vector2(cx, cz));
            if (d <= 0f) return TerrainHeightAt(x, z) - 4f;
            float rolling = 16f + Mathf.PerlinNoise(x * 0.004f + 11f, z * 0.004f + 23f) * 45f
                          + Mathf.PerlinNoise(x * 0.015f + 5f, z * 0.015f + 8f) * 12f + d * 0.03f;
            return Mathf.Lerp(TerrainHeightAt(cx, cz), rolling, Mathf.SmoothStep(0f, 1f, d / 220f));
        }

        static void BuildOuterTerrain(Mats M)
        {
            var td = new TerrainData();
            td.heightmapResolution = 513;
            td.alphamapResolution = 64;
            td.size = new Vector3(OuterSize, OuterHeight, OuterSize);
            AssetDatabase.DeleteAsset(Gen + "/TD_MoonlitVillage_Outer.asset");
            AssetDatabase.CreateAsset(td, Gen + "/TD_MoonlitVillage_Outer.asset");
            td.terrainLayers = new[] { M.grass };
            float ox = (TerrainMinX + TerrainMaxX) / 2f - OuterSize / 2f, oz = (TerrainMinZ + TerrainMaxZ) / 2f - OuterSize / 2f;

            int res = td.heightmapResolution;
            var heights = new float[res, res];
            for (int iz = 0; iz < res; iz++)
                for (int ix = 0; ix < res; ix++)
                {
                    float x = ox + ix / (float)(res - 1) * OuterSize, z = oz + iz / (float)(res - 1) * OuterSize;
                    heights[iz, ix] = Mathf.Clamp01(OuterHeightAt(x, z) / OuterHeight);
                }
            td.SetHeights(0, 0, heights);
            EditorUtility.SetDirty(td);

            var go = Terrain.CreateTerrainGameObject(td);
            go.name = "Terrain_Outer (no collision)";
            go.transform.position = new Vector3(ox, 0f, oz);
            UnityEngine.Object.DestroyImmediate(go.GetComponent<TerrainCollider>());
            var t = go.GetComponent<Terrain>();
            t.heightmapPixelError = 8f;
            t.shadowCastingMode = ShadowCastingMode.Off;

            // 먼 숲: 가까울수록 많이, 멀수록 드물게
            var trees = new GameObject("OuterTrees"); trees.transform.SetParent(go.transform);
            int n = 0, guard = 0;
            while (n < 900 && guard++ < 40000)
            {
                float x = R(ox, ox + OuterSize), z = R(oz, oz + OuterSize);
                float cx = Mathf.Clamp(x, TerrainMinX, TerrainMaxX), cz = Mathf.Clamp(z, TerrainMinZ, TerrainMaxZ);
                float d = Vector2.Distance(new Vector2(x, z), new Vector2(cx, cz));
                if (d < 6f || d > 700f || rng.NextDouble() > 1f - d / 800f) continue;
                PlaceTree(trees.transform, new Vector2(x, z), SpruceNames, 11f, 17f, castShadows: false, heightAt: OuterHeightAt);
                n++;
            }
            Debug.Log("[MoonlitVillage] outer terrain trees: " + n);
        }

        // ── 출구 막기 (2026-10-23): 장승 한 쌍 + 금줄 ──
        // 금줄(새끼줄 + 한지 조각) 아래에 충돌 판을 둬서 지나갈 수 없게 한다. 보이는 막음 표시가 있으므로 "보이지 않는 벽"이 아님.
        // 나중에 지역을 확장하면 금줄만 걷어 내면 골짜기 길이 열린다 (원작: 세시호수·세시논밭·신부여성서쪽)
        static void BuildExitBarriers(Mats M)
        {
            var root = new GameObject("ExitBarriers");
            ExitBarrier(M, root.transform, "North_SesiLake", ExitPaths[0], new Vector2(L(-4f), VillageRadius + L(16f)));
            ExitBarrier(M, root.transform, "West_SesiField", ExitPaths[1], new Vector2(-(VillageRadius + L(20f)), L(40f)));
            ExitBarrier(M, root.transform, "East_SinbuyeoWest", ExitPaths[2], new Vector2(GatePos.x + 44f, GatePos.y));
        }

        static void ExitBarrier(Mats M, Transform parent, string name, Vector2[] path, Vector2 near)
        {
            // near를 길 위로 옮기고, 그 지점의 길 방향(마을 → 바깥)을 구한다
            Vector2 at = path[0], dir = Vector2.up; float bestD = float.MaxValue;
            for (int i = 0; i < path.Length - 1; i++)
            {
                Vector2 a = path[i], ab = path[i + 1] - a;
                float t = Mathf.Clamp01(Vector2.Dot(near - a, ab) / ab.sqrMagnitude);
                Vector2 q = a + ab * t;
                float d = Vector2.Distance(near, q);
                if (d < bestD) { bestD = d; at = q; dir = ab.normalized; }
            }
            Vector2 side = new Vector2(dir.y, -dir.x);   // 길을 가로지르는 방향
            var g = new GameObject("Exit_" + name); g.transform.SetParent(parent);

            Jangseung(M, g.transform, at + side * 4.2f, -dir);
            Jangseung(M, g.transform, at - side * 4.2f, -dir);

            // 금줄: 양 끝 말뚝 사이에 처지는 새끼줄 + 일정 간격 한지 조각
            float ropeHalf = GorgeHalf + 1f;
            Vector2 a2 = at - side * ropeHalf, b2 = at + side * ropeHalf;
            float ya = TerrainHeightAt(a2.x, a2.y) + 1.7f, yb = TerrainHeightAt(b2.x, b2.y) + 1.7f;
            foreach (var e in new[] { a2, b2 })
                Prim(PrimitiveType.Cylinder, "RopeStake", g.transform, new Vector3(e.x, TerrainHeightAt(e.x, e.y) + 0.9f, e.y), new Vector3(0.18f, 0.95f, 0.18f), M.darkWood);
            const int segs = 24;
            Vector3 Rope(float t) { var q = Vector2.Lerp(a2, b2, t); return new Vector3(q.x, Mathf.Lerp(ya, yb, t) - 0.6f * (1f - (2f * t - 1f) * (2f * t - 1f)), q.y); }
            for (int i = 0; i < segs; i++)
            {
                Vector3 p0 = Rope(i / (float)segs), p1 = Rope((i + 1) / (float)segs);
                Prim(PrimitiveType.Cylinder, "Rope", g.transform, (p0 + p1) / 2f, new Vector3(0.07f, Vector3.Distance(p0, p1) / 2f, 0.07f), M.thatchYellow, Quaternion.FromToRotation(Vector3.up, p1 - p0), false);
                if (i % 2 == 1)
                    Prim(PrimitiveType.Cube, "HanjiStrip", g.transform, p1 - Vector3.up * 0.22f, new Vector3(0.14f, 0.38f, 0.02f), M.plaster, Quaternion.LookRotation(new Vector3(dir.x, 0f, dir.y)), false);
            }

            // 충돌 판: 골짜기 바닥과 절벽 아랫부분까지 막음
            float cy = TerrainHeightAt(at.x, at.y);
            var col = new GameObject("BarrierCollider"); col.transform.SetParent(g.transform);
            col.transform.SetPositionAndRotation(new Vector3(at.x, cy + 1.5f, at.y), Quaternion.LookRotation(new Vector3(dir.x, 0f, dir.y)));
            col.AddComponent<BoxCollider>().size = new Vector3((GorgeHalf + 4f) * 2f, 6f, 0.6f);
        }

        // 장승: 나무 기둥 + 얼굴(눈·코·입) + 관모 + 글씨 판. face = 얼굴이 보는 방향 (마을 쪽)
        static void Jangseung(Mats M, Transform parent, Vector2 p, Vector2 face)
        {
            float y = TerrainHeightAt(p.x, p.y);
            var root = new GameObject("Jangseung"); root.transform.SetParent(parent);
            root.transform.SetPositionAndRotation(new Vector3(p.x, y, p.y), Quaternion.LookRotation(new Vector3(face.x, 0f, face.y)));
            var t = root.transform;
            GameObject Part(PrimitiveType type, string n, Vector3 local, Vector3 scale, Material m, bool col = false)
            {
                var o = Prim(type, n, t, Vector3.zero, scale, m, null, col);
                o.transform.localPosition = local; o.transform.localRotation = Quaternion.identity;
                return o;
            }
            Part(PrimitiveType.Cylinder, "Body", new Vector3(0f, 1.4f, 0f), new Vector3(0.6f, 1.4f, 0.6f), M.darkWood, true);
            Part(PrimitiveType.Cylinder, "Head", new Vector3(0f, 2.75f, 0f), new Vector3(0.72f, 0.3f, 0.72f), M.darkWood);
            Part(PrimitiveType.Cylinder, "HatBrim", new Vector3(0f, 3.1f, 0f), new Vector3(1.0f, 0.04f, 1.0f), M.windowDark);
            Part(PrimitiveType.Cylinder, "HatTop", new Vector3(0f, 3.35f, 0f), new Vector3(0.55f, 0.22f, 0.55f), M.windowDark);
            Part(PrimitiveType.Sphere, "EyeL", new Vector3(-0.15f, 2.85f, 0.33f), new Vector3(0.16f, 0.12f, 0.08f), M.plaster);
            Part(PrimitiveType.Sphere, "EyeR", new Vector3(0.15f, 2.85f, 0.33f), new Vector3(0.16f, 0.12f, 0.08f), M.plaster);
            Part(PrimitiveType.Cube, "Nose", new Vector3(0f, 2.68f, 0.36f), new Vector3(0.1f, 0.18f, 0.1f), M.darkWood);
            Part(PrimitiveType.Cube, "Mouth", new Vector3(0f, 2.5f, 0.33f), new Vector3(0.3f, 0.06f, 0.06f), M.redWood);
            Part(PrimitiveType.Cube, "Plaque", new Vector3(0f, 1.6f, 0.31f), new Vector3(0.22f, 1.3f, 0.02f), M.plaster);
        }

        static void BuildVegetation(Mats M)
        {
            var lotusRoot = new GameObject("Lotus");
            int pads = 0, flowers = 0, guard = 0;
            int padCount = Mathf.RoundToInt(110 * MapScale * MapScale), pondPads = Mathf.RoundToInt(75 * MapScale * MapScale);
            while (pads < padCount && guard++ < 90000)
            {
                float x, z;
                if (pads < pondPads) { x = R(L(-42f), L(40f)); z = R(L(-36f), L(34f)); } else { x = R(L(-30f), L(30f)); z = R(L(-194f) + SouthShift, L(-148f) + SouthShift); }
                if (WaterSdf(x, z) > -1.6f) continue;
                if (NearStoneBridge(x, z, 0.5f, 2.5f) || NearWoodBridge(x, z, 1.5f)) continue;
                float s = R(0.7f, 1.3f);
                Prim(PrimitiveType.Cylinder, "LilyPad", lotusRoot.transform, new Vector3(x, WaterY + 0.02f, z), new Vector3(s, 0.02f, s), M.lilyPad, Quaternion.Euler(0, R(0, 360), 0), false);
                pads++;
                if (flowers < Mathf.RoundToInt(36 * MapScale * MapScale) && rng.NextDouble() < 0.4)
                {
                    Prim(PrimitiveType.Sphere, "LotusFlower", lotusRoot.transform, new Vector3(x + R(-0.3f, 0.3f), WaterY + 0.25f, z + R(-0.3f, 0.3f)), new Vector3(0.45f, 0.35f, 0.45f), M.lotus, null, false);
                    flowers++;
                }
            }

            var bambooRoot = new GameObject("Bamboo");
            Vector2[] clusters = { L(-14f, -5f), L(15f, -7f), L(40f, 16f), new Vector2(L(-26f), L(-148f) + SouthShift), new Vector2(L(24f), L(-194f) + SouthShift) };
            foreach (var c in clusters)
                for (int i = 0; i < 12; i++)
                {
                    float x = c.x + R(-2.2f, 2.2f), z = c.y + R(-2.2f, 2.2f);
                    if (WaterSdf(x, z) < 0.6f) continue;
                    if (PathDistance(x, z) < 3f) continue;
                    float y = TerrainHeightAt(x, z);
                    float h = R(4f, 6f);
                    Prim(PrimitiveType.Cylinder, "Bamboo", bambooRoot.transform, new Vector3(x, y + h / 2f, z), new Vector3(0.14f, h / 2f, 0.14f), M.bamboo, Quaternion.Euler(R(-4, 4), 0, R(-4, 4)), false);
                    for (int l = 0; l < 3; l++)
                        Prim(PrimitiveType.Sphere, "Leaves", bambooRoot.transform, new Vector3(x + R(-0.5f, 0.5f), y + h * R(0.6f, 0.95f), z + R(-0.5f, 0.5f)), new Vector3(R(0.6f, 1.1f), R(0.3f, 0.5f), R(0.6f, 1.1f)), M.bamboo, null, false);
                }

            var blossomRoot = new GameObject("BlossomTrees");
            int bn = 0; guard = 0;
            var bplaced = new List<Vector2>();
            int blossomCount = Mathf.RoundToInt(22 * MapScale * MapScale);
            while (bn < blossomCount && guard++ < 12000)
            {
                float x = R(L(-40f), L(40f)), z = R(L(-210f) + SouthShift, L(-140f) + SouthShift);
                float sdf = WaterSdf(x, z);
                if (sdf < 2f || sdf > 9f) continue;
                if (PathDistance(x, z) < 4f) continue;
                if (NearWoodBridge(x, z, 4f)) continue;
                bool close = false; foreach (var q in bplaced) if (Vector2.Distance(q, new Vector2(x, z)) < 6f) { close = true; break; }
                if (close) continue;
                bplaced.Add(new Vector2(x, z));
                PlaceTree(blossomRoot.transform, new Vector2(x, z), SakuraNames, 6f, 8f, trunkCollider: true);
                bn++;
            }

            var treeRoot = new GameObject("Trees");
            var scattered = new List<Vector2>();
            int tries = 0;
            int treeCount = Mathf.RoundToInt(260 * MapScale * MapScale);   // 맵 넓이에 맞춰 나무 수도 늘림 (나무 크기는 그대로)
            while (scattered.Count < treeCount && tries++ < 200000)
            {
                float x = R(TerrainMinX + 6f, TerrainMinX + TerrainSize - 6f), z = R(TerrainMinZ + 6f, TerrainMinZ + TerrainSize - 6f);
                var p = new Vector2(x, z);
                if (WaterSdf(x, z) < 2.5f) continue;
                if (PathDistance(x, z) < 5f) continue;
                if (IslandSdf(x, z) < 3f) continue;
                if (Vector2.Distance(p, PlateauCenter) < PlateauRadius + 6f) continue;
                if (Vector2.Distance(p, GraveCenter) < L(12f)) continue;
                if (NearAnyHouse(p, L(16f))) continue;
                if (Vector2.Distance(p, GatePos) < 14f) continue;
                if (BoundaryDistance(x, z) > -3f) continue;   // 절벽·골짜기는 경계 숲(BuildBoundaryForest)이 맡음
                bool inGrave = Vector2.Distance(p, GraveCenter) < GraveRadius;
                if (inGrave && Vector2.Distance(p, GraveCenter) < GraveClearing + 2f) continue;
                bool inLake = Vector2.Distance(p, LakeCenter) < L(40f);
                if (inLake && WaterSdf(x, z) < 9f) continue;
                // 해자·순환 길 안쪽 마을 광장에는 나무를 심지 않는다
                if (!inGrave && Vector2.Distance(p, Vector2.zero) < RingRoadRadius + L(6f)) continue;
                float spacing = inGrave ? 4.5f : 8f;
                bool close = false; foreach (var q in scattered) if (Vector2.Distance(q, p) < spacing) { close = true; break; }
                if (close) continue;
                scattered.Add(p);
                // 마을 안 = 참나무, 마을 밖 언덕 = 참나무와 가문비 섞음 (2026-10-23 나무 결정, TechNotes 2-11)
                if (inGrave) PlaceTree(treeRoot.transform, p, OakNames, 6f, 9f, trunkCollider: true);
                else if (p.magnitude < VillageRadius || rng.NextDouble() < 0.4) PlaceTree(treeRoot.transform, p, OakNames, 7f, 10f, trunkCollider: true);
                else PlaceTree(treeRoot.transform, p, SpruceNames, 9f, 13f, trunkCollider: true);
            }
            Debug.Log("[MoonlitVillage] trees: " + scattered.Count + ", blossom: " + bn);
        }

        static void Lantern(Transform parent, Vector3 lp, Mats M, float intensity = 12f, float range = 18f)
        {
            var root = new GameObject("Lantern");
            root.transform.SetParent(parent);
            root.transform.position = lp;
            Prim(PrimitiveType.Cylinder, "Post", root.transform, lp + new Vector3(0, 1.2f, 0), new Vector3(0.12f, 1.2f, 0.12f), M.lanternPost);
            Prim(PrimitiveType.Cube, "Paper", root.transform, lp + new Vector3(0, 2.6f, 0), new Vector3(0.55f, 0.75f, 0.55f), M.lantern);
            var pl = new GameObject("PointLight_Warm");
            pl.transform.SetParent(root.transform);
            pl.transform.position = lp + new Vector3(0, 2.6f, 0);
            var l = pl.AddComponent<Light>();
            l.type = LightType.Point;
            l.color = new Color(1f, 0.62f, 0.32f);
            l.intensity = intensity;
            l.range = range;
            l.shadows = LightShadows.Soft;
        }

        static void BuildLanterns(Mats M)
        {
            var root = new GameObject("Lanterns");
            Vector2[] spots =
            {
                L(14f, 3f),
                L(-12f, 4f),
                new Vector2(2.8f, WoodBridgeZ0 + 3f),
                new Vector2(2.8f, WoodBridgeZ1 - 3f),
                new Vector2(PlateauCenter.x - L(12f), PlateauCenter.y + L(10f)),
                // 순환 길 동서남북 (길 바깥쪽)
                new Vector2(0f, RingRoadRadius + L(4f)), new Vector2(0f, -RingRoadRadius - L(4f)),
                new Vector2(-RingRoadRadius - L(4f), L(-6f)), new Vector2(RingRoadRadius + L(4f), L(6f)),
                // 동쪽 대문 안쪽 양옆 (마을 입장 지점)
                new Vector2(GatePos.x - 3f, GatePos.y + 5f), new Vector2(GatePos.x - 3f, GatePos.y - 5f),
            };
            foreach (var s in spots)
            {
                bool graveyardBand = s.y < L(-90f) + SouthShift && s.y > L(-160f) + SouthShift;
                Lantern(root.transform, new Vector3(s.x, TerrainHeightAt(s.x, s.y), s.y), M, graveyardBand ? 8f : 12f, 16f);
            }
        }



        static void BuildSnow(Mats M)
        {
            var go = new GameObject("Snow (particles, follows camera)");
            var ps = go.AddComponent<ParticleSystem>();
            var main = ps.main;
            main.duration = 10f; main.loop = true; main.prewarm = true;
            main.startLifetime = new ParticleSystem.MinMaxCurve(9f, 13f);
            main.startSpeed = 0f;
            main.startSize = new ParticleSystem.MinMaxCurve(0.05f, 0.11f);
            main.startColor = new Color(1f, 1f, 1f, 0.9f);
            main.gravityModifier = 0.045f;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 8000; main.playOnAwake = true;
            var em = ps.emission; em.rateOverTime = 500f;
            var shape = ps.shape; shape.shapeType = ParticleSystemShapeType.Box; shape.scale = new Vector3(70f, 1f, 70f);
            var noise = ps.noise; noise.enabled = true; noise.strength = 0.35f; noise.frequency = 0.25f; noise.scrollSpeed = 0.15f; noise.damping = true; noise.quality = ParticleSystemNoiseQuality.Medium;
            var col = ps.colorOverLifetime; col.enabled = true;
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                      new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.1f), new GradientAlphaKey(1f, 0.85f), new GradientAlphaKey(0f, 1f) });
            col.color = g;
            var rot = ps.rotationOverLifetime; rot.enabled = true; rot.z = new ParticleSystem.MinMaxCurve(-0.6f, 0.6f);
            var r = go.GetComponent<ParticleSystemRenderer>();
            r.renderMode = ParticleSystemRenderMode.Billboard; r.sharedMaterial = M.snow;
            r.shadowCastingMode = ShadowCastingMode.Off; r.receiveShadows = false; r.sortMode = ParticleSystemSortMode.None;

            var follow = go.AddComponent<MoonlitVillage.SnowFollowCamera>();
            follow.heightAboveCamera = 14f; follow.forwardOffset = 12f; follow.fullRate = 500f;
            follow.useZone = true;
            follow.zoneCenter = new Vector3(L(25f), Ground, L(-150f) + SouthShift);
            follow.zoneRadius = L(95f);
            follow.zoneFade = L(22f);
        }

        static void BuildLighting(Mats M)
        {
            var moonRoot = new GameObject("Moon (follows camera)");
            moonRoot.AddComponent<MoonlitVillage.MoonFollowCamera>();
            var moon = Prim(PrimitiveType.Sphere, "MoonDisc", moonRoot.transform, MoonDir * 380f, Vector3.one * 60f, M.moon, null, false);
            moon.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;
            var halo = Prim(PrimitiveType.Quad, "MoonHalo", moonRoot.transform, MoonDir * 379f, Vector3.one * 150f, M.halo, Quaternion.LookRotation(MoonDir), false);
            halo.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;

            var lgo = new GameObject("Moon Light (Directional)");
            var light = lgo.AddComponent<Light>();
            light.type = LightType.Directional;
            light.color = new Color(0.66f, 0.76f, 1f);
            light.intensity = 0.7f;
            light.shadows = LightShadows.Soft;
            light.shadowStrength = 0.9f;
            lgo.transform.rotation = Quaternion.LookRotation(-MoonDir);

            // 하늘 (2026-10-23 결정): Fantasy Skybox FREE의 달 없는 밤하늘. 달은 위의 우리 달 오브젝트가 맡는다 (TechNotes 2-4)
            // 낮 하늘(FS000_Day_05_Sunless)과의 전환은 3단계 시간 흐름 시스템에서 한다
            var sky = AssetDatabase.LoadAssetAtPath<Material>(NightSkyPath);
            if (sky == null)
            {
                Debug.LogWarning("[MoonlitVillage] 밤하늘 머티리얼이 없어 임시 절차적 하늘을 씀: " + NightSkyPath);
                sky = new Material(Shader.Find("Skybox/Procedural"));
                sky.SetFloat("_SunSize", 0f);
                sky.SetColor("_SkyTint", new Color(0.18f, 0.22f, 0.45f));
                sky.SetFloat("_Exposure", 0.5f);
                AssetDatabase.CreateAsset(sky, Gen + "/M_Sky_NightProcedural.mat");
            }
            RenderSettings.skybox = sky;
            RenderSettings.sun = light;
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.09f, 0.11f, 0.21f);
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            // 안개 색 = 밤하늘 지평선 색 (큐브맵 가운데 줄에서 뽑은 값 7,60,113). 먼 지형이 하늘색으로 녹아들어 경계선이 덜 보임
            RenderSettings.fogColor = new Color(7f / 255f, 60f / 255f, 113f / 255f);
            RenderSettings.fogStartDistance = L(60f);
            RenderSettings.fogEndDistance = L(260f);
        }

        // 게임 진행용 오브젝트 (2026-10-23: 생성 후 손으로 하던 작업을 빌더에 포함 → 다시 생성해도 잃지 않음)
        // - PlayerSpawnPoint: 동쪽 대문 바깥, 마을 쪽(서쪽, -X)을 보고 시작 (GameBootstrap이 찾아서 플레이어를 옮김)
        // - 마을 비행 카메라(Main Camera)는 끔: 게임에서는 Core 씬의 카메라를 쓴다 (이 카메라는 전경 캡처·연출 후보용)
        static void BuildGameplay()
        {
            var gameplay = new GameObject("GamePlay");
            var spawn = new GameObject("PlayerSpawnPoint");
            spawn.transform.SetParent(gameplay.transform);
            Vector2 sp = GatePos + new Vector2(16f, 0f);
            spawn.transform.SetPositionAndRotation(new Vector3(sp.x, TerrainHeightAt(sp.x, sp.y), sp.y), Quaternion.Euler(0f, -90f, 0f));
            spawn.AddComponent<SesiVillage.Core.PlayerSpawnPoint>();

            var flyCam = GameObject.FindWithTag("MainCamera");
            if (flyCam != null) flyCam.SetActive(false);
        }

        static void BuildCamera()
        {
            var go = new GameObject("Main Camera");
            go.tag = "MainCamera";
            var cam = go.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.Skybox;
            cam.backgroundColor = new Color(0.02f, 0.03f, 0.08f);
            cam.fieldOfView = 48f;
            cam.nearClipPlane = 0.3f;
            cam.farClipPlane = 900f;
            go.AddComponent<AudioListener>();
            var data = go.AddComponent<UniversalAdditionalCameraData>();
            data.renderPostProcessing = false;
            data.antialiasing = AntialiasingMode.None;
            go.transform.position = new Vector3(0f, 95f * MapScale, L(-130f));
            go.transform.LookAt(new Vector3(0f, Ground, 2f));

            var fly = go.AddComponent<MoonlitVillage.CameraFlythrough>();
            float pt = Ground + PlateauRise;
            Vector3 hill = new Vector3(PlateauCenter.x, pt, PlateauCenter.y);
            Vector3 hillCam = hill + new Vector3(-8f, 2.2f, 6f);
            void W(Vector3 p, Vector3 l, float travel, float hold = 0.8f, float fov = 55f) => fly.waypoints.Add(new MoonlitVillage.CameraFlythrough.Waypoint { position = p, lookAt = l, travelSeconds = travel, holdSeconds = hold, fieldOfView = fov });
            float sz = SouthShift;
            Vector2 seollal = Houses[0].pos;
            // 동쪽 대문 밖 → 마을 입장 → 마을 한 바퀴 → 묘지 → 달맞이다리 → 달맞이고개 (마을 첫 진입 전경 연출 후보)
            // 가로(x·z)만 맵 배율로 (높이는 그대로)
            Vector3 P(float x, float y, float z) => new Vector3(L(x), y, L(z));
            W(new Vector3(GatePos.x + L(40f), 14f, GatePos.y), new Vector3(0f, Ground + 3f, 0f), 0f, 2.0f, 55f);
            W(new Vector3(GatePos.x + L(8f), Ground + 2.5f, GatePos.y), new Vector3(GatePos.x - L(30f), Ground + 2f, GatePos.y), 8f, 0.8f, 55f);
            W(P(40f, 9f, -20f), P(0f, Ground + 3f, 12f), 8f, 0.6f, 55f);
            W(P(4f, 6f, -12f), new Vector3(ChiefHousePos.x, Ground + 3f, ChiefHousePos.y), 7f, 1.0f, 50f);
            W(P(-36f, 8f, 0f), new Vector3(seollal.x, Ground + 2.5f, seollal.y), 9f, 0.8f, 52f);
            W(P(-10f, 12f, 40f), P(10f, Ground + 2f, 66f), 9f, 0.6f, 58f);
            W(P(20f, 12f, -60f), new Vector3(0f, Ground, L(-96f) + sz), 9f, 0.4f, 58f);
            W(new Vector3(L(20f), 9f, L(-72f) + sz), new Vector3(0f, Ground + 1f, L(-92f) + sz), 9f, 0.8f, 55f);
            W(new Vector3(L(2f), 7f, L(-136f) + sz), new Vector3(0f, Ground + 1f, L(-175f) + sz), 9f, 0.6f, 55f);
            W(new Vector3(0f, Ground + 2.2f, L(-168f) + sz), new Vector3(0f, Ground + 1.6f, L(-200f) + sz), 9f, 0.6f, 58f);
            W(new Vector3(L(46f), 20f, L(-176f) + sz), hill + Vector3.up * 2f, 9f, 0.4f, 55f);
            W(hillCam, hillCam + MoonDir * 100f + Vector3.down * 20f, 9f, 8.0f, 55f);
        }
    }
}
