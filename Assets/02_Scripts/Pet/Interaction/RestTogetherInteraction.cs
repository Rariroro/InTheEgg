// 함께 쉬기 상호작용 구현 (개선 버전)
using System.Collections;
using UnityEngine;

public class RestTogetherInteraction : BasePetInteraction
{
    public override string InteractionName => "RestTogether";
    
    // 상호작용 유형 지정
    protected override InteractionType DetermineInteractionType()
    {
        return InteractionType.RestTogether;
    }
    
    // 이 상호작용이 가능한지 확인
    public override bool CanInteract(PetController pet1, PetController pet2)
    {
        PetType type1 = pet1.PetType;
        PetType type2 = pet2.PetType;
        
        // 판다와 레서판다는 함께 쉰다
        if ((type1 == PetType.Panda && type2 == PetType.RedPanda) || 
            (type1 == PetType.RedPanda && type2 == PetType.Panda))
        {
            return true;
        }
        
        // 기타 조건 추가 가능
        return false;
    }
    
    // 위치와 회전을 고정하는 코루틴 추가
    private IEnumerator FixPositionDuringInteraction(
        PetController pet1, PetController pet2,
        Vector3 pos1, Vector3 pos2,
        Quaternion rot1, Quaternion rot2)
    {
        // 상호작용이 진행되는 동안 위치와 회전 고정
        while (pet1.isInteracting && pet2.isInteracting)
        {
            pet1.transform.position = pos1;
            pet2.transform.position = pos2;
            pet1.transform.rotation = rot1;
            pet2.transform.rotation = rot2;

            // 모델도 함께 고정
            if (pet1.petModelTransform) pet1.petModelTransform.rotation = rot1;
            if (pet2.petModelTransform) pet2.petModelTransform.rotation = rot2;

            yield return null;
        }
    }
    
