using UnityEngine;
using UnityEngine.AI;
using System.Collections.Generic;

/// <summary>
/// PetManager 클래스는 게임 씬이 시작될 때 PetSelectionManager에 의해 선택된 펫만
/// NavMesh 위에 스폰(생성)하도록 관리합니다. 선택된 펫이 없을 경우 테스트 용도로
/// 모든 펫을 스폰할 수 있는 기능도 포함되어 있습니다.
/// </summary>
public class PetManager : MonoBehaviour
{
    [Header("펫 스폰 설정")]
    public GameObject[] petPrefabs;  // 60개의 펫 프리팹을 배열로 저장 (인덱스 0 ~ 59)
    public float spawnRadius = 50f;  // 펫을 스폰할 때 사용되는 반경 (NavMesh 내 랜덤 위치)
    public int maxPets = 50;         // 모든 펫을 스폰할 때 최대 스폰 개수 제한 (테스트 목적)

    /// <summary>
    /// Start() 메서드는 게임 씬이 시작될 때 자동으로 호출된다.
    /// PetSelectionManager에 선택된 펫 ID가 있다면 해당 펫만 스폰하고,
    /// 그렇지 않다면 (테스트용) 모든 펫을 스폰하도록 분기 처리한다.
    /// </summary>
    private void Start()
    {
        // PetSelectionManager 인스턴스가 존재하고, 선택된 펫 ID 목록이 하나 이상 있을 경우
        if (PetSelectionManager.Instance != null && PetSelectionManager.Instance.selectedPetIds.Count > 0)
        {
            // PetSelectionManager에 등록된 선택된 펫만 스폰
            SpawnSelectedPets();
        }
        else
        {
            // 선택된 펫이 없거나 PetSelectionManager가 존재하지 않을 경우 테스트용 스폰
            Debug.LogWarning("선택된 펫이 없거나 PetSelectionManager가 없습니다. 기본 동작으로 모든 펫을 스폰합니다.");
            SpawnAllPets();
        }
    }

    /// <summary>
    /// SpawnSelectedPets 메서드는 PetSelectionManager.Instance.selectedPetIds에
    /// 저장된 펫 ID 목록을 순회하면서 해당 ID에 맞는 프리팹을 NavMesh 위
    /// 랜덤 위치에 스폰(Instantiate)합니다.
    /// </summary>
    private void SpawnSelectedPets()
    {
        // 선택된 펫 수를 로그로 출력
        Debug.Log($"선택된 펫 수: {PetSelectionManager.Instance.selectedPetIds.Count}");

        // PetSelectionManager에서 저장된 각각의 petId 문자열을 순회
        foreach (string petId in PetSelectionManager.Instance.selectedPetIds)
        {
            // petId는 "pet_001", "pet_002" 형태이므로 "pet_" 접두사와 숫자 부분이 있는지 확인
            if (petId.StartsWith("pet_") && petId.Length >= 7)
            {
                // "pet_" 이후의 숫자 문자열만 추출 (예: "001", "002", ...)
                string numberPart = petId.Substring(4);

                // 추출한 숫자 문자열을 정수로 변환 시도
                if (int.TryParse(numberPart, out int petIndex))
                {
                    // petIndex는 1부터 시작하므로, 배열 인덱스는 0부터 시작하도록 1을 뺌
                    petIndex = petIndex - 1;

                    // 변환된 petIndex가 petPrefabs 배열의 유효 범위인지 확인
                    if (petIndex >= 0 && petIndex < petPrefabs.Length)
                    {
                        // NavMesh 위에서 사용할 랜덤 위치 계산
                        Vector3 spawnPosition = GetRandomPositionOnNavMesh();

                        // petPrefabs[petIndex] 프리팹을 해당 위치와 기본 회전값(Quaternion.identity)으로 생성
                        GameObject pet = Instantiate(petPrefabs[petIndex], spawnPosition, Quaternion.identity);

                        // 생성된 펫의 이름과 ID 정보를 로그로 출력
                        Debug.Log($"펫 생성: {petId}, 인덱스: {petIndex}, 이름: {pet.name}");
                    }
                    else
                    {
                        // petIndex가 배열 범위를 벗어났을 경우 에러 로그 출력
                        Debug.LogError($"유효하지 않은 펫 인덱스: {petIndex}, ID: {petId}");
                    }
                }
                else
                {
                    // 숫자 부분을 정수로 변환할 수 없는 경우 에러 로그 출력
                    Debug.LogError($"펫 ID 형식 오류: {petId}");
                }
            }
            else
            {
                // petId 문자열이 "pet_"로 시작하지 않거나 길이가 예상과 다를 경우 에러 로그 출력
                Debug.LogError($"잘못된 펫 ID 형식: {petId}");
            }
        }
    }

    /// <summary>
    /// SpawnAllPets 메서드는 테스트 목적으로 petPrefabs 배열에 들어있는
    /// 모든 프리팹을 최대 maxPets만큼 NavMesh 위 랜덤 위치에 생성합니다.
    /// </summary>
    private void SpawnAllPets()
    {
        // petPrefabs 배열 길이 또는 maxPets 중 작은 쪽을 기준으로 반복
        for (int i = 0; i < petPrefabs.Length && i < maxPets; i++)
        {
            // 해당 인덱스의 프리팹이 null이 아니면 생성
            if (petPrefabs[i] != null)
            {
                Vector3 randomPosition = GetRandomPositionOnNavMesh();
                GameObject pet = Instantiate(petPrefabs[i], randomPosition, Quaternion.identity);

                // (옵션) 생성된 테스트 펫에 고유 이름을 붙이거나 디버그 로그를 남길 수 있음
                Debug.Log($"테스트용 펫 생성: 인덱스 {i}, 이름: {pet.name}");
            }
        }
    }

    /// <summary>
    /// GetRandomPositionOnNavMesh 메서드는 지정된 spawnRadius 범위 내에서
    /// NavMesh.SamplePosition을 사용해 유효한 NavMesh 위의 랜덤 위치를 반환한다.
    /// </summary>
    /// <returns>NavMesh 위의 유효한 랜덤 위치(Vector3)</returns>
    private Vector3 GetRandomPositionOnNavMesh()
    {
        // 랜덤 방향 벡터를 구함 (반지름 spawnRadius 내의 구 안에서 랜덤)
        Vector3 randomDirection = Random.insideUnitSphere * spawnRadius;

        // PetManager 오브젝트의 위치를 기준으로 offset 추가
        randomDirection += transform.position;

        NavMeshHit hit;
        Vector3 finalPosition = transform.position; // 기본값 설정 (NavMesh 샘플에 실패 시 사용)

        // NavMesh.SamplePosition을 사용해 randomDirection 근처의 NavMesh 위 위치를 hit에 저장
        if (NavMesh.SamplePosition(randomDirection, out hit, spawnRadius, NavMesh.AllAreas))
        {
            // NavMesh 상의 유효한 위치가 있다면 hit.position을 finalPosition으로 설정
            finalPosition = hit.position;
        }

        return finalPosition;
    }
}
