using UnityEngine;
using UnityEngine.AI;
using System.Collections;
using System.Collections.Generic;

public class PetManager : MonoBehaviour
{
    public GameObject[] petPrefabs;  // 60개의 펫 프리팹 배열
    public float spawnRadius = 50f;  // 스폰 반경 (폴백용)
    public int maxPets = 50;

    [Header("스폰 위치 설정")]
    public PetSpawnSpot[] spawnSpots; // 11개의 스폰 스팟
    public bool useSpawnSpots = true; // 스폰 스팟 사용 여부
    public float spawnOffsetRadius = 2f; // 같은 스팟에 여러 펫 스폰 시 오프셋 반경

    [Header("최초 등장 효과")]
    public GameObject firstAppearanceEffectPrefab; // 최초 등장 효과 프리팹 (옵션)
    public float firstAppearanceDelay = 0.5f; // 최초 등장 펫들 사이의 딜레이

    [Header("선물 시스템")]
    public GameObject[] giftPrefabs; // 선물 프리팹들 (여러 egg 중 랜덤 선택)
    public GameObject celebrationEffectPrefab; // 축하 효과 파티클 프리팹
    public float giftSpawnDelay = 0.5f; // 선물 스폰 딜레이
    public List<GameObject> fireworkPrefabs = new List<GameObject>(); // 불꽃놀이 프리팹들

    [Header("NavMesh 대기 설정")]
    public float navMeshWaitTime = 3f; // NavMesh 베이크 대기 시간

    // 대기 중인 선물들과 해당 펫 정보를 저장하는 딕셔너리
    private Dictionary<GameObject, string> pendingGifts = new Dictionary<GameObject, string>();

    // 스폰 스팟별 사용 카운트 (같은 스팟에 여러 펫이 스폰될 때 오프셋 계산용)
    private Dictionary<int, int> spawnSpotUsageCount = new Dictionary<int, int>();

    // 다음에 사용할 스폰 스팟 인덱스
    private int nextSpawnSpotIndex = 0;

    // 스폰 진행 중 플래그 (중복 스폰 방지)
    private bool isSpawning = false;

    // 스폰 완료 플래그 (한 세션에서 한 번만 스폰)
    private bool hasSpawnedPets = false;

    #region 세션 관리

    /// <summary>
    /// 새 세션을 위해 상태 리셋 (FlutterModeManager에서 호출)
    /// </summary>
    public void ResetForNewSession()
    {
        Debug.Log("[PetManager] 새 세션을 위해 상태 리셋");

        // 기존 펫/Egg 제거
        ClearAllPetsAndGifts();

        // 플래그 리셋
        isSpawning = false;
        hasSpawnedPets = false;

        // 스폰 스팟 초기화
        InitializeSpawnSpots();
    }

    /// <summary>
    /// 모든 펫과 Egg 제거
    /// </summary>
    private void ClearAllPetsAndGifts()
    {
        // 대기 중인 Egg 제거
        foreach (var gift in pendingGifts.Keys)
        {
            if (gift != null) Destroy(gift);
        }
        pendingGifts.Clear();

        // 스폰된 펫 제거
        var pets = FindObjectsOfType<PetController>();
        foreach (var pet in pets)
        {
            if (pet != null) Destroy(pet.gameObject);
        }

        Debug.Log($"[PetManager] 기존 펫/Egg 모두 제거됨");
    }

    #endregion

    // UI에서 접근할 수 있도록 읽기 전용 프로퍼티 제공
    public Dictionary<GameObject, string> PendingGifts => new Dictionary<GameObject, string>(pendingGifts);

    // 선물 개수 반환
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

    // 터치 처리 최적화를 위한 변수
    private float lastTouchTime;
    private const float TOUCH_COOLDOWN = 0.1f;

    private void Start()
    {
        // 스폰 스팟 초기화
        InitializeSpawnSpots();

        // Flutter 데이터 수신 이벤트 구독 (씬 로드 후에 INIT_GAME이 와도 동작)
        if (FlutterModeManager.Instance != null)
        {
            FlutterModeManager.Instance.OnFlutterDataReceived += OnFlutterDataReceived;
        }

        // EnvironmentManager가 환경 스폰과 NavMesh 베이크를 완료할 때까지 기다린 후 펫 스폰
        StartCoroutine(WaitForEnvironmentAndSpawnPets());
    }

    /// <summary>
    /// Flutter에서 INIT_GAME 수신 시 호출 (씬 로드 후에 데이터가 와도 동작)
    /// </summary>
    private void OnFlutterDataReceived()
    {
        Debug.Log("[PetManager] OnFlutterDataReceived 이벤트 수신");
        if (!isSpawning && !hasSpawnedPets)
        {
            StartCoroutine(SpawnPetsFromFlutterData());
        }
        else
        {
            Debug.Log($"[PetManager] 스폰 건너뜀 (isSpawning={isSpawning}, hasSpawnedPets={hasSpawnedPets})");
        }
    }

