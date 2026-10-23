using System;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace MoonlitVillage.EditorTools
{
    // 안개 비교 도구 (2026-10-23)
    // Village 씬을 열어 안개 없음 / 지금 안개 / 진한 안개로 같은 시점을 찍고 Reference/Fog Compare/index.html에 나란히 놓는다.
    // 씬은 저장하지 않는다 (마지막에 다시 열어 바꾼 설정을 버림).
    public static class FogComparePreview
    {
        const string VillagePath = "Assets/Scenes/Village.unity";
        const string OutDir = "Reference/Fog Compare";
        // 땅 높이에서 멀리 보는 시점 위주 (플레이어가 실제로 보는 화면에 가깝게) + 전경
        static readonly string[] ViewNames = { "enter_from_east", "moon_bridge_walk", "hill_moon", "island_chief", "west_bridge", "north_houses", "village_overview", "whole_map" };

        [MenuItem("Moonlit Village/Compare/Fog")]
        public static void Run()
        {
            if (!File.Exists(VillagePath)) { Debug.LogError("[FogCompare] Village 씬이 없음: " + VillagePath); return; }
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            EditorSceneManager.OpenScene(VillagePath, OpenSceneMode.Single);

            bool fog = RenderSettings.fog;
            float start = RenderSettings.fogStartDistance, end = RenderSettings.fogEndDistance;
            var variants = new (string key, string title, Action apply)[]
            {
                ("off",   "안개 없음",                                 () => RenderSettings.fog = false),
                ("now",   $"지금 안개 ({start:0}~{end:0}m)",           () => { }),
                ("thick", $"진한 안개 ({start * 0.6f:0}~{end * 0.65f:0}m)", () => { RenderSettings.fogStartDistance = start * 0.6f; RenderSettings.fogEndDistance = end * 0.65f; }),
            };

            try
            {
                for (int i = 0; i < variants.Length; i++)
                {
                    EditorUtility.DisplayProgressBar("안개 비교", variants[i].title, (float)i / variants.Length);
                    RenderSettings.fog = fog; RenderSettings.fogStartDistance = start; RenderSettings.fogEndDistance = end;
                    variants[i].apply();
                    CaptureViews(variants[i].key);
                }
                WriteIndex(variants);
            }
            finally
            {
                EditorUtility.ClearProgressBar();
                EditorSceneManager.OpenScene(VillagePath, OpenSceneMode.Single);   // 바꾼 안개 설정을 저장하지 않고 버림
            }
            Debug.Log("[FogCompare] 완료: " + Path.GetFullPath(OutDir + "/index.html"));
            EditorUtility.RevealInFinder(OutDir + "/index.html");
        }

        static void CaptureViews(string prefix)
        {
            Camera cam = null;
            foreach (var c in UnityEngine.Object.FindObjectsByType<Camera>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (c.CompareTag("MainCamera")) { cam = c; break; }
            if (cam == null) { Debug.LogError("[FogCompare] 메인 카메라 없음"); return; }
            cam.gameObject.SetActive(true);
            foreach (var v in VillageBuilder.Views())
            {
                if (Array.IndexOf(ViewNames, v.name) < 0) continue;
                cam.transform.position = v.pos; cam.transform.LookAt(v.look); cam.fieldOfView = v.fov;
                VillageBuilder.CaptureTo(cam, OutDir + "/" + prefix + "_" + v.name + ".png");
            }
        }

        static void WriteIndex((string key, string title, Action apply)[] variants)
        {
            var sb = new StringBuilder();
            sb.Append("<!doctype html><html lang=\"ko\"><head><meta charset=\"utf-8\"><title>안개 비교</title><style>");
            sb.Append("body{font-family:sans-serif;margin:16px;background:#1b1b1f;color:#eee}table{border-collapse:collapse}td,th{padding:4px;vertical-align:top}");
            sb.Append("th{position:sticky;top:0;background:#1b1b1f;text-align:left;font-weight:500}img{width:480px;display:block;cursor:zoom-in}td.v{width:120px;font-size:13px;color:#aaa}");
            sb.Append("</style></head><body><h2>안개 비교 (밤)</h2>");
            sb.Append("<p>볼 것: 먼 산·숲이 하늘과 자연스럽게 이어지는가 / 지형 끝·빈 공간이 보이는가 / 마을 안(가까운 곳)이 너무 뿌옇지 않은가. 이미지를 클릭하면 원본 크기.</p><table><tr><th></th>");
            foreach (var v in variants) sb.Append("<th>" + v.title + "</th>");
            sb.Append("</tr>");
            foreach (var view in ViewNames)
            {
                sb.Append("<tr><td class=\"v\">" + view + "</td>");
                foreach (var v in variants)
                {
                    string file = v.key + "_" + view + ".png";
                    sb.Append("<td><a href=\"" + file + "\" target=\"_blank\"><img src=\"" + file + "\" loading=\"lazy\"></a></td>");
                }
                sb.Append("</tr>");
            }
            sb.Append("</table></body></html>");
            Directory.CreateDirectory(OutDir);
            File.WriteAllText(OutDir + "/index.html", sb.ToString(), new UTF8Encoding(false));
        }
    }
}
