// 사자-주머니쥐 장난 상호작용 구현
using System.Collections;
using UnityEngine;

public class LionPossumPrankInteraction : BasePetInteraction
{
    public override string InteractionName => "LionPossumPrank";
    
    // 상호작용 유형 지정
    protected override InteractionType DetermineInteractionType()
    {
        // 새로운 상호작용 유형이 필요한 경우 PetInteractionManager.cs의 InteractionType 열거형에 추가해야 함
        // 여기서는 기존 유형 중 가장 비슷한 ChaseAndRun 사용
        return InteractionType.ChaseAndRun;
    }
    
    // 이 상호작용이 가능한지 확인
    public override bool CanInteract(PetController pet1, PetController pet2)
    {
        PetType type1 = pet1.PetType;
        PetType type2 = pet2.PetType;
        
        // 사자와 주머니쥐 조합인지 확인
        return (type1 == PetType.Lion && type2 == PetType.Possum) || 
               (type1 == PetType.Possum && type2 == PetType.Lion);
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
    
    // 상호작용 수행
    public override IEnumerator PerformInteraction(PetController pet1, PetController pet2)
    {
        Debug.Log($"[LionPossumPrank] {pet1.petName}와(과) {pet2.petName}의 장난 상호작용 시작!");
        
        // 사자와 주머니쥐 식별
        PetController lion = null;
        PetController Possum = null;
        
        // 펫 타입에 따라 사자와 주머니쥐 식별
        if (pet1.PetType == PetType.Lion && pet2.PetType == PetType.Possum)
        {
            lion = pet1;
            Possum = pet2;
        }
        else if (pet1.PetType == PetType.Possum && pet2.PetType == PetType.Lion)
        {
            lion = pet2;
            Possum = pet1;
        }
        else
        {
            // 사자나 주머니쥐가 아닌 경우 (실제로는 CanInteract에서 걸러짐)
            Debug.Log("[LionPossumPrank] 사자와 주머니쥐가 아닌 펫들입니다. 역할을 임의로 배정합니다.");
            lion = Random.value > 0.5f ? pet1 : pet2;
            Possum = lion == pet1 ? pet2 : pet1;
        }
        
        Debug.Log($"[LionPossumPrank] 사자 역할: {lion.petName}, 주머니쥐 역할: {Possum.petName}");
        
        // 원래 상태 저장
        PetOriginalState lionState = new PetOriginalState(lion);
        PetOriginalState PossumState = new PetOriginalState(Possum);
        
        // 위치 고정 코루틴 참조 저장
        Coroutine fixPositionCoroutine = null;
        
        try
        {
            // 1. 사자가 먼저 자리잡고 잠드는 상태 설정
            Vector3 sleepSpot = FindInteractionSpot(lion, Possum, 3f);
            Vector3 lionSleepPos = sleepSpot;
            
            // 사자가 잠들 위치로 이동
            yield return StartCoroutine(MoveToPositions(lion, Possum, lionSleepPos, Possum.transform.position, 5f));
            
            // 사자 감정 표현 - 졸림
            lion.ShowEmotion(EmotionType.Sleepy, 30f);
            
            // 사자 위치 고정 및 잠자는 애니메이션 시작
            lion.agent.isStopped = true;
            Vector3 lionFixedPos = lion.transform.position;
            Quaternion lionFixedRot = lion.transform.rotation;
            
            // 사자만 고정 (주머니쥐는 이동 가능)
            fixPositionCoroutine = StartCoroutine(
                FixPositionDuringInteraction(
                    lion, Possum, 
                    lionFixedPos, Possum.transform.position, 
                    lionFixedRot, Possum.transform.rotation,
                    true, false
                )
            );
            
            // 사자 잠자기 애니메이션 재생
          StartCoroutine(lion.GetComponent<PetAnimationController>().PlayAnimationWithCustomDuration(PetAnimationController.PetAnimationType.Rest, 10f, true, false));
            
            Debug.Log($"[LionPossumPrank] {lion.petName}이(가) 잠들었습니다.");
            
            // 2. 주머니쥐가 사자에게 접근
            // 사자 주변 위치 계산 (코 쪽에 위치하도록)
            Vector3 directionToPossum = (Possum.transform.position - lion.transform.position).normalized;
            Vector3 frontPosition = lion.transform.position + lion.transform.forward * 2f;  // 사자 앞쪽 위치
            
            // 주머니쥐가 사자 앞으로 이동
            Possum.agent.isStopped = false;
            Possum.agent.SetDestination(frontPosition);
            
            // 걷기 애니메이션 설정
            Possum.GetComponent<PetAnimationController>().SetContinuousAnimation(PetAnimationController.PetAnimationType.Walk);
            
            // 주머니쥐가 접근할 때까지 대기
            float approachTimeout = 8f;
            float startTime = Time.time;
            
            while (Time.time - startTime < approachTimeout)
            {
                // 목적지에 가까워지면 중단
                if (!Possum.agent.pathPending && Possum.agent.remainingDistance < 0.5f)
                {
                    Debug.Log($"[LionPossumPrank] {Possum.petName}이(가) {lion.petName}에게 접근했습니다.");
                    break;
                }
                
                yield return null;
            }
            
            // 주머니쥐 이동 멈추기
            Possum.agent.isStopped = true;
            Possum.GetComponent<PetAnimationController>().StopContinuousAnimation();
            
            // 3. 주머니쥐가 장난을 침 (코털 건드리기)
            // 주머니쥐가 사자를 향해 회전
            LookAtOther(Possum, lion);
            
            // 장난치는 애니메이션 (애니메이션 6 - 공격/놀기 사용)
            Possum.ShowEmotion(EmotionType.Joke, 5f);
            yield return StartCoroutine(Possum.GetComponent<PetAnimationController>().PlayAnimationWithCustomDuration(PetAnimationController.PetAnimationType.Attack, 2.5f, false, false));
            
            // 4. 사자가 깨어남
            // 위치 고정 코루틴 중지 (사자가 움직일 수 있도록)
            if (fixPositionCoroutine != null)
            {
                StopCoroutine(fixPositionCoroutine);
                fixPositionCoroutine = null;
            }
            
            // 사자 깨어나는 애니메이션
            lion.HideEmotion();  // 졸린 감정 제거
            lion.ShowEmotion(EmotionType.Angry, 3f);  // 놀란 감정
            yield return StartCoroutine(lion.GetComponent<PetAnimationController>().PlayAnimationWithCustomDuration(PetAnimationController.PetAnimationType.Damage, 1.5f, false, false));
            
            // 사자가 화내는 애니메이션
            lion.HideEmotion();  // 놀란 감정 제거
            lion.ShowEmotion(EmotionType.Angry, 5f);  // 화난 감정
            yield return StartCoroutine(lion.GetComponent<PetAnimationController>().PlayAnimationWithCustomDuration(PetAnimationController.PetAnimationType.Attack, 2f, false, false));
            
            // 5. 주머니쥐가 죽은척 함
            Possum.HideEmotion();
            Possum.ShowEmotion(EmotionType.Sleepy, 20f);
          StartCoroutine(Possum.GetComponent<PetAnimationController>().PlayAnimationWithCustomDuration(PetAnimationController.PetAnimationType.Die, 10f, false, false));
            
            Debug.Log($"[LionPossumPrank] {Possum.petName}이(가) 죽은척을 합니다.");
            
            // 주머니쥐 위치와 회전 고정 (죽은척 하는 동안)
            Vector3 PossumPlayDeadPos = Possum.transform.position;
            Quaternion PossumPlayDeadRot = Possum.transform.rotation;
            
            fixPositionCoroutine = StartCoroutine(
                FixPositionDuringInteraction(
                    lion, Possum, 
                    lion.transform.position, PossumPlayDeadPos, 
                    lion.transform.rotation, PossumPlayDeadRot,
                    false, true
                )
            );
            
            // 6. 사자가 주머니쥐를 잠시 살펴봄
            LookAtOther(lion, Possum);
            yield return StartCoroutine(lion.GetComponent<PetAnimationController>().PlayAnimationWithCustomDuration(0, 2f, false, false));
            
            // 7. 사자가 흥미를 잃고 떠남
            lion.HideEmotion();
            
            // 사자가 다른 방향으로 이동할 목적지 계산
            Vector3 lionLeaveDirection = -directionToPossum; // 주머니쥐와 반대 방향
            Vector3 lionLeaveTarget = lion.transform.position + lionLeaveDirection * 15f;
            lionLeaveTarget = FindValidPositionOnNavMesh(lionLeaveTarget, 20f);
            
            // 사자 이동 시작
            lion.agent.isStopped = false;
            lion.agent.SetDestination(lionLeaveTarget);
            lion.GetComponent<PetAnimationController>().SetContinuousAnimation(PetAnimationController.PetAnimationType.Walk);
            
            Debug.Log($"[LionPossumPrank] {lion.petName}이(가) 떠납니다.");
            
            // 사자가 충분히 멀어질 때까지 대기
            float leaveTimeout = 5f;
            startTime = Time.time;
            
            while (Time.time - startTime < leaveTimeout)
            {
                float distanceTraveled = Vector3.Distance(lion.transform.position, lionFixedPos);
                if (distanceTraveled > 10f)
                {
                    Debug.Log($"[LionPossumPrank] {lion.petName}이(가) 충분히 멀어졌습니다.");
                    break;
                }
                
                yield return null;
            }
            
            // 8. 주머니쥐가 일어나서 신나함
            // 위치 고정 코루틴 중지 (주머니쥐가 움직일 수 있도록)
            if (fixPositionCoroutine != null)
            {
                StopCoroutine(fixPositionCoroutine);
                fixPositionCoroutine = null;
            }
            
            // 주머니쥐가 일어나는 애니메이션
            Possum.HideEmotion();
            Possum.ShowEmotion(EmotionType.Happy, 5f);
            
            // 점프하는 애니메이션으로 신나는 표현
            yield return StartCoroutine(Possum.GetComponent<PetAnimationController>().PlayAnimationWithCustomDuration(PetAnimationController.PetAnimationType.Jump, 2f, false, false));
            
            // 한번 더 신나게 점프
            yield return StartCoroutine(Possum.GetComponent<PetAnimationController>().PlayAnimationWithCustomDuration(PetAnimationController.PetAnimationType.Jump, 2f, false, false));
            
            Debug.Log($"[LionPossumPrank] {Possum.petName}이(가) 신나합니다!");
            
            // 마지막으로 기본 애니메이션으로 전환
            Possum.GetComponent<PetAnimationController>().SetContinuousAnimation(0);
            
            // 대기 시간 추가
            yield return new WaitForSeconds(3f);
        }
        finally
        {
            // 위치 고정 코루틴 중지
            if (fixPositionCoroutine != null)
            {
                StopCoroutine(fixPositionCoroutine);
            }
            
            // 감정 말풍선 숨기기
            lion.HideEmotion();
            Possum.HideEmotion();
            
            // 원래 상태로 복원
            lionState.Restore(lion);
            PossumState.Restore(Possum);
            
            // 애니메이션 원래대로
            lion.GetComponent<PetAnimationController>().StopContinuousAnimation();
            Possum.GetComponent<PetAnimationController>().StopContinuousAnimation();
            
            Debug.Log($"[LionPossumPrank] 상호작용 종료, 펫들의 상태가 원래대로 복원됨");
        }
    }
}