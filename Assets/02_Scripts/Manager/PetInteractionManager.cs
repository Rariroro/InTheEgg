using System.Collections;
using System.Collections.Generic;
using UnityEngine;

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

// 펫 간 상호작용 종류를 정의하는 열거형
public enum InteractionType
{
    Fight,        // 싸우기
    WalkTogether, // 같이 걷기
    RestTogether, // 같이 쉬기
    Race,         // 달리기 시합
    ChaseAndRun,  // 쫓고 쫓기기
    SleepTogether, // 같이 자기
    RideAndWalk,   // 타고 걷기
    SlothKoalaRace, // 나무늘보-코알라 달리기
    ChameleonCamouflage // 카멜레온 위장
}

/// <summary>
/// 간소화된 펫 상호작용 매니저
/// 콜라이더 기반 감지 시스템과 함께 작동
/// </summary>
public class PetInteractionManager : MonoBehaviour
{
    // 싱글톤 패턴
    public static PetInteractionManager Instance { get; private set; }

    [Header("상호작용 설정")]
    [Tooltip("레거시 쿨타임 값 - CooldownManager 사용 시 무시됨")]
    [System.Obsolete("CooldownManager를 사용하세요")]
    public float interactionCooldown = 30f;

    [Header("시작 지연")]
    public float startDelay = 3.0f;
    private bool canStartInteractions = false;

    [Header("쿨타임 관리")]
    [Tooltip("CooldownManager 사용 여부")]
    public bool useCooldownManager = true;

    // 상호작용 관리
    private Dictionary<PetController, PetController> interactingPets = new Dictionary<PetController, PetController>();
    private Dictionary<PetController, float> lastInteractionTime = new Dictionary<PetController, float>(); // 레거시 호환용

    // 쿨타임 시스템 (펫 쌍별로 관리)
    private Dictionary<(string, string), float> interactionCooldowns = new Dictionary<(string, string), float>();
    
    // 등록된 상호작용 컴포넌트
    private List<BasePetInteraction> registeredInteractions = new List<BasePetInteraction>();
    
    // 펫 목록 (선택적 - 디버그용)
    private List<PetController> allPets = new List<PetController>();

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

