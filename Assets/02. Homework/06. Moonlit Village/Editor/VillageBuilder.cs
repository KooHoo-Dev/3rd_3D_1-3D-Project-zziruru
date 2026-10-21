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
        const string ScenePath = Root + "/Moonlit Village.unity";
        const string CaptureDir = "Reference/Moonlit Village/Captures";

        const float TerrainSize = 400f;
        const float TerrainMinX = -200f, TerrainMinZ = -300f;
        const float TerrainHeight = 60f;
        const float Ground = 4f;
        const float WaterDrop = 0.8f;
        const float PondDepth = 2.4f;
        static float WaterY => Ground - WaterDrop;

        const float BridgeLength = 14f;
        const float BridgeWidth = 5f;
        const float DeckRise = 1.4f;
        const float ArchRadius = 4.0f;
        const float ArchHeight = 1.8f;
        const float RampLen = 5.5f;
        const float MoatWidth = 8f;
        static float DeckY => Ground + DeckRise;
        static readonly Vector3 BridgeWest = new Vector3(-20f, 0f, 9f);
        static readonly Vector3 BridgeEast = new Vector3(24f, 0f, -2f);

        const float WoodBridgeX = 0f, WoodBridgeZ0 = -144f, WoodBridgeZ1 = -196f, WoodBridgeWidth = 4.2f;
        static readonly Vector2 LakeCenter = new Vector2(0f, -170f);

        static readonly Vector2 PlateauCenter = new Vector2(70f, -146f);
        const float PlateauRadius = 22f, PlateauSlope = 30f, PlateauRise = 18f;

        static readonly Vector2 GraveCenter = new Vector2(0f, -92f);
        const float GraveRadius = 46f;
        const float GraveLoopRadius = 27f;
        const float GraveClearing = 13f;

        static readonly Vector2 HouseAPos = new Vector2(-50f, 26f);
        static readonly Vector2 HouseBPos = new Vector2(-62f, 0f);
        static readonly Vector4[] YardEllipses =
        {
            new Vector4(-50f, 19f, 11f, 9f), new Vector4(-62f, -7f, 12f, 9f),
        };

        static readonly Vector2 HouseCPos = new Vector2(32f, -56f);
        static readonly Rect HouseCYard = new Rect(20f, -68f, 24f, 18f);

        static readonly Vector2 GatePos = new Vector2(47f, -2.5f);

        static readonly Vector3 MoonDir = new Vector3(0.55f, 0.42f, -0.72f).normalized;

        static System.Random rng = new System.Random(20260919);
        static float R(float a, float b) => a + (float)rng.NextDouble() * (b - a);

        [MenuItem("Moonlit Village/Build Scene")]
        public static void Build()
        {
            rng = new System.Random(20260919);
            EnsureFolders();
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var M = CreateMaterials();
            BuildTerrain(M);
            BuildWater(M);
            BuildBridge(M, BridgeWest, "Bridge_West");
            BuildBridge(M, BridgeEast, "Bridge_East");
            BuildHouse(M);
            BuildWestVillage(M);
            BuildHouseC(M);
            BuildEastGate(M);
            BuildWoodBridge(M);
            BuildGraveyard(M);
            BuildPlateau(M);
            BuildShoreStones(M);
            BuildVegetation(M);
            BuildLanterns(M);
            BuildLighting(M);
            BuildCamera();
            BuildSnow(M);

            EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.SaveAssets();
            Debug.Log("[MoonlitVillage] scene built: " + ScenePath);
        }


        public static void BuildAndCapture() { Build(); CaptureAllViews(); }

        static (string name, Vector3 pos, Vector3 look, float fov)[] Views()
        {
            float pt = Ground + PlateauRise;
            Vector3 pc = new Vector3(PlateauCenter.x, pt, PlateauCenter.y);
            return new[]
            {
                ("island_overview",  new Vector3(-14f, 46f, -66f), new Vector3(0f, Ground, 4f), 50f),
                ("west_bridge",      new Vector3(-34f, 9f, -8f),   new Vector3(-20f, Ground + 1f, 9f), 45f),
                ("house_front",      new Vector3(3f, 6.5f, -6f),   new Vector3(0f, Ground + 2.5f, 13f), 50f),
                ("east_bridge_moon", new Vector3(-6f, 6.5f, -6f),  new Vector3(40f, Ground + 16f, -46f), 60f),
                ("house_c",       new Vector3(HouseCPos.x - 4f, 7f, HouseCPos.y - 22f), new Vector3(HouseCPos.x, Ground + 2.5f, HouseCPos.y - 2f), 50f),
                ("east_gate",        new Vector3(GatePos.x - 16f, 6f, GatePos.y - 9f), new Vector3(GatePos.x, Ground + 3f, GatePos.y), 50f),
                ("house_a",    new Vector3(HouseAPos.x + 10f, 6.5f, HouseAPos.y - 20f), new Vector3(HouseAPos.x, Ground + 2.5f, HouseAPos.y), 50f),
                ("house_b",    new Vector3(HouseBPos.x - 9f, 6.5f, HouseBPos.y - 20f), new Vector3(HouseBPos.x, Ground + 2.5f, HouseBPos.y), 50f),
                ("west_village_air", new Vector3(-24f, 36f, -32f), new Vector3(-54f, Ground, 12f), 55f),
                ("graveyard",        new Vector3(26f, 14f, -60f),  new Vector3(0f, Ground, -92f), 50f),
                ("graveyard_air",    new Vector3(0f, 70f, -40f),   new Vector3(0f, Ground, -96f), 55f),
                ("moon_bridge",      new Vector3(-13f, 8f, -154f), new Vector3(2f, Ground + 1f, -176f), 50f),
                ("moon_bridge_walk", new Vector3(0f, Ground + 2f, -148f), new Vector3(0f, Ground + 1.5f, -194f), 55f),
                ("hill_moon",        pc + new Vector3(-8f, 2.2f, 6f), pc + new Vector3(-8f, 2.2f, 6f) + MoonDir * 100f + Vector3.down * 20f, 55f),
                ("hill_top",         pc + new Vector3(-30f, 14f, 22f), pc + Vector3.up * 2f, 50f),
                ("hill_topdown",     pc + new Vector3(0f, 45f, 0.01f), pc, 55f),
                ("whole_map",        new Vector3(-190f, 150f, -40f), new Vector3(20f, Ground, -100f), 60f),
            };
        }

        [MenuItem("Moonlit Village/Capture All Views")]
        public static void CaptureAllViews()
        {
            var cam = Camera.main;
            if (cam == null) { Debug.LogError("[MoonlitVillage] no main camera"); return; }
            var savedPos = cam.transform.position; var savedRot = cam.transform.rotation; float savedFov = cam.fieldOfView;
            int i = 1;
            foreach (var v in Views())
            {
                cam.transform.position = v.pos; cam.transform.LookAt(v.look); cam.fieldOfView = v.fov;
                CaptureTo(cam, CaptureDir + "/view_" + (i++).ToString("00") + "_" + v.name + ".png");
            }
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

        static void CaptureTo(Camera cam, string path)
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
            new Vector3( 0f,  4f, 17f), new Vector3(-9f, -3f, 10f), new Vector3(10f, -3f, 10f), new Vector3( 0f, 16f, 11f),
        };
        static readonly Vector3[] LobeCircles =
        {
            new Vector3(-26f,-18f, 15f), new Vector3( 0f,-26f, 10f), new Vector3( 26f, 16f, 11f), new Vector3( 30f,-16f, 10f), new Vector3(-28f, 22f, 9f),
        };
        static readonly Vector3[] GravePondCircles =
        {
            new Vector3(0f, -92f, 5.5f), new Vector3(2.5f, -98f, 4.5f), new Vector3(-2f, -87f, 3.5f),
        };
        static readonly Vector3[] MoonLakeCircles =
        {
            new Vector3(0f, -170f, 20f), new Vector3(-17f, -163f, 12f), new Vector3(18f, -177f, 12f), new Vector3(-9f, -185f, 8f), new Vector3(14f, -156f, 8f),
        };

        static readonly Vector2[][] Paths =
        {
            new[] { new Vector2(-33f, 9f), new Vector2(-40f, 11f), new Vector2(-46f, 18f), new Vector2(-50f, 21f) },
            new[] { new Vector2(-40f, 11f), new Vector2(-52f, 4f), new Vector2(-60f, -4f) },
            new[] { new Vector2(37f, -2f), new Vector2(47f, -2.5f), new Vector2(60f, -3f) },
            new[] { new Vector2(40f, -2f), new Vector2(40f, -28f), new Vector2(24f, -48f), new Vector2(8f, -58f), new Vector2(0f, -64f) },
            new[] { new Vector2(18f, -52f), new Vector2(24f, -64f), new Vector2(32f, -71f), new Vector2(32f, -66f) },
            new[] { new Vector2(0f, -64f), new Vector2(-GraveLoopRadius, -92f), new Vector2(-4f, -92f - GraveLoopRadius), new Vector2(GraveLoopRadius, -90f), new Vector2(0f, -64f) },
            new[] { new Vector2(-GraveLoopRadius, -92f), new Vector2(-GraveLoopRadius - 16f, -104f) },
            new[] { new Vector2(GraveLoopRadius, -90f), new Vector2(GraveLoopRadius + 18f, -80f) },
            new[] { new Vector2(-4f, -92f - GraveLoopRadius), new Vector2(0f, -140f) },
            new[] { new Vector2(0f, -198f), new Vector2(16f, -206f), new Vector2(36f, -202f), new Vector2(50f, -186f), new Vector2(60f, -166f), new Vector2(68f, -152f) },
        };

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

        static float BridgeProximity(float x, float z)
        {
            var p = new Vector2(x, z);
            float d = Mathf.Min(Vector2.Distance(p, new Vector2(BridgeWest.x, BridgeWest.z)), Vector2.Distance(p, new Vector2(BridgeEast.x, BridgeEast.z)));
            d = Mathf.Min(d, Vector2.Distance(p, new Vector2(WoodBridgeX, WoodBridgeZ0)));
            d = Mathf.Min(d, Vector2.Distance(p, new Vector2(WoodBridgeX, WoodBridgeZ1)));
            return Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(9f, 20f, d));
        }

        static void Warp(float x, float z, out float wx, out float wz)
        {
            float amp = 6.5f * BridgeProximity(x, z);
            wx = x + (Mathf.PerlinNoise(x * 0.045f + 11.3f, z * 0.045f + 5.7f) - 0.5f) * 2f * amp;
            wz = z + (Mathf.PerlinNoise(x * 0.045f + 41.9f, z * 0.045f + 23.1f) - 0.5f) * 2f * amp;
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
            float d = Vector2.Distance(p, Vector2.zero) - 50f;
            d = Mathf.Min(d, Vector2.Distance(p, GraveCenter) - (GraveRadius + 6f));
            d = Mathf.Min(d, Vector2.Distance(p, LakeCenter) - 36f);
            d = Mathf.Min(d, Vector2.Distance(p, PlateauCenter) - (PlateauRadius + PlateauSlope + 4f));
            d = Mathf.Min(d, PathDistance(x, z) - 7f);
            d = Mathf.Min(d, Vector2.Distance(p, HouseAPos) - 24f);
            d = Mathf.Min(d, Vector2.Distance(p, HouseBPos) - 24f);
            d = Mathf.Min(d, Vector2.Distance(p, HouseCPos) - 22f);
            return d;
        }

        static float PlateauK(float x, float z)
        {
            float dist = Vector2.Distance(new Vector2(x, z), PlateauCenter);
            float edgeNoise = (Mathf.PerlinNoise(x * 0.06f + 300f, z * 0.06f + 120f) - 0.5f) * 6f;
            return Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(PlateauRadius + PlateauSlope + edgeNoise, PlateauRadius + edgeNoise, dist));
        }

        static float TerrainHeightAt(float x, float z)
        {
            float sdf = WaterSdf(x, z);
            float t = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(-2.6f, 0.9f, sdf));
            float bottom = WaterY - PondDepth;
            float h = Mathf.Lerp(bottom, Ground, t);
            h += (Mathf.PerlinNoise(x * 0.05f + 3f, z * 0.05f + 7f) - 0.5f) * 0.35f * t;

            float hillK = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0f, 40f, ZoneDistance(x, z)));
            float hill = Mathf.PerlinNoise(x * 0.02f + 100f, z * 0.02f + 50f) * 0.7f + Mathf.PerlinNoise(x * 0.06f + 200f, z * 0.06f + 80f) * 0.3f;
            h += (hill * 14f + 3f) * hillK;

            float pk = PlateauK(x, z);
            h += PlateauRise * pk + (Mathf.PerlinNoise(x * 0.08f + 500f, z * 0.08f + 20f) - 0.5f) * 0.6f * pk;
            return h;
        }

        static float PlateauTopY(float x, float z) => TerrainHeightAt(x, z);

        static bool InDirtPath(float x, float z)
        {
            float n = (Mathf.PerlinNoise(x * 0.25f, z * 0.25f) - 0.5f) * 1.6f;
            if (PathDistance(x, z) < 3.2f + n) return true;
            if (Vector2.Distance(new Vector2(x, z), GraveCenter) < GraveClearing + (Mathf.PerlinNoise(x * 0.12f + 9f, z * 0.12f + 4f) - 0.5f) * 5f) return true;
            foreach (var e in YardEllipses)
            {
                float dx = (x - e.x) / e.z, dz = (z - e.y) / e.w;
                if (dx * dx + dz * dz + (Mathf.PerlinNoise(x * 0.15f + 50f, z * 0.15f + 70f) - 0.5f) * 0.5f < 1f) return true;
            }
            return false;
        }

        class Mats
        {
            public Material stone, deck, railing, water, shore, lilyPad, lotus, bamboo, trunk, leaves, blossom, deadWood, moon, halo, lantern, lanternPost;
            public Material brick, roof, redWood, eave, windowDark, windowLattice, plinth, fence, inscribedStone, plank, mound, marker, snow;
            public Material thatchYellow, thatchOrange, plaster, darkWood, paperWindow, jar, pumpkin, gourdVine;
            public Material blueRoof, signBoard, totemWood, sunflowerPetal, sunflowerCenter;
            public TerrainLayer grass, dirt, paving;
        }

        static void EnsureFolders()
        {
            if (!AssetDatabase.IsValidFolder(Root)) AssetDatabase.CreateFolder("Assets/02. Homework", "06. Moonlit Village");
            if (!AssetDatabase.IsValidFolder(Gen)) AssetDatabase.CreateFolder(Root, "Generated");
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
            M.trunk = LitMat("PH_Trunk", new Color(0.35f, 0.26f, 0.18f), 0.1f);
            M.leaves = LitMat("PH_Leaves", new Color(0.16f, 0.32f, 0.14f), 0.15f);
            M.blossom = LitMat("PH_Blossom", new Color(0.95f, 0.62f, 0.75f), 0.2f);
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
                    if (pd < 15f + pn) paving = Mathf.Clamp01(Mathf.InverseLerp(15f + pn, 11f + pn, pd));
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

        static Mesh HipRoof(float width, float depth, float h, string name)
        {
            float hw = width / 2f, hd = depth / 2f;
            float hr = Mathf.Max(0.5f, hw - hd);
            var mb = new MeshBuilder(0.2f);
            Vector3 A = new Vector3(-hw, 0, -hd), B = new Vector3(hw, 0, -hd), C = new Vector3(hw, 0, hd), D = new Vector3(-hw, 0, hd);
            Vector3 R0 = new Vector3(-hr, h, 0), R1 = new Vector3(hr, h, 0);
            mb.Quad(A, R0, R1, B, new Vector3(0, hd, -h));
            mb.Quad(C, R1, R0, D, new Vector3(0, hd, h));
            mb.Tri(D, R0, A, new Vector3(-h, hw, 0));
            mb.Tri(B, R1, C, new Vector3(h, hw, 0));
            mb.Quad(A, B, C, D, Vector3.down);
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

        static void BuildHouse(Mats M)
        {
            const float W = 14f, D = 7f, WallH = 3.6f, PlinthH = 0.6f;
            Vector3 H = new Vector3(0f, Ground, 16f);
            float frontZ = H.z - D / 2f;

            var root = new GameObject("House_Main");
            root.transform.position = H;

            Prim(PrimitiveType.Cube, "Plinth", root.transform, H + new Vector3(0, PlinthH / 2f - 0.15f, 0), new Vector3(W + 1.6f, PlinthH + 0.3f, D + 1.6f), M.plinth);
            float wallBase = H.y + PlinthH;
            Prim(PrimitiveType.Cube, "Walls", root.transform, new Vector3(H.x, wallBase + WallH / 2f, H.z), new Vector3(W, WallH, D), M.brick);
            Prim(PrimitiveType.Cube, "BaseBand", root.transform, new Vector3(H.x, wallBase + 0.35f, frontZ - 0.05f), new Vector3(W + 0.1f, 0.7f, 0.1f), M.plinth);

            var octFrame = NGonPrism(8, 1.35f, 0.14f, 22.5f, "OctFrame");
            var octGlass = NGonPrism(8, 1.12f, 0.12f, 22.5f, "OctGlass");
            float bay = W / 3f;
            for (int i = 0; i < 3; i++)
            {
                float x = H.x - W / 2f + bay * (i + 0.5f);
                float y = wallBase + 2.1f;
                MeshObj("OctWindowFrame", root.transform, octFrame, M.redWood, new Vector3(x, y, frontZ - 0.07f), Quaternion.identity);
                MeshObj("OctWindow", root.transform, octGlass, i == 0 ? M.windowLattice : M.windowDark, new Vector3(x, y, frontZ - 0.12f), Quaternion.identity);
                if (i > 0) Prim(PrimitiveType.Cube, "DoorSill", root.transform, new Vector3(x, wallBase + 0.5f, frontZ - 0.06f), new Vector3(1.6f, 0.5f, 0.08f), M.windowDark);
            }
            for (int i = 0; i <= 3; i++)
            {
                float x = Mathf.Clamp(H.x - W / 2f + bay * i, H.x - W / 2f + 0.3f, H.x + W / 2f - 0.3f);
                Prim(PrimitiveType.Cylinder, "RedColumn", root.transform, new Vector3(x, wallBase + WallH / 2f, frontZ - 0.2f), new Vector3(0.44f, WallH / 2f, 0.44f), M.redWood);
                if (i > 0 && i < 3) Prim(PrimitiveType.Cube, "RedPanel", root.transform, new Vector3(x, wallBase + 2.1f, frontZ - 0.05f), new Vector3(0.7f, 2.6f, 0.08f), M.eave);
            }

            float roofBase = wallBase + WallH;
            float overhang = 1.4f;
            Prim(PrimitiveType.Cube, "EaveBoard", root.transform, new Vector3(H.x, roofBase + 0.12f, H.z), new Vector3(W + overhang * 2f, 0.24f, D + overhang * 2f), M.eave);
            var roofMesh = HipRoof(W + overhang * 2f + 0.3f, D + overhang * 2f + 0.3f, 2.6f, "HipRoof");
            MeshObj("Roof", root.transform, roofMesh, M.roof, new Vector3(H.x, roofBase + 0.24f, H.z), Quaternion.identity);
            float ridgeLen = Mathf.Max(1f, (W + overhang * 2f) / 2f - (D + overhang * 2f) / 2f) * 2f;
            Prim(PrimitiveType.Cube, "RidgeBeam", root.transform, new Vector3(H.x, roofBase + 0.24f + 2.62f, H.z), new Vector3(ridgeLen + 0.6f, 0.3f, 0.5f), M.windowDark);

            var fence = new GameObject("Fence");
            fence.transform.SetParent(root.transform);
            float fh = 1.0f, ft = 0.35f, fy = Ground + fh / 2f - 0.05f;
            foreach (int sgn in new[] { -1, 1 })
            {
                float xCorner = H.x + sgn * (W / 2f + 0.8f);
                float xOut = H.x + sgn * (W / 2f + 3.2f);
                Prim(PrimitiveType.Cube, "FenceStub", fence.transform, new Vector3((xCorner + xOut) / 2f, fy, frontZ + 1.2f), new Vector3(Mathf.Abs(xOut - xCorner), fh, ft), M.fence);
                Prim(PrimitiveType.Cube, "FenceSide", fence.transform, new Vector3(xOut, fy, frontZ - 1.8f), new Vector3(ft, fh, 6f), M.fence);
                Prim(PrimitiveType.Cube, "FencePost", fence.transform, new Vector3(xOut, Ground + 0.65f, frontZ - 4.8f), new Vector3(0.5f, 1.4f, 0.5f), M.fence);
                Prim(PrimitiveType.Cube, "FencePost", fence.transform, new Vector3(xOut, Ground + 0.65f, frontZ + 1.2f), new Vector3(0.5f, 1.4f, 0.5f), M.fence);
            }

            foreach (float dx in new[] { H.x, H.x + bay })
            {
                Prim(PrimitiveType.Cube, "DoorStep", root.transform, new Vector3(dx, Ground + 0.2f, frontZ - 1.0f), new Vector3(2.4f, 0.4f, 1.4f), M.plinth);
                Prim(PrimitiveType.Cube, "DoorStep2", root.transform, new Vector3(dx, Ground + 0.5f, frontZ - 0.5f), new Vector3(2.0f, 0.3f, 0.7f), M.plinth);
            }

            var yardTrees = new GameObject("YardPines");
            yardTrees.transform.SetParent(root.transform);
            SimplePine(yardTrees.transform, new Vector2(H.x - 9.5f, frontZ - 7f), M, 4f);
            SimplePine(yardTrees.transform, new Vector2(H.x + 8f, frontZ - 8.5f), M, 4.5f);
            Prim(PrimitiveType.Sphere, "InscribedStone", root.transform, new Vector3(H.x + 9f, Ground + 0.4f, frontZ - 4f), new Vector3(2.2f, 1.3f, 1.5f), M.inscribedStone, Quaternion.Euler(0, 20, 0));
        }


        static void BuildWestVillage(Mats M)
        {
            var root = new GameObject("West_Village");
            BuildThatchedHouse(M, root.transform, HouseAPos, "House_A", M.thatchYellow, false, false, true);
            Mortar(M, root.transform, new Vector2(HouseAPos.x - 6.5f, HouseAPos.y - 4.5f));
            StoneSign(M, root.transform, new Vector2(HouseAPos.x + 7.5f, HouseAPos.y - 4.5f));
            BuildThatchedHouse(M, root.transform, HouseBPos, "House_B", M.thatchOrange, true, true);

            Well(M, root.transform, new Vector2(HouseAPos.x + 8f, HouseAPos.y - 6f));
            JarStand(M, root.transform, new Vector2(HouseAPos.x - 8f, HouseAPos.y - 2f), 6);
            WoodFence(M, root.transform, new Vector2(HouseAPos.x - 12f, HouseAPos.y + 2f), new Vector2(HouseAPos.x - 12f, HouseAPos.y - 10f));
            WoodFence(M, root.transform, new Vector2(HouseAPos.x - 12f, HouseAPos.y - 10f), new Vector2(HouseAPos.x - 4f, HouseAPos.y - 12f));
            WoodFence(M, root.transform, new Vector2(HouseAPos.x + 4f, HouseAPos.y - 12f), new Vector2(HouseAPos.x + 12f, HouseAPos.y - 10f));

            JarStand(M, root.transform, new Vector2(HouseBPos.x + 8f, HouseBPos.y - 3f), 5);
            Well(M, root.transform, new Vector2(HouseBPos.x - 9f, HouseBPos.y - 7f));
            WoodFence(M, root.transform, new Vector2(HouseBPos.x + 12f, HouseBPos.y + 2f), new Vector2(HouseBPos.x + 12f, HouseBPos.y - 12f));
            WoodFence(M, root.transform, new Vector2(HouseBPos.x + 12f, HouseBPos.y - 12f), new Vector2(HouseBPos.x + 3f, HouseBPos.y - 14f));
            WoodFence(M, root.transform, new Vector2(HouseBPos.x - 5f, HouseBPos.y - 14f), new Vector2(HouseBPos.x - 13f, HouseBPos.y - 11f));
            WoodBench(M, root.transform, new Vector2(HouseBPos.x + 3f, HouseBPos.y - 8.5f));
            for (int i = 0; i < 3; i++)
            {
                float x = HouseBPos.x - 6f + i * 2.2f, z = HouseBPos.y - 10f;
                Prim(PrimitiveType.Sphere, "HayStack", root.transform, new Vector3(x, TerrainHeightAt(x, z) + 0.6f, z), new Vector3(1.8f, 1.5f, 1.8f), M.thatchYellow, null, false);
            }
            var pines = new GameObject("VillagePines"); pines.transform.SetParent(root.transform);
            SimplePine(pines.transform, new Vector2(HouseAPos.x + 13f, HouseAPos.y - 2f), M, 4.5f);
            SimplePine(pines.transform, new Vector2(HouseBPos.x - 14f, HouseBPos.y + 2f), M, 4f);
            SimplePine(pines.transform, new Vector2(-36f, 16f), M, 4f);
        }

        static void BuildThatchedHouse(Mats M, Transform parent, Vector2 pos2, string name, Material thatch, bool gourds, bool doubleDoor = false, bool openDoorway = false)
        {
            const float W = 9.5f, D = 6f, WallH = 2.6f, PlinthH = 0.35f;
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
                        Prim(PrimitiveType.Cube, "DarkDoorway", root.transform, new Vector3(x, wallBase + 1.15f, frontZ - 0.04f), new Vector3(bay - 0.2f, 2.3f, 0.06f), M.windowDark, null, false);
                        Prim(PrimitiveType.Cube, "DoorwayLintel", root.transform, new Vector3(x, wallBase + 2.4f, frontZ - 0.14f), new Vector3(bay + 0.4f, 0.25f, 0.24f), M.darkWood, null, false);
                        foreach (int sgn in new[] { -1, 1 })
                            Prim(PrimitiveType.Capsule, "ThatchPillar", root.transform, new Vector3(x + sgn * (bay / 2f + 0.05f), wallBase + WallH / 2f, frontZ - 0.25f), new Vector3(0.7f, WallH / 2f + 0.1f, 0.7f), thatch, null, false);
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
                    Prim(PrimitiveType.Cube, "PaperWindowSmall", root.transform, new Vector3(x, wallBase + 1.85f, frontZ - 0.06f), new Vector3(bay - 1.4f, 0.8f, 0.06f), M.paperWindow);
                    Prim(PrimitiveType.Cube, "Lattice", root.transform, new Vector3(x, wallBase + 1.85f, frontZ - 0.1f), new Vector3(0.05f, 0.8f, 0.04f), M.darkWood, null, false);
                    Prim(PrimitiveType.Cube, "Lattice", root.transform, new Vector3(x, wallBase + 1.85f, frontZ - 0.1f), new Vector3(bay - 1.4f, 0.05f, 0.04f), M.darkWood, null, false);
                    continue;
                }
                if (i == 1)
                {
                    Prim(PrimitiveType.Cube, "PaperWindow", root.transform, new Vector3(x, wallBase + 1.55f, frontZ - 0.06f), new Vector3(bay - 0.9f, 1.3f, 0.06f), M.paperWindow);
                    for (int k = -1; k <= 1; k++)
                        Prim(PrimitiveType.Cube, "Lattice", root.transform, new Vector3(x + k * (bay - 0.9f) / 3.2f, wallBase + 1.55f, frontZ - 0.1f), new Vector3(0.05f, 1.3f, 0.04f), M.darkWood, null, false);
                    Prim(PrimitiveType.Cube, "Lattice", root.transform, new Vector3(x, wallBase + 1.55f, frontZ - 0.1f), new Vector3(bay - 0.9f, 0.05f, 0.04f), M.darkWood, null, false);
                }
                else
                {
                    Prim(PrimitiveType.Cube, "Door", root.transform, new Vector3(x, wallBase + 1.1f, frontZ - 0.06f), new Vector3(bay - 1.1f, 2.0f, 0.06f), M.paperWindow);
                    Prim(PrimitiveType.Cube, "DoorFrame", root.transform, new Vector3(x, wallBase + 1.1f, frontZ - 0.09f), new Vector3(0.06f, 2.0f, 0.05f), M.darkWood, null, false);
                }
            }
            if (!openDoorway) Prim(PrimitiveType.Cube, "Maru", root.transform, new Vector3(H.x, wallBase - 0.05f, frontZ - 0.7f), new Vector3(W - 0.4f, 0.16f, 1.3f), M.plank);
            float roofBase = wallBase + WallH;
            float overhang = 1.3f;
            Prim(PrimitiveType.Cube, "EaveBoard", root.transform, new Vector3(H.x, roofBase + 0.08f, H.z), new Vector3(W + overhang * 2f, 0.16f, D + overhang * 2f), M.darkWood);
            float rw = W + overhang * 2f + 0.4f, rd = D + overhang * 2f + 0.4f, rh = 2.3f;
            var roofMesh = HipRoof(rw, rd, rh, name.Replace(" ", "_") + "_Thatch");
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
        }


        static void BuildHouseC(Mats M)
        {
            var root = new GameObject("House_C_Area");
            BuildThatchedHouse(M, root.transform, HouseCPos, "House_C", M.thatchYellow, true, true);

            float x0 = HouseCYard.xMin, x1 = HouseCYard.xMax, z0 = HouseCYard.yMin, z1 = HouseCYard.yMax;
            StoneWall(M, root.transform, new Vector2(x0, z0), new Vector2(x0, z1));
            StoneWall(M, root.transform, new Vector2(x1, z0), new Vector2(x1, z1));
            StoneWall(M, root.transform, new Vector2(x0, z1), new Vector2(x1, z1));
            StoneWall(M, root.transform, new Vector2(x0, z0), new Vector2(HouseCPos.x - 2f, z0));
            StoneWall(M, root.transform, new Vector2(HouseCPos.x + 2f, z0), new Vector2(x1, z0));

            float fz = HouseCPos.y - 3f;
            WoodBench(M, root.transform, new Vector2(HouseCPos.x - 8.5f, fz - 1.5f));
            Mortar(M, root.transform, new Vector2(HouseCPos.x - 6.2f, fz + 0.5f));
            JarStand(M, root.transform, new Vector2(HouseCPos.x + 8.5f, fz + 1.0f), 3);
            Well(M, root.transform, new Vector2(HouseCPos.x + 8.0f, fz - 4.5f));
            {
                float wx = HouseCPos.x + 10.5f, wz = fz - 5.5f, wy = TerrainHeightAt(wx, wz);
                Prim(PrimitiveType.Cylinder, "WaterJar", root.transform, new Vector3(wx, wy + 0.45f, wz), new Vector3(0.9f, 0.45f, 0.9f), M.pumpkin, null, false);
                Prim(PrimitiveType.Cylinder, "Water", root.transform, new Vector3(wx, wy + 0.9f, wz), new Vector3(0.78f, 0.01f, 0.78f), M.water, null, false);
            }
            {
                float sx = HouseCPos.x + 6f, sz = z0 + 3f;
                Prim(PrimitiveType.Sphere, "InscribedStone", root.transform, new Vector3(sx, TerrainHeightAt(sx, sz) + 0.4f, sz), new Vector3(2.2f, 1.2f, 1.4f), M.inscribedStone, Quaternion.Euler(0, 15, 0));
            }
            for (int i = 0; i < 14; i++)
            {
                float fx = R(x0 + 1.5f, x1 - 1.5f), fzz = R(z0 + 1.5f, fz - 1f);
                if (Mathf.Abs(fx - HouseCPos.x) < 2.5f) continue;
                Prim(PrimitiveType.Sphere, "Flower", root.transform, new Vector3(fx, TerrainHeightAt(fx, fzz) + 0.18f, fzz), new Vector3(0.22f, 0.22f, 0.22f), i % 2 == 0 ? M.sunflowerPetal : M.lotus, null, false);
            }
            var pines = new GameObject("HouseC_Pines"); pines.transform.SetParent(root.transform);
            SimplePine(pines.transform, new Vector2(x0 - 4f, fz - 2f), M, 4.5f);
            SimplePine(pines.transform, new Vector2(x1 + 4f, z1 + 2f), M, 4f);
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

        static void JarStand(Mats M, Transform parent, Vector2 p, int count)
        {
            float y = TerrainHeightAt(p.x, p.y);
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
            while (placed.Count < 26 && guard++ < 12000)
            {
                float x = GraveCenter.x + R(-GraveRadius, GraveRadius), z = GraveCenter.y + R(-GraveRadius + 4f, GraveRadius - 6f);
                if (Vector2.Distance(new Vector2(x, z), GraveCenter) > GraveRadius) continue;
                if (WaterSdf(x, z) < 4f) continue;
                if (PathDistance(x, z) < 5f) continue;
                if (Vector2.Distance(new Vector2(x, z), GraveCenter) < GraveClearing + 3f) continue;
                if (Vector2.Distance(new Vector2(x, z), HouseCPos) < 18f) continue;
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
            ScatterMarkers(M, markers.transform, 12);
            var big = new GameObject("BigTrees"); big.transform.SetParent(root.transform);
            foreach (var o in new[] { new Vector2(-8f, -86f), new Vector2(8f, -88f), new Vector2(-7f, -100f), new Vector2(8f, -102f) })
                SimpleTree(big.transform, o, M, 10f, 7f);
            foreach (var o in new[] { new Vector2(6f, -66f), new Vector2(-30f, -96f), new Vector2(30f, -86f), new Vector2(2f, -126f), new Vector2(-16f, -76f), new Vector2(18f, -110f) })
            {
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
                if (WaterSdf(x, z) < 3f || PathDistance(x, z) < 3.5f || Vector2.Distance(p, HouseCPos) < 18f) continue;
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
            while (n < 14 && guard++ < 4000)
            {
                float a = R(0f, Mathf.PI * 2f), r = R(10f, PlateauRadius + 10f);
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
            for (int i = 0; i < 26; i++)
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

        static void SimpleTree(Transform parent, Vector2 p, Mats M, float h, float canopy, Material leaf = null)
        {
            float y = TerrainHeightAt(p.x, p.y);
            Prim(PrimitiveType.Cylinder, "Trunk", parent, new Vector3(p.x, y + h / 2f, p.y), new Vector3(0.09f * h, h / 2f, 0.09f * h), M.trunk, Quaternion.Euler(R(-6, 6), 0, R(-6, 6)), false);
            for (int k = 0; k < 4; k++)
                Prim(PrimitiveType.Sphere, "Canopy", parent, new Vector3(p.x + R(-canopy * 0.4f, canopy * 0.4f), y + h + R(-canopy * 0.25f, canopy * 0.3f), p.y + R(-canopy * 0.4f, canopy * 0.4f)), Vector3.one * R(canopy * 0.7f, canopy), leaf ?? M.leaves, null, false);
        }

        static void SimplePine(Transform parent, Vector2 p, Mats M, float h)
        {
            float y = TerrainHeightAt(p.x, p.y);
            Prim(PrimitiveType.Cylinder, "Trunk", parent, new Vector3(p.x, y + h / 2f, p.y), new Vector3(0.4f, h / 2f, 0.4f), M.trunk, Quaternion.Euler(R(-14, 14), 0, R(-14, 14)), false);
            for (int k = 0; k < 3; k++)
                Prim(PrimitiveType.Sphere, "Canopy", parent, new Vector3(p.x + R(-1.5f, 1.5f), y + h + R(-0.6f, 0.8f), p.y + R(-1.5f, 1.5f)), new Vector3(R(2.4f, 3.4f), R(1.0f, 1.5f), R(2.4f, 3.4f)), M.leaves, null, false);
        }

        static void BuildVegetation(Mats M)
        {
            var lotusRoot = new GameObject("Lotus");
            int pads = 0, flowers = 0, guard = 0;
            while (pads < 110 && guard++ < 40000)
            {
                float x, z;
                if (pads < 75) { x = R(-42, 40); z = R(-36, 34); } else { x = R(-30, 30); z = R(-194, -148); }
                if (WaterSdf(x, z) > -1.6f) continue;
                if (NearStoneBridge(x, z, 0.5f, 2.5f) || NearWoodBridge(x, z, 1.5f)) continue;
                float s = R(0.7f, 1.3f);
                Prim(PrimitiveType.Cylinder, "LilyPad", lotusRoot.transform, new Vector3(x, WaterY + 0.02f, z), new Vector3(s, 0.02f, s), M.lilyPad, Quaternion.Euler(0, R(0, 360), 0), false);
                pads++;
                if (flowers < 36 && rng.NextDouble() < 0.4)
                {
                    Prim(PrimitiveType.Sphere, "LotusFlower", lotusRoot.transform, new Vector3(x + R(-0.3f, 0.3f), WaterY + 0.25f, z + R(-0.3f, 0.3f)), new Vector3(0.45f, 0.35f, 0.45f), M.lotus, null, false);
                    flowers++;
                }
            }

            var bambooRoot = new GameObject("Bamboo");
            Vector2[] clusters = { new Vector2(-14f, -5f), new Vector2(15f, -7f), new Vector2(40f, 16f), new Vector2(-44f, 32f), new Vector2(-26f, -148f), new Vector2(24f, -194f) };
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
            while (bn < 22 && guard++ < 6000)
            {
                float x = R(-40f, 40f), z = R(-210f, -140f);
                float sdf = WaterSdf(x, z);
                if (sdf < 2f || sdf > 9f) continue;
                if (PathDistance(x, z) < 4f) continue;
                if (NearWoodBridge(x, z, 4f)) continue;
                bool close = false; foreach (var q in bplaced) if (Vector2.Distance(q, new Vector2(x, z)) < 6f) { close = true; break; }
                if (close) continue;
                bplaced.Add(new Vector2(x, z));
                SimpleTree(blossomRoot.transform, new Vector2(x, z), M, R(4f, 5.5f), R(4f, 5.5f), M.blossom);
                bn++;
            }

            var treeRoot = new GameObject("Trees");
            var scattered = new List<Vector2>();
            int tries = 0;
            while (scattered.Count < 260 && tries++ < 80000)
            {
                float x = R(TerrainMinX + 6f, TerrainMinX + TerrainSize - 6f), z = R(TerrainMinZ + 6f, TerrainMinZ + TerrainSize - 6f);
                var p = new Vector2(x, z);
                if (WaterSdf(x, z) < 2.5f) continue;
                if (PathDistance(x, z) < 5f) continue;
                if (IslandSdf(x, z) < 3f) continue;
                if (Vector2.Distance(p, PlateauCenter) < PlateauRadius + 6f) continue;
                if (Vector2.Distance(p, GraveCenter) < 12f) continue;
                if (Vector2.Distance(p, HouseAPos) < 16f || Vector2.Distance(p, HouseBPos) < 16f) continue;
                if (Vector2.Distance(p, GatePos) < 14f) continue;
                if (HouseCYard.Contains(p) || Vector2.Distance(p, HouseCPos) < 15f) continue;
                bool inGrave = Vector2.Distance(p, GraveCenter) < GraveRadius;
                if (inGrave && Vector2.Distance(p, GraveCenter) < GraveClearing + 2f) continue;
                bool inLake = Vector2.Distance(p, LakeCenter) < 40f;
                if (inLake && WaterSdf(x, z) < 9f) continue;
                if (!inGrave && ZoneDistance(x, z) < -10f && Vector2.Distance(p, Vector2.zero) < 44f) continue;
                float spacing = inGrave ? 4.5f : 8f;
                bool close = false; foreach (var q in scattered) if (Vector2.Distance(q, p) < spacing) { close = true; break; }
                if (close) continue;
                scattered.Add(p);
                if (inGrave) SimpleTree(treeRoot.transform, p, M, R(3.5f, 6f), R(2.6f, 4f)); else SimpleTree(treeRoot.transform, p, M, R(5f, 8f), R(3.5f, 5f));
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
                new Vector2(14f, 3f),
                new Vector2(-12f, 4f),
                new Vector2(2.8f, WoodBridgeZ0 + 3f),
                new Vector2(2.8f, WoodBridgeZ1 - 3f),
                new Vector2(PlateauCenter.x - 12f, PlateauCenter.y + 10f),
            };
            foreach (var s in spots)
                Lantern(root.transform, new Vector3(s.x, TerrainHeightAt(s.x, s.y), s.y), M, s.y < -90f && s.y > -160f ? 8f : 12f, 16f);
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
            follow.zoneCenter = new Vector3(25f, Ground, -150f);
            follow.zoneRadius = 95f;
            follow.zoneFade = 22f;
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

            var sky = new Material(Shader.Find("Skybox/Procedural"));
            sky.SetFloat("_SunSize", 0f);
            sky.SetFloat("_SunSizeConvergence", 10f);
            sky.SetFloat("_AtmosphereThickness", 0.55f);
            sky.SetColor("_SkyTint", new Color(0.18f, 0.22f, 0.45f));
            sky.SetColor("_GroundColor", new Color(0.03f, 0.04f, 0.08f));
            sky.SetFloat("_Exposure", 0.5f);
            AssetDatabase.CreateAsset(sky, Gen + "/M_Sky_NightProcedural.mat");
            RenderSettings.skybox = sky;
            RenderSettings.sun = light;
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.09f, 0.11f, 0.21f);
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = new Color(0.03f, 0.04f, 0.10f);
            RenderSettings.fogStartDistance = 60f;
            RenderSettings.fogEndDistance = 260f;
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
            go.transform.position = new Vector3(-12f, 40f, -58f);
            go.transform.LookAt(new Vector3(0f, Ground, 2f));

            var fly = go.AddComponent<MoonlitVillage.CameraFlythrough>();
            float pt = Ground + PlateauRise;
            Vector3 hill = new Vector3(PlateauCenter.x, pt, PlateauCenter.y);
            Vector3 hillCam = hill + new Vector3(-8f, 2.2f, 6f);
            void W(Vector3 p, Vector3 l, float travel, float hold = 0.8f, float fov = 55f) => fly.waypoints.Add(new MoonlitVillage.CameraFlythrough.Waypoint { position = p, lookAt = l, travelSeconds = travel, holdSeconds = hold, fieldOfView = fov });
            W(new Vector3(-72f, 15f, -22f), new Vector3(HouseAPos.x, Ground + 3f, HouseAPos.y), 0f, 2.0f, 50f);
            W(new Vector3(-58f, 6.5f, -14f), new Vector3(HouseBPos.x, Ground + 2.5f, HouseBPos.y), 9f, 0.8f, 52f);
            W(new Vector3(-36f, 6.0f, -4f), new Vector3(BridgeWest.x, Ground + 1.5f, BridgeWest.z), 9f, 0.6f, 52f);
            W(new Vector3(-8f, 9f, -16f), new Vector3(0f, Ground + 3f, 12f), 9f, 0.6f, 55f);
            W(new Vector3(4f, 6f, -4f), new Vector3(0f, Ground + 3f, 14f), 7f, 1.0f, 50f);
            W(new Vector3(16f, 7f, -14f), new Vector3(GatePos.x, Ground + 3f, GatePos.y), 8f, 0.8f, 55f);
            W(new Vector3(34f, 12f, -34f), new Vector3(0f, Ground, -80f), 9f, 0.4f, 58f);
            W(new Vector3(20f, 9f, -72f), new Vector3(0f, Ground + 1f, -92f), 9f, 0.8f, 55f);
            W(new Vector3(2f, 7f, -136f), new Vector3(0f, Ground + 1f, -175f), 9f, 0.6f, 55f);
            W(new Vector3(0f, Ground + 2.2f, -168f), new Vector3(0f, Ground + 1.6f, -200f), 9f, 0.6f, 58f);
            W(new Vector3(46f, 20f, -176f), hill + Vector3.up * 2f, 9f, 0.4f, 55f);
            W(hillCam, hillCam + MoonDir * 100f + Vector3.down * 20f, 9f, 8.0f, 55f);
        }
    }
}
