using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

namespace LegendaryPet
{
    /// <summary>
    /// 레전드 펫을 관리하는 싱글톤 매니저
    /// PetManager와 동일한 방식으로 작동
    /// </summary>
    public class LegendaryPetManager : MonoBehaviour
    {
        private static LegendaryPetManager instance;
        public static LegendaryPetManager Instance
        {
            get
            {
                if (instance == null)
                {
                    instance = FindObjectOfType<LegendaryPetManager>();
                    if (instance == null)
                    {
                        GameObject managerObject = new GameObject("LegendaryPetManager");
                        instance = managerObject.AddComponent<LegendaryPetManager>();
                        DontDestroyOnLoad(managerObject);
                    }
                }
                return instance;
            }
        }
        
        [Header("레전드 펫 프리팹")]
        public GameObject[] legendaryPetPrefabs;  // 레전드 펫 프리팹 배열 (인덱스로 관리)
        
        [Header("스폰 설정")]
        public float spawnRadius = 50f;  // 스폰 반경
        public int maxLegendaryPets = 3; // 동시에 존재할 수 있는 최대 레전드 펫 수
        
        [Header("최초 등장 효과")]
        public GameObject firstAppearanceEffectPrefab; // 최초 등장 효과 프리팹
        public float firstAppearanceDelay = 0.5f; // 최초 등장 펫들 사이의 딜레이
        
        [Header("선물 시스템")]
        public GameObject giftPrefab; // 선물 프리팹
        public GameObject celebrationEffectPrefab; // 축하 효과 파티클 프리팹
        public float giftSpawnDelay = 0.5f; // 선물 스폰 딜레이
        public List<GameObject> fireworkPrefabs = new List<GameObject>(); // 불꽃놀이 프리팹들
        
        [Header("NavMesh 대기 설정")]
        public float navMeshWaitTime = 3f; // NavMesh 베이크 대기 시간
        
        [Header("레전드 펫 관리")]
        [SerializeField] private List<LegendaryPetController> legendaryPets = new List<LegendaryPetController>();
        
        // 대기 중인 선물들과 해당 레전드 펫 정보를 저장하는 딕셔너리
        private Dictionary<GameObject, string> pendingGifts = new Dictionary<GameObject, string>();
        
        [Header("특수 효과 설정")]
        [SerializeField] private bool globalEffectsEnabled = true;
        [SerializeField] private float effectIntensityMultiplier = 1f;
        
        // 이벤트
        public delegate void LegendaryPetEvent(LegendaryPetController pet);
        public event LegendaryPetEvent OnLegendaryPetSpawned;
        public event LegendaryPetEvent OnLegendaryPetRemoved;
        
        // 프로퍼티
        public List<LegendaryPetController> LegendaryPets => new List<LegendaryPetController>(legendaryPets);
        public int CurrentLegendaryPetCount => legendaryPets.Count;
        public bool CanSpawnMore => legendaryPets.Count < maxLegendaryPets;
        
        private void Awake()
        {
            if (instance != null && instance != this)
            {
                Destroy(gameObject);
                return;
            }
            
            instance = this;
            DontDestroyOnLoad(gameObject);
            
            Debug.Log("[LegendaryPetManager] 매니저 초기화 완료");
        }
        
        private void Start()
        {
            // PetManager와 동일하게 환경 준비 완료 후 스폰
            StartCoroutine(WaitForEnvironmentAndSpawnLegendaryPets());
        }
        
