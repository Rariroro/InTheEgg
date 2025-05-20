// 함께 자기 상호작용 구현 (개선 버전)
using System.Collections;
using UnityEngine;

public class SleepTogetherInteraction : BasePetInteraction
{
    public override string InteractionName => "SleepTogether";
    
    // 상호작용 유형 지정
    protected override InteractionType DetermineInteractionType()
    {
        return InteractionType.SleepTogether;
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
    
    // 이 상호작용이 가능한지 확인
    public override bool CanInteract(PetController pet1, PetController pet2)
    {
        PetType type1 = pet1.PetType;
        PetType type2 = pet2.PetType;
        
        // 늑대와 개는 함께 잔다
        if ((type1 == PetType.Wolf && type2 == PetType.Dog) || 
            (type1 == PetType.Dog && type2 == PetType.Wolf))
        {
            return true;
        }
        
        return false;
    }
    
    // 펫 크기 확인 헬퍼 함수 (큰 동물인지 작은 동물인지)
    private bool IsPetLarge(PetType petType)
    {
        // 큰 동물 목록
        PetType[] largeAnimals = new PetType[]
        {
            PetType.Cow, PetType.Elk, PetType.Deer, PetType.Camel, PetType.Bison,
            PetType.Ostrich, PetType.Horse, PetType.Zebra, PetType.Bull, PetType.Lioness,
            PetType.Giraffe, PetType.Lion, PetType.Rhino, PetType.Elephant, PetType.Sheep,
            PetType.Gorilla, PetType.Bear, PetType.Tiger, PetType.Buffalo, PetType.Hippo
        };
        
        return System.Array.IndexOf(largeAnimals, petType) >= 0;
    }
    
    // 상호작용 수행
    public override IEnumerator PerformInteraction(PetController pet1, PetController pet2)
    {
        Debug.Log($"[SleepTogether] {pet1.petName}와(과) {pet2.petName}가 함께 자기 시작했습니다!");
        
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
            
            // 1. 수면 위치 찾기 - 약간 그늘지거나 숨겨진 느낌으로
            Vector3 sleepSpot = FindInteractionSpot(pet1, pet2, 2f);
            
            // 큰 동물과 작은 동물 구분 (크기에 따라 상대 위치 설정)
            bool pet1IsLarge = IsPetLarge(pet1.PetType);
            bool pet2IsLarge = IsPetLarge(pet2.PetType);
            
            float sleepDistance = 3f; // 기본 거리
            if (pet1IsLarge && pet2IsLarge) sleepDistance = 5f; // 둘 다 큰 경우 더 넓게
            else if (!pet1IsLarge && !pet2IsLarge) sleepDistance = 2f; // 둘 다 작은 경우 더 좁게
            
            // 각 펫의 목적지 계산
            Vector3 pet1SleepPos, pet2SleepPos;
            CalculateStartPositions(pet1, pet2, out pet1SleepPos, out pet2SleepPos, sleepDistance);
            
            // 2. 두 펫이 동기화된 속도로 수면 위치로 이동
            float syncedSpeed = Mathf.Min(pet1State.originalSpeed, pet2State.originalSpeed) * 0.7f; // 약간 느린 속도로
            pet1.agent.speed = syncedSpeed;
            pet2.agent.speed = syncedSpeed;
            
            // 이동
            yield return StartCoroutine(MoveToPositions(pet1, pet2, pet1SleepPos, pet2SleepPos, 12f));
            
            // 3. 두 펫 모두 이동을 멈추고 서로 마주보게 하기
            LookAtEachOther(pet1, pet2);
            
            // 위치 고정 설정
            Vector3 pet1FixedPos = pet1.transform.position;
            Vector3 pet2FixedPos = pet2.transform.position;
            Quaternion pet1FixedRot = pet1.transform.rotation;
            Quaternion pet2FixedRot = pet2.transform.rotation;
            
            // 잠시 대기 (안정화를 위해)
            yield return new WaitForSeconds(0.5f);
            
            // 4. 두 펫이 잠자리에 듦
            // 서로 마주 보고 약간 회전 (포개져서 자는 느낌)
            PositionForSleeping(pet1, pet2, sleepSpot);
            
            // 위치 고정 코루틴 시작 (업데이트된 위치로)
            pet1FixedPos = pet1.transform.position;
            pet2FixedPos = pet2.transform.position;
            pet1FixedRot = pet1.transform.rotation;
            pet2FixedRot = pet2.transform.rotation;
            
            fixPositionCoroutine = StartCoroutine(
                FixPositionDuringInteraction(
                    pet1, pet2, 
                    pet1FixedPos, pet2FixedPos, 
                    pet1FixedRot, pet2FixedRot
                )
            );
            
            Debug.Log($"[SleepTogether] {pet1.petName}와(과) {pet2.petName}가 잠자리에 듭니다.");
            
            // 잠자기 전 하품하거나 기지개 펴는 동작 (펫 종류에 따라 다른 애니메이션)
            if (Random.value > 0.5f)
            {
                yield return StartCoroutine(pet1.GetComponent<PetAnimationController>().PlayAnimationWithCustomDuration(3, 1.5f, false, false));
            }
            else
            {
                yield return StartCoroutine(pet2.GetComponent<PetAnimationController>().PlayAnimationWithCustomDuration(3, 1.5f, false, false));
            }
            
            // 잠자기 애니메이션 시작
            StartCoroutine(pet1.GetComponent<PetAnimationController>().PlayAnimationWithCustomDuration(5, 5.0f, true, false));
            StartCoroutine(pet2.GetComponent<PetAnimationController>().PlayAnimationWithCustomDuration(5, 5.0f, true, false));
            
            // 5. 수면 중 작은 움직임 표현
            float sleepTime = 15f;
            float startTime = Time.time;
            float nextMovementTime = startTime + Random.Range(3f, 6f);
            
            while (Time.time - startTime < sleepTime)
            {
                if (Time.time >= nextMovementTime)
                {
                    // 랜덤으로 어떤 펫이 움직일지 결정
                    PetController movingPet = Random.value > 0.5f ? pet1 : pet2;
                    
                    // 자는 중 작은 움직임 (자세 약간 변경)
                    int movementType = Random.Range(0, 3);
                    
                    switch (movementType)
                    {
                        case 0: // 미세한 몸 뒤척임 (애니메이션 잠시 중단 후 다시 시작)
                            if (movingPet.animator != null)
                            {
                                movingPet.animator.SetInteger("animation", 0);
                                yield return new WaitForSeconds(0.3f);
                                movingPet.animator.SetInteger("animation", 5);
                            }
                            break;
                            
                        case 1: // 잠시 귀 움직임이나 꼬리 흔들기 (다른 애니메이션으로 잠시 전환)
                            yield return StartCoroutine(movingPet.GetComponent<PetAnimationController>().PlayAnimationWithCustomDuration(3, 0.5f, false, false));
                            StartCoroutine(movingPet.GetComponent<PetAnimationController>().PlayAnimationWithCustomDuration(5, 0.5f, true, false));
                            break;
                            
                        case 2: // 약간 위치 조정
                            Vector3 currentPos = movingPet.transform.position;
                            Vector3 adjustedPos = currentPos + new Vector3(Random.Range(-0.3f, 0.3f), 0, Random.Range(-0.3f, 0.3f));
                            
                            // 애니메이션 중단 없이 부드럽게 위치 조정
                            float adjustTime = 0.5f;
                            float elapsedTime = 0f;
                            
                            while (elapsedTime < adjustTime)
                            {
                                movingPet.transform.position = Vector3.Lerp(currentPos, adjustedPos, elapsedTime / adjustTime);
                                elapsedTime += Time.deltaTime;
                                yield return null;
                            }
                            
                            movingPet.transform.position = adjustedPos;
                            break;
                    }
                    
                    // 다음 움직임 시간 설정
                    nextMovementTime = Time.time + Random.Range(2.5f, 5f);
                }
                
                yield return null;
            }
            
          // 6. 깨어나는 과정
            Debug.Log($"[SleepTogether] {pet1.petName}와(과) {pet2.petName}가 잠에서 깨어납니다.");
            
            // 위치 고정 코루틴 중지
            if (fixPositionCoroutine != null)
            {
                StopCoroutine(fixPositionCoroutine);
                fixPositionCoroutine = null;
            }
            
            // 먼저 한 펫이 깨어나고, 다른 펫을 깨우는 상호작용
            PetController firstAwake = Random.value > 0.5f ? pet1 : pet2;
            PetController secondAwake = firstAwake == pet1 ? pet2 : pet1;
            
            // 감정 표현 업데이트
            firstAwake.ShowEmotion(EmotionType.Surprised, 5f);
            
            // 첫 번째 펫이 깨어남
            yield return StartCoroutine(firstAwake.GetComponent<PetAnimationController>().PlayAnimationWithCustomDuration(3, 1.5f, false, false));
            
            // 첫 번째 펫이 두 번째 펫을 바라봄
            LookAtOther(firstAwake, secondAwake);
            
            // 잠시 대기
            yield return new WaitForSeconds(0.5f);
            
            // 첫 번째 펫이 두 번째 펫을 건드려 깨움 (애니메이션 4 - 앉는 동작으로 대체)
            yield return StartCoroutine(firstAwake.GetComponent<PetAnimationController>().PlayAnimationWithCustomDuration(4, 1.0f, false, false));
            
            // 두 번째 펫이 깨어남
            secondAwake.ShowEmotion(EmotionType.Surprised, 5f);
            yield return StartCoroutine(secondAwake.GetComponent<PetAnimationController>().PlayAnimationWithCustomDuration(3, 1.5f, false, false));
            
            // 두 펫 모두 기본 idle 애니메이션으로 전환
            pet1.GetComponent<PetAnimationController>().SetContinuousAnimation(0);
            pet2.GetComponent<PetAnimationController>().SetContinuousAnimation(0);
            
            // 잠시 대기
            yield return new WaitForSeconds(1.5f);
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
            
            Debug.Log($"[SleepTogether] {pet1.petName}와(과) {pet2.petName}의 함께 자기 완료");
        }
    }
    
