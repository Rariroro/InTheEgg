// 타고 걷기 상호작용 구현 (개선 버전)
using System.Collections;
using UnityEngine;

public class RideAndWalkInteraction : BasePetInteraction
{
    public override string InteractionName => "RideAndWalk";
    // 상호작용 유형 지정
    protected override InteractionType DetermineInteractionType()
    {
        return InteractionType.RideAndWalk;
    }
    // 이 상호작용이 가능한지 확인
    public override bool CanInteract(PetController pet1, PetController pet2)
    {
        PetType type1 = pet1.PetType;
        PetType type2 = pet2.PetType;
        
        // 미어켓과 멧돼지는 타고 걷기 가능
        if ((type1 == PetType.Meerkat && type2 == PetType.Boar) || 
            (type1 == PetType.Boar && type2 == PetType.Meerkat))
        {
            return true;
        }
        
        return false;
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
        Debug.Log($"[RideAndWalk] {pet1.petName}와(과) {pet2.petName}의 타고 걷기 상호작용 시작!");
        
        // 미어켓과 멧돼지 식별
        PetController meerkat = null;
        PetController boar = null;
        
        // 펫 타입에 따라 미어켓과 멧돼지 식별
        if (pet1.PetType == PetType.Meerkat && pet2.PetType == PetType.Boar)
        {
            meerkat = pet1;
            boar = pet2;
        }
        else if (pet2.PetType == PetType.Meerkat && pet1.PetType == PetType.Boar)
        {
            meerkat = pet2;
            boar = pet1;
        }
        else
        {
            // 미어켓이나 멧돼지가 아닌 경우, 첫 번째 펫을 미어켓으로, 두 번째 펫을 멧돼지로 취급
            Debug.Log("[RideAndWalk] 미어켓과 멧돼지가 아닌 펫들입니다. 역할을 임의로 배정합니다.");
            meerkat = pet1;
            boar = pet2;
        }
        
        Debug.Log($"[RideAndWalk] 미어켓 역할: {meerkat.petName}, 멧돼지 역할: {boar.petName}");
        
        // 원래 상태 저장 (개선된 헬퍼 클래스 사용)
        PetOriginalState meerkatState = new PetOriginalState(meerkat);
        PetOriginalState boarState = new PetOriginalState(boar);
        Transform originalMeerkatParent = meerkat.transform.parent;
        
        // 위치 고정 코루틴 참조 저장
        Coroutine fixPositionCoroutine = null;
        
        try
        {
            // 감정 표현 (사랑 표정) - 두 동물 모두 즐거운 상호작용임을 표현
            meerkat.ShowEmotion(EmotionType.Love, 30f);
            boar.ShowEmotion(EmotionType.Love, 30f);
            // 1. 상호작용 위치 찾기
            Vector3 interactionSpot = FindInteractionSpot(meerkat, boar, 2f);
            
            // 2. 두 펫의 위치 계산 (상호작용 지점 기준으로 서로 마주보는 방향으로 배치)
            // 두 펫 사이의 방향 벡터 계산
            Vector3 directionVector = (boar.transform.position - meerkat.transform.position).normalized;
            directionVector.y = 0; // y축 무시 (수평 방향만 고려)
            
            // 상호작용 간격 설정 (펫 사이 거리)
            float interactionDistance = 5.0f; // 더 큰 값으로 설정하여 펫들 사이 간격 확보
            
            // 각 펫의 위치 계산
            Vector3 meerkatPosition = interactionSpot - directionVector * (interactionDistance / 2);
            Vector3 boarPosition = interactionSpot + directionVector * (interactionDistance / 2);
            
            // 각 위치가 NavMesh 위에 있는지 확인하고 보정
            meerkatPosition = FindValidPositionOnNavMesh(meerkatPosition, 5f);
            boarPosition = FindValidPositionOnNavMesh(boarPosition, 5f);
            
            // 공통 MoveToPositions 메소드 사용
            yield return StartCoroutine(MoveToPositions(meerkat, boar, meerkatPosition, boarPosition, 10f));
            
            // 3. 두 펫 모두 이동을 멈추고 서로 마주보게 하기
            LookAtEachOther(meerkat, boar);
            
            // 위치 고정을 위한 변수 저장
            Vector3 meerkatFixedPos = meerkat.transform.position;
            Vector3 boarFixedPos = boar.transform.position;
            Quaternion meerkatFixedRot = meerkat.transform.rotation;
            Quaternion boarFixedRot = boar.transform.rotation;
            
            // 초기 상호작용 단계에서는 위치 고정 코루틴 시작
            fixPositionCoroutine = StartCoroutine(
                FixPositionDuringInteraction(
                    meerkat, boar, 
                    meerkatFixedPos, boarFixedPos, 
                    meerkatFixedRot, boarFixedRot
                )
            );
            
            // 잠시 대기 (안정화를 위해)
            yield return new WaitForSeconds(0.5f);
            
            // 4. 티몬과 품바처럼 놀기 시작
            Debug.Log($"[RideAndWalk] {meerkat.petName}와(과) {boar.petName}가 놀기 시작합니다.");
            
            // 애니메이션 컨트롤러 참조
            PetAnimationController meerkatAnimController = meerkat.GetComponent<PetAnimationController>();
            PetAnimationController boarAnimController = boar.GetComponent<PetAnimationController>();
            
            // 미어켓이 먼저 점프하는 애니메이션 (애니메이션 3)
            yield return StartCoroutine(PlaySimultaneousAnimations(meerkat, boar, PetAnimationController.PetAnimationType.Jump, 0, 1.5f));
            
            // 멧돼지가 반응하여 움직이는 애니메이션 (애니메이션 6 - 공격/놀기)
            yield return StartCoroutine(PlaySimultaneousAnimations(boar, meerkat, PetAnimationController.PetAnimationType.Attack, 0, 2.0f));
            
            // 미어켓이 다시 점프
            yield return StartCoroutine(PlaySimultaneousAnimations(meerkat, boar, PetAnimationController.PetAnimationType.Jump, 0, 1.5f));
            
            // 서로 상호작용하는 애니메이션 반복 (3번)
            for (int i = 0; i < 3; i++)
            {
                // 미어켓이 즐거워하는 애니메이션 (애니메이션 6 - 공격/놀기)
                yield return StartCoroutine(meerkatAnimController.PlayAnimationWithCustomDuration(PetAnimationController.PetAnimationType.Attack, 1.0f, false, false));
                
                // 멧돼지도 즐거워하는 애니메이션
                if (i % 2 == 0)
                {
                    // 짝수 번째는 애니메이션 3 (점프)
                    yield return StartCoroutine(boarAnimController.PlayAnimationWithCustomDuration(PetAnimationController.PetAnimationType.Jump, 1.0f, false, false));
                }
                else
                {
                    // 홀수 번째는 애니메이션 6 (공격/놀기)
                    yield return StartCoroutine(boarAnimController.PlayAnimationWithCustomDuration(PetAnimationController.PetAnimationType.Attack, 1.0f, false, false));
                }
            }
            
            // 5. 미어켓이 멧돼지 위에 타기 준비
            Debug.Log($"[RideAndWalk] {meerkat.petName}이(가) {boar.petName}의 등에 타려고 합니다.");
            
            // 멧돼지가 앉는 애니메이션 (애니메이션 4 - 앉기/먹기)
            yield return StartCoroutine(boarAnimController.PlayAnimationWithCustomDuration(PetAnimationController.PetAnimationType.Eat, 2.0f, false, false));
            
            // 미어켓이 점프하는 애니메이션
            yield return StartCoroutine(meerkatAnimController.PlayAnimationWithCustomDuration(PetAnimationController.PetAnimationType.Jump, 1.5f, false, false));
            
            // 6. 미어켓이 멧돼지 위에 올라탈 때는 위치 고정 코루틴 중지
            if (fixPositionCoroutine != null)
            {
                StopCoroutine(fixPositionCoroutine);
                fixPositionCoroutine = null;
            }
            
            // 미어켓 NavMeshAgent 비활성화 (물리적 이동을 직접 제어하기 위해)
            meerkat.agent.enabled = false;
            
            // 멧돼지의 등 위치 계산 (약간 위쪽)
            Vector3 ridePosition = boar.transform.position + new Vector3(0, 1.7f, 0);
            
            // 미어켓을 부드럽게 멧돼지 등 위로 이동
            float rideTime = 1.0f;
            float elapsedTime = 0f;
            Vector3 startPosition = meerkat.transform.position;
            
            while (elapsedTime < rideTime)
            {
                meerkat.transform.position = Vector3.Lerp(startPosition, ridePosition, elapsedTime / rideTime);
                elapsedTime += Time.deltaTime;
                yield return null;
            }
            
            // 최종 위치 설정
            meerkat.transform.position = ridePosition;
            
            // 미어켓을 멧돼지의 자식으로 설정하여 함께 움직이도록 함
            meerkat.transform.SetParent(boar.transform);
            
            // 미어켓이 앞을 보도록 회전
            if (meerkat.petModelTransform != null)
            {
                meerkat.petModelTransform.rotation = boar.transform.rotation;
            }
            
            Debug.Log($"[RideAndWalk] {meerkat.petName}이(가) {boar.petName}의 등에 탔습니다.");
            
            // 7. 함께 걷기 시작
            // 멧돼지 이동 재개
            boar.agent.isStopped = false;
            boarAnimController.SetContinuousAnimation(PetAnimationController.PetAnimationType.Walk); // 걷기 애니메이션
            
            // 미어켓은 앉은 자세 (애니메이션 4)
            meerkatAnimController.SetContinuousAnimation(PetAnimationController.PetAnimationType.Eat);
            
            Debug.Log($"[RideAndWalk] {boar.petName}이(가) {meerkat.petName}을(를) 태우고 걷기 시작합니다.");
            
            // 랜덤한 방향으로 이동
            for (int i = 0; i < 3; i++) // 3번의 목적지 변경
            {
                // 랜덤 방향과 거리
                Vector3 randomDirection = new Vector3(Random.Range(-1f, 1f), 0, Random.Range(-1f, 1f)).normalized;
                float distance = Random.Range(10f, 20f);
                
                // 목적지 계산
                Vector3 destination = boar.transform.position + randomDirection * distance;
                destination = FindValidPositionOnNavMesh(destination, distance);
                
                // 멧돼지에게 새 목적지 설정
                boar.agent.SetDestination(destination);
                
                Debug.Log($"[RideAndWalk] 새 목적지로 이동 중: {destination}");
                
                // 목적지에 도착하거나 5초가 경과할 때까지 대기
                float moveTimeout = 5f;
                float startTime = Time.time;
                
                while (Time.time - startTime < moveTimeout)
                {
                    // 도착했는지 확인
                    if (!boar.agent.pathPending && boar.agent.remainingDistance < 0.5f)
                    {
                        Debug.Log("[RideAndWalk] 목적지에 도착");
                        break;
                    }
                    
                    // 가끔 미어켓이 즐거워하는 애니메이션 표시
                    if (Random.value < 0.01f) // 약 1% 확률
                    {
                        StartCoroutine(meerkatAnimController.PlayAnimationWithCustomDuration(PetAnimationController.PetAnimationType.Jump, 1.0f, false, false));
                        yield return new WaitForSeconds(1.0f);
                        meerkatAnimController.SetContinuousAnimation(PetAnimationController.PetAnimationType.Eat); // 다시 앉은 자세로
                    }
                    
                    yield return null;
                }
                
                // 각 이동 사이에 잠시 멈춤
                boar.agent.isStopped = true;
                boarAnimController.SetContinuousAnimation(0); // 기본 자세
                
                yield return new WaitForSeconds(1.0f);
                
                // 다시 움직임 시작
                boar.agent.isStopped = false;
                boarAnimController.SetContinuousAnimation(PetAnimationController.PetAnimationType.Walk); // 걷기 애니메이션
            }
            
            // 8. 마지막 목적지 도착 후 미어켓이 내리기
            Debug.Log($"[RideAndWalk] 여행이 끝났습니다. {meerkat.petName}이(가) 내립니다.");
            
            // 멧돼지 정지
            boar.agent.isStopped = true;
            boarAnimController.SetContinuousAnimation(0); // 기본 자세
            
            // 내리는 과정에서 멧돼지의 위치 고정 (미어켓은 이동해야 하므로 고정하지 않음)
            Vector3 finalBoarPos = boar.transform.position;
            Quaternion finalBoarRot = boar.transform.rotation;
            
            // 멧돼지만 고정하는 코루틴 시작
            fixPositionCoroutine = StartCoroutine(
                FixPositionDuringInteraction(
                    meerkat, boar,
                    meerkatFixedPos, finalBoarPos,
                    meerkatFixedRot, finalBoarRot,
                    false, true // 미어켓은 고정하지 않고 멧돼지만 고정
                )
            );
            
            // 멧돼지가 앉는 애니메이션
            yield return StartCoroutine(boarAnimController.PlayAnimationWithCustomDuration(PetAnimationController.PetAnimationType.Eat, 1.5f, false, false));
            
            // 미어켓이 점프하는 애니메이션
            yield return StartCoroutine(meerkatAnimController.PlayAnimationWithCustomDuration(PetAnimationController.PetAnimationType.Jump, 1.5f, false, false));
            
            // 미어켓을 부모에서 분리
            meerkat.transform.SetParent(null);
            
            // 미어켓을 멧돼지 옆에 위치시킴
            Vector3 dismountPosition = boar.transform.position + new Vector3(1.5f, 0, 0);
            dismountPosition = FindValidPositionOnNavMesh(dismountPosition, 2f);
            meerkat.transform.position = dismountPosition;
            
            // 서로 마주보게 회전
            LookAtEachOther(meerkat, boar);
            
            // 미어켓의 NavMeshAgent 다시 활성화
            meerkat.agent.enabled = true;
            
            Debug.Log($"[RideAndWalk] {meerkat.petName}이(가) {boar.petName}에서 내렸습니다.");
            
            // 작별 인사 - 서로 즐거워하는 애니메이션
            yield return StartCoroutine(PlaySimultaneousAnimations(meerkat, boar, PetAnimationController.PetAnimationType.Attack, PetAnimationController.PetAnimationType.Attack, 2.0f));
            
            // 마지막으로 기본 애니메이션으로 전환
            meerkatAnimController.SetContinuousAnimation(0);
            boarAnimController.SetContinuousAnimation(0);
            
            // 잠시 대기
            yield return new WaitForSeconds(1.0f);
        }
        finally
        {
            // 진행 중인 위치 고정 코루틴이 있다면 중지
            if (fixPositionCoroutine != null)
            {
                StopCoroutine(fixPositionCoroutine);
            }
            
            // 감정 말풍선 숨기기
            meerkat.HideEmotion();
            boar.HideEmotion();
            
            // 원래 상태로 복원
            if (meerkat.transform.parent == boar.transform)
            {
                // 아직 부모-자식 관계가 남아있다면 분리
                meerkat.transform.SetParent(originalMeerkatParent);
            }
            
            // NavMeshAgent 상태 복원
            meerkatState.Restore(meerkat);
            boarState.Restore(boar);
            
            // 애니메이션 원래대로
            meerkat.GetComponent<PetAnimationController>().StopContinuousAnimation();
            boar.GetComponent<PetAnimationController>().StopContinuousAnimation();
            
            Debug.Log($"[RideAndWalk] 상호작용 종료, 펫들의 상태가 원래대로 복원됨");
        }
    }
}