        // 환경 준비 완료까지 기다리는 코루틴 (PetManager와 동일)
        private IEnumerator WaitForEnvironmentAndSpawnLegendaryPets()
        {
            // EnvironmentManager 찾기
            EnvironmentManager environmentManager = FindObjectOfType<EnvironmentManager>();
            
            if (environmentManager != null)
            {
                // EnvironmentManager가 초기화를 완료할 때까지 대기
                yield return new WaitUntil(() => environmentManager.IsInitializationComplete);
            }
            else
            {
                Debug.LogWarning("[LegendaryPetManager] EnvironmentManager를 찾을 수 없습니다. 기본 대기 시간 적용");
                // EnvironmentManager가 없으면 기본 대기 시간
                yield return new WaitForSeconds(3f);
            }
            
            // 추가 안전 대기
            yield return new WaitForSeconds(1f);
            
            // 이제 레전드 펫 스폰 시작
            if (LegendaryPetSelectionManager.Instance != null && 
                LegendaryPetSelectionManager.Instance.selectedLegendaryPetIds.Count > 0)
            {
                StartCoroutine(SpawnSelectedLegendsWithEffects());
            }
            else
            {
                Debug.LogWarning("[LegendaryPetManager] 선택된 레전드 펫이 없거나 LegendaryPetSelectionManager가 없습니다. 기본 동작으로 모든 레전드 펫을 스폰합니다.");
                SpawnAllLegendaryPets();
            }
        }
        
        // 선택된 레전드 펫을 효과와 함께 스폰하는 코루틴
        private IEnumerator SpawnSelectedLegendsWithEffects()
        {
            // 일반 레전드 펫과 최초 등장 레전드 펫을 분리
            List<string> normalLegends = new List<string>();
            List<string> firstAppearanceLegends = new List<string>();
            
            foreach (string legendId in LegendaryPetSelectionManager.Instance.selectedLegendaryPetIds)
            {
                if (LegendaryPetSelectionManager.Instance.IsLegendaryPetFirstAppearance(legendId))
                {
                    firstAppearanceLegends.Add(legendId);
                }
                else
                {
                    normalLegends.Add(legendId);
                }
            }
            
            // 먼저 일반 레전드 펫들을 스폰
            foreach (string legendId in normalLegends)
            {
                SpawnLegendaryPet(legendId, false);
            }
            
            // 최초 등장 레전드 펫들을 딜레이를 두고 효과와 함께 스폰
            // 최초 등장 레전드 펫들은 선물로 스폰
            foreach (string legendId in firstAppearanceLegends)
            {
                SpawnGiftForLegendaryPet(legendId);
                yield return new WaitForSeconds(giftSpawnDelay);
            }
        }
        
        // 모든 레전드 펫을 스폰하는 메서드 (PetManager의 SpawnAllPets와 동일)
        private void SpawnAllLegendaryPets()
        {
            int spawnCount = 0;
            for (int i = 0; i < legendaryPetPrefabs.Length && spawnCount < maxLegendaryPets; i++)
            {
                if (legendaryPetPrefabs[i] != null)
                {
                    Vector3 randomPosition = GetRandomSpawnPosition();
                    
                    // NavMesh 위치 확인
                    NavMeshHit hit;
                    if (NavMesh.SamplePosition(randomPosition, out hit, 50f, NavMesh.AllAreas))
                    {
                        randomPosition = hit.position;
                    }
                    
                    GameObject legendObject = Instantiate(legendaryPetPrefabs[i], randomPosition, Quaternion.identity);
                    LegendaryPetController controller = legendObject.GetComponent<LegendaryPetController>();
                    
                    if (controller != null)
                    {
                        spawnCount++;
                        Debug.Log($"[LegendaryPetManager] 레전드 펫 스폰 ({spawnCount}/{maxLegendaryPets}): {controller.PetType}");
                    }
                    else
                    {
                        Debug.LogError($"[LegendaryPetManager] 프리팹 {i}에 LegendaryPetController가 없습니다");
                        Destroy(legendObject);
                    }
                }
            }
            
            if (spawnCount == 0)
            {
                Debug.LogWarning("[LegendaryPetManager] 스폰된 레전드 펫이 없습니다. 프리팹 설정을 확인하세요.");
            }
            else
            {
                Debug.Log($"[LegendaryPetManager] 총 {spawnCount}개의 레전드 펫 스폰 완료");
            }
        }
        
