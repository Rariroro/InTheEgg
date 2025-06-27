


// 수정된 PetInteractionManager
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

// 펫 종류를 구분하는 열거형 - 총 60종류의 펫 타입 정의
public enum PetType
{
    Turtle, Flamingo, Chick, Chicken, Pig, Cow, Cat, Dog, Duck, Elk, Boar, Wolf,
    Rabbit, Skunk, Deer, Raccoon, Owl, Fox, Squirrel, Mole, Porcupine, Camel, Goat,
    Anteater, Iguana, Pangolin, Alpaca, Kangaroo, Meerkat, Mule, Bison, Ostrich,
    Horse, Zebra, Bull, Lioness, Giraffe, Lion, Rhino, Elephant, Sheep, Gorilla,
    Possum, Leopard, Bear, Peacock, Tiger, Panda, Monkey, Sloth, RedPanda, Koala,
    Malayan, Chameleon, Buffalo, Hippo, Armadillo, Crocodile, Platypus, Otter
}

// 펫 간 상호작용 종류를 정의하는 열거형 - 6가지 상호작용 유형
public enum InteractionType
{
    Fight,        // 싸우기 - 두 펫이 서로 공격하는 상호작용
    WalkTogether, // 같이 걷기 - 두 펫이 함께 걷는 상호작용
    RestTogether, // 같이 쉬기 - 두 펫이 함께 휴식하는 상호작용
    Race,         // 달리기 시합 - 두 펫이 목표 지점까지 경주하는 상호작용
    ChaseAndRun,  // 쫓고 쫓기기 - 한 펫이 다른 펫을 쫓고 도망가는 상호작용
    SleepTogether, // 같이 자기 - 두 펫이 함께 자는 상호작용
    RideAndWalk,   // 타고 걷기 - 미어켓이 멧돼지에 타고 함께 걷는 상호작용
    SlothKoalaRace, // 나무늘보-코알라 달리기 - 느린 동물들의 달리기 시합
        ChameleonCamouflage // 카멜레온 위장 - 카멜레온이 위험을 감지하고 위장하는 상호작용

}



public class PetInteractionManager : MonoBehaviour
{
    // 싱글톤 패턴 구현
    public static PetInteractionManager Instance { get; private set; }

    [Header("상호작용 설정")]
    public float interactionDistance = 5f;
    public float interactionCooldown = 30f;
    
    [Header("성능 최적화 설정")]
    public float interactionCheckInterval = 0.5f;  // 0.1f -> 0.5f로 증가
    public int maxChecksPerFrame = 10;  // 5 -> 10으로 증가
    
    // 공간 분할 최적화를 위한 그리드 설정
    [Header("공간 분할 최적화")]
    public float gridCellSize = 20f;  // 그리드 셀 크기
    private Dictionary<int, List<PetController>> spatialGrid = new Dictionary<int, List<PetController>>();
    private float gridUpdateInterval = 1f;  // 그리드 업데이트 간격
    private float lastGridUpdate = 0f;
    
    // 시작 지연 시간
    public float startDelay = 3.0f;
    private bool canStartInteractions = false;

    // 캐싱된 펫 리스트
    private List<PetController> allPets = new List<PetController>();
    private Dictionary<PetController, int> petToIndexMap = new Dictionary<PetController, int>();
    
    // 상호작용 중인 펫 쌍 추적
    private Dictionary<PetController, PetController> interactingPets = new Dictionary<PetController, PetController>();
    private Dictionary<PetController, float> lastInteractionTime = new Dictionary<PetController, float>();
    
    // 등록된 상호작용 컴포넌트
    private List<BasePetInteraction> registeredInteractions = new List<BasePetInteraction>();

    // 거리 계산 최적화
    private float interactionDistanceSquared;
    
    // LOD 시스템을 위한 카메라 참조
    private Camera mainCamera;
    private float lodDistance = 50f;  // 이 거리 이상의 펫은 체크 빈도 감소

    private void Awake()
    {
        // 싱글톤 패턴 구현
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
            return;
        }

        // 거리 제곱값 미리 계산
        interactionDistanceSquared = interactionDistance * interactionDistance;
        
        // 카메라 참조 캐싱
        mainCamera = Camera.main;
        
