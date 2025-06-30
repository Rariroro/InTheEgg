// 함께 걷기 상호작용 구현 (개선 버전)
using System.Collections;
using UnityEngine;

public class WalkTogetherInteraction : BasePetInteraction
{
    public override string InteractionName => "WalkTogether";
    
    // 상호작용 유형 지정
    protected override InteractionType DetermineInteractionType()
    {
        return InteractionType.WalkTogether;
    }
    
    // 이 상호작용이 가능한지 확인
    public override bool CanInteract(PetController pet1, PetController pet2)
    {
        // 원래 상호작용 규칙을 검사하는 로직
        PetType type1 = pet1.PetType;
        PetType type2 = pet2.PetType;
        
        return (type1 == PetType.Monkey && type2 == PetType.Gorilla) || 
               (type1 == PetType.Gorilla && type2 == PetType.Monkey);
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
    protected override IEnumerator PerformInteraction(PetController pet1, PetController pet2)
    {
        Debug.Log($"[WalkTogether] {pet1.petName}와(과) {pet2.petName}가 함께 걷기 시작했습니다!");
        
        // 원래 상태 저장 (개선된 헬퍼 클래스 사용)
        PetOriginalState pet1State = new PetOriginalState(pet1);
        PetOriginalState pet2State = new PetOriginalState(pet2);
        
        // 위치 고정 코루틴 참조 저장
        Coroutine fixPositionCoroutine = null;
        
        try
        {
            // 감정 표현 추가 - 행복한 표정
            pet1.ShowEmotion(EmotionType.Friend, 30f);
            pet2.ShowEmotion(EmotionType.Friend, 30f);
            
            // 1. 먼저 두 펫을 나란히 서게 배치 - 준비 단계
            Vector3 pet1Position, pet2Position;
            CalculateStartPositions(pet1, pet2, out pet1Position, out pet2Position, 2.5f);
            
            // 걷기 전 시작 위치로 이동
            yield return StartCoroutine(MoveToPositions(pet1, pet2, pet1Position, pet2Position, 5f));
            
            // 준비 위치에서 잠시 고정
            Vector3 pet1FixedPos = pet1.transform.position;
            Vector3 pet2FixedPos = pet2.transform.position;
            Quaternion pet1FixedRot = pet1.transform.rotation;
            Quaternion pet2FixedRot = pet2.transform.rotation;
            
            // 초기에 위치 고정 코루틴 시작
            fixPositionCoroutine = StartCoroutine(
                FixPositionDuringInteraction(
                    pet1, pet2, 
                    pet1FixedPos, pet2FixedPos, 
                    pet1FixedRot, pet2FixedRot
                )
            );
            
            // 잠시 대기 (안정화를 위해)
            yield return new WaitForSeconds(0.5f);
            
            // 위치 고정 코루틴 중지 (이동을 시작하기 위해)
            if (fixPositionCoroutine != null)
            {
                StopCoroutine(fixPositionCoroutine);
                fixPositionCoroutine = null;
            }
            
            // 2. 나란히 걷기 시작
            // 속도 동기화 (두 펫이 동일한 속도로 걷도록)
            float syncedSpeed = Mathf.Min(pet1State.originalSpeed, pet2State.originalSpeed) * 0.8f;
            pet1.agent.speed = syncedSpeed;
            pet2.agent.speed = syncedSpeed;
            
            // 걷기 애니메이션 활성화
            pet1.GetComponent<PetAnimationController>().SetContinuousAnimation(PetAnimationController.PetAnimationType.Walk);
            pet2.GetComponent<PetAnimationController>().SetContinuousAnimation(PetAnimationController.PetAnimationType.Walk);
            
            // 3. 각 펫이 나란히 걷도록 목적지 설정
            float walkTime = 15f; // 총 걷는 시간
            float startTime = Time.time;
            int pathUpdateCount = 0;
            
            while (Time.time - startTime < walkTime)
            {
                // 5초마다 또는 목적지에 도착했을 때 새 목적지 설정
                bool shouldUpdatePath = false;
                
                if (pathUpdateCount == 0) // 처음에는 무조건 경로 설정
                {
                    shouldUpdatePath = true;
                }
                else
                {
                    // 두 펫 모두 목적지에 가까워지면 새 경로 설정
                    bool pet1NearDestination = !pet1.agent.pathPending && pet1.agent.remainingDistance < 2f;
                    bool pet2NearDestination = !pet2.agent.pathPending && pet2.agent.remainingDistance < 2f;
                    
                    if (pet1NearDestination && pet2NearDestination)
                    {
                        shouldUpdatePath = true;
                    }
                    
                    // 또는 일정 시간이 지나면 경로 갱신
                    if (Time.time - startTime > pathUpdateCount * 5f)
                    {
                        shouldUpdatePath = true;
                    }
                }
                
                if (shouldUpdatePath)
                {
                    pathUpdateCount++;
                    
                    // 새로운 걷기 방향 설정 (이전 방향에서 약간 랜덤하게 변경)
                    Vector3 midPoint = (pet1.transform.position + pet2.transform.position) / 2;
                    float randomAngle = Random.Range(-45f, 45f);
                    Vector3 walkDirection = Quaternion.Euler(0, randomAngle, 0) * 
                                          (pet1.transform.forward + pet2.transform.forward).normalized;
                    
                    // 측면 벡터 계산 (전방 벡터의 수직 방향)
                    Vector3 sideDirection = Vector3.Cross(Vector3.up, walkDirection).normalized;
                    
                    // 목적지 거리
                    float targetDistance = Random.Range(8f, 50f);
                    
                    // 중앙 목적지 계산
                    Vector3 centerTarget = midPoint + walkDirection * targetDistance;
                    
                    // 각 펫의 목적지 계산 (나란히 걷도록)
                    float petSpacing = 2.5f;
                    Vector3 pet1Target = centerTarget - sideDirection * (petSpacing / 2);
                    Vector3 pet2Target = centerTarget + sideDirection * (petSpacing / 2);
                    
                    // NavMesh 보정
                    pet1Target = FindValidPositionOnNavMesh(pet1Target, 55f);
                    pet2Target = FindValidPositionOnNavMesh(pet2Target, 55f);
                    
                    // 두 펫을 움직이게 설정
                    pet1.agent.isStopped = false;
                    pet2.agent.isStopped = false;
                    pet1.agent.SetDestination(pet1Target);
                    pet2.agent.SetDestination(pet2Target);
                    
                    Debug.Log($"[WalkTogether] 새 목적지 설정: 펫1({pet1Target}), 펫2({pet2Target})");
                    
                    // 서로 같은 방향 보도록 회전
                    Quaternion targetRotation = Quaternion.LookRotation(walkDirection);
                    
                    // 첫 번째 펫 회전
                    pet1.transform.rotation = targetRotation;
                    if (pet1.petModelTransform != null)
                    {
                        pet1.petModelTransform.rotation = targetRotation;
                    }
                    
                    // 두 번째 펫 회전
                    pet2.transform.rotation = targetRotation;
                    if (pet2.petModelTransform != null)
                    {
                        pet2.petModelTransform.rotation = targetRotation;
                    }
                }
                
                yield return null;
            }
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
            
            // 4. 종료 처리
            // 원래 설정 복원
            pet1State.Restore(pet1);
            pet2State.Restore(pet2);
            
            // 애니메이션 정리
            pet1.GetComponent<PetAnimationController>().StopContinuousAnimation();
            pet2.GetComponent<PetAnimationController>().StopContinuousAnimation();
            
            Debug.Log($"[WalkTogether] {pet1.petName}와(과) {pet2.petName}의 함께 걷기 완료");
        }
    }
}