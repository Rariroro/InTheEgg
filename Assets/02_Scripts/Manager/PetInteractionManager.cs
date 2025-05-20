


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

    // 상호작용 규칙 리스트
    // public List<InteractionRule> interactionRules = new List<InteractionRule>();

    // 상호작용 거리 설정
    public float interactionDistance = 5f;

    // 상호작용 쿨다운 시간
    public float interactionCooldown = 30f;

    // 상호작용 중인 펫 쌍을 추적하는 딕셔너리
    private Dictionary<PetController, PetController> interactingPets = new Dictionary<PetController, PetController>();

    // 각 펫의 마지막 상호작용 시간을 기록하는 딕셔너리
    private Dictionary<PetController, float> lastInteractionTime = new Dictionary<PetController, float>();

    // 등록된 상호작용 컴포넌트 목록
    private List<BasePetInteraction> registeredInteractions = new List<BasePetInteraction>();

    // 초기화 시 실행되는 함수
    private void Awake()
    {
        // 싱글톤 패턴 구현부
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
            return;
        }

        // 상호작용 규칙 설정 메서드 호출
        // SetupInteractionRules();

        // 상호작용 컴포넌트 등록
        RegisterInteractions();
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

    // 상호작용 규칙 초기화
    // private void SetupInteractionRules()
    // {
    //     // (기존 코드 그대로 유지)
    //     // 다양한 펫 간 상호작용 규칙 추가
    //     interactionRules.Add(new InteractionRule(PetType.Leopard, PetType.Tiger, InteractionType.Fight));
    //     interactionRules.Add(new InteractionRule(PetType.Rabbit, PetType.Turtle, InteractionType.Race));
    //     // ... 기타 규칙들 ...
    // }
    // 씬 시작 후 상호작용 체크 시작 전 지연 시간 추가
    public float startDelay = 3.0f;
    private bool canStartInteractions = false;

    private void Start()
    {
        // 지정된 시간 후에 상호작용 체크 활성화
        StartCoroutine(EnableInteractionsAfterDelay());
    }

    private IEnumerator EnableInteractionsAfterDelay()
    {
        Debug.Log("[PetInteractionManager] 상호작용 체크 지연 중...");
        yield return new WaitForSeconds(startDelay);
        canStartInteractions = true;
        Debug.Log("[PetInteractionManager] 상호작용 체크 활성화!");
    }
    // 매 프레임마다 실행되는 함수 - 상호작용 가능성을 지속적으로 체크
    private void Update()
    {
        // 준비가 됐을 때만 상호작용 체크
        if (canStartInteractions)
        {
            CheckForPetInteractions();
        }
    }

    // 펫 간 상호작용 가능성 확인하는 메서드
    private void CheckForPetInteractions()
    {
        // 현재 존재하는 모든 펫을 가져옴
        PetController[] allPets = FindObjectsOfType<PetController>();

        // 각 펫 쌍마다 상호작용 가능성 확인 (이중 반복문으로 모든 가능한 펫 쌍 검사)
        for (int i = 0; i < allPets.Length; i++)
        {
            for (int j = i + 1; j < allPets.Length; j++) // i+1부터 시작해 중복 검사 방지
            {
                // 이미 상호작용 중인 펫은 건너뛰기
                if (IsInteracting(allPets[i]) || IsInteracting(allPets[j]))
                {
                    continue;
                }

                // 상호작용 쿨다운 체크 - 최근에 상호작용한 펫은 쿨다운 기간 동안 건너뛰기
                if (IsOnCooldown(allPets[i]) || IsOnCooldown(allPets[j]))
                {
                    continue;
                }

                // 두 펫 사이의 거리 확인
                float distance = Vector3.Distance(allPets[i].transform.position, allPets[j].transform.position);

                // 상호작용 거리 이내인 경우만 처리
                if (distance <= interactionDistance)
                {
                    // 디버그용 펫 이름 가져오기
                    string pet1Name = allPets[i].petName;
                    string pet2Name = allPets[j].petName;

                    // 두 펫의 종류 가져오기
                    PetType type1 = allPets[i].PetType;
                    PetType type2 = allPets[j].PetType;

                    // 적합한 상호작용 찾기
                    BasePetInteraction suitableInteraction = FindSuitableInteraction(allPets[i], allPets[j]);

                    if (suitableInteraction != null)
                    {
                        Debug.Log($"[PetInteractionManager] {pet1Name}와(과) {pet2Name} 사이에 {suitableInteraction.InteractionName} 상호작용 발견. 상호작용 시작!");

                        // 상호작용 시작
                        StartInteraction(allPets[i], allPets[j], suitableInteraction);

                        // 상호작용 쿨다운 설정
                        lastInteractionTime[allPets[i]] = Time.time;
                        lastInteractionTime[allPets[j]] = Time.time;

                        // 상호작용 기록 등록
                        interactingPets[allPets[i]] = allPets[j];
                        interactingPets[allPets[j]] = allPets[i];
                    }
                }
            }
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

        // 혹은 이전 방식대로 규칙 기반 검색 (새 컴포넌트가 없는 경우를 위해)
        // InteractionType? interactionType = GetInteractionType(pet1.PetType, pet2.PetType);
        // if (interactionType.HasValue)
        // {
        // 여기에는 이전 방식의 상호작용 선택 로직이 들어갈 수 있음
        // 하지만 컴포넌트 기반으로 전환됐으므로 이 부분은 점차 사라질 것임
        // }

        // 적합한 상호작용이 없는 경우
        return null;
    }

    // 두 펫 타입에 대한 상호작용 규칙 찾기 (이전 방식 유지)
    // private InteractionType? GetInteractionType(PetType type1, PetType type2)
    // {
    //     // 모든 상호작용 규칙 검사
    //     foreach (var rule in interactionRules)
    //     {
    //         // 두 펫 타입이 규칙에 정의된 타입과 일치하는지 확인 (순서 무관)
    //         if ((rule.pet1 == type1 && rule.pet2 == type2) || (rule.pet1 == type2 && rule.pet2 == type1))
    //         {
    //             return rule.interactionType; // 일치하는 상호작용 타입 반환
    //         }
    //     }

    //     return null; // 일치하는 규칙이 없으면 null 반환
    // }

    // 상호작용 시작 메서드
    private void StartInteraction(PetController pet1, PetController pet2, BasePetInteraction interaction)
    {
        // 상호작용 컴포넌트에 작업 위임
        interaction.StartInteraction(pet1, pet2);
    }

    // 펫이 상호작용 중인지 확인하는 메서드
    private bool IsInteracting(PetController pet)
    {
        return interactingPets.ContainsKey(pet); // 상호작용 중인 펫 딕셔너리에 존재하는지 확인
    }

    // 펫이 쿨다운 중인지 확인하는 메서드
    // private bool IsOnCooldown(PetController pet)
    // {
    //     if (lastInteractionTime.TryGetValue(pet, out float lastTime))
    //     {
    //         return Time.time - lastTime < interactionCooldown; // 마지막 상호작용 후 쿨다운 시간이 지났는지 확인
    //     }
    //     return false; // 처음 상호작용하는 경우 쿨다운 없음
    // }
    // 펫이 쿨다운 중인지 확인하는 메서드
    private bool IsOnCooldown(PetController pet)
    {
        if (lastInteractionTime.TryGetValue(pet, out float lastTime))
        {
            bool onCooldown = Time.time - lastTime < interactionCooldown;
            if (onCooldown)
            {
                float remainingTime = interactionCooldown - (Time.time - lastTime);
                Debug.Log($"[PetInteractionManager] {pet.petName}의 쿨다운 중. 남은 시간: {remainingTime:F1}초");
            }
            return onCooldown;
        }
        return false; // 처음 상호작용하는 경우 쿨다운 없음
    }


    // 상호작용 종료 알림 수신 메서드 (BasePetInteraction에서 호출)
    public void NotifyInteractionEnded(PetController pet1, PetController pet2)
    {
        // 상호작용 기록 제거
        if (pet1 != null) interactingPets.Remove(pet1);
        if (pet2 != null) interactingPets.Remove(pet2);
    }
}