using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using Unity.AI.Navigation;

public class EnvironmentManager : MonoBehaviour
{
    [Header("환경 프리팹")]
    public GameObject foodstoreEnvironment;     // 음식가게
    public GameObject orchardEnvironment;       // 과수원
    public GameObject berryfieldEnvironment;    // 산딸기밭
    public GameObject honeypotEnvironment;      // 꿀통
    public GameObject sunflowerEnvironment;     // 해바라기
    public GameObject cucumberEnvironment;      // 오이밭
    public GameObject ricefieldEnvironment;     // 논
    public GameObject watermelonEnvironment;    // 수박밭
    public GameObject cornfieldEnvironment;     // 옥수수밭
    public GameObject forestEnvironment;        // 숲
    public GameObject pondEnvironment;          // 물웅덩이
    public GameObject flowersEnvironment;       // 꽃
    public GameObject fenceEnvironment;         // 초식동물용울타리

    [Header("최초 등장 효과")]
    public GameObject firstAppearanceEffectPrefab; // 최초 등장 효과 프리팹 (옵션)
    public float firstAppearanceDelay = 1f; // 최초 등장 환경들 사이의 딜레이

    [Header("선물 시스템")]
    public GameObject giftPrefab;              // 선물 프리팹
    public GameObject celebrationEffectPrefab; // 축하 효과 파티클 프리팹
    public float giftSpawnDelay = 0.5f;        // 선물 스폰 딜레이

    [Header("NavMesh 설정")]
    public NavMeshSurface navMeshSurface;     // NavMesh Surface 참조
    public float navMeshBakeDelay = 0.5f;      // NavMesh 베이크 전 추가 대기 시간
    
    // 환경 ID와 프리팹 연결을 위한 딕셔너리
    private Dictionary<string, GameObject> environmentPrefabs = new Dictionary<string, GameObject>();
    
    // 대기 중인 선물들과 해당 환경 정보를 저장하는 딕셔너리
    private Dictionary<GameObject, string> pendingGifts = new Dictionary<GameObject, string>();
    
    // TerrainTextureSwitchManager 캐싱
    private TerrainTextureSwitchManager terrainManager;
    
    // 터치 처리 최적화를 위한 변수
    private float lastTouchTime;
    private const float TOUCH_COOLDOWN = 0.1f; // 터치 쿨다운

    // 생성된 환경 오브젝트들을 추적
    private List<GameObject> spawnedEnvironments = new List<GameObject>();
public bool IsInitializationComplete { get; private set; } = false;

    private void Awake()
    {
        // 딕셔너리 초기화
        InitializeEnvironmentPrefabs();
        
        // NavMeshSurface 찾기
        if (navMeshSurface == null)
        {
            GameObject terrain = GameObject.Find("Terrain");
            if (terrain != null)
            {
                navMeshSurface = terrain.GetComponent<NavMeshSurface>();
            }
        }
    }

    private void InitializeEnvironmentPrefabs()
    {
        environmentPrefabs.Clear();
        environmentPrefabs.Add("env_foodstore", foodstoreEnvironment);
        environmentPrefabs.Add("env_orchard", orchardEnvironment);
        environmentPrefabs.Add("env_berryfield", berryfieldEnvironment);
        environmentPrefabs.Add("env_honeypot", honeypotEnvironment);
        environmentPrefabs.Add("env_sunflower", sunflowerEnvironment);
        environmentPrefabs.Add("env_cucumber", cucumberEnvironment);
        environmentPrefabs.Add("env_ricefield", ricefieldEnvironment);
        environmentPrefabs.Add("env_watermelon", watermelonEnvironment);
        environmentPrefabs.Add("env_cornfield", cornfieldEnvironment);
        environmentPrefabs.Add("env_forest", forestEnvironment);
        environmentPrefabs.Add("env_pond", pondEnvironment);
        environmentPrefabs.Add("env_flowers", flowersEnvironment);
        environmentPrefabs.Add("env_fence", fenceEnvironment);
    }

