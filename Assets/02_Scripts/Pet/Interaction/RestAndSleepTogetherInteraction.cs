// RestAndSleepTogetherInteraction.cs (최적화된 통합 버전)
using System.Collections;
using UnityEngine;
using UnityEngine.AI;
using PetAIProperties = PetTraits;
public class RestAndSleepTogetherInteraction : BasePetInteraction
{
    public override string InteractionName => "RestAndSleepTogether";

    // 우선순위: 45 (12순위)
    public override int Priority => 45;

    [Header("상호작용 모드")]
    [Tooltip("true면 잠자기, false면 휴식")]
    public bool isSleepMode = false;

    [Header("기본 설정")]
    [Tooltip("상호작용 총 시간")]
    public float interactionDuration = 15f;

    [Tooltip("상호작용 위치로 이동하는 최대 시간")]
    public float moveTimeout = 10f;

    [Tooltip("펫들 간의 거리")]
    public float defaultDistance = 3f;

    [Tooltip("큰 동물끼리의 거리")]
    public float largeAnimalDistance = 5f;

    [Tooltip("작은 동물끼리의 거리")]
    public float smallAnimalDistance = 2f;

    [Header("애니메이션 설정")]
    [Tooltip("휴식/잠자기 애니메이션 지속 시간")]
    public float restAnimationDuration = 3.0f;

    [Tooltip("준비 동작 (기지개 등) 지속 시간")]
    public float prepAnimationDuration = 1.5f;

    [Tooltip("깨어나기 애니메이션 지속 시간")]
    public float wakeUpAnimationDuration = 1.5f;

    [Header("이벤트 설정")]
    [Tooltip("특별 이벤트 최소 간격")]
    public float eventMinInterval = 2.5f;

    [Tooltip("특별 이벤트 최대 간격")]
    public float eventMaxInterval = 5f;

    [Tooltip("이벤트 발생 확률")]
    [Range(0f, 1f)]
    public float eventChance = 0.3f;

    [Header("감정 표현")]
    [Tooltip("감정 표현 지속 시간")]
    public float emotionDuration = 30f;

    [Header("잠자기 모드 전용")]
    [Tooltip("먼저 깨어날 펫의 확률")]
    [Range(0f, 1f)]
    public float firstWakeProbability = 0.5f;

    [Tooltip("잠자기 위치 조정 오프셋")]
    public float sleepPositionOffset = 0.3f;

    [Header("안전 설정")]
    [Tooltip("NavMeshAgent 안전 체크 최대 대기 시간")]
    public float agentSafetyTimeout = 5f;

    // 큰 동물 목록 (클래스 레벨 변수로 저장)
    private static readonly PetType[] LargeAnimals = new PetType[]
    {
        PetType.Cow, PetType.Elk, PetType.Deer, PetType.Camel, PetType.Bison,
        PetType.Ostrich, PetType.Horse, PetType.Zebra, PetType.Bull, PetType.Lioness,
        PetType.Giraffe, PetType.Lion, PetType.Rhino, PetType.Elephant, PetType.Sheep,
        PetType.Gorilla, PetType.Bear, PetType.Tiger, PetType.Buffalo, PetType.Hippo
    };

    protected override InteractionType DetermineInteractionType()
    {
        return isSleepMode ? InteractionType.SleepTogether : InteractionType.RestTogether;
    }