        public void RegisterLegendaryPet(LegendaryPetController pet)
        {
            if (pet == null || legendaryPets.Contains(pet)) return;
            
            legendaryPets.Add(pet);
            Debug.Log($"[LegendaryPetManager] {pet.PetName} ({pet.PetType}) 등록 완료");
            
            OnLegendaryPetSpawned?.Invoke(pet);
        }
        
        public void UnregisterLegendaryPet(LegendaryPetController pet)
        {
            if (pet == null || !legendaryPets.Contains(pet)) return;
            
            legendaryPets.Remove(pet);
            Debug.Log($"[LegendaryPetManager] {pet.PetName} ({pet.PetType}) 등록 해제");
            
            OnLegendaryPetRemoved?.Invoke(pet);
        }
        
        // 레전드 펫 ID로 스폰 (PetManager와 동일한 방식)
        private void SpawnLegendaryPet(string legendaryPetId, bool withFirstAppearanceEffect)
        {
            // 레전드 펫 ID 형식: "legend_001", "legend_002", ... 에서 숫자 부분 추출
            if (legendaryPetId.StartsWith("legend_") && legendaryPetId.Length >= 10)
            {
                string numberPart = legendaryPetId.Substring(7); // "001", "002", ...
                if (int.TryParse(numberPart, out int legendIndex))
                {
                    // 인덱스는 0부터 시작하므로 1을 빼줌
                    legendIndex = legendIndex - 1;
                    
                    // 유효한 인덱스인지 확인
                    if (legendIndex >= 0 && legendIndex < legendaryPetPrefabs.Length)
                    {
                        Vector3 spawnPosition = GetRandomSpawnPosition();
                        SpawnLegendaryPetAtPosition(legendaryPetId, spawnPosition, withFirstAppearanceEffect);
                    }
                    else
                    {
                        Debug.LogError($"[LegendaryPetManager] 유효하지 않은 레전드 펫 인덱스: {legendIndex}");
                    }
                }
            }
        }
        
        // 특정 위치에 레전드 펫을 스폰하는 메서드
        private void SpawnLegendaryPetAtPosition(string legendaryPetId, Vector3 position, bool withFirstAppearanceEffect)
        {
            if (!CanSpawnMore)
            {
                Debug.LogWarning($"[LegendaryPetManager] 최대 레전드 펫 수({maxLegendaryPets})에 도달했습니다");
                return;
            }
            
            // 레전드 펫 ID에서 인덱스 추출
            if (legendaryPetId.StartsWith("legend_") && legendaryPetId.Length >= 10)
            {
                string numberPart = legendaryPetId.Substring(7);
                if (int.TryParse(numberPart, out int legendIndex))
                {
                    legendIndex = legendIndex - 1;
                    
                    if (legendIndex >= 0 && legendIndex < legendaryPetPrefabs.Length)
                    {
                        // NavMesh 위의 가장 가까운 유효한 위치 찾기
                        NavMeshHit hit;
                        Vector3 spawnPosition = position;
                        
                        if (NavMesh.SamplePosition(position, out hit, 100f, NavMesh.AllAreas))  // 50f → 100f 확대
                        {
                            spawnPosition = hit.position;
                        }
                        else
                        {
                            Debug.LogWarning($"[LegendaryPetManager] {legendaryPetId}: NavMesh 위치를 찾을 수 없습니다.");
                        }
                        
                        // 레전드 펫 스폰
                        GameObject legendObject = Instantiate(legendaryPetPrefabs[legendIndex], spawnPosition, Quaternion.identity);
                        LegendaryPetController controller = legendObject.GetComponent<LegendaryPetController>();
                        
                        if (controller != null)
                        {
                            // 최초 등장 효과
                            if (withFirstAppearanceEffect && firstAppearanceEffectPrefab != null)
                            {
                                GameObject effect = Instantiate(firstAppearanceEffectPrefab, spawnPosition, Quaternion.identity);
                                Destroy(effect, 5f);
                            }
                            
                            Debug.Log($"[LegendaryPetManager] {legendaryPetId} 스폰 완료 - 위치: {spawnPosition}");
                        }
                        else
                        {
                            Debug.LogError($"[LegendaryPetManager] {legendaryPetId}: LegendaryPetController를 찾을 수 없습니다");
                            Destroy(legendObject);
                        }
                    }
                }
            }
        }
        