    private void Start()
{
    // 초기화 시작
    IsInitializationComplete = false;
    
    // EnvironmentSelectionManager가 존재하는지 확인
    if (EnvironmentSelectionManager.Instance != null && 
        EnvironmentSelectionManager.Instance.selectedEnvironmentIds.Count > 0)
    {
        // 선택된 환경이 있는 경우
        StartCoroutine(WaitForTerrainManagerAndSpawn());
    }
    else
    {
        // 선택된 환경이 없거나 매니저가 없는 경우 (펫 빌리지 씬에서 직접 시작)
        Debug.LogWarning("EnvironmentSelectionManager가 없거나 선택된 환경이 없습니다. 기본 환경을 스폰합니다.");
        StartCoroutine(SpawnDefaultEnvironments());
    }
}

// 새로 추가: 기본 환경 스폰 (펫 빌리지 씬에서 직접 시작할 때)
private IEnumerator SpawnDefaultEnvironments()
{
    // TerrainTextureSwitchManager 대기
    float waitTime = 0f;
    const float maxWaitTime = 5f;
    
    while (terrainManager == null && waitTime < maxWaitTime)
    {
        terrainManager = TerrainTextureSwitchManager.GetInstance();
        if (terrainManager == null)
        {
            yield return new WaitForSeconds(0.1f);
            waitTime += 0.1f;
        }
    }
    
    // 기본 환경들 스폰 (예: 몇 가지 기본 환경)
    string[] defaultEnvironments = {
        "env_forest",
        "env_pond", 
        "env_flowers"
    };
    
    Debug.Log("기본 환경 스폰 시작...");
    
    foreach (string envId in defaultEnvironments)
    {
        if (environmentPrefabs.ContainsKey(envId) && environmentPrefabs[envId] != null)
        {
            yield return StartCoroutine(SpawnEnvironmentCoroutine(envId, false));
            yield return new WaitForSeconds(0.2f);
        }
    }
    
    // 물리 엔진 동기화
    yield return new WaitForSeconds(1f);
    Physics.SyncTransforms();
    
    // NavMesh 베이크
    Debug.Log("기본 환경 NavMesh 베이크 시작...");
    yield return StartCoroutine(BakeNavMeshAfterDelay());
    
    // 초기화 완료 플래그 설정
    IsInitializationComplete = true;
    Debug.Log("기본 환경 초기화 완료!");
}