    public override bool CanInteract(PetController pet1, PetController pet2)
    {
        PetType type1 = pet1.PetType;
        PetType type2 = pet2.PetType;

        // 같은 종끼리는 항상 가능
        if (type1 == type2)
        {
            return true;
        }

        // 판다와 레서판다는 함께 쉬거나 잔다
        if ((type1 == PetType.Panda && type2 == PetType.RedPanda) || 
            (type1 == PetType.RedPanda && type2 == PetType.Panda))
        {
            return true;
        }

        // 늑대와 개는 함께 쉬거나 잔다
        if ((type1 == PetType.Wolf && type2 == PetType.Dog) || 
            (type1 == PetType.Dog && type2 == PetType.Wolf))
        {
            return true;
        }

      

        // 초식동물끼리는 함께 쉴 수 있음
        bool pet1IsHerbivore = (pet1.diet & PetAIProperties.DietaryFlags.Grass) != 0;
        bool pet2IsHerbivore = (pet2.diet & PetAIProperties.DietaryFlags.Grass) != 0;
        
        if (pet1IsHerbivore && pet2IsHerbivore)
        {
            return true;
        }

        return false;
    }

    protected override IEnumerator PerformInteraction(PetController pet1, PetController pet2)
    {
        string actionName = isSleepMode ? "잠자기" : "휴식";
        Debug.Log($"[{InteractionName}] {pet1.petName}와(과) {pet2.petName}의 {actionName} 시작!");

        // NavMeshAgent 준비 확인 (재시도 로직 포함)
        bool pet1Ready = false;
        bool pet2Ready = false;
        
        // 첫 번째 시도
        yield return StartCoroutine(WaitUntilAgentIsReady(pet1, agentSafetyTimeout));
        yield return StartCoroutine(WaitUntilAgentIsReady(pet2, agentSafetyTimeout));
        
        pet1Ready = IsAgentSafelyReady(pet1);
        pet2Ready = IsAgentSafelyReady(pet2);
        
        // 첫 번째 시도 실패 시 agent 재활성화 후 재시도
        if (!pet1Ready || !pet2Ready)
        {
            Debug.LogWarning($"[{InteractionName}] NavMeshAgent 준비 실패. 재시도합니다. (pet1: {pet1Ready}, pet2: {pet2Ready})");
            
            // Agent 재활성화 시도
            if (!pet1Ready && pet1.agent != null)
            {
                pet1.agent.enabled = false;
                yield return new WaitForSeconds(0.2f);
                pet1.agent.enabled = true;
                pet1.agent.Warp(pet1.transform.position);
            }
            
            if (!pet2Ready && pet2.agent != null)
            {
                pet2.agent.enabled = false;
                yield return new WaitForSeconds(0.2f);
                pet2.agent.enabled = true;
                pet2.agent.Warp(pet2.transform.position);
            }
            
            // 두 번째 시도
            yield return StartCoroutine(WaitUntilAgentIsReady(pet1, agentSafetyTimeout));
            yield return StartCoroutine(WaitUntilAgentIsReady(pet2, agentSafetyTimeout));
            
            pet1Ready = IsAgentSafelyReady(pet1);
            pet2Ready = IsAgentSafelyReady(pet2);
        }

        if (!pet1Ready || !pet2Ready)
        {
            Debug.LogError($"[{InteractionName}] NavMeshAgent 준비 실패로 상호작용을 중단합니다. (pet1: {pet1.petName} ready: {pet1Ready}, pet2: {pet2.petName} ready: {pet2Ready})");
            EndInteraction(pet1, pet2);
            yield break;
        }

        // 원래 상태 저장
        PetOriginalState pet1State = new PetOriginalState(pet1);
        PetOriginalState pet2State = new PetOriginalState(pet2);

        try
        {
            // 감정 표현
            EmotionType emotion = isSleepMode ? EmotionType.Sleep : EmotionType.Sleepy;
            pet1.ShowEmotion(emotion, emotionDuration);
            pet2.ShowEmotion(emotion, emotionDuration);

            // 1. 준비 단계
            yield return StartCoroutine(PreparePhase(pet1, pet2));

            // 2. 메인 상호작용 단계 (휴식 또는 잠자기)
            yield return StartCoroutine(MainInteractionPhase(pet1, pet2));

            // 3. 종료 단계
            yield return StartCoroutine(EndPhase(pet1, pet2));

            Debug.Log($"[{InteractionName}] {actionName} 완료!");
        }
        finally
        {
            // 최종 정리
            Debug.Log($"[{InteractionName}] 상호작용 정리 시작.");

            // 원래 상태 복원
            pet1State.Restore(pet1);
            pet2State.Restore(pet2);

            // 애니메이션 정리
            pet1.GetComponent<PetAnimationController>()?.StopContinuousAnimation();
            pet2.GetComponent<PetAnimationController>()?.StopContinuousAnimation();

            // 공통 종료 처리
            EndInteraction(pet1, pet2);
            Debug.Log($"[{InteractionName}] 상호작용 정리 완료.");
        }
    }