        // 선물로 레전드 펫 스폰
        private void SpawnGiftForLegendaryPet(string legendaryPetId)
        {
            if (giftPrefab == null)
            {
                Debug.LogWarning("[LegendaryPetManager] 선물 프리팹이 설정되지 않았습니다. 직접 스폰합니다.");
                SpawnLegendaryPet(legendaryPetId, true);
                return;
            }
            
            Vector3 giftPosition = GetRandomSpawnPosition();
            
            // NavMesh 위치 찾기
            NavMeshHit hit;
            if (NavMesh.SamplePosition(giftPosition, out hit, 50f, NavMesh.AllAreas))
            {
                giftPosition = hit.position;
            }
            
            // 선물 생성
            GameObject gift = Instantiate(giftPrefab, giftPosition + Vector3.up * 0.5f, Quaternion.identity);
            
            // 선물 딕셔너리에 추가
            pendingGifts.Add(gift, legendaryPetId);
            
            Debug.Log($"[LegendaryPetManager] {legendaryPetId}를 위한 선물 생성 - 위치: {giftPosition}");
        }
        
        // 선물 열기 (터치 시 호출)
        public void OpenGift(GameObject gift)
        {
            if (gift != null && pendingGifts.ContainsKey(gift))
            {
                string legendaryPetId = pendingGifts[gift];
                Vector3 giftPosition = gift.transform.position;
                
                // 선물 제거
                pendingGifts.Remove(gift);
                Destroy(gift);
                
                // 축하 효과
                if (celebrationEffectPrefab != null)
                {
                    GameObject celebration = Instantiate(celebrationEffectPrefab, giftPosition, Quaternion.identity);
                    Destroy(celebration, 5f);
                }
                
                // 불꽃놀이 효과
                if (fireworkPrefabs != null && fireworkPrefabs.Count > 0)
                {
                    GameObject firework = Instantiate(
                        fireworkPrefabs[Random.Range(0, fireworkPrefabs.Count)],
                        giftPosition + Vector3.up * 5f,
                        Quaternion.identity
                    );
                    Destroy(firework, 10f);
                }
                
                // 레전드 펫 스폰
                SpawnLegendaryPetAtPosition(legendaryPetId, giftPosition, true);
                
                Debug.Log($"[LegendaryPetManager] 선물 열기 완료: {legendaryPetId}");
            }
        }
        
        public void RemoveLegendaryPet(LegendaryPetController pet)
        {
            if (pet == null) return;
            
            UnregisterLegendaryPet(pet);
            Destroy(pet.gameObject);
        }
        
        public void RemoveAllLegendaryPets()
        {
            List<LegendaryPetController> petsToRemove = new List<LegendaryPetController>(legendaryPets);
            foreach (var pet in petsToRemove)
            {
                RemoveLegendaryPet(pet);
            }
        }
        