    private IEnumerator WaitForTerrainManagerAndSpawn()
{
    // TerrainTextureSwitchManager가 준비될 때까지 대기
    float waitTime = 0f;
    const float maxWaitTime = 5f;
    
    while (terrainManager == null && waitTime < maxWaitTime)
    {
        terrainManager = TerrainTextureSwitchManager.GetInstance();
        if (terrainManager == null)
        {
            yield return new WaitForSeconds(0.1f);
            waitTime += 0.1f;
        }
    }
    
    if (terrainManager == null)
    {
        Debug.LogError("TerrainTextureSwitchManager를 찾을 수 없습니다!");
        yield return StartCoroutine(BakeNavMeshAfterDelay());
        IsInitializationComplete = true; // 초기화 완료 설정
        yield break;
    }
    
    yield return new WaitForSeconds(0.2f);
    
    // 선택된 환경 스폰
    yield return StartCoroutine(SpawnSelectedEnvironmentsWithEffects());
    
    // 초기화 완료 플래그 설정
    IsInitializationComplete = true;
    Debug.Log("선택된 환경 초기화 완료!");
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

    private IEnumerator SpawnEnvironmentCoroutine(string environmentId, bool withFirstAppearanceEffect = false)
    {
        // 환경 ID에 해당하는 프리팹 가져오기
        if (!environmentPrefabs.ContainsKey(environmentId))
        {
            Debug.LogError($"알 수 없는 환경 ID: {environmentId}");
            yield break;
        }
        
        GameObject prefab = environmentPrefabs[environmentId];
        
        // 프리팹 존재 여부 확인
        if (prefab == null)
        {
            Debug.LogError($"환경 프리팹이 할당되지 않았습니다: {environmentId}");
            yield break;
        }
        
        // 프리팹 그대로 인스턴스화 (프리팹의 위치/회전 사용)
        GameObject environment = Instantiate(prefab);
        spawnedEnvironments.Add(environment);

        // 프레임 대기 - 오브젝트가 완전히 생성되도록
        yield return null;
        yield return new WaitForFixedUpdate();

        // TerrainTextureSwitchManager에서 해당 환경의 토글 끄기
        if (terrainManager != null)
        {
            terrainManager.DisableGroupByEnvironmentId(environmentId);
            Debug.Log($"환경 {environmentId} 스폰과 동시에 지형 토글을 껐습니다.");
        }
        else
        {
            Debug.LogError("TerrainTextureSwitchManager를 찾을 수 없습니다!");
        }
 // ★ 펫 유인 시스템 호출 추가
    if (EnvironmentPetAttractor.Instance != null)
    {
        EnvironmentPetAttractor.Instance.OnEnvironmentSpawned(environmentId, environment.transform.position);
    }
        if (withFirstAppearanceEffect)
        {
            ApplyFirstAppearanceEffect(environment);
            Debug.Log($"최초 등장 환경 생성 완료: {environmentId}");
        }
        else
        {
            Debug.Log($"일반 환경 생성 완료: {environmentId}");
        }

        // 물리 엔진 동기화
        Physics.SyncTransforms();
    }

    private void SpawnEnvironment(string environmentId, bool withFirstAppearanceEffect = false)
    {
        StartCoroutine(SpawnEnvironmentCoroutine(environmentId, withFirstAppearanceEffect));
    }

    private void HandleGiftTouch()
    {
        if (Input.GetMouseButtonDown(0))
        {
            lastTouchTime = Time.time;
            
            // 카메라가 없으면 처리하지 않음
            if (Camera.main == null) return;
            
            Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
            RaycastHit hit;

            if (Physics.Raycast(ray, out hit))
            {
                GameObject hitObject = hit.collider.gameObject;
                
                // 터치한 오브젝트가 대기 중인 선물인지 확인
                if (pendingGifts.ContainsKey(hitObject))
                {
                    string environmentId = pendingGifts[hitObject];
                    StartCoroutine(OpenGift(hitObject, environmentId));
                }
            }
        }
    }

    private IEnumerator OpenGift(GameObject gift, string environmentId)
    {
        // 선물을 대기 목록에서 제거
        pendingGifts.Remove(gift);

        // 축하 효과 파티클 실행
        if (celebrationEffectPrefab != null)
        {
            GameObject celebration = Instantiate(celebrationEffectPrefab, gift.transform.position, Quaternion.identity);
            
            // 축하 효과 크기 조정
            const float scale = 1.5f;
            celebration.transform.localScale = Vector3.one * scale;
            
            // 5초 후 축하 효과 제거
            Destroy(celebration, 5f);
        }

        // 선물 제거 애니메이션
        yield return StartCoroutine(RemoveGiftWithAnimation(gift));

        // 잠시 대기
        yield return new WaitForSeconds(0.5f);

        // 환경 스폰 (코루틴으로 실행하고 완료 대기)
        yield return StartCoroutine(SpawnEnvironmentCoroutine(environmentId, true));
 // ★ 선물로 나온 환경도 펫 유인
    GameObject spawnedEnv = spawnedEnvironments[spawnedEnvironments.Count - 1];
    if (EnvironmentPetAttractor.Instance != null && spawnedEnv != null)
    {
        yield return new WaitForSeconds(0.5f); // 약간의 딜레이
        EnvironmentPetAttractor.Instance.OnEnvironmentSpawned(environmentId, spawnedEnv.transform.position);
    }
        // 환경이 완전히 배치될 때까지 추가 대기
        yield return new WaitForSeconds(1f);

        // 물리 엔진 동기화
        Physics.SyncTransforms();

        // NavMesh 재베이크
        yield return StartCoroutine(BakeNavMeshAfterDelay());

        Debug.Log($"선물을 열어 환경이 나타났습니다: {environmentId}");
    }

    private IEnumerator RemoveGiftWithAnimation(GameObject gift)
    {
        Vector3 originalScale = gift.transform.localScale;
        const float duration = 0.5f;
        float elapsed = 0f;

        // 선물이 회전하면서 사라지는 효과
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / duration;

            // 스케일 감소
            gift.transform.localScale = Vector3.Lerp(originalScale, Vector3.zero, t);
            
            // 회전 효과
            gift.transform.Rotate(0, 360 * Time.deltaTime * 2, 0);
            
            // 위로 약간 이동
            gift.transform.position += Vector3.up * Time.deltaTime * 2;

            yield return null;
        }

        // 선물 오브젝트 제거
        Destroy(gift);
    }

