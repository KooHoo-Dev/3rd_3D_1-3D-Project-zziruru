using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace SesiVillage.Core
{
    /// <summary>
    /// 게임 시작 순서를 관리한다. Core 씬에 하나만 둔다.
    /// Village(맵) 불러오기 → Village_GamePlay(NPC·잡초) 불러오기 → 활성 씬 지정 → 플레이어를 시작 위치로 옮기기 → 플레이어 켜기
    /// </summary>
    public class GameBootstrap : MonoBehaviour
    {
        [SerializeField] private string villageSceneName = "Village";
        [Tooltip("Village와 함께 Additive로 불러올 씬들 (NPC·잡초 같은 손으로 놓는 오브젝트)")]
        [SerializeField] private string[] gameplaySceneNames = { "Village_GamePlay" };
        [SerializeField] private GameObject player;   // Core 씬의 Player (꺼진 상태로 둔다)
        [SerializeField] private ExploreCamera exploreCamera;

        private IEnumerator Start()
        {
            yield return LoadAdditive(villageSceneName);
            Scene village = SceneManager.GetSceneByName(villageSceneName);
            if (!village.isLoaded) yield break;   // 맵이 없으면 더 진행할 수 없음 (에러는 LoadAdditive가 남김)

            // Village 다음에 불러온다: NPC(NpcPlacement)가 Start에서 Village의 NpcSpot을 찾기 때문
            foreach (var sceneName in gameplaySceneNames)
                yield return LoadAdditive(sceneName);   // 실패해도 맵은 돌아가므로 계속

            // Village를 활성 씬으로 지정한다. (하늘·안개·환경광이 Village 기준으로 적용됨)
            SceneManager.SetActiveScene(village);

            if (player == null)
            {
                Debug.LogError("[GameBootstrap] Player가 연결되지 않았습니다. Inspector의 Player 칸을 확인하세요.");
                yield break;
            }

            PlayerSpawnPoint spawnPoint = FindFirstObjectByType<PlayerSpawnPoint>();
            if (spawnPoint == null)
            {
                Debug.LogWarning("[GameBootstrap] PlayerSpawnPoint를 찾지 못했습니다. 플레이어를 현재 위치에서 시작합니다.");
            }
            else
            {
                player.transform.SetPositionAndRotation(spawnPoint.transform.position, spawnPoint.transform.rotation);
            }

            player.SetActive(true);
            if (exploreCamera != null) exploreCamera.SnapBehind(player.transform);
            else Debug.LogWarning("Explore Camera 가 비어있습니다");
        }

        /// <summary>씬을 Additive로 불러온다. 이미 열려 있으면 건너뜀</summary>
        private IEnumerator LoadAdditive(string sceneName)
        {
            // 에디터에서 여러 씬을 열어 둔 채 Play하면 이미 열려 있음 → 또 불러오면 NPC가 두 명씩 생김
            if (SceneManager.GetSceneByName(sceneName).isLoaded) yield break;

            // Additive: 지금 열린 씬을 닫지 않고 "더해서" 연다.
            // Async: 여러 프레임에 나눠서 불러오므로 그동안 게임이 멈추지 않는다.
            AsyncOperation loadingTask = SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Additive);
            if (loadingTask == null)
            {
                Debug.LogError($"[GameBootstrap] '{sceneName}' 씬을 불러올 수 없습니다. Build Profiles의 씬 목록을 확인하세요.");
                yield break;
            }
            yield return loadingTask;   // 다 불러올 때까지 여기서 기다린다
        }
    }
}
