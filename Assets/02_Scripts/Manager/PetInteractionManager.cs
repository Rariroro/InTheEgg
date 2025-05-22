


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
// 펫 간 상호작용 규칙을 정의하는 클래스
// [System.Serializable] // Unity 인스펙터에서 표시 가능하도록 함
// public class InteractionRule
// {
//     public PetType pet1; // 첫 번째 펫 유형
//     public PetType pet2; // 두 번째 펫 유형
//     public InteractionType interactionType; // 두 펫 간 발생할 상호작용 유형

//     // 생성자
//     public InteractionRule(PetType pet1, PetType pet2, InteractionType interactionType)
//     {
//         this.pet1 = pet1;
//         this.pet2 = pet2;
//         this.interactionType = interactionType;
//     }

//     // 두 펫이 규칙에 부합하는지 확인
//     public bool MatchesPets(PetType typeA, PetType typeB)
//     {
//         return (pet1 == typeA && pet2 == typeB) || (pet1 == typeB && pet2 == typeA);
//     }
// }
public class PetInteractionManager : MonoBehaviour
{
    // 싱글톤 패턴 구현
    public static PetInteractionManager Instance { get; private set; }

    [Header("상호작용 설정")]
    public float interactionDistance = 5f;
    public float interactionCooldown = 30f;
    
    [Header("성능 최적화 설정")]
    public float interactionCheckInterval = 0.1f; // 상호작용 체크 간격 (초)
    public int maxChecksPerFrame = 5; // 프레임당 최대 체크 수
    
    // 씬 시작 후 상호작용 체크 시작 전 지연 시간
    public float startDelay = 3.0f;
    private bool canStartInteractions = false;

    // 캐싱된 펫 리스트 (성능 최적화)
    private List<PetController> allPets = new List<PetController>();
    private Dictionary<PetController, int> petToIndexMap = new Dictionary<PetController, int>();
    
    // 상호작용 중인 펫 쌍을 추적하는 딕셔너리
    private Dictionary<PetController, PetController> interactingPets = new Dictionary<PetController, PetController>();

    // 각 펫의 마지막 상호작용 시간을 기록하는 딕셔너리
    private Dictionary<PetController, float> lastInteractionTime = new Dictionary<PetController, float>();

    // 등록된 상호작용 컴포넌트 목록
    private List<BasePetInteraction> registeredInteractions = new List<BasePetInteraction>();

    // 상호작용 체크 최적화를 위한 변수들
    private int currentCheckIndex = 0;
    private int currentPairIndex = 0;
    private Coroutine interactionCheckCoroutine;
    
    // 거리 계산 최적화 (제곱 거리 사용)
    private float interactionDistanceSquared;