    private IEnumerator SpawnSelectedEnvironmentsWithEffects()
    {
        List<string> selectedIds = EnvironmentSelectionManager.Instance.selectedEnvironmentIds;

        Debug.Log($"선택된 환경 수: {selectedIds.Count}");

        // 일반 환경과 최초 등장 환경을 분리
        List<string> normalEnvironments = new List<string>();
        List<string> firstAppearanceEnvironments = new List<string>();

        if (selectedIds.Count > 0)
        {
            foreach (string environmentId in selectedIds)
            {
                if (EnvironmentSelectionManager.Instance.IsEnvironmentFirstAppearance(environmentId))
                {
                    firstAppearanceEnvironments.Add(environmentId);
                }
                else
                {
                    normalEnvironments.Add(environmentId);
                }
            }

            // 먼저 일반 환경들을 모두 스폰 (코루틴으로 실행)
            List<Coroutine> spawnCoroutines = new List<Coroutine>();
            foreach (string environmentId in normalEnvironments)
            {
                Coroutine spawnCoroutine = StartCoroutine(SpawnEnvironmentCoroutine(environmentId, false));
                spawnCoroutines.Add(spawnCoroutine);

                // 각 스폰 사이에 약간의 딜레이
                yield return new WaitForSeconds(0.1f);
            }

            // 모든 스폰 코루틴이 완료될 때까지 대기
            foreach (var coroutine in spawnCoroutines)
            {
                yield return coroutine;
            }
        }

        // 모든 일반 환경 스폰이 완료된 후 추가 대기
        yield return new WaitForSeconds(1f);

        // 물리 엔진 동기화
        Physics.SyncTransforms();

        // 초기 NavMesh 베이크 (일반 환경이 있든 없든 항상 실행)
        Debug.Log("초기 NavMesh 베이크 시작...");
        yield return StartCoroutine(BakeNavMeshAfterDelay());

        // 최초 등장 환경들은 선물로 스폰
        foreach (string environmentId in firstAppearanceEnvironments)
        {
            SpawnGiftForEnvironment(environmentId);
            yield return new WaitForSeconds(giftSpawnDelay);
        }
            IsInitializationComplete = true;

    }

    private void SpawnGiftForEnvironment(string environmentId)
    {
        // 환경 ID에 해당하는 프리팹 가져오기
        if (!environmentPrefabs.ContainsKey(environmentId))
        {
            Debug.LogError($"알 수 없는 환경 ID: {environmentId}");
            return;
        }
        
        GameObject environmentPrefab = environmentPrefabs[environmentId];
        
        // 프리팹 존재 여부 확인
        if (environmentPrefab == null)
        {
            Debug.LogError($"환경 프리팹이 할당되지 않았습니다: {environmentId}");
            return;
        }

        // 선물 프리팹 존재 여부 확인
        if (giftPrefab == null)
        {
            Debug.LogError("선물 프리팹이 할당되지 않았습니다!");
            // 선물 프리팹이 없으면 바로 환경 스폰하고 베이크
            StartCoroutine(SpawnEnvironmentAndBake(environmentId));
            return;
        }

        // 환경 프리팹의 위치에 선물 생성
        Vector3 giftPosition = environmentPrefab.transform.position;
        
        // 선물을 약간 위로 띄워서 더 잘 보이게 함
        giftPosition.y += 7f;
        
        GameObject gift = Instantiate(giftPrefab, giftPosition, giftPrefab.transform.rotation);
        
        // 선물에 약간의 회전 애니메이션 추가
        StartCoroutine(RotateGift(gift));
        
        // 대기 중인 선물 목록에 추가
        pendingGifts.Add(gift, environmentId);

        Debug.Log($"환경용 선물 생성 완료: {environmentId} at {giftPosition}");
    }

