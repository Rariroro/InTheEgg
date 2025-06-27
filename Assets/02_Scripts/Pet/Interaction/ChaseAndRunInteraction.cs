// 쫓고 쫓기기 상호작용 구현 (개선 버전)
using System.Collections;
using UnityEngine;

public class ChaseAndRunInteraction : BasePetInteraction
{
    public override string InteractionName => "ChaseAndRun";
    
    // 상호작용 유형 지정
    protected override InteractionType DetermineInteractionType()
    {
        return InteractionType.ChaseAndRun;
    }
    
    // 위치와 회전을 고정하는 코루틴 추가
    private IEnumerator FixPositionDuringInteraction(
        PetController pet1, PetController pet2,
        Vector3 pos1, Vector3 pos2,
        Quaternion rot1, Quaternion rot2,
        bool fixPet1 = true, bool fixPet2 = true)
    {
        // 상호작용이 진행되는 동안 위치와 회전 고정
        while (pet1.isInteracting && pet2.isInteracting)
        {
            // 펫1 위치/회전 고정 (필요한 경우)
            if (fixPet1)
            {
                pet1.transform.position = pos1;
                pet1.transform.rotation = rot1;
                // 모델도 함께 고정
                if (pet1.petModelTransform) pet1.petModelTransform.rotation = rot1;
            }
            
            // 펫2 위치/회전 고정 (필요한 경우)
            if (fixPet2)
            {
                pet2.transform.position = pos2;
                pet2.transform.rotation = rot2;
                // 모델도 함께 고정
                if (pet2.petModelTransform) pet2.petModelTransform.rotation = rot2;
            }
            
            yield return null;
        }
    }
    
    // 이 상호작용이 가능한지 확인
    public override bool CanInteract(PetController pet1, PetController pet2)
    {
        PetType type1 = pet1.PetType;
        PetType type2 = pet2.PetType;
        
        // 고양이와 개는 쫓고 쫓김
        if ((type1 == PetType.Cat && type2 == PetType.Dog) || 
            (type1 == PetType.Dog && type2 == PetType.Cat))
        {
            return true;
        }
        
        return false;
    }
    