        private Vector3 GetRandomSpawnPosition()
        {
            float mapRange = 150f;
            
            // 여러 번 시도하여 유효한 NavMesh 위치 찾기
            for (int i = 0; i < 10; i++)
            {
                // 맵 전체에서 랜덤 위치 시도
                Vector3 randomPos = new Vector3(
                    Random.Range(-mapRange, mapRange),
                    0,
                    Random.Range(-mapRange, mapRange)
                );
                
                // NavMesh 위치 찾기 (넓은 범위로 검색)
                NavMeshHit hit;
                if (NavMesh.SamplePosition(randomPos, out hit, 50f, NavMesh.AllAreas))
                {
                    Debug.Log($"[LegendaryPetManager] NavMesh 위치 찾기 성공 (시도 {i+1}/10): {hit.position}");
                    return hit.position;
                }
            }
            
            // 모든 시도 실패 시 중심점 근처에서 재시도
            NavMeshHit centerHit;
            if (NavMesh.SamplePosition(Vector3.zero, out centerHit, 200f, NavMesh.AllAreas))
            {
                Debug.LogWarning("[LegendaryPetManager] 랜덤 위치를 찾지 못해 중심점 근처로 스폰합니다.");
                return centerHit.position;
            }
            
            // 최후의 수단: 매니저 위치 근처
            if (NavMesh.SamplePosition(transform.position, out centerHit, 100f, NavMesh.AllAreas))
            {
                Debug.LogWarning("[LegendaryPetManager] 매니저 위치 근처로 스폰합니다.");
                return centerHit.position;
            }
            
            Debug.LogError("[LegendaryPetManager] NavMesh 위치를 전혀 찾을 수 없습니다!");
            return transform.position;
        }
        
        public void SetGlobalEffects(bool enabled)
        {
            globalEffectsEnabled = enabled;
            
            foreach (var pet in legendaryPets)
            {
                if (pet != null)
                {
                    pet.SetActive(enabled);
                }
            }
            
            Debug.Log($"[LegendaryPetManager] 글로벌 효과: {(enabled ? "활성화" : "비활성화")}");
        }
        
        public void SetAllPetsMovementPattern(LegendaryPetAI.MovementPattern pattern)
        {
            foreach (var pet in legendaryPets)
            {
                if (pet != null)
                {
                    LegendaryPetAI ai = pet.GetComponent<LegendaryPetAI>();
                    if (ai != null)
                    {
                        ai.SetMovementPattern(pattern);
                    }
                }
            }
            
            Debug.Log($"[LegendaryPetManager] 모든 레전드 펫 움직임 패턴 변경: {pattern}");
        }
        
        public LegendaryPetController GetLegendaryPetByType(LegendaryPetType type)
        {
            return legendaryPets.Find(pet => pet.PetType == type);
        }
        
        public List<LegendaryPetController> GetLegendaryPetsByType(LegendaryPetType type)
        {
            return legendaryPets.FindAll(pet => pet.PetType == type);
        }
        
        // 선물 개수 반환 (PetManager와 동일한 인터페이스)
        public int GetPendingGiftCount() => pendingGifts.Count;
        
        // 선물 리스트 반환
        public List<GameObject> GetPendingGiftList()
        {
            List<GameObject> gifts = new List<GameObject>();
            foreach (var gift in pendingGifts.Keys)
            {
                if (gift != null)
                    gifts.Add(gift);
            }
            return gifts;
        }
        
        // 디버그 명령어
        [ContextMenu("Spawn Test Legendary Pet")]
        private void DebugSpawnTest()
        {
            if (legendaryPetPrefabs != null && legendaryPetPrefabs.Length > 0)
            {
                string testId = $"legend_{(1).ToString("D3")}"; // legend_001
                SpawnLegendaryPet(testId, true);
            }
        }
        
        [ContextMenu("Remove All Legendary Pets")]
        private void DebugRemoveAll()
        {
            RemoveAllLegendaryPets();
        }
        
        [ContextMenu("Toggle Global Effects")]
        private void DebugToggleEffects()
        {
            SetGlobalEffects(!globalEffectsEnabled);
        }
        
        private void OnDrawGizmosSelected()
        {
            // 스폰 반경 표시
            Gizmos.color = new Color(1f, 0.5f, 0f, 0.3f);
            Gizmos.DrawWireSphere(transform.position, spawnRadius);
            
            // 현재 레전드 펫 위치 표시
            Gizmos.color = Color.yellow;
            foreach (var pet in legendaryPets)
            {
                if (pet != null)
                {
                    Gizmos.DrawWireCube(pet.transform.position + Vector3.up * 2f, Vector3.one * 0.5f);
                    Gizmos.DrawLine(transform.position, pet.transform.position);
                }
            }
        }
    }
}