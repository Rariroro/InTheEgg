// 여우-두더지 상호작용 구현
using System.Collections;
using UnityEngine;

public class FoxMoleInteraction : BasePetInteraction
{
    public override string InteractionName => "FoxMoleHunt";
    
    // 상호작용 유형 지정
    protected override InteractionType DetermineInteractionType()
    {
                // 새로운 상호작용 유형이 필요한 경우 PetInteractionManager.cs의 InteractionType 열거형에 추가해야 함
        return InteractionType.ChaseAndRun; // 기존 쫓고 쫓기기 타입 활용
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
        
        // 여우와 두더지 간 상호작용만 가능
        if ((type1 == PetType.Fox && type2 == PetType.Mole) || 
            (type1 == PetType.Mole && type2 == PetType.Fox))
        {
            return true;
        }
        
        return false;
    }
    
    // 상호작용 수행
    protected override IEnumerator PerformInteraction(PetController pet1, PetController pet2)
    {
        Debug.Log($"[FoxMoleHunt] {pet1.petName}와(과) {pet2.petName} 사이의 여우-두더지 상호작용 시작!");
        
        // 여우와 두더지 식별
        PetController fox = null;
        PetController mole = null;
        
        if (pet1.PetType == PetType.Fox && pet2.PetType == PetType.Mole)
        {
            fox = pet1;
            mole = pet2;
        }
        else if (pet1.PetType == PetType.Mole && pet2.PetType == PetType.Fox)
        {
            fox = pet2;
            mole = pet1;
        }
        else
        {
            // 여우나 두더지가 아닌 경우 임의로 역할 배정 (오류 방지용)
            fox = pet1;
            mole = pet2;
            Debug.LogWarning("[FoxMoleHunt] 여우와 두더지가 아닌 펫들로 상호작용이 시작되었습니다.");
        }
        
        Debug.Log($"[FoxMoleHunt] 여우 역할: {fox.petName}, 두더지 역할: {mole.petName}");
        
        // 원래 상태 저장 (개선된 헬퍼 클래스 사용)
        PetOriginalState foxState = new PetOriginalState(fox);
        PetOriginalState moleState = new PetOriginalState(mole);
        
        // 위치 고정 코루틴 참조 저장
        Coroutine fixPositionCoroutine = null;
        
        // 두더지 숨김 처리를 위한 변수
        Vector3 moleBurrowPosition = Vector3.zero;
        bool moleIsHidden = false;
        
        try
        {
            // 감정 표현 추가 - 여우는 공격적, 두더지는 두려움
            fox.ShowEmotion(EmotionType.Hungry, 30f);
            mole.ShowEmotion(EmotionType.Scared, 30f);
            
            // 1. 쫓기 단계 - 여우가 두더지를 발견하고 쫓기 시작
            Debug.Log($"[FoxMoleHunt] {fox.petName}가 {mole.petName}를 발견하고 쫓기 시작합니다.");
            
            // 여우 속도 증가 (빠르게 쫓도록)
            fox.agent.speed = foxState.originalSpeed * 1.8f;
            fox.agent.acceleration = foxState.originalAcceleration * 1.5f;
            
            // 두더지 속도 증가 (도망가도록)
            mole.agent.speed = moleState.originalSpeed * 1.6f;
            mole.agent.acceleration = moleState.originalAcceleration * 1.4f;
            
            // 두더지가 도망가는 방향 (현재 위치에서 여우 반대 방향)
            Vector3 escapeDirection = (mole.transform.position - fox.transform.position).normalized;
            Vector3 escapeTarget = mole.transform.position + escapeDirection * 15f;
            escapeTarget = FindValidPositionOnNavMesh(escapeTarget, 18f);
            
            // 여우는 두더지를 향해, 두더지는 도망치도록 설정
            fox.agent.isStopped = false;
            mole.agent.isStopped = false;
            fox.agent.SetDestination(mole.transform.position);
            mole.agent.SetDestination(escapeTarget);
            
            // 달리기 애니메이션 설정
            fox.GetComponent<PetAnimationController>().SetContinuousAnimation(PetAnimationController.PetAnimationType.Run); // 달리기
            mole.GetComponent<PetAnimationController>().SetContinuousAnimation(PetAnimationController.PetAnimationType.Run); // 달리기
            
            // 2. 추격 단계 - 여우가 두더지를 일정 시간 쫓음 (최대 10초)
            float chaseTime = 0f;
            float totalChaseTime = 10f;
            
            // 가까워졌는지 확인을 위한 거리 threshold
            float catchDistance = 3.0f;
            bool isNearEnough = false;
            
            while (chaseTime < totalChaseTime && !isNearEnough)
            {
                // 여우가 두더지에게 가까워졌는지 확인
                float distance = Vector3.Distance(fox.transform.position, mole.transform.position);
                
                if (distance < catchDistance)
                {
                    isNearEnough = true;
                    Debug.Log($"[FoxMoleHunt] {fox.petName}가 {mole.petName}에게 접근했습니다! 거리: {distance}");
                }
                else
                {
                    // 여우의 목적지를 두더지의 현재 위치로 계속 업데이트
                    fox.agent.SetDestination(mole.transform.position);
                }
                
                chaseTime += Time.deltaTime;
                yield return null;
            }
            
            // 시간 초과하면 강제로 가까워졌다고 처리
            if (!isNearEnough)
            {
                Debug.Log("[FoxMoleHunt] 추격 시간 초과. 공격 단계로 진행합니다.");
                // 두더지를 여우 근처로 강제 이동 (시각적 연출을 위해)
                Vector3 teleportPos = fox.transform.position + fox.transform.forward * 2.5f;
                teleportPos = FindValidPositionOnNavMesh(teleportPos, 3f);
                mole.transform.position = teleportPos;
            }
            
            // 3. 공격 단계 - 여우가 두더지를 공격하고 두더지가 도망가려 함
            Debug.Log($"[FoxMoleHunt] {fox.petName}가 {mole.petName}를 공격합니다!");
            
            // 두 펫 모두 이동 중지
            fox.agent.isStopped = true;
            mole.agent.isStopped = true;
            
            // 서로 마주보게 설정
            LookAtEachOther(fox, mole);
            
            // 공격 애니메이션 재생
            yield return StartCoroutine(fox.GetComponent<PetAnimationController>().PlayAnimationWithCustomDuration(PetAnimationController.PetAnimationType.Attack, 1.5f, false, false));
            
            // 두더지는 아파하는 애니메이션 재생
            yield return StartCoroutine(mole.GetComponent<PetAnimationController>().PlayAnimationWithCustomDuration(PetAnimationController.PetAnimationType.Damage, 1.0f, false, false));
               // 두더지 NavMeshAgent 비활성화 (물리적 이동을 직접 제어하기 위해)
            mole.agent.enabled = false;
            
            // 4. 숨기 단계 - 두더지가 땅을 파고 들어감
            Debug.Log($"[FoxMoleHunt] {mole.petName}가 땅을 파고 들어갑니다!");
            
            // 두더지가 땅을 파는 애니메이션 (앉기/팔 움직임으로 표현)
            yield return StartCoroutine(mole.GetComponent<PetAnimationController>().PlayAnimationWithCustomDuration(PetAnimationController.PetAnimationType.Eat, 2.0f, false, false));
            
            // 현재 두더지 위치 저장 (나중에 다시 나오기 위해)
            moleBurrowPosition = mole.transform.position;
            moleIsHidden = true;
            
            // 두더지를 땅속으로 "숨기기" (Y 위치를 낮춰서 시각적으로 표현)
            Vector3 hiddenPosition = moleBurrowPosition;
            hiddenPosition.y -= 2.0f; // 땅속으로 2유닛 내려감
            
            // 애니메이션으로 두더지를 땅속으로 이동
            float hideTime = 1.0f;
            float elapsedTime = 0f;
            
            while (elapsedTime < hideTime)
            {
                mole.transform.position = Vector3.Lerp(moleBurrowPosition, hiddenPosition, elapsedTime / hideTime);
                elapsedTime += Time.deltaTime;
                yield return null;
            }
            
            // 최종 위치 설정
            mole.transform.position = hiddenPosition;
            
            // 5. 여우 반응 - 여우가 땅을 파보려고 시도
            Debug.Log($"[FoxMoleHunt] {fox.petName}가 땅을 파려고 시도합니다.");
            
            // 여우가 두더지가 사라진 위치를 파는 애니메이션 (앉기/먹기로 표현)
            yield return StartCoroutine(fox.GetComponent<PetAnimationController>().PlayAnimationWithCustomDuration(PetAnimationController.PetAnimationType.Eat, 3.0f, false, false));
            
            // 잠시 쉬는 애니메이션
            // yield return StartCoroutine(fox.GetComponent<PetAnimationController>().PlayAnimationWithCustomDuration(5, 2.0f, false, false));
            
            // 6. 여우 이동 - 여우가 포기하고 다른 곳으로 이동
            Debug.Log($"[FoxMoleHunt] {fox.petName}가 포기하고 이동을 시작합니다.");
            
            // 새로운 랜덤 목적지 설정
            Vector3 randomDirection = new Vector3(Random.Range(-1f, 1f), 0, Random.Range(-1f, 1f)).normalized;
            Vector3 foxDestination = fox.transform.position + randomDirection * 15f;
            foxDestination = FindValidPositionOnNavMesh(foxDestination, 20f);
            
            // 여우 이동 시작
            fox.agent.isStopped = false;
            fox.GetComponent<PetAnimationController>().SetContinuousAnimation(PetAnimationController.PetAnimationType.Walk); // 걷기 애니메이션
            fox.agent.SetDestination(foxDestination);
            
            // 여우가 충분히 멀어질 때까지 대기
            float waitTime = 0f;
            float minDistance = 8.0f; // 최소 8유닛 떨어져야 함
            bool foxIsFarEnough = false;
            
            while (waitTime < 5.0f && !foxIsFarEnough)
            {
                float distanceFromBurrow = Vector3.Distance(fox.transform.position, moleBurrowPosition);
                
                if (distanceFromBurrow > minDistance)
                {
                    foxIsFarEnough = true;
                    Debug.Log($"[FoxMoleHunt] {fox.petName}가 충분히 멀어졌습니다. 거리: {distanceFromBurrow}");
                }
                
                waitTime += Time.deltaTime;
                yield return null;
            }
            
            // 7. 두더지 재등장 - 두더지가 땅에서 다시 나옴
            Debug.Log($"[FoxMoleHunt] {mole.petName}가 땅속에서 나옵니다.");
            
         
            // 땅속에서 원래 위치로 서서히 이동
            elapsedTime = 0f;
            float emergeTime = 1.5f;
            
            while (elapsedTime < emergeTime)
            {
                mole.transform.position = Vector3.Lerp(hiddenPosition, moleBurrowPosition, elapsedTime / emergeTime);
                elapsedTime += Time.deltaTime;
                yield return null;
            }
            
            // 최종 위치 설정
            mole.transform.position = moleBurrowPosition;
            
            // NavMeshAgent 다시 활성화
            mole.agent.enabled = true;
            
            // 두더지가 나오는 애니메이션 (점프로 표현)
            yield return StartCoroutine(mole.GetComponent<PetAnimationController>().PlayAnimationWithCustomDuration(PetAnimationController.PetAnimationType.Jump, 1.0f, false, false));
            
            // 주변을 둘러보는 애니메이션 (회전)
            float lookAroundTime = 2.0f;
            float rotationSpeed = 100.0f;
            float lookTime = 0f;
            
            while (lookTime < lookAroundTime)
            {
                // 두더지가 제자리에서 회전
                mole.transform.Rotate(Vector3.up, rotationSpeed * Time.deltaTime);
                lookTime += Time.deltaTime;
                yield return null;
            }
            
            // 안도의 애니메이션 (앉기)
            yield return StartCoroutine(mole.GetComponent<PetAnimationController>().PlayAnimationWithCustomDuration(PetAnimationController.PetAnimationType.Eat, 1.5f, false, false));
            
            // 잠시 대기
            yield return new WaitForSeconds(1.0f);
        }
        finally
        {
            // 감정 말풍선 숨기기
            fox.HideEmotion();
            mole.HideEmotion();
            
            // 두더지가 여전히 숨어있다면 원래 위치로 복원
            if (moleIsHidden)
            {
                mole.transform.position = moleBurrowPosition;
                mole.agent.enabled = true;
            }
            
            // 원래 상태로 복원
            foxState.Restore(fox);
            moleState.Restore(mole);
            
            // 애니메이션 원래대로
            fox.GetComponent<PetAnimationController>().StopContinuousAnimation();
            mole.GetComponent<PetAnimationController>().StopContinuousAnimation();
            
            Debug.Log($"[FoxMoleHunt] 상호작용 종료, 펫들의 상태가 원래대로 복원됨");
        }
    }
}