    // 상호작용 수행
    public override IEnumerator PerformInteraction(PetController pet1, PetController pet2)
    {
        Debug.Log($"[ChaseAndRun] {pet1.petName}와(과) {pet2.petName} 사이의 쫓고 쫓기기 상호작용 시작!");
        
        // 쫓는 펫과 도망가는 펫 결정 (기본적으로 개가 고양이를 쫓음)
        PetController chaser = null;
        PetController runner = null;
        
        if (pet1.PetType == PetType.Dog && pet2.PetType == PetType.Cat)
        {
            chaser = pet1;
            runner = pet2;
        }
        else if (pet1.PetType == PetType.Cat && pet2.PetType == PetType.Dog)
        {
            chaser = pet2;
            runner = pet1;
        }
        else
        {
            // 고양이와 개가 아닌 경우 무작위로 결정
            chaser = Random.value > 0.5f ? pet1 : pet2;
            runner = chaser == pet1 ? pet2 : pet1;
        }
        
        Debug.Log($"[ChaseAndRun] 쫓는 펫: {chaser.petName}, 도망가는 펫: {runner.petName}");
        
        // 원래 상태 저장 (개선된 헬퍼 클래스 사용)
        PetOriginalState chaserState = new PetOriginalState(chaser);
        PetOriginalState runnerState = new PetOriginalState(runner);
        
        // 위치 고정 코루틴 참조 저장
        Coroutine fixPositionCoroutine = null;
        
        try
        {
            // 감정 표현 추가 - 쫓는 펫은 화남, 도망가는 펫은 두려움
            chaser.ShowEmotion(EmotionType.Love, 40f);
            runner.ShowEmotion(EmotionType.Angry, 40f);
            
            // 서로 마주보게 준비 (시작 전 긴장감)
            Vector3 chaserPos = chaser.transform.position;
            Vector3 runnerPos = runner.transform.position;
            
            // 위치 고정 설정
            Vector3 chaserFixedPos = chaserPos;
            Vector3 runnerFixedPos = runnerPos;
            Quaternion chaserFixedRot = chaser.transform.rotation;
            Quaternion runnerFixedRot = runner.transform.rotation;
            
            // 시작 동작 - 서로 마주보게 하기 (놀라는 효과 연출)
            LookAtEachOther(chaser, runner);
            chaserFixedRot = chaser.transform.rotation;
            runnerFixedRot = runner.transform.rotation;
            
            // 위치 고정 코루틴 시작
            fixPositionCoroutine = StartCoroutine(
                FixPositionDuringInteraction(
                    chaser, runner, 
                    chaserFixedPos, runnerFixedPos, 
                    chaserFixedRot, runnerFixedRot
                )
            );
            
            yield return new WaitForSeconds(1.0f); // 1초 대기
            
            // 위치 고정 코루틴 중지 (추격 시작을 위해)
            if (fixPositionCoroutine != null)
            {
                StopCoroutine(fixPositionCoroutine);
                fixPositionCoroutine = null;
            }
            
            // 애니메이션 컨트롤러 참조
            PetAnimationController chaserAnimController = chaser.GetComponent<PetAnimationController>();
            PetAnimationController runnerAnimController = runner.GetComponent<PetAnimationController>();
            
            // 쫓고 쫓기는 시간 설정
            float chaseTime = 40f; // 상호작용 총 지속 시간
            float startTime = Time.time; // 시작 시간 기록
            
            // 이동 가능하도록 설정
            chaser.agent.isStopped = false;
            runner.agent.isStopped = false;
            
            // 애니메이션 설정 (뛰기 -> 2)
            chaserAnimController.SetContinuousAnimation(PetAnimationController.PetAnimationType.Run); // 쫓는 펫 달리기 애니메이션
            runnerAnimController.SetContinuousAnimation(PetAnimationController.PetAnimationType.Run); // 도망가는 펫 달리기 애니메이션
            
            // 속도 변화 설정
            float runnerBaseSpeed = runnerState.originalSpeed * 1.9f; // 도망가는 펫 기본 속도
            float runnerPanicSpeed = runnerState.originalSpeed * 2.5f; // 도망가는 펫 공포 속도 (매우 가까울 때)
            float chaserBaseSpeed = chaserState.originalSpeed * 1.7f; // 쫓는 펫 기본 속도
            float chaserSprintSpeed = chaserState.originalSpeed * 2.2f; // 쫓는 펫 스프린트 속도 (멀어질 때)
            
            // 가속도 증가 (더 빠른 반응을 위해)
            chaser.agent.acceleration = chaserState.originalAcceleration * 2f;
            runner.agent.acceleration = runnerState.originalAcceleration * 2f;
            
            // 딜레이 설정 (방향 업데이트 간격)
            float normalUpdateInterval = 0.3f; // 일반적인 업데이트 간격
            float closeUpdateInterval = 0.1f; // 가까울 때 업데이트 간격
            float directionChangeInterval = 3f; // 방향 변경 주기
            float lastDirectionChangeTime = 0f; // 마지막 방향 변경 시간
            
            // 추격 단계 설정 (0: 시작, 1: 추격 중, 2: 근접 접근, 3: 멀어짐)
            int chasePhase = 0;
            
            // 이동 제어 루프 - 지정된 chase 시간 동안 추격 계속
            while (Time.time - startTime < chaseTime)
            {
                // 두 펫 사이의 거리 계산
                float distanceBetween = Vector3.Distance(chaser.transform.position, runner.transform.position);
                // 거리에 따라 업데이트 간격 조정 (가까울수록 더 자주 업데이트)
                float updateInterval = distanceBetween < 5f ? closeUpdateInterval : normalUpdateInterval;
                
                // 거리에 따른 속도 조정
                if (distanceBetween < 3f) // 매우 가까워짐
                {
                    runner.agent.speed = runnerPanicSpeed; // 도망가는 펫 공포 속도 적용
                    chaser.agent.speed = chaserBaseSpeed; // 쫓는 펫 기본 속도 유지
                    
                    if (chasePhase != 2) // 단계 전환 시 로그 출력
                    {
                        Debug.Log($"[ChaseAndRun] 쫓는 펫이 매우 가까워짐! 도망치는 펫이 더 빨리 도망침");
                        chasePhase = 2;
                        
                        // // 감정 표현 업데이트 - 도망치는 펫 더 무서워함
                        // runner.ShowEmotion(EmotionType.Scared, 40f);
                    }
                }
                else if (distanceBetween > 10f) // 멀어짐
                {
                    runner.agent.speed = runnerBaseSpeed; // 도망가는 펫 기본 속도 적용
                    chaser.agent.speed = chaserSprintSpeed; // 쫓는 펫 스프린트 속도 적용
                    
                    if (chasePhase != 3) // 단계 전환 시 로그 출력
                    {
                        Debug.Log($"[ChaseAndRun] 쫓는 펫이 멀어짐! 추격 속도 증가");
                        chasePhase = 3;
                        
                        // // 감정 표현 업데이트 - 쫓는 펫 더 화남
                        // chaser.ShowEmotion(EmotionType.Angry, 40f);
                    }
                }
                else // 적절한 거리
                {
                    runner.agent.speed = runnerBaseSpeed; // 도망가는 펫 기본 속도 적용
                    chaser.agent.speed = chaserBaseSpeed; // 쫓는 펫 기본 속도 적용
                    
                    if (chasePhase != 1) // 단계 전환 시 로그 출력
                    {
                        Debug.Log($"[ChaseAndRun] 쫓고 쫓기는 중...");
                        chasePhase = 1;
                    }
                }
                
                // 도망 방향 변경 로직
                bool shouldChangeDirection = false;
                
                // 정기적인 방향 변경 (directionChangeInterval 주기마다)
                if (Time.time - lastDirectionChangeTime > directionChangeInterval)
                {
                    shouldChangeDirection = true;
                    lastDirectionChangeTime = Time.time;
                }
                
                // 매우 가까워졌을 때 긴급 방향 전환 (50% 확률)
                if (distanceBetween < 2.5f && Random.value > 0.5f)
                {
                    shouldChangeDirection = true;
                }
                
                // 방향 변경이 필요한 경우
                if (shouldChangeDirection)
                {
                    // 도망치는 방향에 무작위성 추가 (기본 방향 + 무작위 오프셋)
                    Vector3 baseRunDirection = (runner.transform.position - chaser.transform.position).normalized;
                    
                    // 무작위 방향 성분 추가 (좌우로 흔들리는 효과)
                    Vector3 randomOffset = new Vector3(Random.Range(-1f, 1f), 0, Random.Range(-1f, 1f));
                    randomOffset = randomOffset.normalized * Random.Range(0.3f, 0.7f); // 무작위성 강도 조절
                    
                    Vector3 finalRunDirection = (baseRunDirection + randomOffset).normalized;
                    // 현재 위치에서 계산된 방향으로 10~20 유닛 떨어진 지점을 목표로 설정
                    Vector3 runTarget = runner.transform.position + finalRunDirection * Random.Range(10f, 20f);
                    
                    // NavMesh 위의 유효한 위치로 보정
                    runTarget = FindValidPositionOnNavMesh(runTarget, 20f);
                    runner.agent.SetDestination(runTarget);
                    Debug.Log($"[ChaseAndRun] 도망치는 펫 방향 변경: {runner.petName} -> {runTarget}");
                }
                
                // 쫓는 펫은 도망가는 펫의 현재 위치를 목표로 계속 업데이트
                chaser.agent.SetDestination(runner.transform.position);
                
                // 다음 업데이트까지 대기
                yield return new WaitForSeconds(updateInterval);
                
                // 가끔 쫓는 펫이 갑자기 스퍼트를 내는 효과 (임의의 순간에 5% 확률로)
                if (distanceBetween > 5f && distanceBetween < 12f && Random.value > 0.95f)
                {
                    float originalSpeed = chaser.agent.speed;
                    chaser.agent.speed = chaserState.originalSpeed * 2.8f; // 순간 가속
                    Debug.Log($"[ChaseAndRun] {chaser.petName}이(가) 순간 가속!");
                    
                    yield return new WaitForSeconds(0.8f); // 0.8초 동안 스퍼트 유지
                    
                    chaser.agent.speed = originalSpeed; // 원래 속도로 복귀
                }
            }
            
            Debug.Log($"[ChaseAndRun] 쫓고 쫓기기 종료 (시간 초과: {chaseTime}초)");
            
            // 쫓던 동물은 쉬고, 쫓기던 동물은 계속 도망치도록 종료 연출
            yield return SeparatePetsAfterChase(chaser, runner, chaserAnimController, runnerAnimController);
        }
        finally
        {
            // 위치 고정 코루틴 중지
            if (fixPositionCoroutine != null)
            {
                StopCoroutine(fixPositionCoroutine);
            }
            
            // 감정 말풍선 숨기기
            chaser.HideEmotion();
            runner.HideEmotion();
            
            // 원래 속도와 가속도로 복원 (예외 발생해도 실행됨)
            chaserState.Restore(chaser);
            runnerState.Restore(runner);
            
            // 애니메이션 컨트롤러 리셋
            chaser.GetComponent<PetAnimationController>().StopContinuousAnimation();
            runner.GetComponent<PetAnimationController>().StopContinuousAnimation();
            
            Debug.Log($"[ChaseAndRun] 펫들의 속도와 가속도가 원래대로 복원됨");
        }
    }
    