    // 환경 준비 완료까지 기다리는 코루틴
    private IEnumerator WaitForEnvironmentAndSpawnPets()
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
            Debug.LogWarning("EnvironmentManager를 찾을 수 없습니다. 기본 대기 시간 적용");
            // EnvironmentManager가 없으면 기본 대기 시간
            yield return new WaitForSeconds(3f);
        }

        // 추가 안전 대기
        yield return new WaitForSeconds(1f);

        // 이미 스폰 완료된 경우 건너뜀
        if (hasSpawnedPets)
        {
            Debug.Log("[PetManager] 이미 스폰 완료됨 - 건너뜀");
            yield break;
        }

        // Flutter 모드: 이미 데이터가 있으면 스폰
        if (FlutterModeManager.Instance != null && FlutterModeManager.Instance.IsFlutterMode)
        {
            Debug.Log("[PetManager] Flutter 모드 - Egg/펫 자동 생성");
            yield return StartCoroutine(SpawnPetsFromFlutterData());
        }
        // PetChoice 모드: 자동 스폰 (버튼 없이)
        else if (PetSelectionManager.Instance != null && PetSelectionManager.Instance.selectedPetIds.Count > 0)
        {
            Debug.Log($"[PetManager] PetChoice 모드 - {PetSelectionManager.Instance.selectedPetIds.Count}마리 자동 스폰");
            yield return StartCoroutine(SpawnPetsFromSelectionManager());
        }
        // PetVillage에서 바로 시작한 경우: 모든 펫 자동 스폰
        else
        {
            Debug.LogWarning("선택된 펫이 없습니다. 모든 펫을 스폰합니다.");
            SpawnAllPets();
            hasSpawnedPets = true;
        }
    }

    /// <summary>
    /// PetSelectionManager 데이터로 펫 스폰 (PetChoice 모드)
    /// </summary>
    private IEnumerator SpawnPetsFromSelectionManager()
    {
        isSpawning = true;

        int spawnedCount = 0;
        int eggCount = 0;

        foreach (string petId in PetSelectionManager.Instance.selectedPetIds)
        {
            bool isFirstAppearance = PetSelectionManager.Instance.IsPetFirstAppearance(petId);

            if (isFirstAppearance)
            {
                SpawnGiftForPet(petId);
                eggCount++;
            }
            else
            {
                SpawnPet(petId, false);
                spawnedCount++;
            }

            yield return new WaitForSeconds(0.1f);
        }

        Debug.Log($"[PetManager] PetChoice 모드 스폰 완료. 직접 스폰: {spawnedCount}마리, Egg 생성: {eggCount}마리");
        isSpawning = false;
        hasSpawnedPets = true;
    }

    /// <summary>
    /// Flutter 데이터로 펫 스폰 (Flutter 모드 전용)
    /// isSpawned=true → 직접 스폰
    /// isSpawned=false → Egg 생성 (유저가 터치하면 스폰)
    /// </summary>
    private IEnumerator SpawnPetsFromFlutterData()
    {
        // 이미 스폰 완료된 경우 즉시 반환
        if (hasSpawnedPets)
        {
            Debug.Log("[PetManager] SpawnPetsFromFlutterData - 이미 스폰 완료됨, 건너뜀");
            yield break;
        }

        // 즉시 플래그 설정하여 중복 호출 방지
        isSpawning = true;
        hasSpawnedPets = true;

        var gameData = FlutterModeManager.Instance.GameData;
        if (gameData?.pets == null || gameData.pets.Count == 0)
        {
            Debug.LogWarning("[PetManager] Flutter 데이터에 펫이 없습니다.");
            isSpawning = false;
            yield break;
        }

        int spawnedCount = 0;
        int eggCount = 0;

        foreach (var petData in gameData.pets)
        {
            if (petData.isSpawned)
            {
                // 이미 스폰된 펫은 바로 생성
                GameObject pet = SpawnPetWithFlutterData(petData.petCardId, petData.petIntimacy, false);
                if (pet != null)
                {
                    Debug.Log($"[PetManager] Flutter 펫 직접 스폰: {petData.petCardId} (친밀도: {petData.petIntimacy})");
                    spawnedCount++;
                }
            }
            else
            {
                // firstAppearance 펫은 Egg로 생성 (유저가 터치해서 스폰)
                SpawnGiftForPet(petData.petCardId);
                Debug.Log($"[PetManager] Flutter 펫 Egg 생성: {petData.petCardId}");
                eggCount++;
            }

            yield return new WaitForSeconds(0.1f);
        }

        Debug.Log($"[PetManager] Flutter 모드 스폰 완료. 직접 스폰: {spawnedCount}마리, Egg 생성: {eggCount}마리");
        isSpawning = false;
        // hasSpawnedPets는 메서드 시작 시 이미 true로 설정됨
    }

    /// <summary>
    /// Flutter 데이터와 함께 펫 스폰 (친밀도 초기화 포함)
