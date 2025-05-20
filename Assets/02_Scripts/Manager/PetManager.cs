// using UnityEngine;
// using UnityEngine.AI;

// public class PetManager : MonoBehaviour
// {
//     public GameObject[] petPrefabs;
//     public int maxPets = 50;
//     public float spawnRadius = 50f;

//     private void Start()
//     {
//         SpawnPets();
//     }

//     private void SpawnPets()
//     {
//         for (int i = 0; i < maxPets; i++)
//         {
//             if (i >= petPrefabs.Length) break;

//             Vector3 randomPosition = GetRandomPositionOnNavMesh();
//             GameObject pet = Instantiate(petPrefabs[i], randomPosition, Quaternion.identity);
//         }
//     }

//     private Vector3 GetRandomPositionOnNavMesh()
//     {
//         Vector3 randomDirection = Random.insideUnitSphere * spawnRadius;
//         randomDirection += transform.position;
//         NavMeshHit hit;
//         Vector3 finalPosition = Vector3.zero;
//         if (NavMesh.SamplePosition(randomDirection, out hit, spawnRadius, 1))
//         {
//             finalPosition = hit.position;
//         }
//         return finalPosition;
//     }
// }

using UnityEngine;
using UnityEngine.AI;
using System.Collections.Generic;

public class PetManager : MonoBehaviour
{
    public GameObject[] petPrefabs;  // 60개의 펫 프리팹 배열
    public float spawnRadius = 50f;  // 스폰 반경
 public int maxPets = 50;
    private void Start()
    {
        // PetSelectionManager가 존재하는지 확인
        if (PetSelectionManager.Instance != null && PetSelectionManager.Instance.selectedPetIds.Count > 0)
        {
            // 선택된 펫만 스폰
            SpawnSelectedPets();
        }
        else
        {
            // 선택된 펫이 없거나 매니저가 없는 경우 (테스트 목적으로만 사용)
            Debug.LogWarning("선택된 펫이 없거나 PetSelectionManager가 없습니다. 기본 동작으로 모든 펫을 스폰합니다.");
            SpawnAllPets();
        }
    }

    // 선택된 펫만 스폰하는 메서드
    private void SpawnSelectedPets()
    {
        Debug.Log($"선택된 펫 수: {PetSelectionManager.Instance.selectedPetIds.Count}");
        
        foreach (string petId in PetSelectionManager.Instance.selectedPetIds)
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
                        Debug.Log($"펫 생성: {petId}, 인덱스: {petIndex}, 이름: {pet.name}");
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
    }

    // 모든 펫을 스폰하는 메서드 (테스트 목적)
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

    // NavMesh 위의 랜덤 위치를 반환하는 메서드
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