    // 쫓기 상호작용 후 자연스러운 종료를 위한 메서드
    private IEnumerator SeparatePetsAfterChase(
        PetController chaser, PetController runner,
        PetAnimationController chaserAnimController, PetAnimationController runnerAnimController)
    {
        Debug.Log($"[ChaseAndRun] 추격 종료 시작 - {chaser.petName}이(가) 지쳐서 멈추고, {runner.petName}이(가) 도망침");
        
        // 1. 쫓던 동물이 지쳐서 멈춤
        chaser.agent.isStopped = true;
        
        // // 감정 표현 업데이트 - 쫓는 펫은 지침
        // chaser.ShowEmotion(EmotionType.Sleepy, 10f);
        
        // 쫓던 동물은 쉬는 애니메이션(5번) 실행
        yield return StartCoroutine(chaserAnimController.PlayAnimationWithCustomDuration(PetAnimationController.PetAnimationType.Rest, 3.0f, false, false));
        
        // 2. 쫓기던 동물은 계속 도망감
        // 현재 도망치는 방향 계산
        Vector3 escapeDirection = (runner.transform.position - chaser.transform.position).normalized;
        Vector3 escapeTarget = runner.transform.position + escapeDirection * 20f;
        
        // // 감정 표현 업데이트 - 도망치는 펫은 계속 두려움
        // runner.ShowEmotion(EmotionType.Scared, 10f);
        
        // NavMesh에서 유효한 위치 찾기
        escapeTarget = FindValidPositionOnNavMesh(escapeTarget, 20f);
        runner.agent.SetDestination(escapeTarget);
        Debug.Log($"[ChaseAndRun] {runner.petName}이(가) 계속 도망가는 중... ({escapeTarget})");
        
        // 3. 쫓기던 동물이 일정 거리를 도망간 후 종료
        float initialDistance = Vector3.Distance(chaser.transform.position, runner.transform.position);
        float safeDistance = initialDistance + 10f; // 안전 거리 (초기 거리 + 10 유닛)
        float maxWaitTime = 5f;  // 최대 대기 시간
        float startTime = Time.time;
        
        while (Time.time - startTime < maxWaitTime)
        {
            float currentDistance = Vector3.Distance(chaser.transform.position, runner.transform.position);
            
            // 충분히 멀어졌거나 목적지에 도착했으면 종료
            if (currentDistance >= safeDistance ||
                (!runner.agent.pathPending && runner.agent.remainingDistance <= runner.agent.stoppingDistance))
            {
                Debug.Log($"[ChaseAndRun] {runner.petName}이(가) 충분히 도망침 (거리: {currentDistance})");
                break;
            }
            
            yield return null; // 다음 프레임까지 대기
        }
        
        // 4. 쫓던 동물은 일어나는 모션 (다시 idle 애니메이션으로)
        chaserAnimController.StopContinuousAnimation();
        
        // 5. 쫓기던 동물은 뛰기를 점차 걷기로 전환 (애니메이션 1번 - 걷기)
        runnerAnimController.SetContinuousAnimation(PetAnimationController.PetAnimationType.Walk);
        
        yield return new WaitForSeconds(1.5f); // 추가 대기 시간
        
        Debug.Log($"[ChaseAndRun] 추격 종료 연출 완료");
    }
}