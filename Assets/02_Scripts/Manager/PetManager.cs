using UnityEngine;
using UnityEngine.AI;
using System.Collections;
using System.Collections.Generic;

public class PetManager : MonoBehaviour
{
    public GameObject[] petPrefabs;  // 60개의 펫 프리팹 배열
    public float spawnRadius = 50f;  // 스폰 반경
    public int maxPets = 50;

    [Header("최초 등장 효과")]
    public GameObject firstAppearanceEffectPrefab; // 최초 등장 효과 프리팹 (옵션)
    public float firstAppearanceDelay = 0.5f; // 최초 등장 펫들 사이의 딜레이

    [Header("선물 시스템")]
    public GameObject giftPrefab; // 선물 프리팹
    public GameObject celebrationEffectPrefab; // 축하 효과 파티클 프리팹
    public float giftSpawnDelay = 0.5f; // 선물 스폰 딜레이
    public List<GameObject> fireworkPrefabs = new List<GameObject>(); // 불꽃놀이 프리팹들

    [Header("NavMesh 대기 설정")]
    public float navMeshWaitTime = 3f; // NavMesh 베이크 대기 시간

    // 대기 중인 선물들과 해당 펫 정보를 저장하는 딕셔너리
    private Dictionary<GameObject, string> pendingGifts = new Dictionary<GameObject, string>();
    
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
    // EnvironmentManager가 환경 스폰과 NavMesh 베이크를 완료할 때까지 기다린 후 펫 스폰
    StartCoroutine(WaitForEnvironmentAndSpawnPets());
}

// 새로 추가: 환경 준비 완료까지 기다리는 코루틴
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
    
    // 이제 펫 스폰 시작
    if (PetSelectionManager.Instance != null && PetSelectionManager.Instance.selectedPetIds.Count > 0)
    {
        StartCoroutine(SpawnSelectedPetsWithEffects());
    }
    else
    {
        Debug.LogWarning("선택된 펫이 없거나 PetSelectionManager가 없습니다. 기본 동작으로 모든 펫을 스폰합니다.");
        SpawnAllPets();
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
                    
                    GameObject pet = Instantiate(petPrefabs[petIndex], spawnPosition, Quaternion.identity);

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
                    // 랜덤 위치에 펫 생성
                    Vector3 spawnPosition = GetRandomPositionOnNavMesh();
                    
                    // 약간 위에서 스폰하여 지면에 확실히 닿도록 함
                    spawnPosition.y += 0.5f;
                    
                    GameObject pet = Instantiate(petPrefabs[petIndex], spawnPosition, Quaternion.identity);

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
        if (Input.GetMouseButtonDown(0))
        {
            lastTouchTime = Time.time;
            
            if (Camera.main == null) return;
            
            Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
            RaycastHit hit;
            
            // Ignore Raycast 레이어를 제외한 모든 레이어와 충돌 검사
            // (PreferredZone 등 무시해야 할 오브젝트 제외)
            int layerMask = ~LayerMask.GetMask("Ignore Raycast");
            
            if (Physics.Raycast(ray, out hit, Mathf.Infinity, layerMask))
            {
                GameObject hitObject = hit.collider.gameObject;
                
                // 터치한 오브젝트가 대기 중인 선물인지 확인
                if (pendingGifts.ContainsKey(hitObject))
                {
                    string petId = pendingGifts[hitObject];
                    StartCoroutine(OpenGift(hitObject, petId));
                }
            }
        }
    }
    
    private void SpawnGiftForPet(string petId)
    {
        if (giftPrefab == null)
        {
            Debug.LogError("선물 프리팹이 할당되지 않았습니다!");
            SpawnPet(petId, true);
            return;
        }
        
        // 선물 스폰 위치
        Vector3 giftPosition = GetRandomPositionOnNavMesh();
        giftPosition.y += 5f; // 선물을 공중에 띄움
        
        GameObject gift = Instantiate(giftPrefab, giftPosition, giftPrefab.transform.rotation);
        
        // 선물 회전 애니메이션
        StartCoroutine(RotateGift(gift));
        
        // 대기 중인 선물 목록에 추가
        pendingGifts.Add(gift, petId);
        
    }
    
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
            GameObject celebration = Instantiate(celebrationEffectPrefab, gift.transform.position, Quaternion.identity);
            celebration.transform.localScale = Vector3.one * 1.5f;
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
        SpawnPetAtPosition(petId, giftPosition, true);
        
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

    // 글로우 효과 코루틴 (새로 추가)
    private IEnumerator GlowEffect(GameObject pet)
    {
        Renderer[] renderers = pet.GetComponentsInChildren<Renderer>();
        Color originalEmission = Color.black;
        bool hasEmissiveMaterial = false;

        // 머터리얼이 Emission을 지원하는지 확인
        foreach (Renderer renderer in renderers)
        {
            if (renderer.material.HasProperty("_EmissionColor"))
            {
                originalEmission = renderer.material.GetColor("_EmissionColor");
                hasEmissiveMaterial = true;
                break;
            }
        }

        if (hasEmissiveMaterial)
        {
            float duration = 2f;
            float elapsed = 0f;
            Color glowColor = Color.yellow * 2f; // 밝은 노란색

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float intensity = Mathf.Sin(elapsed * Mathf.PI / duration);
                
                foreach (Renderer renderer in renderers)
                {
                    if (renderer.material.HasProperty("_EmissionColor"))
                    {
                        renderer.material.SetColor("_EmissionColor", Color.Lerp(originalEmission, glowColor, intensity));
                    }
                }
                
                yield return null;
            }

            // 원래 색상으로 복원
            foreach (Renderer renderer in renderers)
            {
                if (renderer.material.HasProperty("_EmissionColor"))
                {
                    renderer.material.SetColor("_EmissionColor", originalEmission);
                }
            }
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
                GameObject pet = Instantiate(petPrefabs[i], randomPosition, Quaternion.identity);
            }
        }
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
        // 정리 작업
        pendingGifts.Clear();
    }
}