/// </summary>
private GameObject SpawnPetWithFlutterData(string petId, int intimacy, bool withFirstAppearanceEffect)
{
    if (!petId.StartsWith("pet_") || petId.Length < 7)
    {
        Debug.LogError($"[PetManager] 잘못된 펫 ID 형식: {petId}");
        return null;
    }

    string numberPart = petId.Substring(4);
    if (!int.TryParse(numberPart, out int petIndex))
    {
        Debug.LogError($"[PetManager] 펫 ID 파싱 오류: {petId}");
        return null;
    }

    petIndex = petIndex - 1;
    if (petIndex < 0 || petIndex >= petPrefabs.Length)
    {
        Debug.LogError($"[PetManager] 유효하지 않은 펫 인덱스: {petIndex}, ID: {petId}");
        return null;
    }

    Vector3 spawnPosition = GetNextSpawnPosition();
    spawnPosition.y += 0.5f;
    Quaternion rotation = Quaternion.Euler(0, 180, 0);

    GameObject pet = Instantiate(petPrefabs[petIndex], spawnPosition, rotation);

    // 친밀도 설정
    PetController controller = pet.GetComponent<PetController>();
    if (controller != null && controller.Needs != null)
    {
        controller.Needs.SetAffection(intimacy);

        // FlutterBridge에 펫 등록 (친밀도 동기화용)
        if (FlutterBridge.Instance != null)
        {
            FlutterBridge.Instance.RegisterSpawnedPet(petId, controller);
        }
    }

    if (withFirstAppearanceEffect)
    {
        ApplyFirstAppearanceEffect(pet);
    }

    return pet;
}
    // 스폰 스팟 초기화
    private void InitializeSpawnSpots()
    {
        // 스폰 스팟이 설정되지 않았으면 씬에서 찾기
        if (spawnSpots == null || spawnSpots.Length == 0)
        {
            spawnSpots = FindObjectsOfType<PetSpawnSpot>();
            if (spawnSpots != null && spawnSpots.Length > 0)
            {
                // 인덱스 순으로 정렬
                System.Array.Sort(spawnSpots, (a, b) => a.SpotIndex.CompareTo(b.SpotIndex));
        // Debug.Log($"[PetManager] 씬에서 {spawnSpots.Length}개의 스폰 스팟을 찾았습니다.");
            }
            else
            {
                Debug.LogWarning("[PetManager] 스폰 스팟을 찾을 수 없습니다. 랜덤 위치로 스폰됩니다.");
                useSpawnSpots = false;
            }
        }

        // 스폰 스팟 사용 카운트 초기화
        spawnSpotUsageCount.Clear();
        if (spawnSpots != null)
        {
            for (int i = 0; i < spawnSpots.Length; i++)
            {
                spawnSpotUsageCount[i] = 0;
                if (spawnSpots[i] != null)
                {
                    spawnSpots[i].ResetPetCount();
                }
            }
        }
    }

    // 선택된 펫을 효과와 함께 스폰하는 코루틴 (새로 추가)
    private IEnumerator SpawnSelectedPetsWithEffects()
    {

        // 일반 펫과 최초 등장 펫을 분리
        List<string> normalPets = new List<string>();
        List<string> firstAppearancePets = new List<string>();

        foreach (string petId in PetSelectionManager.Instance.selectedPetIds)
        {
            if (PetSelectionManager.Instance.IsPetFirstAppearance(petId))
            {
                firstAppearancePets.Add(petId);
            }
            else
            {
                normalPets.Add(petId);
            }
        }

        // 먼저 일반 펫들을 스폰
        foreach (string petId in normalPets)
        {
            SpawnPet(petId, false);
        }

        // 최초 등장 펫들을 딜레이를 두고 효과와 함께 스폰
        // 최초 등장 펫들은 선물로 스폰
        foreach (string petId in firstAppearancePets)
        {
            SpawnGiftForPet(petId);
            yield return new WaitForSeconds(giftSpawnDelay);
        }
    }

    // 특정 위치에 펫을 스폰하는 메서드
    private void SpawnPetAtPosition(string petId, Vector3 position, bool withFirstAppearanceEffect)
    {
        // 펫 ID 형식: "pet_001", "pet_002", ... 에서 숫자 부분 추출
        if (petId.StartsWith("pet_") && petId.Length >= 7)
        {
            string numberPart = petId.Substring(4); // "001", "002", ...
            if (int.TryParse(numberPart, out int petIndex))
            {
                // 인덱스는 0부터 시작하므로 1을 빼줌
                petIndex = petIndex - 1;
                
                // 유효한 인덱스인지 확인
                if (petIndex >= 0 && petIndex < petPrefabs.Length)
                {
                    // NavMesh 위의 가장 가까운 유효한 위치 찾기
                    NavMeshHit hit;
                    Vector3 spawnPosition = position;
                    
                    // 더 넓은 범위에서 NavMesh 위치 찾기
                    if (NavMesh.SamplePosition(position, out hit, 50f, NavMesh.AllAreas))
                    {
                        spawnPosition = hit.position;
                    }
                    else
                    {
                        Debug.LogWarning($"[PetManager] {petId}: 주어진 위치 근처에서 NavMesh를 찾을 수 없습니다. 대체 위치 시도 중...");
                        
                        // 스폰 중심점에서 다시 시도
                        if (NavMesh.SamplePosition(Vector3.zero, out hit, 100f, NavMesh.AllAreas))
                        {
                            spawnPosition = hit.position;
                        }
                        else
                        {
                            Debug.LogError($"[PetManager] {petId}: NavMesh를 전혀 찾을 수 없습니다!");
                        }
                    }
                    
                    // 약간 위에서 스폰하여 지면에 확실히 닿도록 함
                    spawnPosition.y += 0.5f;

                    // 180도 회전하여 카메라를 향하도록 스폰
                    Quaternion rotation = Quaternion.Euler(0, 180, 0);
                    GameObject pet = Instantiate(petPrefabs[petIndex], spawnPosition, rotation);

                    if (withFirstAppearanceEffect)
                    {
                        // 최초 등장 효과 적용
                        ApplyFirstAppearanceEffect(pet);
                    }
                    else
                    {
                    }
                }
                else
                {
                    Debug.LogError($"유효하지 않은 펫 인덱스: {petIndex}, ID: {petId}");
                }
            }
            else
            {
                Debug.LogError($"펫 ID 형식 오류: {petId}");
            }
        }
        else
        {
            Debug.LogError($"잘못된 펫 ID 형식: {petId}");
        }
    }

    // 펫을 스폰하는 메서드 (효과 옵션 추가)
    private void SpawnPet(string petId, bool withFirstAppearanceEffect)
    {
        // 펫 ID 형식: "pet_001", "pet_002", ... 에서 숫자 부분 추출
        if (petId.StartsWith("pet_") && petId.Length >= 7)
        {
            string numberPart = petId.Substring(4); // "001", "002", ...
            if (int.TryParse(numberPart, out int petIndex))
            {
                // 인덱스는 0부터 시작하므로 1을 빼줌
                petIndex = petIndex - 1;

                // 유효한 인덱스인지 확인
                if (petIndex >= 0 && petIndex < petPrefabs.Length)
                {
                    // 스폰 위치 결정
                    Vector3 spawnPosition = GetNextSpawnPosition();

                    // 약간 위에서 스폰하여 지면에 확실히 닿도록 함
                    spawnPosition.y += 0.5f;

                    // 180도 회전하여 카메라를 향하도록 스폰
                    Quaternion rotation = Quaternion.Euler(0, 180, 0);
                    GameObject pet = Instantiate(petPrefabs[petIndex], spawnPosition, rotation);

                    if (withFirstAppearanceEffect)
                    {
                        // 최초 등장 효과 적용
                        ApplyFirstAppearanceEffect(pet);
                    }
                    else
                    {
                    }
                }
                else
                {
                    Debug.LogError($"유효하지 않은 펫 인덱스: {petIndex}, ID: {petId}");
                }
            }
            else
            {
                Debug.LogError($"펫 ID 형식 오류: {petId}");
            }
        }
        else
        {
            Debug.LogError($"잘못된 펫 ID 형식: {petId}");
        }
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
        bool shouldProcess = false;
        Vector3 inputPosition = Vector3.zero;

        // 모바일 터치 입력 처리
        if (Input.touchCount > 0)
        {
            Touch touch = Input.GetTouch(0);
            if (touch.phase == TouchPhase.Began)
            {
                shouldProcess = true;
                inputPosition = touch.position;

                // 모바일에서 UI 터치 확인
                if (UnityEngine.EventSystems.EventSystem.current != null &&
                    UnityEngine.EventSystems.EventSystem.current.IsPointerOverGameObject(touch.fingerId))
                {
        // Debug.Log("[PetManager] UI가 터치를 가로챔 (모바일)");
                    return;
                }
            }
        }
        // PC 마우스 입력 처리
        else if (Input.GetMouseButtonDown(0))
        {
            shouldProcess = true;
            inputPosition = Input.mousePosition;

            // PC에서 UI 클릭 확인
            if (UnityEngine.EventSystems.EventSystem.current != null &&
                UnityEngine.EventSystems.EventSystem.current.IsPointerOverGameObject())
            {
        // Debug.Log("[PetManager] UI가 터치를 가로챔 (PC)");
                return;
            }
        }

        if (!shouldProcess) return;

        lastTouchTime = Time.time;

        if (Camera.main == null)
        {
            Debug.LogWarning("[PetManager] 메인 카메라가 없음");
            return;
        }

        Ray ray = Camera.main.ScreenPointToRay(inputPosition);

        // 모든 충돌 객체를 가져와서 가장 가까운 선물 찾기
        RaycastHit[] hits = Physics.RaycastAll(ray, Mathf.Infinity);

        if (hits.Length > 0)
        {
            // 거리순으로 정렬
            System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

            // 가장 가까운 선물 찾기
            foreach (RaycastHit hit in hits)
            {
                GameObject hitObject = hit.collider.gameObject;

                // 터치한 오브젝트가 대기 중인 선물인지 확인
                if (pendingGifts.ContainsKey(hitObject))
                {
                    string petId = pendingGifts[hitObject];
        // Debug.Log($"[PetManager] 선물 터치 감지: {petId}");
                    StartCoroutine(OpenGift(hitObject, petId));
                    return;
                }
            }

            // 선물이 아닌 다른 객체를 터치한 경우
        // Debug.Log($"[PetManager] 터치한 객체: {hits[0].collider.gameObject.name} (레이어: {LayerMask.LayerToName(hits[0].collider.gameObject.layer)})");
        }
        else
        {
        // Debug.Log("[PetManager] Raycast가 아무것도 감지하지 못함");
        }
    }
    
    private void SpawnGiftForPet(string petId)
    {
        // 배열 null 체크 및 빈 배열 체크
        if (giftPrefabs == null || giftPrefabs.Length == 0)
        {
            Debug.LogError("선물 프리팹이 할당되지 않았습니다!");
            SpawnPet(petId, true);
            return;
        }

        // 배열에서 null이 아닌 프리팹들만 필터링
        List<GameObject> validPrefabs = new List<GameObject>();
        foreach (GameObject prefab in giftPrefabs)
        {
            if (prefab != null)
            {
                validPrefabs.Add(prefab);
            }
        }

        // 유효한 프리팹이 없는 경우
        if (validPrefabs.Count == 0)
        {
            Debug.LogError("유효한 선물 프리팹이 없습니다!");
            SpawnPet(petId, true);
            return;
        }

        // 유효한 프리팹 중에서 랜덤 선택
        GameObject selectedGiftPrefab = validPrefabs[Random.Range(0, validPrefabs.Count)];

        // 선물 스폰 위치 - 스폰 스팟 사용
        Vector3 giftPosition = GetNextSpawnPosition();
        // giftPosition.y += 0.5f; // 지면에서 약간 위에 배치

        GameObject gift = Instantiate(selectedGiftPrefab, giftPosition, selectedGiftPrefab.transform.rotation);

        // 선물 회전 애니메이션
        StartCoroutine(RotateGift(gift));

        // 대기 중인 선물 목록에 추가
        pendingGifts.Add(gift, petId);

    }
    
    private IEnumerator RotateGift(GameObject gift)
    {
        // 첫 프레임 대기 - Transform 초기화 완료를 위해 중요!
        yield return null;

        if (gift == null || !pendingGifts.ContainsKey(gift))
            yield break;

        // 초기 위치 저장 (누적 방지를 위해 필수)
        Vector3 originalPosition = gift.transform.position;

        while (gift != null && pendingGifts.ContainsKey(gift))
        {
            gift.transform.Rotate(0, 30 * Time.deltaTime, 0);

            // 위아래 흔들림 효과 - 절대 위치로 설정 (누적 방지)
            float bobbing = Mathf.Sin(Time.time * 2f) * 0.1f;
            gift.transform.position = originalPosition + Vector3.up * bobbing;

            yield return null;
        }
    }
    
    private IEnumerator OpenGift(GameObject gift, string petId)
    {
        // 선물 위치 저장
        Vector3 giftPosition = gift.transform.position;

        // 지면 높이 확인
        RaycastHit groundHit;
        if (Physics.Raycast(giftPosition + Vector3.up * 10f, Vector3.down, out groundHit, 20f))
        {
            giftPosition.y = groundHit.point.y;
        }
        else
        {
            giftPosition.y = 0; // 기본값
        }

        // 선물을 대기 목록에서 제거
        pendingGifts.Remove(gift);


        // 축하 효과 파티클 실행
        if (celebrationEffectPrefab != null)
        {
            GameObject celebration = Instantiate(celebrationEffectPrefab, gift.transform.position+ Vector3.up * 2f, Quaternion.identity);
            celebration.transform.localScale = Vector3.one * 3f;
            Destroy(celebration, 5f);
        }

        // 불꽃놀이 효과
        if (fireworkPrefabs != null && fireworkPrefabs.Count > 0)
        {
            StartCoroutine(LaunchFireworks(gift.transform.position));
        }

        // 선물 제거 애니메이션
        yield return StartCoroutine(RemoveGiftWithAnimation(gift));

        // 잠시 대기
        yield return new WaitForSeconds(0.5f);

        // 펫 스폰 - 선물이 있던 위치에 스폰
        GameObject spawnedPet = SpawnPetAtPositionWithReturn(petId, giftPosition, true);

        // ★ Flutter 모드일 때 펫 스폰 알림 전송
        if (FlutterModeManager.Instance?.IsFlutterMode == true)
        {
            PetController controller = spawnedPet?.GetComponent<PetController>();

            // Flutter에서 받은 친밀도 적용
            int intimacy = FlutterModeManager.Instance.GetPetIntimacy(petId);
            if (controller != null && controller.Needs != null)
            {
                controller.Needs.SetAffection(intimacy);
            }

            FlutterBridge.Instance?.SendPetSpawned(petId, controller);
        }
    }

    /// <summary>
    /// 특정 위치에 펫을 스폰하고 GameObject 반환
    /// </summary>
    private GameObject SpawnPetAtPositionWithReturn(string petId, Vector3 position, bool withFirstAppearanceEffect)
    {
        if (!petId.StartsWith("pet_") || petId.Length < 7)
        {
            Debug.LogError($"잘못된 펫 ID 형식: {petId}");
            return null;
        }

        string numberPart = petId.Substring(4);
        if (!int.TryParse(numberPart, out int petIndex))
        {
            Debug.LogError($"펫 ID 형식 오류: {petId}");
            return null;
        }

        petIndex = petIndex - 1;
        if (petIndex < 0 || petIndex >= petPrefabs.Length)
        {
            Debug.LogError($"유효하지 않은 펫 인덱스: {petIndex}, ID: {petId}");
            return null;
        }

        // NavMesh 위의 가장 가까운 유효한 위치 찾기
        NavMeshHit hit;
        Vector3 spawnPosition = position;

        if (NavMesh.SamplePosition(position, out hit, 50f, NavMesh.AllAreas))
        {
            spawnPosition = hit.position;
        }
        else
        {
            Debug.LogWarning($"[PetManager] {petId}: 주어진 위치 근처에서 NavMesh를 찾을 수 없습니다.");
            if (NavMesh.SamplePosition(Vector3.zero, out hit, 100f, NavMesh.AllAreas))
            {
                spawnPosition = hit.position;
            }
        }

        spawnPosition.y += 0.5f;
        Quaternion rotation = Quaternion.Euler(0, 180, 0);
        GameObject pet = Instantiate(petPrefabs[petIndex], spawnPosition, rotation);

        if (withFirstAppearanceEffect)
        {
            ApplyFirstAppearanceEffect(pet);
        }

        return pet;
    }
    
    private IEnumerator LaunchFireworks(Vector3 spawnCenter)
    {
        if (fireworkPrefabs == null || fireworkPrefabs.Count == 0)
            yield break;
        
        for (int i = 0; i < fireworkPrefabs.Count; i++)
        {
            GameObject fireworkPrefab = fireworkPrefabs[i];
            
            if (fireworkPrefab != null)
            {
                Vector3 randomOffset = new Vector3(Random.Range(-2f, 2f), 0, Random.Range(-2f, 2f));
                Vector3 spawnPosition = spawnCenter + randomOffset;
                
                GameObject firework = Instantiate(fireworkPrefab, spawnPosition, Quaternion.identity);
                Destroy(firework, 5f);
                
                yield return new WaitForSeconds(0.3f);
            }
        }
    }
    
    private IEnumerator RemoveGiftWithAnimation(GameObject gift)
    {
        Vector3 originalScale = gift.transform.localScale;
        const float duration = 0.5f;
        float elapsed = 0f;
        
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / duration;
            
            gift.transform.localScale = Vector3.Lerp(originalScale, Vector3.zero, t);
            gift.transform.Rotate(0, 360 * Time.deltaTime * 2, 0);
            gift.transform.position += Vector3.up * Time.deltaTime * 2;
            
            yield return null;
        }
        
        Destroy(gift);
    }

    // 최초 등장 효과를 적용하는 메서드 (새로 추가)
    private void ApplyFirstAppearanceEffect(GameObject pet)
    {
        // 1. 파티클 효과 (프리팹이 있는 경우)
        if (firstAppearanceEffectPrefab != null)
        {
            GameObject effect = Instantiate(firstAppearanceEffectPrefab, pet.transform.position, Quaternion.identity);
            Destroy(effect, 3f); // 3초 후 제거
        }

        // 2. 스케일 애니메이션 효과
        StartCoroutine(ScaleInEffect(pet));

        // 3. 글로우 효과 (Material이 있는 경우)
        StartCoroutine(GlowEffect(pet));
    }

    // 스케일 인 효과 코루틴 (새로 추가)
    private IEnumerator ScaleInEffect(GameObject pet)
    {
        Vector3 originalScale = pet.transform.localScale;
        pet.transform.localScale = Vector3.zero;

        float duration = 1f;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float progress = elapsed / duration;
            
            // 이징 효과 (Ease Out Back)
            float scale = EaseOutBack(progress);
            pet.transform.localScale = originalScale * scale;
            
            yield return null;
        }

        pet.transform.localScale = originalScale;
    }

    // 글로우 효과 코루틴 (Material 인스턴스 관리 개선)
    private IEnumerator GlowEffect(GameObject pet)
    {
        Renderer[] renderers = pet.GetComponentsInChildren<Renderer>();
        List<Material> createdMaterials = new List<Material>();
        Dictionary<Renderer, Color> originalEmissions = new Dictionary<Renderer, Color>();
        bool hasEmissiveMaterial = false;

        try
        {
            // 머터리얼이 Emission을 지원하는지 확인 및 원본 색상 저장
            foreach (Renderer renderer in renderers)
            {
                // renderer.material 접근 시 Material 인스턴스가 생성됨
                Material mat = renderer.material;
                if (mat != null && mat.HasProperty("_EmissionColor"))
                {
                    createdMaterials.Add(mat);
                    originalEmissions[renderer] = mat.GetColor("_EmissionColor");
                    hasEmissiveMaterial = true;
                }
            }

            if (hasEmissiveMaterial)
            {
                float duration = 2f;
                float elapsed = 0f;
                Color glowColor = Color.yellow * 2f; // 밝은 노란색

                while (elapsed < duration && pet != null)
                {
                    elapsed += Time.deltaTime;
                    float intensity = Mathf.Sin(elapsed * Mathf.PI / duration);

                    foreach (var kvp in originalEmissions)
                    {
                        if (kvp.Key != null && kvp.Key.material != null)
                        {
                            kvp.Key.material.SetColor("_EmissionColor", Color.Lerp(kvp.Value, glowColor, intensity));
                        }
                    }

                    yield return null;
                }

                // 원래 색상으로 복원
                foreach (var kvp in originalEmissions)
                {
                    if (kvp.Key != null && kvp.Key.material != null)
                    {
                        kvp.Key.material.SetColor("_EmissionColor", kvp.Value);
                    }
                }
            }
        }
        finally
        {
            // Material 인스턴스 정리 (메모리 누수 방지)
            // 주의: 실제로는 Material이 Renderer에 의해 관리되므로
            // 여기서 Destroy하면 안됨. Renderer가 파괴될 때 자동 정리됨.
            // 하지만 리스트는 정리해야 함
            createdMaterials.Clear();
            originalEmissions.Clear();
        }
    }

    // Ease Out Back 이징 함수 (새로 추가)
    private float EaseOutBack(float t)
    {
        float c1 = 1.70158f;
        float c3 = c1 + 1f;
        return 1f + c3 * Mathf.Pow(t - 1f, 3f) + c1 * Mathf.Pow(t - 1f, 2f);
    }

    // 기존 메서드들...
    private void SpawnAllPets()
    {
        for (int i = 0; i < petPrefabs.Length && i < maxPets; i++)
        {
            if (petPrefabs[i] != null)
            {
                Vector3 randomPosition = GetRandomPositionOnNavMesh();

                // 180도 회전하여 카메라를 향하도록 스폰
                Quaternion rotation = Quaternion.Euler(0, 180, 0);
                GameObject pet = Instantiate(petPrefabs[i], randomPosition, rotation);
            }
        }
    }

    // 다음 스폰 위치를 가져오는 메서드
    private Vector3 GetNextSpawnPosition()
    {
        // 스폰 스팟 사용 모드이고 유효한 스폰 스팟이 있는 경우
        if (useSpawnSpots && spawnSpots != null && spawnSpots.Length > 0)
        {
            // 현재 스폰 스팟 인덱스 결정
            int spotIndex = nextSpawnSpotIndex % spawnSpots.Length;
            PetSpawnSpot currentSpot = spawnSpots[spotIndex];

            if (currentSpot != null)
            {
                // 이 스팟에서 몇 번째로 스폰되는지 확인
                int usageCount = spawnSpotUsageCount.ContainsKey(spotIndex) ? spawnSpotUsageCount[spotIndex] : 0;

                // 스폰 위치 계산 (중복 시 오프셋 적용)
                Vector3 spawnPosition = currentSpot.GetSpawnPosition(usageCount);

                // NavMesh 위치 검증
                NavMeshHit hit;
                if (NavMesh.SamplePosition(spawnPosition, out hit, 10f, NavMesh.AllAreas))
                {
                    spawnPosition = hit.position;
                }

                // 사용 카운트 증가
                spawnSpotUsageCount[spotIndex] = usageCount + 1;
                currentSpot.OnPetSpawned();

                // 다음 스폰 스팟 인덱스로 이동
                nextSpawnSpotIndex++;

        // Debug.Log($"[PetManager] 스폰 스팟 #{spotIndex + 1}에 펫 스폰 (사용 횟수: {usageCount + 1})");
                return spawnPosition;
            }
        }

        // 스폰 스팟을 사용하지 않거나 실패한 경우 랜덤 위치 반환
        return GetRandomPositionOnNavMesh();
    }

    private Vector3 GetRandomPositionOnNavMesh()
    {
        // 여러 번 시도하여 유효한 위치 찾기
        for (int i = 0; i < 10; i++)
        {
            Vector3 randomDirection = Random.insideUnitSphere * spawnRadius;
            randomDirection += transform.position;
            randomDirection.y = 0; // y축은 0으로 설정

            NavMeshHit hit;
            if (NavMesh.SamplePosition(randomDirection, out hit, spawnRadius, NavMesh.AllAreas))
            {
                return hit.position;
            }
        }

        // 모든 시도가 실패하면 중심점에서 가장 가까운 NavMesh 위치 반환
        NavMeshHit centerHit;
        if (NavMesh.SamplePosition(transform.position, out centerHit, spawnRadius * 2, NavMesh.AllAreas))
        {
            Debug.LogWarning($"[PetManager] 랜덤 위치를 찾지 못해 중심점 근처로 스폰합니다.");
            return centerHit.position;
        }

        // 그래도 실패하면 기본 위치
        Debug.LogError($"[PetManager] NavMesh 위치를 전혀 찾을 수 없습니다!");
        return transform.position;
    }
    
    private void OnDestroy()
    {
        // 이벤트 구독 해제
        if (FlutterModeManager.Instance != null)
        {
            FlutterModeManager.Instance.OnFlutterDataReceived -= OnFlutterDataReceived;
        }

        // 정리 작업
        pendingGifts.Clear();
    }

    // 씬 뷰에서 스폰 스팟 시각화
    private void OnDrawGizmos()
    {
        if (!useSpawnSpots) return;

        // 런타임이 아닐 때도 스폰 스팟 찾기
        if (spawnSpots == null || spawnSpots.Length == 0)
        {
            spawnSpots = FindObjectsOfType<PetSpawnSpot>();
            if (spawnSpots != null && spawnSpots.Length > 0)
            {
                System.Array.Sort(spawnSpots, (a, b) => a.SpotIndex.CompareTo(b.SpotIndex));
            }
        }

        if (spawnSpots != null)
        {
            // 스폰 스팟 간 연결선 그리기
            Gizmos.color = new Color(0.5f, 0.5f, 1f, 0.3f);
            for (int i = 0; i < spawnSpots.Length - 1; i++)
            {
                if (spawnSpots[i] != null && spawnSpots[i + 1] != null)
                {
                    Gizmos.DrawLine(spawnSpots[i].transform.position, spawnSpots[i + 1].transform.position);
                }
            }

            // 첫 번째와 마지막 스폰 스팟 연결 (순환)
            if (spawnSpots.Length > 2 && spawnSpots[0] != null && spawnSpots[spawnSpots.Length - 1] != null)
            {
                Gizmos.color = new Color(0.5f, 0.5f, 1f, 0.2f);
                Gizmos.DrawLine(spawnSpots[0].transform.position, spawnSpots[spawnSpots.Length - 1].transform.position);
            }
        }
    }
}