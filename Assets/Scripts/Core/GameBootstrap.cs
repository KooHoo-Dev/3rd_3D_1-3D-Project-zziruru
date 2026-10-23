using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace SesiVillage.Core
{
    /// <summary>
    /// 게임 시작 순서를 관리한다. Core 씬에 하나만 둔다.
    /// Village 불러오기 → 활성 씬 지정 → 플레이어를 시작 위치로 옮기기 → 플레이어 켜기
    /// </summary>
    public class GameBootstrap : MonoBehaviour
    {
        [SerializeField] private string villageSceneName = "Village";
        [SerializeField] private GameObject player;   // Core 씬의 Player (꺼진 상태로 둔다)
        [SerializeField] private ExploreCamera exploreCamera;

        private IEnumerator Start()
        {
            if (SceneManager.GetSceneByName(villageSceneName).isLoaded == false)
            {
                // Additive: 지금 열린 Core 씬을 닫지 않고 Village를 "더해서" 연다.
                // Async: 여러 프레임에 나눠서 불러오므로 그동안 게임이 멈추지 않는다.
                AsyncOperation loadingTask = SceneManager.LoadSceneAsync(villageSceneName, LoadSceneMode.Additive);
                
                if (loadingTask == null)
                {
                    Debug.LogError($"[GameBootstrap] '{villageSceneName}' 씬을 불러올 수 없습니다. Build Profiles의 씬 목록을 확인하세요.");
                    yield break;
                }
                
                yield return loadingTask;  // 다 불러올 때까지 여기서 기다린다
            }
            
            // Village를 활성 씬으로 지정한다. (하늘·안개·환경광이 Village 기준으로 적용됨)
            Scene village = SceneManager.GetSceneByName(villageSceneName);
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
    }
}
