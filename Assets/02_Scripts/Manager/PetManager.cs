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

    [Header("NavMesh 대기 설정")]
    public float navMeshWaitTime = 3f; // NavMesh 베이크 대기 시간

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
        Debug.Log("EnvironmentManager 초기화 완료, 펫 스폰 시작");
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
        Debug.Log($"선택된 펫 수: {PetSelectionManager.Instance.selectedPetIds.Count}");
        
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
        foreach (string petId in firstAppearancePets)
        {
            SpawnPet(petId, true);
            yield return new WaitForSeconds(firstAppearanceDelay);
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
                    GameObject pet = Instantiate(petPrefabs[petIndex], spawnPosition, Quaternion.identity);

                    if (withFirstAppearanceEffect)
                    {
                        // 최초 등장 효과 적용
                        ApplyFirstAppearanceEffect(pet);
                        Debug.Log($"최초 등장 펫 생성: {petId}, 인덱스: {petIndex}, 이름: {pet.name}");
                    }
                    else
                    {
                        Debug.Log($"일반 펫 생성: {petId}, 인덱스: {petIndex}, 이름: {pet.name}");
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
        Vector3 randomDirection = Random.insideUnitSphere * spawnRadius;
        randomDirection += transform.position;
        NavMeshHit hit;
        Vector3 finalPosition = transform.position;
        
        if (NavMesh.SamplePosition(randomDirection, out hit, spawnRadius, NavMesh.AllAreas))
        {
            finalPosition = hit.position;
        }
        
        return finalPosition;
    }
}