        // 상호작용 컴포넌트 등록
        RegisterInteractions();
    }

    private void Start()
    {
        // 초기 펫 리스트 구축
        RefreshPetList();
        
        // 지정된 시간 후에 상호작용 체크 활성화
        StartCoroutine(EnableInteractionsAfterDelay());
    }

    private IEnumerator EnableInteractionsAfterDelay()
    {
        Debug.Log("[PetInteractionManager] 상호작용 체크 지연 중...");
        yield return new WaitForSeconds(startDelay);
        canStartInteractions = true;
        
        // 최적화된 상호작용 체크 시작
        StartCoroutine(OptimizedSpatialInteractionCheck());
        Debug.Log("[PetInteractionManager] 상호작용 체크 활성화!");
    }

    private void RegisterInteractions()
    {
        registeredInteractions.AddRange(GetComponents<BasePetInteraction>());
        Debug.Log($"[PetInteractionManager] {registeredInteractions.Count}개의 상호작용 컴포넌트 등록됨");
    }

    public void RefreshPetList()
    {
        allPets.Clear();
        petToIndexMap.Clear();
        spatialGrid.Clear();
        
        PetController[] foundPets = FindObjectsOfType<PetController>();
        
        for (int i = 0; i < foundPets.Length; i++)
        {
            if (foundPets[i] != null)
            {
                allPets.Add(foundPets[i]);
                petToIndexMap[foundPets[i]] = i;
            }
        }
        
        // 그리드 업데이트
        UpdateSpatialGrid();
        
        Debug.Log($"[PetInteractionManager] 펫 리스트 새로고침 완료. 총 {allPets.Count}마리");
    }

    public void RegisterPet(PetController pet)
    {
        if (pet != null && !allPets.Contains(pet))
        {
            int index = allPets.Count;
            allPets.Add(pet);
            petToIndexMap[pet] = index;
            
            // 그리드에 추가
            AddPetToGrid(pet);
            
            // Debug.Log($"[PetInteractionManager] 펫 등록: {pet.petName}");
        }
    }

    public void UnregisterPet(PetController pet)
    {
        if (pet != null && allPets.Contains(pet))
        {
            allPets.Remove(pet);
            petToIndexMap.Remove(pet);
            
            // 그리드에서 제거
            RemovePetFromGrid(pet);
            
            // 상호작용 정리
            if (interactingPets.ContainsKey(pet))
            {
                PetController partner = interactingPets[pet];
                interactingPets.Remove(pet);
                if (partner != null && interactingPets.ContainsKey(partner))
                {
                    interactingPets.Remove(partner);
                }
            }
            
            lastInteractionTime.Remove(pet);
            
            // Debug.Log($"[PetInteractionManager] 펫 제거: {pet.petName}");
        }
    }

    // 공간 분할 그리드 업데이트
    private void UpdateSpatialGrid()
    {
        spatialGrid.Clear();
        
        foreach (PetController pet in allPets)
        {
            if (pet != null)
            {
                AddPetToGrid(pet);
            }
        }
        
        lastGridUpdate = Time.time;
    }

    // 그리드에 펫 추가
    private void AddPetToGrid(PetController pet)
    {
        int gridKey = GetGridKey(pet.transform.position);
        
        if (!spatialGrid.ContainsKey(gridKey))
        {
            spatialGrid[gridKey] = new List<PetController>();
        }
        
        spatialGrid[gridKey].Add(pet);
    }

    // 그리드에서 펫 제거
    private void RemovePetFromGrid(PetController pet)
    {
        // 모든 그리드 셀에서 제거 (펫이 이동했을 수 있으므로)
        foreach (var kvp in spatialGrid)
        {
            kvp.Value.Remove(pet);
        }
    }

    // 위치를 그리드 키로 변환
    private int GetGridKey(Vector3 position)
    {
        int x = Mathf.FloorToInt(position.x / gridCellSize);
        int z = Mathf.FloorToInt(position.z / gridCellSize);
        return x * 1000 + z;  // 간단한 해시 함수
    }

    // 인접한 그리드 셀 가져오기
    private List<int> GetNeighborGridKeys(int gridKey)
    {
        List<int> neighbors = new List<int>();
        int x = gridKey / 1000;
        int z = gridKey % 1000;
        
        // 9개의 인접 셀 (자신 포함)
        for (int dx = -1; dx <= 1; dx++)
        {
            for (int dz = -1; dz <= 1; dz++)
            {
                neighbors.Add((x + dx) * 1000 + (z + dz));
            }
        }
        
        return neighbors;
    }

    // 최적화된 공간 분할 기반 상호작용 체크
    private IEnumerator OptimizedSpatialInteractionCheck()
    {
        while (canStartInteractions)
        {
            // 그리드 주기적 업데이트
            if (Time.time - lastGridUpdate > gridUpdateInterval)
            {
                UpdateSpatialGrid();
            }

            // 펫이 2마리 미만이면 체크 불필요
            if (allPets.Count < 2)
            {
                yield return new WaitForSeconds(interactionCheckInterval);
                continue;
            }

            int checksThisFrame = 0;
            
            // 각 그리드 셀 검사
            foreach (var gridCell in spatialGrid)
            {
                if (gridCell.Value.Count == 0)
                    continue;
                
                List<PetController> petsInCell = gridCell.Value;
                List<int> neighborKeys = GetNeighborGridKeys(gridCell.Key);
                
                // 같은 셀 내의 펫들 체크
                for (int i = 0; i < petsInCell.Count; i++)
                {
                    PetController pet1 = petsInCell[i];
                    if (pet1 == null) continue;
                    
                    // LOD 체크 - 카메라에서 멀리 있는 펫은 체크 빈도 감소
                    if (mainCamera != null)
                    {
                        float camDistSqr = (pet1.transform.position - mainCamera.transform.position).sqrMagnitude;
                        if (camDistSqr > lodDistance * lodDistance && Random.value > 0.3f)
                            continue;
                    }
                    
                    // 같은 셀 내의 다른 펫들과 체크
                    for (int j = i + 1; j < petsInCell.Count; j++)
                    {
                        if (checksThisFrame >= maxChecksPerFrame)
                        {
                            yield return new WaitForSeconds(interactionCheckInterval);
                            checksThisFrame = 0;
                        }
                        
                        PetController pet2 = petsInCell[j];
                        if (pet2 != null)
                        {
                            CheckPetPairInteraction(pet1, pet2);
                            checksThisFrame++;
                        }
                    }
                    
                    // 인접 셀의 펫들과 체크
                    foreach (int neighborKey in neighborKeys)
                    {
                        if (neighborKey == gridCell.Key) continue;  // 같은 셀은 이미 체크함
                        
                        if (spatialGrid.ContainsKey(neighborKey))
                        {
                            List<PetController> neighborsInCell = spatialGrid[neighborKey];
                            
                            foreach (PetController neighborPet in neighborsInCell)
                            {
                                if (checksThisFrame >= maxChecksPerFrame)
                                {
                                    yield return new WaitForSeconds(interactionCheckInterval);
                                    checksThisFrame = 0;
                                }
                                
                                if (neighborPet != null)
                                {
                                    CheckPetPairInteraction(pet1, neighborPet);
                                    checksThisFrame++;
                                }
                            }
                        }
                    }
                }
            }
            
            // 다음 체크까지 대기
            yield return new WaitForSeconds(interactionCheckInterval);
        }
    }

    // 두 펫 간의 상호작용 가능성 체크 (최적화됨)
    private void CheckPetPairInteraction(PetController pet1, PetController pet2)
    {
        // 빠른 거부 조건들
        if (pet1 == null || pet2 == null) return;
        if (IsInteracting(pet1) || IsInteracting(pet2)) return;
        if (IsOnCooldown(pet1) || IsOnCooldown(pet2)) return;

        // 빠른 거리 체크 (Bounding Box 체크)
        Vector3 diff = pet2.transform.position - pet1.transform.position;
        if (Mathf.Abs(diff.x) > interactionDistance || 
            Mathf.Abs(diff.z) > interactionDistance)
            return;

        // 정확한 거리 체크 (제곱 거리 사용)
        float distanceSquared = diff.sqrMagnitude;
        if (distanceSquared > interactionDistanceSquared)
            return;

        // 적합한 상호작용 찾기
        BasePetInteraction suitableInteraction = FindSuitableInteraction(pet1, pet2);
        if (suitableInteraction == null)
            return;

        Debug.Log($"[PetInteractionManager] {pet1.petName}와(과) {pet2.petName} 사이에 {suitableInteraction.InteractionName} 상호작용 시작!");

        // 상호작용 시작
        StartInteraction(pet1, pet2, suitableInteraction);

        // 상호작용 기록
        float currentTime = Time.time;
        lastInteractionTime[pet1] = currentTime;
        lastInteractionTime[pet2] = currentTime;
        interactingPets[pet1] = pet2;
        interactingPets[pet2] = pet1;
    }

    // 적합한 상호작용 찾기 (캐싱 가능)
    private BasePetInteraction FindSuitableInteraction(PetController pet1, PetController pet2)
    {
        // 상호작용 우선순위에 따라 정렬할 수 있음
        foreach (var interaction in registeredInteractions)
        {
            if (interaction.CanInteract(pet1, pet2))
            {
                return interaction;
            }
        }
        return null;
    }

   // PetInteractionManager.cs