    private void Awake()
    {
        // 싱글톤 패턴 구현부
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject); // 씬 전환 시에도 유지
        }
        else
        {
            Destroy(gameObject);
            return;
        }

        // 거리 제곱값 미리 계산
        interactionDistanceSquared = interactionDistance * interactionDistance;
        
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
        
        // 최적화된 상호작용 체크 코루틴 시작
        interactionCheckCoroutine = StartCoroutine(OptimizedInteractionCheck());
        Debug.Log("[PetInteractionManager] 상호작용 체크 활성화!");
    }

    // 상호작용 컴포넌트 등록 메서드
    private void RegisterInteractions()
    {
        // 이 게임 오브젝트에 붙어 있는 모든 상호작용 컴포넌트 가져오기
        registeredInteractions.AddRange(GetComponents<BasePetInteraction>());

        Debug.Log($"[PetInteractionManager] {registeredInteractions.Count}개의 상호작용 컴포넌트 등록됨");
        foreach (var interaction in registeredInteractions)
        {
            Debug.Log($"  - {interaction.InteractionName}");
        }
    }

    // 펫 리스트 새로고침 (펫이 동적으로 생성/삭제될 때 호출)
    public void RefreshPetList()
    {
        allPets.Clear();
        petToIndexMap.Clear();
        
        // 현재 존재하는 모든 펫을 한 번만 찾아서 캐싱
        PetController[] foundPets = FindObjectsOfType<PetController>();
        
        for (int i = 0; i < foundPets.Length; i++)
        {
            if (foundPets[i] != null)
            {
                allPets.Add(foundPets[i]);
                petToIndexMap[foundPets[i]] = i;
            }
        }
        
        Debug.Log($"[PetInteractionManager] 펫 리스트 새로고침 완료. 총 {allPets.Count}마리");
    }

    // 펫을 리스트에 등록 (새 펫이 생성될 때 호출)
    public void RegisterPet(PetController pet)
    {
        if (pet != null && !allPets.Contains(pet))
        {
            int index = allPets.Count;
            allPets.Add(pet);
            petToIndexMap[pet] = index;
            Debug.Log($"[PetInteractionManager] 펫 등록: {pet.petName}");
        }
    }

    // 펫을 리스트에서 제거 (펫이 삭제될 때 호출)
    public void UnregisterPet(PetController pet)
    {
        if (pet != null && allPets.Contains(pet))
        {
            allPets.Remove(pet);
            petToIndexMap.Remove(pet);
            
            // 상호작용 중이었다면 정리
            if (interactingPets.ContainsKey(pet))
            {
                PetController partner = interactingPets[pet];
                interactingPets.Remove(pet);
                if (partner != null && interactingPets.ContainsKey(partner))
                {
                    interactingPets.Remove(partner);
                }
            }
            
            // 쿨다운 정보도 제거
            lastInteractionTime.Remove(pet);
            
            Debug.Log($"[PetInteractionManager] 펫 제거: {pet.petName}");
        }
    }

    // 최적화된 상호작용 체크 코루틴
    private IEnumerator OptimizedInteractionCheck()
    {
        while (canStartInteractions)
        {
            // 펫 리스트가 비어있거나 2마리 미만이면 체크하지 않음
            if (allPets.Count < 2)
            {
                yield return new WaitForSeconds(interactionCheckInterval);
                continue;
            }

            // 프레임당 제한된 수만큼만 체크
            int checksThisFrame = 0;
            
            while (checksThisFrame < maxChecksPerFrame && allPets.Count >= 2)
            {
                // null 체크 및 리스트 정리
                if (currentCheckIndex >= allPets.Count)
                {
                    CleanupNullPets();
                    currentCheckIndex = 0;
                    currentPairIndex = 0;
                    break;
                }

                PetController pet1 = allPets[currentCheckIndex];
                
                // null 체크
                if (pet1 == null)
                {
                    allPets.RemoveAt(currentCheckIndex);
                    continue;
                }

                // 두 번째 펫 인덱스 계산
                int pet2Index = currentCheckIndex + 1 + currentPairIndex;
                
                if (pet2Index >= allPets.Count)
                {
                    // 현재 펫의 모든 쌍 체크 완료, 다음 펫으로
                    currentCheckIndex++;
                    currentPairIndex = 0;
                    continue;
                }

                PetController pet2 = allPets[pet2Index];
                
                // null 체크
                if (pet2 == null)
                {
                    allPets.RemoveAt(pet2Index);
                    continue;
                }

                // 상호작용 체크 수행
                CheckPetPairInteraction(pet1, pet2);
                
                currentPairIndex++;
                checksThisFrame++;
            }

            // 다음 프레임까지 대기
            yield return new WaitForSeconds(interactionCheckInterval);
        }
    }

    // 두 펫 간의 상호작용 가능성 체크
    private void CheckPetPairInteraction(PetController pet1, PetController pet2)
    {
        // 이미 상호작용 중인 펫은 건너뛰기
        if (IsInteracting(pet1) || IsInteracting(pet2))
        {
            return;
        }

        // 상호작용 쿨다운 체크
        if (IsOnCooldown(pet1) || IsOnCooldown(pet2))
        {
            return;
        }

        // 거리 체크 (제곱 거리 사용으로 최적화)
        Vector3 pos1 = pet1.transform.position;
        Vector3 pos2 = pet2.transform.position;
        float distanceSquared = (pos1 - pos2).sqrMagnitude;

        // 상호작용 거리 이내인 경우만 처리
        if (distanceSquared <= interactionDistanceSquared)
        {
            // 적합한 상호작용 찾기
            BasePetInteraction suitableInteraction = FindSuitableInteraction(pet1, pet2);

            if (suitableInteraction != null)
            {
                Debug.Log($"[PetInteractionManager] {pet1.petName}와(과) {pet2.petName} 사이에 {suitableInteraction.InteractionName} 상호작용 시작!");

                // 상호작용 시작
                StartInteraction(pet1, pet2, suitableInteraction);

                // 상호작용 쿨다운 설정
                lastInteractionTime[pet1] = Time.time;
                lastInteractionTime[pet2] = Time.time;

                // 상호작용 기록 등록
                interactingPets[pet1] = pet2;
                interactingPets[pet2] = pet1;
            }
        }
    }

    // null인 펫들을 리스트에서 제거
    private void CleanupNullPets()
    {
        for (int i = allPets.Count - 1; i >= 0; i--)
        {
            if (allPets[i] == null)
            {
                allPets.RemoveAt(i);
            }
        }
        
        // 딕셔너리도 정리
        var keysToRemove = new List<PetController>();
        foreach (var key in petToIndexMap.Keys)
        {
            if (key == null)
            {
                keysToRemove.Add(key);
            }
        }
        
        foreach (var key in keysToRemove)
        {
            petToIndexMap.Remove(key);
            interactingPets.Remove(key);
            lastInteractionTime.Remove(key);
        }
    }

    // 적합한 상호작용 컴포넌트 찾기
    private BasePetInteraction FindSuitableInteraction(PetController pet1, PetController pet2)
    {
        // 모든 등록된 상호작용 중에서 현재 펫 조합에 적합한 것 찾기
        foreach (var interaction in registeredInteractions)
        {
            if (interaction.CanInteract(pet1, pet2))
            {
                return interaction;
            }
        }

        return null; // 적합한 상호작용이 없는 경우
    }

    // 상호작용 시작 메서드
    private void StartInteraction(PetController pet1, PetController pet2, BasePetInteraction interaction)
    {
        // 상호작용 컴포넌트에 작업 위임
        interaction.StartInteraction(pet1, pet2);
    }

    // 펫이 상호작용 중인지 확인하는 메서드
    private bool IsInteracting(PetController pet)
    {
        return interactingPets.ContainsKey(pet);
    }

    // 펫이 쿨다운 중인지 확인하는 메서드
    private bool IsOnCooldown(PetController pet)
    {
        if (lastInteractionTime.TryGetValue(pet, out float lastTime))
        {
            return Time.time - lastTime < interactionCooldown;
        }
        return false; // 처음 상호작용하는 경우 쿨다운 없음
    }

    // 상호작용 종료 알림 수신 메서드 (BasePetInteraction에서 호출)
    public void NotifyInteractionEnded(PetController pet1, PetController pet2)
    {
        // 상호작용 기록 제거
        if (pet1 != null) interactingPets.Remove(pet1);
        if (pet2 != null) interactingPets.Remove(pet2);
        
        Debug.Log($"[PetInteractionManager] 상호작용 종료: {pet1?.petName} - {pet2?.petName}");
    }

    // 강제로 펫 리스트 새로고침 (디버그용)
    [ContextMenu("펫 리스트 새로고침")]
    public void ForceRefreshPetList()
    {
        RefreshPetList();
    }

    // 현재 상태 출력 (디버그용)
    [ContextMenu("현재 상태 출력")]
    public void PrintCurrentStatus()
    {
        Debug.Log($"[PetInteractionManager] 현재 상태:");
        Debug.Log($"  - 총 펫 수: {allPets.Count}");
        Debug.Log($"  - 상호작용 중인 펫 쌍: {interactingPets.Count / 2}");
        Debug.Log($"  - 쿨다운 중인 펫: {lastInteractionTime.Count}");
        Debug.Log($"  - 체크 간격: {interactionCheckInterval}초");
        Debug.Log($"  - 프레임당 최대 체크: {maxChecksPerFrame}");
    }

    private void OnDestroy()
    {
        // 코루틴 정리
        if (interactionCheckCoroutine != null)
        {
            StopCoroutine(interactionCheckCoroutine);
        }
        
        // 싱글톤 정리
        if (Instance == this)
        {
            Instance = null;
        }
    }

    // Update 메서드 제거 - 이제 코루틴으로 처리
}