    /// <summary>
    /// 1단계: 준비
    /// </summary>
    private IEnumerator PreparePhase(PetController pet1, PetController pet2)
    {
        Debug.Log($"[{InteractionName}] 1단계: 준비");

        // 상호작용 위치 계산
        Vector3 interactionSpot = FindInteractionSpot(pet1, pet2, 2f);

        // 펫 크기에 따른 거리 결정
        float distance = CalculateDistanceBySize(pet1, pet2);

        // 목표 위치 계산
        Vector3 pet1Target, pet2Target;
        CalculateStartPositions(pet1, pet2, out pet1Target, out pet2Target, distance);

        // 동기화된 속도로 이동 (느리게)
        float syncedSpeed = Mathf.Min(pet1.baseSpeed, pet2.baseSpeed) * 0.7f;
        pet1.agent.speed = syncedSpeed;
        pet2.agent.speed = syncedSpeed;

        // 위치로 이동
        yield return StartCoroutine(MoveToPositions(pet1, pet2, pet1Target, pet2Target, moveTimeout));

        // 서로 마주보기
        yield return StartCoroutine(SmoothlyLookAtEachOther(pet1, pet2, 0.5f));

        // 잠들기 전 루틴
        if (isSleepMode)
        {
            // 제자리에서 빙글빙글 돌기 (개/고양이 습성)
            int rotationPet = Random.Range(0, 3); // 0: 없음, 1: pet1, 2: pet2
            if (rotationPet > 0)
            {
                PetController rotatingPet = rotationPet == 1 ? pet1 : pet2;
                var rotAnim = rotatingPet.GetComponent<PetAnimationController>();
                
                Debug.Log($"[{InteractionName}] {rotatingPet.petName}이(가) 편한 자세를 찾습니다.");
                yield return StartCoroutine(rotAnim.PlayAnimationWithCustomDuration(
                    PetAnimationController.PetAnimationType.Walk, 2f, false, false));
                
                // 회전하면서 자리 잡기
                float rotationTime = 0f;
                while (rotationTime < 2f)
                {
                    rotatingPet.transform.Rotate(0, 180f * Time.deltaTime, 0);
                    rotationTime += Time.deltaTime;
                    yield return null;
                }
            }
        }

        // 준비 동작 (기지개 + 하품)
        if (Random.value < 0.7f)
        {
            var prepPet = Random.value < 0.5f ? pet1 : pet2;
            var prepAnim = prepPet.GetComponent<PetAnimationController>();
            
            // 하품 감정 표현
            prepPet.ShowEmotion(EmotionType.Sleepy, 3f);
            
            // 기지개 애니메이션
            yield return StartCoroutine(prepAnim.PlayAnimationWithCustomDuration(
                PetAnimationController.PetAnimationType.Jump, prepAnimationDuration, false, false));
        }

        // 앉는 동작으로 전환
        var pet1Anim = pet1.GetComponent<PetAnimationController>();
        var pet2Anim = pet2.GetComponent<PetAnimationController>();
        
        StartCoroutine(pet1Anim.PlayAnimationWithCustomDuration(
            PetAnimationController.PetAnimationType.Eat, 1f, false, false));
        yield return StartCoroutine(pet2Anim.PlayAnimationWithCustomDuration(
            PetAnimationController.PetAnimationType.Eat, 1f, false, false));

        // 잠자기 모드일 경우 추가 위치 조정
        if (isSleepMode)
        {
            AdjustSleepingPositions(pet1, pet2, interactionSpot);
        }
    }