    private IEnumerator SpawnEnvironmentAndBake(string environmentId)
    {
        yield return StartCoroutine(SpawnEnvironmentCoroutine(environmentId, true));
        yield return new WaitForSeconds(1f);
        Physics.SyncTransforms();
        yield return StartCoroutine(BakeNavMeshAfterDelay());
    }

    private IEnumerator RotateGift(GameObject gift)
    {
        while (gift != null && pendingGifts.ContainsKey(gift))
        {
            gift.transform.Rotate(0, 30 * Time.deltaTime, 0);
            
            // 약간의 위아래 흔들림 효과
            float bobbing = Mathf.Sin(Time.time * 2f) * 0.1f;
            gift.transform.position += Vector3.up * bobbing;
            
            yield return null;
        }
    }

    // NavMesh 베이크 코루틴
    private IEnumerator BakeNavMeshAfterDelay()
    {
        // 환경 오브젝트들이 완전히 배치될 때까지 잠시 대기
        yield return new WaitForSeconds(navMeshBakeDelay);
        
        // NavMeshSurface가 없으면 다시 찾기 시도
        if (navMeshSurface == null)
        {
            GameObject terrain = GameObject.Find("Terrain");
            if (terrain != null)
            {
                navMeshSurface = terrain.GetComponent<NavMeshSurface>();
            }
            
            // 그래도 없으면 모든 NavMeshSurface 찾기
            if (navMeshSurface == null)
            {
                navMeshSurface = FindObjectOfType<NavMeshSurface>();
            }
        }
        
        // NavMesh 베이크
        if (navMeshSurface != null)
        {
            Debug.Log("NavMesh 베이크 시작...");
            
            // 기존 NavMesh 데이터 제거
            navMeshSurface.RemoveData();
            
            // 새로운 NavMesh 베이크
            navMeshSurface.BuildNavMesh();
            
            Debug.Log("NavMesh 베이크 완료!");
        }
        else
        {
            Debug.LogError("NavMeshSurface를 찾을 수 없습니다! Terrain 오브젝트에 NavMeshSurface 컴포넌트를 추가해주세요.");
        }
    }

    private void ApplyFirstAppearanceEffect(GameObject environment)
    {
        // 1. 파티클 효과 (프리팹이 있는 경우)
        if (firstAppearanceEffectPrefab != null)
        {
            // 환경 오브젝트의 중심점 찾기
            Bounds bounds = GetEnvironmentBounds(environment);
            Vector3 effectPosition = bounds.center;
            effectPosition.y = bounds.min.y; // 바닥에 생성

            GameObject effect = Instantiate(firstAppearanceEffectPrefab, effectPosition, Quaternion.identity);
            
            // 환경 크기에 맞게 이펙트 스케일 조정
            float scale = Mathf.Max(bounds.size.x, bounds.size.z) / 10f;
            effect.transform.localScale = Vector3.one * Mathf.Clamp(scale, 0.5f, 3f);
            
            Destroy(effect, 5f); // 5초 후 제거
        }

        // 2. 스케일 애니메이션 효과
        StartCoroutine(ScaleInEffect(environment));

        // 3. 플래시 효과
        StartCoroutine(FlashEffect(environment));
    }