        // 상호작용 컴포넌트 등록
        RegisterInteractions();
    }

    private void Start()
    {
        // 초기 펫 리스트 구축 (선택적)
        RefreshPetList();
        
        // 지정된 시간 후에 상호작용 활성화
        StartCoroutine(EnableInteractionsAfterDelay());
    }

    private IEnumerator EnableInteractionsAfterDelay()
    {
        yield return new WaitForSeconds(startDelay);
        canStartInteractions = true;
        // Debug.Log("[PetInteractionManager] 상호작용 시스템 활성화!");
    }

    private void RegisterInteractions()
    {
        registeredInteractions.AddRange(GetComponents<BasePetInteraction>());
        // Debug.Log($"[PetInteractionManager] {registeredInteractions.Count}개의 상호작용 컴포넌트 등록됨");
    }

    /// <summary>
    /// 콜라이더 감지기로부터 상호작용 요청 처리
    /// </summary>
    public void RequestInteraction(PetController pet1, PetController pet2)
    {
        if (!canStartInteractions)
            return;

        // 빠른 거부 조건들
        if (pet1 == null || pet2 == null)
            return;

        if (IsInteracting(pet1) || IsInteracting(pet2))
            return;

        // 펫 쌍 쿨타임 체크 (새로운 방식)
        if (IsInteractionOnCooldown(pet1, pet2))
            return;

        // 개별 펫 쿨타임 체크 (레거시 호환)
        if (IsOnCooldown(pet1) || IsOnCooldown(pet2))
            return;

        // 적합한 상호작용 찾기
        BasePetInteraction suitableInteraction = FindSuitableInteraction(pet1, pet2);
        if (suitableInteraction == null)
            return;

        // 상호작용 시작
        StartInteraction(pet1, pet2, suitableInteraction);
    }
    
    private void StartInteraction(PetController pet1, PetController pet2, BasePetInteraction interaction)
    {
        // Debug.Log($"[PetInteractionManager] {pet1.petName}와 {pet2.petName} 사이에 {interaction.InteractionName} 시작!");

        // 상호작용 실행
        interaction.StartInteraction(pet1, pet2);

        // 펫 쌍 쿨타임 기록 (새로운 방식)
        var key = GetInteractionKey(pet1, pet2);
        interactionCooldowns[key] = Time.time;

        // 개별 펫 쿨타임 기록 (레거시 호환)
        if (useCooldownManager && CooldownManager.Instance != null)
        {
            // CooldownManager 사용
            CooldownManager.Instance.StartCooldown(
                CooldownManager.CooldownType.PetInteraction,
                pet1.petName);
            CooldownManager.Instance.StartCooldown(
                CooldownManager.CooldownType.PetInteraction,
                pet2.petName);
        }
        else
        {
            // 레거시 방식 사용
            float currentTime = Time.time;
            lastInteractionTime[pet1] = currentTime;
            lastInteractionTime[pet2] = currentTime;
        }

        interactingPets[pet1] = pet2;
        interactingPets[pet2] = pet1;
    }

    private BasePetInteraction FindSuitableInteraction(PetController pet1, PetController pet2)
    {
        foreach (var interaction in registeredInteractions)
        {
            if (interaction.CanInteract(pet1, pet2))
            {
                return interaction;
            }
        }
        return null;
    }

    /// <summary>
    /// 상호작용 종료 시 호출
    /// </summary>
    public void NotifyInteractionEnded(PetController pet1, PetController pet2)
    {
        bool pet1Valid = pet1 != null;
        bool pet2Valid = pet2 != null;
        
        if (pet1Valid)
        {
            if (interactingPets.ContainsKey(pet1))
            {
                interactingPets.Remove(pet1);
            }
            pet1.State.EndInteraction();
        }
        
        if (pet2Valid)
        {
            if (interactingPets.ContainsKey(pet2))
            {
                interactingPets.Remove(pet2);
            }
            pet2.State.EndInteraction();
        }
        
        // Debug.Log($"[PetInteractionManager] 상호작용 종료: {(pet1Valid ? pet1.petName : "null")} - {(pet2Valid ? pet2.petName : "null")}");
    }

    private bool IsInteracting(PetController pet)
    {
        return interactingPets.ContainsKey(pet);
    }

    private bool IsOnCooldown(PetController pet)
    {
        // 이 메서드는 개별 펫의 쿨다운을 체크하는 레거시 메서드
        // 새로운 IsInteractionOnCooldown 메서드를 사용하는 것을 권장
        if (useCooldownManager && CooldownManager.Instance != null)
        {
            // CooldownManager 사용
            return CooldownManager.Instance.IsOnCooldown(
                CooldownManager.CooldownType.PetInteraction,
                pet.petName);
        }
        else
        {
            // 레거시 방식 사용
            if (lastInteractionTime.TryGetValue(pet, out float lastTime))
            {
                #pragma warning disable CS0618 // 사용되지 않는 멤버 사용 경고 무시
                return Time.time - lastTime < interactionCooldown;
                #pragma warning restore CS0618
            }
            return false;
        }
    }

    /// <summary>
    /// 펫 쌍의 상호작용 쿨타임 체크 (새로운 방식)
    /// </summary>
    private bool IsInteractionOnCooldown(PetController pet1, PetController pet2)
    {
        var key = GetInteractionKey(pet1, pet2);

        if (interactionCooldowns.ContainsKey(key))
        {
            float timeSinceLastInteraction = Time.time - interactionCooldowns[key];
            float cooldown = 60f; // 기본 60초

            // CooldownManager는 내부적으로 설정값을 관리하므로 기본값 사용

            if (timeSinceLastInteraction < cooldown)
            {
                // 쿨타임 중
                ShowCooldownFeedback(pet1, pet2, cooldown - timeSinceLastInteraction);
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// 펫 쌍의 고유 키 생성
    /// </summary>
    private (string, string) GetInteractionKey(PetController pet1, PetController pet2)
    {
        string name1 = pet1.petName;
        string name2 = pet2.petName;
        // 알파벳 순서로 정렬하여 순서에 관계없이 같은 키를 생성
        return name1.CompareTo(name2) < 0 ? (name1, name2) : (name2, name1);
    }

    /// <summary>
    /// 쿨타임 피드백 표시
    /// </summary>
    private void ShowCooldownFeedback(PetController pet1, PetController pet2, float remainingTime)
    {
        // UI나 감정 표현으로 쿨타임 피드백
        string message = $"쿨타임: {remainingTime:F0}초 남음";
        Debug.Log($"[PetInteractionManager] {pet1.petName} - {pet2.petName} {message}");

        // 펫 위에 쿨타임 표시 (Sleepy 감정으로 대체)
        if (pet1 != null) pet1.ShowEmotion(EmotionType.Sleepy, 2f);
        if (pet2 != null) pet2.ShowEmotion(EmotionType.Sleepy, 2f);
    }

    // === 선택적 메서드들 (디버그/호환성용) ===
    
    public void RefreshPetList()
    {
        allPets.Clear();
        PetController[] foundPets = FindObjectsOfType<PetController>();
        allPets.AddRange(foundPets);
        // Debug.Log($"[PetInteractionManager] 펫 리스트 새로고침 완료. 총 {allPets.Count}마리");
    }

    public void RegisterPet(PetController pet)
    {
        if (pet != null && !allPets.Contains(pet))
        {
            allPets.Add(pet);
        // Debug.Log($"[PetInteractionManager] 펫 등록: {pet.petName}");
        }
    }

    public void UnregisterPet(PetController pet)
    {
        if (pet != null && allPets.Contains(pet))
        {
            allPets.Remove(pet);
            
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

    // === 디버그 메서드들 ===
    
    [ContextMenu("펫 리스트 새로고침")]
    public void ForceRefreshPetList()
    {
        RefreshPetList();
    }

    [ContextMenu("현재 상태 출력")]
    public void PrintCurrentStatus()
    {
        // Debug.Log($"[PetInteractionManager] 현재 상태:");
        // Debug.Log($"  - 총 펫 수: {allPets.Count}");
        // Debug.Log($"  - 상호작용 중인 펫 쌍: {interactingPets.Count / 2}");
        // Debug.Log($"  - 쿨다운 중인 펫: {lastInteractionTime.Count}");
        // Debug.Log($"  - 등록된 상호작용: {registeredInteractions.Count}개");
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