    /// <summary>
    /// 2단계: 메인 상호작용 (휴식 또는 잠자기)
    /// </summary>
    private IEnumerator MainInteractionPhase(PetController pet1, PetController pet2)
    {
        Debug.Log($"[{InteractionName}] 2단계: 메인 상호작용");

        var pet1Anim = pet1.GetComponent<PetAnimationController>();
        var pet2Anim = pet2.GetComponent<PetAnimationController>();

        // 휴식/잠자기 애니메이션 시작
        pet1Anim.SetContinuousAnimation(PetAnimationController.PetAnimationType.Rest);
        pet2Anim.SetContinuousAnimation(PetAnimationController.PetAnimationType.Rest);

        float elapsedTime = 0f;
        float nextEventTime = Random.Range(eventMinInterval, eventMaxInterval);

        while (elapsedTime < interactionDuration)
        {
            // 특별 이벤트 체크
            if (elapsedTime >= nextEventTime && Random.value < eventChance)
            {
                yield return StartCoroutine(PerformSpecialEvent(pet1, pet2));
                nextEventTime = elapsedTime + Random.Range(eventMinInterval, eventMaxInterval);
            }

            elapsedTime += Time.deltaTime;
            yield return null;
        }
    }

    /// <summary>
    /// 3단계: 종료 (깨어나기)
    /// </summary>
    private IEnumerator EndPhase(PetController pet1, PetController pet2)
    {
        Debug.Log($"[{InteractionName}] 3단계: 종료");

        var pet1Anim = pet1.GetComponent<PetAnimationController>();
        var pet2Anim = pet2.GetComponent<PetAnimationController>();

        if (isSleepMode)
        {
            // 잠자기 모드: 친구 깨우기 시퀀스
            PetController firstAwake = Random.value < firstWakeProbability ? pet1 : pet2;
            PetController secondAwake = firstAwake == pet1 ? pet2 : pet1;
            var firstAnim = firstAwake.GetComponent<PetAnimationController>();
            var secondAnim = secondAwake.GetComponent<PetAnimationController>();

            // 첫 번째 펫이 천천히 깨어남
            firstAnim.StopContinuousAnimation();
            // Rest → Eat (앉은 자세)
            yield return StartCoroutine(firstAnim.PlayAnimationWithCustomDuration(
                PetAnimationController.PetAnimationType.Eat, 1f, false, false));
            
            // 놀람 표정으로 주변 둘러보기
            firstAwake.ShowEmotion(EmotionType.Surprised, 3f);
            yield return new WaitForSeconds(0.5f);
            
            // 일어서기
            yield return StartCoroutine(firstAnim.PlayAnimationWithCustomDuration(
                PetAnimationController.PetAnimationType.Jump, wakeUpAnimationDuration, false, false));

            // 두 번째 펫에게 다가가기
            Vector3 originalPos = firstAwake.transform.position;
            Vector3 targetPos = secondAwake.transform.position + (firstAwake.transform.position - secondAwake.transform.position).normalized * 1.5f;
            float moveTime = 1f;
            float elapsedTime = 0f;
            
            while (elapsedTime < moveTime)
            {
                firstAwake.transform.position = Vector3.Lerp(originalPos, targetPos, elapsedTime / moveTime);
                elapsedTime += Time.deltaTime;
                yield return null;
            }
            
            // 친구를 부드럽게 건드려 깨우기
            firstAwake.ShowEmotion(EmotionType.Friend, 3f);
            yield return StartCoroutine(firstAnim.PlayAnimationWithCustomDuration(
                PetAnimationController.PetAnimationType.Attack, 0.5f, false, false));
            
            yield return new WaitForSeconds(0.3f);
            
            // 두 번째 펫이 깜짝 놀라며 일어남
            secondAwake.ShowEmotion(EmotionType.Surprised, 3f);
            secondAnim.StopContinuousAnimation();
            yield return StartCoroutine(secondAnim.PlayAnimationWithCustomDuration(
                PetAnimationController.PetAnimationType.Jump, wakeUpAnimationDuration * 1.5f, false, false));
            
            // 연쇄 하품 효과
            if (Random.value < 0.7f)
            {
                Debug.Log($"[{InteractionName}] 연쇄 하품!");
                firstAwake.ShowEmotion(EmotionType.Sleepy, 2f);
                yield return new WaitForSeconds(0.5f);
                secondAwake.ShowEmotion(EmotionType.Sleepy, 2f);
                yield return new WaitForSeconds(0.5f);
            }
        }
        else
        {
            // 휴식 모드: 함께 기지개 시퀀스
            pet1Anim.StopContinuousAnimation();
            pet2Anim.StopContinuousAnimation();

            // 동시에 앉은 자세로 전환
            StartCoroutine(pet1Anim.PlayAnimationWithCustomDuration(
                PetAnimationController.PetAnimationType.Eat, 1f, false, false));
            yield return StartCoroutine(pet2Anim.PlayAnimationWithCustomDuration(
                PetAnimationController.PetAnimationType.Eat, 1f, false, false));
            
            yield return new WaitForSeconds(0.5f);
            
            // 서로 바라보며 Friend 감정
            yield return StartCoroutine(SmoothlyLookAtEachOther(pet1, pet2, 0.5f));
            pet1.ShowEmotion(EmotionType.Friend, 3f);
            pet2.ShowEmotion(EmotionType.Friend, 3f);
            
            yield return new WaitForSeconds(0.5f);
            
            // 동시에 기지개 (아침 체조)
            Debug.Log($"[{InteractionName}] 함께 기지개를 켭니다.");
            StartCoroutine(pet1Anim.PlayAnimationWithCustomDuration(
                PetAnimationController.PetAnimationType.Jump, 2f, false, false));
            yield return StartCoroutine(pet2Anim.PlayAnimationWithCustomDuration(
                PetAnimationController.PetAnimationType.Jump, 2f, false, false));
        }

        // 상쾌한 기분 표현
        pet1.ShowEmotion(EmotionType.Happy, 5f);
        pet2.ShowEmotion(EmotionType.Happy, 5f);

        yield return new WaitForSeconds(1f);
    }