    private Bounds GetEnvironmentBounds(GameObject environment)
    {
        Renderer[] renderers = environment.GetComponentsInChildren<Renderer>();
        if (renderers.Length == 0)
        {
            return new Bounds(environment.transform.position, Vector3.one);
        }

        Bounds bounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++)
        {
            bounds.Encapsulate(renderers[i].bounds);
        }
        return bounds;
    }

    private IEnumerator ScaleInEffect(GameObject environment)
    {
        Vector3 originalScale = environment.transform.localScale;
        environment.transform.localScale = Vector3.zero;

        const float duration = 1.5f;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float progress = elapsed / duration;
            
            // 이징 효과 (Ease Out Elastic)
            float scale = EaseOutElastic(progress);
            environment.transform.localScale = originalScale * scale;
            
            yield return null;
        }

        environment.transform.localScale = originalScale;
    }

    private IEnumerator FlashEffect(GameObject environment)
    {
        Renderer[] renderers = environment.GetComponentsInChildren<Renderer>();
        
        // 원본 머터리얼 저장
        Material[][] originalMaterials = new Material[renderers.Length][];
        for (int i = 0; i < renderers.Length; i++)
        {
            originalMaterials[i] = renderers[i].materials;
        }

        // URP 호환 흰색 머터리얼 생성
        Material flashMaterial = CreateURPFlashMaterial();
        
        // flashMaterial이 생성되지 않았으면 플래시 효과 건너뛰기
        if (flashMaterial == null)
        {
            Debug.LogWarning("URP 플래시 머터리얼을 생성할 수 없습니다. 플래시 효과를 건너뜁니다.");
            yield break;
        }

        const float duration = 0.5f;
        const int flashCount = 3;
        
        for (int flash = 0; flash < flashCount; flash++)
        {
            // 플래시 ON
            foreach (Renderer renderer in renderers)
            {
                Material[] flashMaterials = new Material[renderer.materials.Length];
                for (int j = 0; j < flashMaterials.Length; j++)
                {
                    flashMaterials[j] = flashMaterial;
                }
                renderer.materials = flashMaterials;
            }
            
            yield return new WaitForSeconds(duration / (flashCount * 2));
            
            // 플래시 OFF (원본 복원)
            for (int i = 0; i < renderers.Length; i++)
            {
                if (renderers[i] != null)
                {
                    renderers[i].materials = originalMaterials[i];
                }
            }
            
            yield return new WaitForSeconds(duration / (flashCount * 2));
        }

        // 플래시 머터리얼 정리
        if (flashMaterial != null)
        {
            DestroyImmediate(flashMaterial);
        }
    }

    private Material CreateURPFlashMaterial()
    {
        Shader urpShader = null;
        
        // URP 셰이더 찾기 (우선순위 순)
        string[] urpShaderNames = {
            "Universal Render Pipeline/Lit",
            "Universal Render Pipeline/Simple Lit", 
            "Universal Render Pipeline/Unlit"
        };
        
        foreach (string shaderName in urpShaderNames)
        {
            urpShader = Shader.Find(shaderName);
            if (urpShader != null)
            {
                break;
            }
        }
        
        // URP 셰이더를 찾지 못한 경우
        if (urpShader == null)
        {
            Debug.LogError("URP 셰이더를 찾을 수 없습니다!");
            return null;
        }
        
        // 플래시 머터리얼 생성
        Material flashMaterial = new Material(urpShader);
        flashMaterial.color = Color.white;
        
        // URP Lit 셰이더인 경우 추가 속성 설정
        if (urpShader.name.Contains("Lit"))
        {
            if (flashMaterial.HasProperty("_Metallic"))
                flashMaterial.SetFloat("_Metallic", 0f);
            if (flashMaterial.HasProperty("_Smoothness"))
                flashMaterial.SetFloat("_Smoothness", 1f);
        }
        
        return flashMaterial;
    }

    private float EaseOutElastic(float t)
    {
        const float c4 = (2f * Mathf.PI) / 3f;
        return t == 0f ? 0f : t == 1f ? 1f : Mathf.Pow(2f, -10f * t) * Mathf.Sin((t * 10f - 0.75f) * c4) + 1f;
    }
    
    private void OnDestroy()
    {
        // 정리 작업
        pendingGifts.Clear();
        environmentPrefabs.Clear();
        spawnedEnvironments.Clear();
    }
}