    // 두 펫의 위치 조정 (함께 자기 위한 배치)
    private void PositionForSleeping(PetController pet1, PetController pet2, Vector3 sleepSpot)
    {
        // 무작위 방향 생성
        Vector3 sleepDirection = new Vector3(Random.Range(-1f, 1f), 0, Random.Range(-1f, 1f)).normalized;
        
        // 두 펫을 약간 겹치게 배치 (실제로는 약간 떨어진 상태로 보임)
        // 첫 번째 펫은 좀 더 중앙에
        Vector3 pet1Pos = sleepSpot - sleepDirection * 0.3f;
        Vector3 pet2Pos = sleepSpot + sleepDirection * 0.3f;
        
        // 위치 설정
        pet1.transform.position = pet1Pos;
        pet2.transform.position = pet2Pos;
        
        // 서로를 향해 약간 기울어진 상태로 회전
        Quaternion pet1Rotation = Quaternion.LookRotation(pet2.transform.position - pet1.transform.position);
        Quaternion pet2Rotation = Quaternion.LookRotation(pet1.transform.position - pet2.transform.position);
        
        // 회전 적용
        pet1.transform.rotation = pet1Rotation;
        pet2.transform.rotation = pet2Rotation;
        
        if (pet1.petModelTransform != null)
        {
            pet1.petModelTransform.rotation = pet1Rotation;
        }
        
        if (pet2.petModelTransform != null)
        {
            pet2.petModelTransform.rotation = pet2Rotation;
        }
    }
}