    /// <summary>
    /// 특별 이벤트 수행
    /// </summary>
    private IEnumerator PerformSpecialEvent(PetController pet1, PetController pet2)
    {
        PetController activePet = Random.value < 0.5f ? pet1 : pet2;
        PetController passivePet = activePet == pet1 ? pet2 : pet1;
        var activeAnim = activePet.GetComponent<PetAnimationController>();
        var passiveAnim = passivePet.GetComponent<PetAnimationController>();

        int eventType = isSleepMode ? Random.Range(0, 8) : Random.Range(0, 5);
        
        switch (eventType)
        {
            case 0: // 기지개 (하품)
                Debug.Log($"[{InteractionName}] {activePet.petName}이(가) 기지개를 켭니다.");
                activePet.ShowEmotion(EmotionType.Sleepy, 3f);
                yield return StartCoroutine(activeAnim.PlayAnimationWithCustomDuration(
                    PetAnimationController.PetAnimationType.Jump, 1.5f, false, false));
                activeAnim.SetContinuousAnimation(PetAnimationController.PetAnimationType.Rest);
                break;

            case 1: // 상대 바라보기
                Debug.Log($"[{InteractionName}] {activePet.petName}이(가) {passivePet.petName}을(를) 바라봅니다.");
                yield return StartCoroutine(SmoothlyLookAtEachOther(activePet, passivePet, 0.5f));
                yield return new WaitForSeconds(2f);
                break;

            case 2: // 자세 변경 (앉았다가 다시 눕기)
                Debug.Log($"[{InteractionName}] {activePet.petName}이(가) 자세를 바꿉니다.");
                yield return StartCoroutine(activeAnim.PlayAnimationWithCustomDuration(
                    PetAnimationController.PetAnimationType.Eat, 2f, false, false));
                activeAnim.SetContinuousAnimation(PetAnimationController.PetAnimationType.Rest);
                break;

            case 3: // 제자리에서 빙글빙글 돌다가 다시 눕기
                Debug.Log($"[{InteractionName}] {activePet.petName}이(가) 편한 자세를 찾습니다.");
                // 짧은 걷기 애니메이션으로 제자리 회전 표현
                activeAnim.StopContinuousAnimation();
                yield return StartCoroutine(activeAnim.PlayAnimationWithCustomDuration(
                    PetAnimationController.PetAnimationType.Walk, 1f, false, false));
                // 90도씩 회전
                for (int i = 0; i < 3; i++)
                {
                    activePet.transform.Rotate(0, 90f, 0);
                    yield return new WaitForSeconds(0.2f);
                }
                activeAnim.SetContinuousAnimation(PetAnimationController.PetAnimationType.Rest);
                break;

            case 4: // 친구와 가까이 붙기 (친밀도 표현)
                if (Vector3.Distance(activePet.transform.position, passivePet.transform.position) > 2f)
                {
                    Debug.Log($"[{InteractionName}] {activePet.petName}이(가) {passivePet.petName}에게 더 가까이 갑니다.");
                    activePet.ShowEmotion(EmotionType.Friend, 3f);
                    passivePet.ShowEmotion(EmotionType.Love, 3f);
                    Vector3 moveDir = (passivePet.transform.position - activePet.transform.position).normalized;
                    activePet.transform.position += moveDir * 0.5f;
                }
                break;

            // 잠자기 모드 전용 이벤트
            case 5: // 코골이와 반응
                if (isSleepMode)
                {
                    Debug.Log($"[{InteractionName}] {activePet.petName}이(가) 코를 곱니다.");
                    // 코골이 표현 (짧은 Attack 애니메이션)
                    yield return StartCoroutine(activeAnim.PlayAnimationWithCustomDuration(
                        PetAnimationController.PetAnimationType.Attack, 0.2f, false, false));
                    activeAnim.SetContinuousAnimation(PetAnimationController.PetAnimationType.Rest);
                    
                    // 상대방 반응
                    passivePet.ShowEmotion(EmotionType.Surprised, 2f);
                    yield return new WaitForSeconds(0.5f);
                    passivePet.ShowEmotion(EmotionType.Confused, 2f);
                    // 상대방이 잠시 쳐다보기
                    yield return StartCoroutine(SmoothlyLookAtEachOther(passivePet, activePet, 0.3f));
                    yield return new WaitForSeconds(1f);
                }
                break;

            case 6: // 꿈꾸기 (좋은 꿈 vs 악몽)
                if (isSleepMode)
                {
                    bool isGoodDream = Random.value > 0.5f;
                    if (isGoodDream)
                    {
                        Debug.Log($"[{InteractionName}] {activePet.petName}이(가) 좋은 꿈을 꿉니다.");
                        activePet.ShowEmotion(EmotionType.Happy, 5f);
                        // 다리 살짝 움직이기 (달리는 꿈)
                        yield return StartCoroutine(activeAnim.PlayAnimationWithCustomDuration(
                            PetAnimationController.PetAnimationType.Jump, 0.5f, false, false));
                    }
                    else
                    {
                        Debug.Log($"[{InteractionName}] {activePet.petName}이(가) 악몽을 꿉니다.");
                        activePet.ShowEmotion(EmotionType.Confused, 3f);
                        // 몸 떨기 효과
                        for (int i = 0; i < 3; i++)
                        {
                            activePet.transform.position += new Vector3(Random.Range(-0.05f, 0.05f), 0, Random.Range(-0.05f, 0.05f));
                            yield return new WaitForSeconds(0.1f);
                        }
                    }
                    activeAnim.SetContinuousAnimation(PetAnimationController.PetAnimationType.Rest);
                }
                break;

            case 7: // 장난스럽게 친구 건드리기
                if (isSleepMode && Random.value < 0.3f)
                {
                    Debug.Log($"[{InteractionName}] {activePet.petName}이(가) {passivePet.petName}을(를) 살짝 건드립니다.");
                    activePet.ShowEmotion(EmotionType.Joke, 3f);
                    
                    // 살짝 일어나서
                    activeAnim.StopContinuousAnimation();
                    yield return StartCoroutine(activeAnim.PlayAnimationWithCustomDuration(
                        PetAnimationController.PetAnimationType.Eat, 1f, false, false));
                    
                    // 가볍게 건드리기
                    yield return StartCoroutine(activeAnim.PlayAnimationWithCustomDuration(
                        PetAnimationController.PetAnimationType.Attack, 0.5f, false, false));
                    
                    // 상대방 반응
                    passivePet.ShowEmotion(EmotionType.Surprised, 2f);
                    passiveAnim.StopContinuousAnimation();
                    yield return new WaitForSeconds(0.5f);
                    
                    // 둘 다 다시 자기
                    activeAnim.SetContinuousAnimation(PetAnimationController.PetAnimationType.Rest);
                    passiveAnim.SetContinuousAnimation(PetAnimationController.PetAnimationType.Rest);
                }
                break;
        }
    }

