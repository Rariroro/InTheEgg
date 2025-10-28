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
        private static bool applicationIsQuitting = false;
        
        public static LegendaryPetManager Instance
        {
            get
            {
                if (applicationIsQuitting)
                {
                    return null;
                }
                
                if (instance == null)
                {
                    instance = FindObjectOfType<LegendaryPetManager>();
                    if (instance == null && Application.isPlaying)
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
        // 중요: 프리팹 할당 순서가 ID와 매칭됩니다!
        // [0-10]: 드래곤 11개 (Amber, Blossom, Cloud, Ocean, Peach, Snow, Spring, Star, Storm, Sunset, Volcano)
        // [11-20]: 유니콘 10개 (Dream, Mint, Night, Prism, Pure, Rose, Shadow, Sky, Terra, Twin)
        // pet_legend_001 = 배열[0], pet_legend_021 = 배열[20]
        public GameObject[] legendaryPetPrefabs;  // 레전드 펫 프리팹 배열 (인덱스로 관리)

        [Header("스폰 설정")]
        public float spawnRadius = 50f;  // 스폰 반경
        public int maxLegendaryPets = 21; // 동시에 존재할 수 있는 최대 레전드 펫 수 (전체 21개)
        
        [Header("최초 등장 효과")]
        public GameObject firstAppearanceEffectPrefab; // 최초 등장 효과 프리팹
        public float firstAppearanceDelay = 0.5f; // 최초 등장 펫들 사이의 딜레이
        
        [Header("선물 시스템")]
        public GameObject giftPrefab; // 선물 프리팹
        public GameObject celebrationEffectPrefab; // 축하 효과 파티클 프리팹
        public float giftSpawnDelay = 0.5f; // 선물 스폰 딜레이
        public List<GameObject> fireworkPrefabs = new List<GameObject>(); // 불꽃놀이 프리팹들

        [Header("5단계 스폰 시스템")]
        [Tooltip("A 좌표: Gift가 생성될 위치")]
        public Vector3 giftSpawnPosition = new Vector3(0, 0, 10);

        [Tooltip("비행 경로 웨이포인트 (B→C→D→F)")]
        public Vector3[] flightWaypoints = new Vector3[4] {
            new Vector3(0, 5, 0),    // B: 펫 등장 위치
            new Vector3(5, 3, 5),    // C: 첫 번째 경유지
            new Vector3(-5, 2, 0),   // D: 두 번째 경유지
            new Vector3(0, 0, -10)   // F: 최종 착륙 위치
        };

        [Tooltip("B좌표 등장 시 사용할 특별 이펙트")]
        public GameObject appearanceEffectPrefab;

        [Tooltip("날아가는 동안 표시될 트레일 이펙트")]
        public GameObject flyingTrailPrefab;

        [Tooltip("펫이 날아가는 속도")]
        public float flyingSpeed = 10f;

        [Tooltip("펫이 날아가는 높이 커브")]
        public AnimationCurve flyingHeightCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);
        
        [Header("NavMesh 대기 설정")]
        public float navMeshWaitTime = 3f; // NavMesh 베이크 대기 시간
        
        [Header("레전드 펫 관리")]
        [SerializeField] private List<LegendaryPetController> legendaryPets = new List<LegendaryPetController>();
        
        // 대기 중인 선물들과 해당 레전드 펫 정보를 저장하는 딕셔너리
        private Dictionary<GameObject, string> pendingGifts = new Dictionary<GameObject, string>();
        
        // 터치 처리 최적화를 위한 변수
        private float lastTouchTime;
        private const float TOUCH_COOLDOWN = 0.1f;
        
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
            applicationIsQuitting = false;
            
        // Debug.Log("[LegendaryPetManager] 매니저 초기화 완료");
        }
        
        private void Start()
        {
            // PetManager와 동일하게 환경 준비 완료 후 스폰
            StartCoroutine(WaitForEnvironmentAndSpawnLegendaryPets());
        }
        
        private void Update()
        {
            // 선물이 없으면 Update 실행하지 않음
            if (pendingGifts.Count == 0) return;
            
            // 터치 쿨다운 체크
            if (Time.time - lastTouchTime < TOUCH_COOLDOWN) return;
            
            // 선물 터치 감지
            HandleGiftTouch();
        }
        
        private void HandleGiftTouch()
        {
            if (Input.GetMouseButtonDown(0))
            {
                lastTouchTime = Time.time;
                
                if (Camera.main == null) return;
                
                Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
                RaycastHit hit;
                
                // Ignore Raycast 레이어를 제외한 모든 레이어와 충돌 검사
                int layerMask = ~LayerMask.GetMask("Ignore Raycast");
                
                if (Physics.Raycast(ray, out hit, Mathf.Infinity, layerMask))
                {
                    GameObject hitObject = hit.collider.gameObject;
                    
                    // 터치한 오브젝트가 대기 중인 선물인지 확인
                    if (pendingGifts.ContainsKey(hitObject))
                    {
                        string legendaryPetId = pendingGifts[hitObject];
                        StartCoroutine(OpenGiftCoroutine(hitObject, legendaryPetId));
                    }
                }
            }
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

                    // 180도 회전하여 카메라를 향하도록 스폰
                    Quaternion rotation = Quaternion.Euler(0, 180, 0);
                    GameObject legendObject = Instantiate(legendaryPetPrefabs[i], randomPosition, rotation);
                    LegendaryPetController controller = legendObject.GetComponent<LegendaryPetController>();
                    
                    if (controller != null)
                    {
                        spawnCount++;
        // Debug.Log($"[LegendaryPetManager] 레전드 펫 스폰 ({spawnCount}/{maxLegendaryPets}): {controller.PetType}");
                    }
                    else
                    {
                        Debug.LogError($"[LegendaryPetManager] 프리팹 {i}에 LegendaryPetController가 없습니다");
                        if (Application.isPlaying)
                        {
                            Destroy(legendObject);
                        }
                    }
                }
            }
            
            if (spawnCount == 0)
            {
                Debug.LogWarning("[LegendaryPetManager] 스폰된 레전드 펫이 없습니다. 프리팹 설정을 확인하세요.");
            }
            else
            {
        // Debug.Log($"[LegendaryPetManager] 총 {spawnCount}개의 레전드 펫 스폰 완료");
            }
        }
        
        public void RegisterLegendaryPet(LegendaryPetController pet)
        {
            if (pet == null || legendaryPets.Contains(pet)) return;
            
            legendaryPets.Add(pet);
        // Debug.Log($"[LegendaryPetManager] {pet.PetName} ({pet.PetType}) 등록 완료");
            
            OnLegendaryPetSpawned?.Invoke(pet);
        }
        
        public void UnregisterLegendaryPet(LegendaryPetController pet)
        {
            if (pet == null || !legendaryPets.Contains(pet)) return;
            
            legendaryPets.Remove(pet);
        // Debug.Log($"[LegendaryPetManager] {pet.PetName} ({pet.PetType}) 등록 해제");
            
            OnLegendaryPetRemoved?.Invoke(pet);
        }
        
        // 레전드 펫 ID로 스폰 (새로운 ID 형식 지원)
        private void SpawnLegendaryPet(string legendaryPetId, bool withFirstAppearanceEffect)
        {
            int legendIndex = -1;

            // pet_legend_XXX 형식 지원 (주요 형식)
            if (legendaryPetId.StartsWith("pet_legend_") && legendaryPetId.Length >= 14)
            {
                string numberPart = legendaryPetId.Substring(11);  // "pet_legend_" 이후 부분
                if (int.TryParse(numberPart, out int number))
                {
                    legendIndex = number - 1;  // pet_legend_001 = 인덱스 0
                }
            }
            // 기존 형식도 지원 (하위 호환성)
            else if (legendaryPetId.StartsWith("unicorn"))
            {
                string numberPart = legendaryPetId.Replace("unicorn", "");
                if (int.TryParse(numberPart, out int unicornNumber) && unicornNumber >= 1 && unicornNumber <= 5)
                {
                    legendIndex = unicornNumber - 1; // 유니콘은 인덱스 0-4
                }
            }
            else if (legendaryPetId.StartsWith("dragon"))
            {
                string numberPart = legendaryPetId.Replace("dragon", "");
                if (int.TryParse(numberPart, out int dragonNumber) && dragonNumber >= 1 && dragonNumber <= 5)
                {
                    legendIndex = dragonNumber + 4; // 드래곤은 인덱스 5-9
                }
            }
            // 기존 ID 형식도 지원: "legend_001", "legend_002", ...
            else if (legendaryPetId.StartsWith("legend_") && legendaryPetId.Length >= 10)
            {
                string numberPart = legendaryPetId.Substring(7);
                if (int.TryParse(numberPart, out int number))
                {
                    legendIndex = number - 1;
                }
            }

            // 유효한 인덱스인지 확인하고 스폰
            if (legendIndex >= 0 && legendIndex < legendaryPetPrefabs.Length)
            {
                Vector3 spawnPosition = GetRandomSpawnPosition();
                SpawnLegendaryPetAtPosition(legendaryPetId, spawnPosition, withFirstAppearanceEffect);
            }
            else
            {
                Debug.LogError($"[LegendaryPetManager] 유효하지 않은 레전드 펫 ID: {legendaryPetId} (인덱스: {legendIndex})");
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
            
            int legendIndex = -1;

            // pet_legend_XXX 형식 지원 (주요 형식)
            if (legendaryPetId.StartsWith("pet_legend_") && legendaryPetId.Length >= 14)
            {
                string numberPart = legendaryPetId.Substring(11);  // "pet_legend_" 이후 부분
                if (int.TryParse(numberPart, out int number))
                {
                    legendIndex = number - 1;  // pet_legend_001 = 인덱스 0
                }
            }
            // 기존 형식도 지원 (하위 호환성)
            else if (legendaryPetId.StartsWith("unicorn"))
            {
                string numberPart = legendaryPetId.Replace("unicorn", "");
                if (int.TryParse(numberPart, out int unicornNumber) && unicornNumber >= 1 && unicornNumber <= 5)
                {
                    legendIndex = unicornNumber - 1;
                }
            }
            else if (legendaryPetId.StartsWith("dragon"))
            {
                string numberPart = legendaryPetId.Replace("dragon", "");
                if (int.TryParse(numberPart, out int dragonNumber) && dragonNumber >= 1 && dragonNumber <= 5)
                {
                    legendIndex = dragonNumber + 4;
                }
            }
            // 기존 ID 형식도 지원
            else if (legendaryPetId.StartsWith("legend_") && legendaryPetId.Length >= 10)
            {
                string numberPart = legendaryPetId.Substring(7);
                if (int.TryParse(numberPart, out int number))
                {
                    legendIndex = number - 1;
                }
            }
            
            // 유효한 인덱스인지 확인
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

                // 180도 회전하여 카메라를 향하도록 스폰
                Quaternion rotation = Quaternion.Euler(0, 180, 0);
                // 레전드 펫 스폰
                GameObject legendObject = Instantiate(legendaryPetPrefabs[legendIndex], spawnPosition, rotation);
                LegendaryPetController controller = legendObject.GetComponent<LegendaryPetController>();
                
                if (controller != null)
                {
                    // 최초 등장 효과
                    if (withFirstAppearanceEffect && firstAppearanceEffectPrefab != null)
                    {
                        GameObject effect = Instantiate(firstAppearanceEffectPrefab, spawnPosition, Quaternion.identity);
                        if (Application.isPlaying)
                        {
                            Destroy(effect, 5f);
                        }
                    }

        // Debug.Log($"[LegendaryPetManager] {legendaryPetId} 스폰 완료 - 위치: {spawnPosition}");
                }
                else
                {
                    Debug.LogError($"[LegendaryPetManager] {legendaryPetId}: LegendaryPetController를 찾을 수 없습니다");
                    if (Application.isPlaying)
                    {
                        Destroy(legendObject);
                    }
                }
            }
            else
            {
                Debug.LogError($"[LegendaryPetManager] 유효하지 않은 레전드 펫 인덱스: {legendIndex} (ID: {legendaryPetId})");
            }
        }
        
        // 선물로 레전드 펫 스폰 (3단계 시스템 적용)
        private void SpawnGiftForLegendaryPet(string legendaryPetId)
        {
            if (giftPrefab == null)
            {
                Debug.LogWarning("[LegendaryPetManager] 선물 프리팹이 설정되지 않았습니다. 직접 스폰합니다.");
                SpawnLegendaryPet(legendaryPetId, true);
                return;
            }

            // A 좌표에 선물 생성 (giftSpawnPosition 사용)
            Vector3 giftPosition = giftSpawnPosition;

            // NavMesh 위치 찾기
            NavMeshHit hit;
            if (NavMesh.SamplePosition(giftPosition, out hit, 50f, NavMesh.AllAreas))
            {
                giftPosition = hit.position;
            }

            // 선물 생성 (A 좌표)
            GameObject gift = Instantiate(giftPrefab, giftPosition + Vector3.up * 0.5f, Quaternion.identity);

            // 선물 회전 애니메이션 추가
            StartCoroutine(RotateGift(gift));

            // 선물 딕셔너리에 추가
            pendingGifts.Add(gift, legendaryPetId);

            Debug.Log($"[LegendaryPetManager] {legendaryPetId}를 위한 선물 생성 - A좌표: {giftPosition}");
        }

        // 선물 회전 애니메이션 코루틴
        private IEnumerator RotateGift(GameObject gift)
        {
            while (gift != null && pendingGifts.ContainsKey(gift))
            {
                gift.transform.Rotate(0, 30 * Time.deltaTime, 0);

                // 위아래 흔들림 효과
                float bobbing = Mathf.Sin(Time.time * 2f) * 0.1f;
                gift.transform.position += Vector3.up * bobbing;

                yield return null;
            }
        }
        
        // 선물 열기 코루틴 (3단계 시스템)
        private IEnumerator OpenGiftCoroutine(GameObject gift, string legendaryPetId)
        {
            if (gift == null || !pendingGifts.ContainsKey(gift))
                yield break;

            // 선물 위치 저장 (A 좌표)
            Vector3 giftPos = gift.transform.position;

            // 선물 제거
            pendingGifts.Remove(gift);

            // A 좌표에서 축하 효과
            if (celebrationEffectPrefab != null)
            {
                GameObject celebration = Instantiate(celebrationEffectPrefab, giftPos, Quaternion.identity);
                if (Application.isPlaying)
                {
                    Destroy(celebration, 5f);
                }
            }

            // 선물 오브젝트 제거
            if (Application.isPlaying)
            {
                Destroy(gift);
            }

            // 약간의 딜레이
            yield return new WaitForSeconds(0.3f);

            // B 좌표에서 펫 등장
            yield return StartCoroutine(SpawnLegendaryPetWithThreeStageSystem(legendaryPetId));

            Debug.Log($"[LegendaryPetManager] 3단계 스폰 완료: {legendaryPetId}");
        }

        // 3단계 스폰 시스템 코루틴
        private IEnumerator SpawnLegendaryPetWithThreeStageSystem(string legendaryPetId)
        {
            int legendIndex = GetLegendaryPetIndex(legendaryPetId);
            if (legendIndex < 0 || legendIndex >= legendaryPetPrefabs.Length)
            {
                Debug.LogError($"[LegendaryPetManager] 유효하지 않은 레전드 펫 인덱스: {legendIndex}");
                yield break;
            }

            // B 좌표에서 펫 생성 (NavMesh 체크 없이)
            Vector3 appearPos = flightWaypoints[0]; // B 좌표

            // 180도 회전하여 카메라를 향하도록 스폰
            Quaternion rotation = Quaternion.Euler(0, 180, 0);

            // 직접 목표 위치에서 생성 (메모리 오류 방지)
            GameObject legendObject = Instantiate(legendaryPetPrefabs[legendIndex], appearPos, rotation);

            // NavMeshAgent 안전하게 비활성화
            NavMeshAgent spawnedAgent = legendObject.GetComponent<NavMeshAgent>();
            if (spawnedAgent != null)
            {
                // 메모리 오류 방지: 컴포넌트 완전 비활성화
                spawnedAgent.enabled = false;
                spawnedAgent.updatePosition = false;
                spawnedAgent.updateRotation = false;
                spawnedAgent.updateUpAxis = false;
            }

            LegendaryPetController controller = legendObject.GetComponent<LegendaryPetController>();
            if (controller == null)
            {
                Debug.LogError($"[LegendaryPetManager] LegendaryPetController를 찾을 수 없습니다");
                if (Application.isPlaying)
                {
                    Destroy(legendObject);
                }
                yield break;
            }

            // 즉시 초기화 (애니메이터와 traits 설정)
            controller.InitializeImmediate();

            // 컨트롤러에 날아다니는 상태 설정 (이제 animator가 준비됨)
            controller.SetFlying(true);

            // B 좌표 등장 효과
            if (appearanceEffectPrefab != null)
            {
                GameObject appearEffect = Instantiate(appearanceEffectPrefab, appearPos, Quaternion.identity);
                appearEffect.transform.localScale = Vector3.one * 2f;
                if (Application.isPlaying)
                {
                    Destroy(appearEffect, 5f);
                }
            }

            // 불꽃놀이 효과
            if (fireworkPrefabs != null && fireworkPrefabs.Count > 0)
            {
                for (int i = 0; i < Mathf.Min(3, fireworkPrefabs.Count); i++)
                {
                    Vector3 fireworkOffset = new Vector3(Random.Range(-3f, 3f), 0, Random.Range(-3f, 3f));
                    GameObject firework = Instantiate(
                        fireworkPrefabs[Random.Range(0, fireworkPrefabs.Count)],
                        appearPos + Vector3.up * 5f + fireworkOffset,
                        Quaternion.identity
                    );
                    if (Application.isPlaying)
                    {
                        Destroy(firework, 10f);
                    }
                    yield return new WaitForSeconds(0.2f);
                }
            }

            // 잠시 대기 (등장 모션)
            yield return new WaitForSeconds(1f);

            // B → C → D → F 순차적으로 날아가기
            yield return StartCoroutine(FlyPetThroughWaypoints(legendObject, controller));
        }

        // 여러 경유지를 거쳐 날아가는 코루틴
        private IEnumerator FlyPetThroughWaypoints(GameObject petObject, LegendaryPetController controller)
        {
            if (petObject == null || flightWaypoints == null || flightWaypoints.Length < 2)
                yield break;

            // B(0) → C(1) → D(2) → F(3) 순차 비행
            for (int i = 1; i < flightWaypoints.Length; i++)
            {
                Vector3 destination = flightWaypoints[i];
                bool isLastWaypoint = (i == flightWaypoints.Length - 1);

                // 각 구간별 비행
                yield return StartCoroutine(FlyPetToDestination(petObject, controller, destination, isLastWaypoint));

                // 대기 시간 제거 - 끊김 현상 방지
                // if (!isLastWaypoint)
                // {
                //     yield return new WaitForSeconds(0.2f);
                // }
            }
        }

        // 펫을 목적지로 날아가게 하는 코루틴 (개별 구간)
        private IEnumerator FlyPetToDestination(GameObject petObject, LegendaryPetController controller, Vector3 destination, bool isFinalDestination = true)
        {
            if (petObject == null) yield break;

            Vector3 startPos = petObject.transform.position;
            Vector3 endPos = destination;
            Vector3 navMeshEndPos = endPos; // NavMesh 위치 저장용

            // 최종 목적지에서만 NavMesh 위치 확인 (실제 착륙 위치)
            if (isFinalDestination)
            {
                NavMeshHit hit;
                if (NavMesh.SamplePosition(endPos, out hit, 100f, NavMesh.AllAreas))
                {
                    navMeshEndPos = hit.position;
                    // 비행 경로는 원래 목표 위치 사용, NavMesh 위치는 나중에 사용
                }
            }

            // 트레일 이펙트 추가 (안전하게)
            GameObject trail = null;
            if (flyingTrailPrefab != null && petObject != null)
            {
                trail = Instantiate(flyingTrailPrefab, petObject.transform.position, Quaternion.identity);
                // 부모 설정 (메모리 안전성)
                if (trail != null)
                {
                    trail.transform.SetParent(petObject.transform);
                }
            }

            // NavMeshAgent 일시 비활성화
            NavMeshAgent agent = petObject.GetComponent<NavMeshAgent>();
            if (agent != null)
            {
                agent.enabled = false;
            }

            // 날아가기 애니메이션
            float journey = 0f;
            float distance = Vector3.Distance(startPos, endPos);

            // 시작 회전과 목표 회전 계산 (Y축만 회전)
            Vector3 direction = (endPos - startPos);
            direction.y = 0; // Y 성분 제거하여 수평 방향만 계산
            direction = direction.normalized;

            Quaternion startRotation = petObject.transform.rotation;
            Quaternion targetRotation = startRotation;

            if (direction != Vector3.zero)
            {
                // Y축 회전만 계산
                float targetYAngle = Mathf.Atan2(direction.x, direction.z) * Mathf.Rad2Deg;
                targetRotation = Quaternion.Euler(0, targetYAngle, 0);
            }

            while (journey < 1f)
            {
                journey += Time.deltaTime * flyingSpeed / distance;
                journey = Mathf.Clamp01(journey); // 오버슈팅 방지

                float curveValue = flyingHeightCurve.Evaluate(journey);

                // 포물선 경로 계산
                Vector3 currentPos = Vector3.Lerp(startPos, endPos, journey);

                // 최종 목적지에서는 착륙을 부드럽게 처리
                if (isFinalDestination && journey > 0.8f)
                {
                    // 마지막 20%에서 높이를 점진적으로 감소
                    float landingProgress = (journey - 0.8f) / 0.2f; // 0 to 1
                    float heightMultiplier = 1f - landingProgress; // 1 to 0
                    currentPos.y += curveValue * 5f * heightMultiplier;

                    // NavMesh 위치로 부드럽게 전환
                    currentPos = Vector3.Lerp(currentPos, navMeshEndPos, landingProgress * 0.5f);
                }
                else
                {
                    currentPos.y += curveValue * 5f; // 최대 높이 5 유닛
                }

                petObject.transform.position = currentPos;

                // Y축만 부드럽게 회전 (처음 50% 동안 회전 완료)
                float rotationProgress = Mathf.Min(journey * 2f, 1f);
                petObject.transform.rotation = Quaternion.Slerp(startRotation, targetRotation, rotationProgress);

                // X, Z축 강제 고정 (안전장치)
                Vector3 currentEuler = petObject.transform.eulerAngles;
                currentEuler.x = 0;
                currentEuler.z = 0;
                petObject.transform.eulerAngles = currentEuler;

                yield return null;
            }

            // 최종 목적지에서만 NavMeshAgent 활성화
            if (isFinalDestination)
            {
                // 이미 부드럽게 착륙했으므로 강제 위치 설정 제거
                // petObject.transform.position은 이미 navMeshEndPos에 가까운 위치

                // 날아다니는 상태 해제
                controller.SetFlying(false);

                // NavMeshAgent 재활성화 (안전하게)
                if (agent != null)
                {
                    // 현재 위치가 이미 NavMesh 위치에 가까우므로
                    petObject.transform.position = navMeshEndPos;

                    // 활성화 전 설정
                    agent.updatePosition = true;
                    agent.updateRotation = true;
                    agent.enabled = true;

                    // 이미 올바른 위치에 있으므로 Warp 필요 없음
                    // Warp는 순간이동을 유발할 수 있음
                    if (agent.enabled && agent.isOnNavMesh)
                    {
                        // agent.Warp(navMeshEndPos); 제거
                    }
                }

                // 착지 효과
                if (firstAppearanceEffectPrefab != null)
                {
                    GameObject landEffect = Instantiate(firstAppearanceEffectPrefab, endPos, Quaternion.identity);
                    if (Application.isPlaying)
                    {
                        Destroy(landEffect, 3f);
                    }
                }

                Debug.Log($"[LegendaryPetManager] 펫이 최종 목적지(F)에 도착: {endPos}, Flying 상태 해제");
            }
            else
            {
                // 중간 경유지에서는 간단한 효과만
                Debug.Log($"[LegendaryPetManager] 경유지 통과: {endPos}");
            }

            // 트레일 이펙트 제거 (최종 목적지에서만, 안전하게)
            if (trail != null && isFinalDestination)
            {
                // 부모 해제 전 null 체크
                if (trail.transform != null)
                {
                    trail.transform.SetParent(null);
                }

                // 안전한 파괴
                if (Application.isPlaying)
                {
                    Destroy(trail, 2f);
                }
            }
        }

        // 레전드 펫 인덱스 가져오기 헬퍼 메서드
        private int GetLegendaryPetIndex(string legendaryPetId)
        {
            // pet_legend_XXX 형식 지원
            if (legendaryPetId.StartsWith("pet_legend_") && legendaryPetId.Length >= 14)
            {
                string numberPart = legendaryPetId.Substring(11);
                if (int.TryParse(numberPart, out int number))
                {
                    return number - 1;
                }
            }
            // 기존 형식들도 지원
            else if (legendaryPetId.StartsWith("unicorn"))
            {
                string numberPart = legendaryPetId.Replace("unicorn", "");
                if (int.TryParse(numberPart, out int unicornNumber) && unicornNumber >= 1 && unicornNumber <= 5)
                {
                    return unicornNumber - 1;
                }
            }
            else if (legendaryPetId.StartsWith("dragon"))
            {
                string numberPart = legendaryPetId.Replace("dragon", "");
                if (int.TryParse(numberPart, out int dragonNumber) && dragonNumber >= 1 && dragonNumber <= 5)
                {
                    return dragonNumber + 4;
                }
            }
            else if (legendaryPetId.StartsWith("legend_") && legendaryPetId.Length >= 10)
            {
                string numberPart = legendaryPetId.Substring(7);
                if (int.TryParse(numberPart, out int number))
                {
                    return number - 1;
                }
            }

            return -1;
        }
        
        // 기존 OpenGift 메서드 (외부에서 호출용)
        public void OpenGift(GameObject gift)
        {
            if (gift != null && pendingGifts.ContainsKey(gift))
            {
                string legendaryPetId = pendingGifts[gift];
                StartCoroutine(OpenGiftCoroutine(gift, legendaryPetId));
            }
        }
        
        public void RemoveLegendaryPet(LegendaryPetController pet)
        {
            if (pet == null) return;

            UnregisterLegendaryPet(pet);
            if (Application.isPlaying)
            {
                Destroy(pet.gameObject);
            }
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
        // Debug.Log($"[LegendaryPetManager] NavMesh 위치 찾기 성공 (시도 {i+1}/10): {hit.position}");
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
            
        // Debug.Log($"[LegendaryPetManager] 글로벌 효과: {(enabled ? "활성화" : "비활성화")}");
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
            
        // Debug.Log($"[LegendaryPetManager] 모든 레전드 펫 움직임 패턴 변경: {pattern}");
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

            // 5단계 스폰 위치 표시
            // A 좌표 (Gift 위치) - 빨간색
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(giftSpawnPosition, 1f);
            Gizmos.DrawWireCube(giftSpawnPosition + Vector3.up * 2f, Vector3.one * 0.5f);
            DrawGizmoLabel(giftSpawnPosition + Vector3.up * 3f, "A: Gift");

            // 비행 경로 웨이포인트 표시
            if (flightWaypoints != null && flightWaypoints.Length > 0)
            {
                string[] waypointLabels = new string[] { "B: Appear", "C: Waypoint 1", "D: Waypoint 2", "F: Final" };
                Color[] waypointColors = new Color[] {
                    Color.yellow,       // B: 노란색
                    new Color(1f, 0.5f, 0f),  // C: 주황색
                    new Color(0.5f, 0f, 1f),  // D: 보라색
                    Color.green         // F: 초록색
                };

                for (int i = 0; i < flightWaypoints.Length && i < waypointColors.Length; i++)
                {
                    Vector3 waypoint = flightWaypoints[i];
                    Gizmos.color = waypointColors[i];
                    Gizmos.DrawWireSphere(waypoint, 1f);
                    Gizmos.DrawWireCube(waypoint + Vector3.up * 2f, Vector3.one * 0.5f);

                    if (i < waypointLabels.Length)
                    {
                        DrawGizmoLabel(waypoint + Vector3.up * 3f, waypointLabels[i]);
                    }
                }

                // 경로 표시 (A → B → C → D → F)
                // A → B
                Gizmos.color = new Color(1f, 0f, 0f, 0.5f);
                if (flightWaypoints.Length > 0)
                {
                    Gizmos.DrawLine(giftSpawnPosition, flightWaypoints[0]);
                }

                // B → C → D → F
                for (int i = 0; i < flightWaypoints.Length - 1; i++)
                {
                    float t = (float)i / (flightWaypoints.Length - 1);
                    Gizmos.color = Color.Lerp(new Color(1f, 1f, 0f, 0.5f), new Color(0f, 1f, 0f, 0.5f), t);
                    Gizmos.DrawLine(flightWaypoints[i], flightWaypoints[i + 1]);

                    // 화살표 표시 (방향성)
                    Vector3 direction = (flightWaypoints[i + 1] - flightWaypoints[i]).normalized;
                    Vector3 midPoint = (flightWaypoints[i] + flightWaypoints[i + 1]) * 0.5f;
                    DrawArrow(midPoint, direction, 0.5f);
                }
            }

            // 현재 레전드 펫 위치 표시
            Gizmos.color = Color.cyan;
            foreach (var pet in legendaryPets)
            {
                if (pet != null)
                {
                    Gizmos.DrawWireCube(pet.transform.position + Vector3.up * 2f, Vector3.one * 0.3f);
                }
            }
        }

        // 화살표 그리기 헬퍼 메서드
        private void DrawArrow(Vector3 position, Vector3 direction, float size)
        {
            if (direction == Vector3.zero) return;

            Vector3 right = Vector3.Cross(Vector3.up, direction).normalized * size;
            Vector3 left = -right;

            Gizmos.DrawLine(position, position - direction * size + right * 0.5f);
            Gizmos.DrawLine(position, position - direction * size + left * 0.5f);
        }

        // Gizmo 라벨 그리기 헬퍼 메서드
        private void DrawGizmoLabel(Vector3 position, string text)
        {
#if UNITY_EDITOR
            UnityEditor.Handles.Label(position, text);
#endif
        }
        
        private void OnDestroy()
        {
            if (instance == this)
            {
                instance = null;
            }
            
            // 대기 중인 선물 정리
            if (pendingGifts != null)
            {
                foreach (var gift in pendingGifts.Keys)
                {
                    if (gift != null && Application.isPlaying)
                    {
                        Destroy(gift);
                    }
                }
                pendingGifts.Clear();
            }
            
            // 레전드 펫 리스트 정리
            if (legendaryPets != null)
            {
                legendaryPets.Clear();
            }
            
            // 이벤트 구독 해제
            OnLegendaryPetSpawned = null;
            OnLegendaryPetRemoved = null;
        }
        
        private void OnApplicationQuit()
        {
            applicationIsQuitting = true;
        }
    }
}