    // 상호작용 수행
    public override IEnumerator PerformInteraction(PetController pet1, PetController pet2)
    {
        Debug.Log($"[RestTogether] {pet1.petName}와(과) {pet2.petName}가 함께 쉬기 시작했습니다!");
        
        // 원래 상태 저장 (개선된 헬퍼 클래스 사용)
        PetOriginalState pet1State = new PetOriginalState(pet1);
        PetOriginalState pet2State = new PetOriginalState(pet2);
        
        // 위치 고정 코루틴 참조 저장
        Coroutine fixPositionCoroutine = null;
        
        try
        {
            // 감정 표현 추가 - 졸린 표정
            pet1.ShowEmotion(EmotionType.Sleepy, 30f);
            pet2.ShowEmotion(EmotionType.Sleepy, 30f);
            
            // 1. 두 펫이 쉬기에 적합한 위치 찾기
            Vector3 restSpot = FindInteractionSpot(pet1, pet2, 2f);
            
            // 2. 두 펫이 쉬기 위치로 이동하도록 목적지 설정
            Vector3 pet1RestPos, pet2RestPos;
            CalculateStartPositions(pet1, pet2, out pet1RestPos, out pet2RestPos, 3f);
            
            // 공통 MoveToPositions 메소드 사용
            yield return StartCoroutine(MoveToPositions(pet1, pet2, pet1RestPos, pet2RestPos, 10f));
            
            // 3. 두 펫 모두 이동을 멈추고 서로 마주보게 하기
            LookAtEachOther(pet1, pet2);
            
            // 위치 고정 설정
            Vector3 pet1FixedPos = pet1.transform.position;
            Vector3 pet2FixedPos = pet2.transform.position;
            Quaternion pet1FixedRot = pet1.transform.rotation;
            Quaternion pet2FixedRot = pet2.transform.rotation;
            
            // 위치 고정 코루틴 시작
            fixPositionCoroutine = StartCoroutine(
                FixPositionDuringInteraction(
                    pet1, pet2, 
                    pet1FixedPos, pet2FixedPos, 
                    pet1FixedRot, pet2FixedRot
                )
            );
            
            // 잠시 대기 (안정화를 위해)
            yield return new WaitForSeconds(0.5f);
            
            // 4. 쉬기 애니메이션으로 자연스럽게 전환
            // 애니메이션 컨트롤러를 통해 쉬기 애니메이션 재생
            StartCoroutine(pet1.GetComponent<PetAnimationController>().PlayAnimationWithCustomDuration(5, 3.0f, true, false));
            StartCoroutine(pet2.GetComponent<PetAnimationController>().PlayAnimationWithCustomDuration(5, 3.0f, true, false));
            
            Debug.Log($"[RestTogether] {pet1.petName}와(과) {pet2.petName}가 함께 쉬는 중");
            
            // 5. 쉬기 중간에 특별한 이벤트 추가 (랜덤하게)
            float totalRestTime = 12f; // 총 쉬는 시간
            float elapsedTime = 0f;
            float nextEventTime = Random.Range(3f, 6f); // 첫 이벤트 시간
            
            while (elapsedTime < totalRestTime)
            {
                // 이벤트 타이밍이 되었을 때
                if (elapsedTime >= nextEventTime)
                {
                    // 랜덤으로 어떤 펫이 특별 동작을 할지 결정
                    PetController activePet = Random.value > 0.5f ? pet1 : pet2;
                    PetController passivePet = activePet == pet1 ? pet2 : pet1;
                    
                    // 특별 동작 종류 (랜덤)
                    int eventType = Random.Range(0, 3);
                    
                    switch (eventType)
                    {
                        case 0: // 기지개 펴기 (애니메이션 3 - 점프 사용)
                            Debug.Log($"[RestTogether] {activePet.petName}가 기지개를 폅니다.");
                            yield return StartCoroutine(activePet.GetComponent<PetAnimationController>().PlayAnimationWithCustomDuration(3, 1.5f, false, false));
                            yield return StartCoroutine(activePet.GetComponent<PetAnimationController>().PlayAnimationWithCustomDuration(5, 0.5f, true, false));
                            break;
                            
                        case 1: // 다른 펫 쳐다보기
                            Debug.Log($"[RestTogether] {activePet.petName}가 {passivePet.petName}을(를) 쳐다봅니다.");
                            LookAtOther(activePet, passivePet);
                            yield return new WaitForSeconds(2f);
                            // 다시 원래 위치로 회전
                            LookAtEachOther(pet1, pet2);
                            break;
                            
                        case 2: // 잠시 앉았다 일어서기 (애니메이션 4 - 식사/앉기 사용)
                            Debug.Log($"[RestTogether] {activePet.petName}가 잠시 자세를 바꿉니다.");
                            yield return StartCoroutine(activePet.GetComponent<PetAnimationController>().PlayAnimationWithCustomDuration(4, 2f, false, false));
                            yield return StartCoroutine(activePet.GetComponent<PetAnimationController>().PlayAnimationWithCustomDuration(5, 0.5f, true, false));
                            break;
                    }
                    
                    // 다음 이벤트 시간 설정
                    nextEventTime = elapsedTime + Random.Range(2.5f, 5f);
                }
                
                yield return null;
                elapsedTime += Time.deltaTime;
            }
            
            // 6. 쉬기 종료 준비 (일어나는 애니메이션)
            Debug.Log($"[RestTogether] {pet1.petName}와(과) {pet2.petName}가 쉬기를 마칩니다.");
            
            // 일어나는 애니메이션 (0번 - 기본 idle 애니메이션으로 전환)
            pet1.GetComponent<PetAnimationController>().SetContinuousAnimation(0);
            pet2.GetComponent<PetAnimationController>().SetContinuousAnimation(0);
            
            // 잠시 대기
            yield return new WaitForSeconds(1f);
        }
        finally
        {
            // 위치 고정 코루틴 중지
            if (fixPositionCoroutine != null)
            {
                StopCoroutine(fixPositionCoroutine);
            }
            
            // 감정 말풍선 숨기기
            pet1.HideEmotion();
            pet2.HideEmotion();
            
            // 7. 원래 상태 복원
            pet1State.Restore(pet1);
            pet2State.Restore(pet2);
            
            Debug.Log($"[RestTogether] {pet1.petName}와(과) {pet2.petName}의 함께 쉬기 완료");
        }
    }
}