    /// <summary>
    /// 펫 크기에 따른 거리 계산
    /// </summary>
    private float CalculateDistanceBySize(PetController pet1, PetController pet2)
    {
        bool pet1IsLarge = IsPetLarge(pet1.PetType);
        bool pet2IsLarge = IsPetLarge(pet2.PetType);

        if (pet1IsLarge && pet2IsLarge)
            return largeAnimalDistance;
        else if (!pet1IsLarge && !pet2IsLarge)
            return smallAnimalDistance;
        else
            return defaultDistance;
    }

    /// <summary>
    /// 펫이 큰 동물인지 확인
    /// </summary>
    private bool IsPetLarge(PetType petType)
    {
        return System.Array.IndexOf(LargeAnimals, petType) >= 0;
    }

    /// <summary>
    /// 잠자기 위한 위치 조정
    /// </summary>
    private void AdjustSleepingPositions(PetController pet1, PetController pet2, Vector3 centerPoint)
    {
        Vector3 direction = new Vector3(Random.Range(-1f, 1f), 0, Random.Range(-1f, 1f)).normalized;
        
        // 약간 가까운 위치로 조정
        pet1.transform.position = centerPoint - direction * sleepPositionOffset;
        pet2.transform.position = centerPoint + direction * sleepPositionOffset;

        // 서로를 향해 약간 기울어진 각도로 회전
        Quaternion pet1Rot = Quaternion.LookRotation(pet2.transform.position - pet1.transform.position);
        Quaternion pet2Rot = Quaternion.LookRotation(pet1.transform.position - pet2.transform.position);

        pet1.transform.rotation = pet1Rot;
        pet2.transform.rotation = pet2Rot;

        if (pet1.petModelTransform != null)
            pet1.petModelTransform.rotation = pet1Rot;
        if (pet2.petModelTransform != null)
            pet2.petModelTransform.rotation = pet2Rot;
    }

    /// <summary>
    /// NavMeshAgent가 안전하게 준비되었는지 확인
    /// </summary>
    private bool IsAgentSafelyReady(PetController pet)
    {
        return pet != null && pet.agent != null && pet.agent.enabled && pet.agent.isOnNavMesh;
    }
}