// 수정 제안 1: PetInteractionManager.cs
private void StartInteraction(PetController pet1, PetController pet2, BasePetInteraction interaction)
{
    // PetController의 상태 플래그만 설정해줍니다.
    // UpdateAI() 호출과 interaction.StartInteraction()을 제거합니다.
    pet1.BeginInteraction(pet2, interaction);
    pet2.BeginInteraction(pet1, interaction);

    // 상호작용 기록
    float currentTime = Time.time;
    lastInteractionTime[pet1] = currentTime;
    lastInteractionTime[pet2] = currentTime;
    interactingPets[pet1] = pet2;
    interactingPets[pet2] = pet1;
}

// 상호작용 종료 시 isInteracting 플래그를 false로 만들어줘야 합니다.
public void NotifyInteractionEnded(PetController pet1, PetController pet2)
{
    if (pet1 != null) {
        interactingPets.Remove(pet1);
        // isInteracting 플래그는 여기서 직접 제어하지 않고,
        // AI가 WanderAction 같은 다른 상태로 전환될 때 자연스럽게 해제되도록 둡니다.
        // 또는, 상호작용 종료 시 명확하게 false로 설정하고 싶다면 아래 주석을 해제합니다.
        // pet1.isInteracting = false; 
        pet1.interactionPartner = null;
        pet1.currentInteractionLogic = null;
    }
    if (pet2 != null) {
        interactingPets.Remove(pet2);
        // pet2.isInteracting = false;
        pet2.interactionPartner = null;
        pet2.currentInteractionLogic = null;
    }
    
    Debug.Log($"[PetInteractionManager] 상호작용 종료: {pet1?.petName} - {pet2?.petName}");
}

    private bool IsInteracting(PetController pet)
    {
        return interactingPets.ContainsKey(pet);
    }

    private bool IsOnCooldown(PetController pet)
    {
        if (lastInteractionTime.TryGetValue(pet, out float lastTime))
        {
            return Time.time - lastTime < interactionCooldown;
        }
        return false;
    }

  

    // 디버그용 메서드들
    [ContextMenu("펫 리스트 새로고침")]
    public void ForceRefreshPetList()
    {
        RefreshPetList();
    }

    [ContextMenu("현재 상태 출력")]
    public void PrintCurrentStatus()
    {
        Debug.Log($"[PetInteractionManager] 현재 상태:");
        Debug.Log($"  - 총 펫 수: {allPets.Count}");
        Debug.Log($"  - 상호작용 중인 펫 쌍: {interactingPets.Count / 2}");
        Debug.Log($"  - 쿨다운 중인 펫: {lastInteractionTime.Count}");
        Debug.Log($"  - 그리드 셀 수: {spatialGrid.Count}");
        Debug.Log($"  - 체크 간격: {interactionCheckInterval}초");
        Debug.Log($"  - 프레임당 최대 체크: {maxChecksPerFrame}");
    }

    private void OnDestroy()
    {
        StopAllCoroutines();
        
        if (Instance == this)
        {
            Instance = null;
        }
    }
}