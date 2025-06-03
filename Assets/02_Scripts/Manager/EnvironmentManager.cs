using UnityEngine;
using System.Collections;
using System.Collections.Generic;

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

    // 환경 ID와 프리팹 연결을 위한 딕셔너리
    private Dictionary<string, GameObject> environmentPrefabs = new Dictionary<string, GameObject>();
    
    // 대기 중인 선물들과 해당 환경 정보를 저장하는 딕셔너리
    private Dictionary<GameObject, string> pendingGifts = new Dictionary<GameObject, string>();

    private void Awake()
    {
        // 딕셔너리 초기화
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
        // EnvironmentSelectionManager가 존재하는지 확인
        if (EnvironmentSelectionManager.Instance != null)
        {
            // 선택된 환경을 효과와 함께 스폰
            StartCoroutine(SpawnSelectedEnvironmentsWithEffects());
        }
        else
        {
            Debug.LogWarning("EnvironmentSelectionManager가 없습니다.");
        }
    }

    private void Update()
    {
        // 선물 터치 감지
        HandleGiftTouch();
    }

    // 선물 터치를 처리하는 메서드
    private void HandleGiftTouch()
    {
        if (Input.GetMouseButtonDown(0))
        {
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

    // 선물을 여는 코루틴
    private IEnumerator OpenGift(GameObject gift, string environmentId)
    {
        // 선물을 대기 목록에서 제거
        pendingGifts.Remove(gift);

        // 축하 효과 파티클 실행
        if (celebrationEffectPrefab != null)
        {
            GameObject celebration = Instantiate(celebrationEffectPrefab, gift.transform.position, Quaternion.identity);
            
            // 축하 효과 크기 조정
            float scale = 1.5f;
            celebration.transform.localScale = Vector3.one * scale;
            
            // 5초 후 축하 효과 제거
            Destroy(celebration, 5f);
        }

        // 선물 제거 애니메이션
        yield return StartCoroutine(RemoveGiftWithAnimation(gift));

        // 잠시 대기
        yield return new WaitForSeconds(0.5f);

        // 환경 스폰
        SpawnEnvironment(environmentId, true);

        Debug.Log($"선물을 열어 환경이 나타났습니다: {environmentId}");
    }

    // 선물 제거 애니메이션 코루틴
    private IEnumerator RemoveGiftWithAnimation(GameObject gift)
    {
        Vector3 originalScale = gift.transform.localScale;
        float duration = 0.5f;
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

    // 선택된 환경을 효과와 함께 스폰하는 코루틴 (수정됨)
    private IEnumerator SpawnSelectedEnvironmentsWithEffects()
    {
        List<string> selectedIds = EnvironmentSelectionManager.Instance.selectedEnvironmentIds;
        
        Debug.Log($"선택된 환경 수: {selectedIds.Count}");
        
        // 선택된 환경이 없어도 괜찮음 (하나도 선택하지 않은 경우)
        if (selectedIds.Count == 0)
        {
            Debug.Log("선택된 환경이 없습니다. 기본 지형만 사용합니다.");
            yield break;
        }

        // 일반 환경과 최초 등장 환경을 분리
        List<string> normalEnvironments = new List<string>();
        List<string> firstAppearanceEnvironments = new List<string>();

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

        // 먼저 일반 환경들을 스폰
        foreach (string environmentId in normalEnvironments)
        {
            SpawnEnvironment(environmentId, false);
        }

        // 최초 등장 환경들은 선물로 스폰
        foreach (string environmentId in firstAppearanceEnvironments)
        {
            SpawnGiftForEnvironment(environmentId);
            yield return new WaitForSeconds(giftSpawnDelay);
        }
    }

    // 환경을 위한 선물을 스폰하는 메서드
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
            // 선물 프리팹이 없으면 바로 환경 스폰
            SpawnEnvironment(environmentId, true);
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

    // 선물 회전 애니메이션 코루틴
    private IEnumerator RotateGift(GameObject gift)
    {
        while (gift != null && pendingGifts.ContainsKey(gift))
        {
            gift.transform.Rotate(0, 30 * Time.deltaTime, 0);
            
            // 약간의 위아래 흔들림 효과
            float bobbing = Mathf.Sin(Time.time * 2f) * 0.1f;
            Vector3 originalPos = gift.transform.position;
            originalPos.y += bobbing * Time.deltaTime;
            gift.transform.position = originalPos;
            
            yield return null;
        }
    }
    
    // 지정된 ID의 환경 스폰 (효과 옵션 추가)
    private void SpawnEnvironment(string environmentId, bool withFirstAppearanceEffect = false)
    {
        // 환경 ID에 해당하는 프리팹 가져오기
        if (!environmentPrefabs.ContainsKey(environmentId))
        {
            Debug.LogError($"알 수 없는 환경 ID: {environmentId}");
            return;
        }
        
        GameObject prefab = environmentPrefabs[environmentId];
        
        // 프리팹 존재 여부 확인
        if (prefab == null)
        {
            Debug.LogError($"환경 프리팹이 할당되지 않았습니다: {environmentId}");
            return;
        }
        
        // 프리팹 그대로 인스턴스화 (프리팹의 위치/회전 사용)
        GameObject environment = Instantiate(prefab);

        if (withFirstAppearanceEffect)
        {
            // 최초 등장 효과 적용
            ApplyFirstAppearanceEffect(environment);
            Debug.Log($"최초 등장 환경 생성 완료: {environmentId}");
        }
        else
        {
            Debug.Log($"일반 환경 생성 완료: {environmentId}");
        }
    }

    // 최초 등장 효과를 적용하는 메서드
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

    // 환경 오브젝트의 바운드를 계산하는 메서드
    private Bounds GetEnvironmentBounds(GameObject environment)
    {
        Renderer[] renderers = environment.GetComponentsInChildren<Renderer>();
        if (renderers.Length == 0)
        {
            return new Bounds(environment.transform.position, Vector3.one);
        }

        Bounds bounds = renderers[0].bounds;
        foreach (Renderer renderer in renderers)
        {
            bounds.Encapsulate(renderer.bounds);
        }
        return bounds;
    }

    // 스케일 인 효과 코루틴
    private IEnumerator ScaleInEffect(GameObject environment)
    {
        Vector3 originalScale = environment.transform.localScale;
        environment.transform.localScale = Vector3.zero;

        float duration = 1.5f;
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

    // 플래시 효과 코루틴
    // EnvironmentManager.cs의 FlashEffect 코루틴 수정

// 플래시 효과 코루틴
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

    float duration = 0.5f;
    int flashCount = 3;
    
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

// URP 호환 플래시 머터리얼 생성 메서드 (새로 추가)
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

    // Ease Out Elastic 이징 함수
    private float EaseOutElastic(float t)
    {
        float c4 = (2f * Mathf.PI) / 3f;
        return t == 0f ? 0f : t == 1f ? 1f : Mathf.Pow(2f, -10f * t) * Mathf.Sin((t * 10f - 0.75f) * c4) + 1f;
    }
}