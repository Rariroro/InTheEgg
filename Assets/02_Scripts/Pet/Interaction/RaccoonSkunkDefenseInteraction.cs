// 너구리-스컹크 방어 상호작용 구현
using System.Collections;
using UnityEngine;

public class RaccoonSkunkDefenseInteraction : BasePetInteraction
{
    public override string InteractionName => "RaccoonSkunkDefense";
    
    // 상호작용 유형 지정 (기존 ChaseAndRun 유형 활용)
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
        
        // 너구리와 스컹크 간 상호작용만 가능
        if ((type1 == PetType.Raccoon && type2 == PetType.Skunk) || 
            (type1 == PetType.Skunk && type2 == PetType.Raccoon))
        {
            return true;
        }
        
        return false;
    }
    
    // 상호작용 수행
    public override IEnumerator PerformInteraction(PetController pet1, PetController pet2)
    {
        Debug.Log($"[RaccoonSkunkDefense] {pet1.petName}와(과) {pet2.petName} 사이의 너구리-스컹크 상호작용 시작!");
        
        // 너구리와 스컹크 식별
        PetController raccoon = null;
        PetController skunk = null;
        
        if (pet1.PetType == PetType.Raccoon && pet2.PetType == PetType.Skunk)
        {
            raccoon = pet1;
            skunk = pet2;
        }
        else if (pet1.PetType == PetType.Skunk && pet2.PetType == PetType.Raccoon)
        {
            raccoon = pet2;
            skunk = pet1;
        }
        else
        {
            // 너구리나 스컹크가 아닌 경우 (실제로는 CanInteract에서 걸러짐)
            Debug.LogWarning("[RaccoonSkunkDefense] 너구리와 스컹크가 아닌 펫들로 상호작용이 시작되었습니다.");
            raccoon = pet1;
            skunk = pet2;
        }
        
        Debug.Log($"[RaccoonSkunkDefense] 너구리 역할: {raccoon.petName}, 스컹크 역할: {skunk.petName}");
        
        // 원래 상태 저장
        PetOriginalState raccoonState = new PetOriginalState(raccoon);
        PetOriginalState skunkState = new PetOriginalState(skunk);
        
        // 위치 고정 코루틴 참조 저장
        Coroutine fixPositionCoroutine = null;
        
        try
        {
            // 1. 접근 단계 - 너구리가 스컹크에게 접근
            Debug.Log($"[RaccoonSkunkDefense] {raccoon.petName}가 {skunk.petName}에게 접근합니다.");
            
            // 감정 표현 - 너구리는 공격적, 스컹크는 경계심
            raccoon.ShowEmotion(EmotionType.Angry, 30f);
            skunk.ShowEmotion(EmotionType.Scared, 30f);
            
            // 너구리가 스컹크에게 접근할 수 있는 위치 계산
            Vector3 approachPoint = FindInteractionSpot(raccoon, skunk, 4f);
            
            // 각 동물의 위치 계산
            Vector3 raccoonApproachPos = approachPoint + (raccoon.transform.position - skunk.transform.position).normalized * 3f;
            Vector3 skunkPosition = skunk.transform.position;
            
            // NavMesh 보정
            // raccoonApproachPos = FindValidPositionOnNavMesh(raccoonApproachPos, 5f);
            
            // 너구리만 이동
            raccoon.agent.isStopped = false;
            raccoon.agent.SetDestination(raccoonApproachPos);
            raccoon.GetComponent<PetAnimationController>().SetContinuousAnimation(PetAnimationController.PetAnimationType.Walk); // 걷기 애니메이션
            
            // 스컹크는 제자리에서 경계
            skunk.agent.isStopped = true;
            skunk.GetComponent<PetAnimationController>().SetContinuousAnimation(0); // 기본 애니메이션
            
            // 너구리가 스컹크에게 충분히 가까워질 때까지 대기
            float approachTimeout = 8f;
            float startTime = Time.time;
            
            while (Time.time - startTime < approachTimeout)
            {
                // 스컹크는 너구리를 계속 바라봄
                LookAtOther(skunk, raccoon);
                
                // 목적지에 가까워지면 중단
                if (!raccoon.agent.pathPending && raccoon.agent.remainingDistance < 1.0f)
                {
                    Debug.Log($"[RaccoonSkunkDefense] {raccoon.petName}이(가) {skunk.petName}에게 접근했습니다!");
                    break;
                }
                
                yield return null;
            }
            
            // 너구리 이동 정지 및 스컹크 바라보기
            raccoon.agent.isStopped = true;
            raccoon.GetComponent<PetAnimationController>().StopContinuousAnimation();
            LookAtEachOther(raccoon, skunk);
            
            // 위치 고정 설정
            Vector3 raccoonFixedPos = raccoon.transform.position;
            Vector3 skunkFixedPos = skunk.transform.position;
            Quaternion raccoonFixedRot = raccoon.transform.rotation;
            Quaternion skunkFixedRot = skunk.transform.rotation;
            
            // 위치 고정 코루틴 시작
            fixPositionCoroutine = StartCoroutine(
                FixPositionDuringInteraction(
                    raccoon, skunk, 
                    raccoonFixedPos, skunkFixedPos, 
                    raccoonFixedRot, skunkFixedRot
                )
            );
            
            // 잠시 대기
            yield return new WaitForSeconds(1.0f);
            
            // 2. 너구리가 스컹크 공격 시도
            Debug.Log($"[RaccoonSkunkDefense] {raccoon.petName}가 {skunk.petName}를 공격하려고 시도합니다.");
            
            // 위치 고정 코루틴 중지 (움직임 시작을 위해)
            if (fixPositionCoroutine != null)
            {
                StopCoroutine(fixPositionCoroutine);
                fixPositionCoroutine = null;
            }
            
            // 공격 애니메이션 실행
            yield return StartCoroutine(raccoon.GetComponent<PetAnimationController>().PlayAnimationWithCustomDuration(PetAnimationController.PetAnimationType.Attack, 1.5f, false, false));
            
          // 스컹크 방어 단계 코드 부분을 수정합니다
// 3. 스컹크 방어 단계 - 스컹크가 뒤돌아 방구 뀌기
Debug.Log($"[RaccoonSkunkDefense] {skunk.petName}가 뒤를 돌아 방어를 위해 방구를 뀝니다!");

// 스컹크 감정 변경 - 방어
skunk.ShowEmotion(EmotionType.Angry, 5f);

// 스컹크가 너구리를 향해 뒤를 돌리는 연출
Vector3 directionToRaccoon = (raccoon.transform.position - skunk.transform.position).normalized;
Quaternion originalRotation = skunk.transform.rotation;

// 180도 회전하여 꼬리가 너구리를 향하도록 함
Quaternion backwardsRotation = Quaternion.LookRotation(-directionToRaccoon);
float turnTime = 0.5f;
float elapsedTime = 0f;

// 부드럽게 회전시키기
while (elapsedTime < turnTime)
{
    float ratio = elapsedTime / turnTime;
    skunk.transform.rotation = Quaternion.Lerp(originalRotation, backwardsRotation, ratio);
    
    if (skunk.petModelTransform != null)
    {
        skunk.petModelTransform.rotation = skunk.transform.rotation;
    }
    
    elapsedTime += Time.deltaTime;
    yield return null;
}

// 스컹크의 방구 뀌는 애니메이션 (공격 동작으로 표현)
yield return StartCoroutine(skunk.GetComponent<PetAnimationController>().PlayAnimationWithCustomDuration(PetAnimationController.PetAnimationType.Attack, 2.0f, false, false));

// 방구 분사 효과 (시각적인 효과가 필요한 경우 여기에 추가)
// 예: 파티클 시스템이 있다면 활성화 등

// 스컹크가 다시 원래 방향으로 돌아오는 연출
elapsedTime = 0f;
while (elapsedTime < turnTime)
{
    float ratio = elapsedTime / turnTime;
    skunk.transform.rotation = Quaternion.Lerp(backwardsRotation, originalRotation, ratio);
    
    if (skunk.petModelTransform != null)
    {
        skunk.petModelTransform.rotation = skunk.transform.rotation;
    }
    
    elapsedTime += Time.deltaTime;
    yield return null;
}
            // 4. 너구리 반응 - 놀라서 뒤로 물러남
            Debug.Log($"[RaccoonSkunkDefense] {raccoon.petName}가 놀라서 뒤로 물러납니다!");
            
            // 너구리 감정 변경 - 놀람
            raccoon.ShowEmotion(EmotionType.Surprised, 5f);
            
            // 너구리 뒤로 물러나는 위치 계산
            Vector3 retreatDirection = (raccoon.transform.position - skunk.transform.position).normalized;
            Vector3 retreatPosition = raccoon.transform.position + retreatDirection * 6f;
            retreatPosition = FindValidPositionOnNavMesh(retreatPosition, 8f);
            
            // 너구리 물러나기
            raccoon.agent.isStopped = false;
            raccoon.agent.SetDestination(retreatPosition);
            raccoon.GetComponent<PetAnimationController>().SetContinuousAnimation(PetAnimationController.PetAnimationType.Run); // 달리기 애니메이션 (빠르게 도망)
            
            // 물러나는 동안 대기
            float retreatTimeout = 4f;
            startTime = Time.time;
            
            while (Time.time - startTime < retreatTimeout)
            {
                // 목적지에 도착하면 중단
                if (!raccoon.agent.pathPending && raccoon.agent.remainingDistance < 0.5f)
                {
                    Debug.Log($"[RaccoonSkunkDefense] {raccoon.petName}이(가) 물러나기를 완료했습니다.");
                    break;
                }
                
                yield return null;
            }
            
            // 너구리 정지 및 기본 애니메이션으로 전환
            raccoon.agent.isStopped = true;
            raccoon.GetComponent<PetAnimationController>().SetContinuousAnimation(0); // 기본 애니메이션
            
            // 5. 스컹크 도망 단계
            Debug.Log($"[RaccoonSkunkDefense] {skunk.petName}가 안전한 곳으로 도망갑니다.");
            
            // 스컹크 감정 변경 - 안심
            skunk.ShowEmotion(EmotionType.Happy, 10f);
            
            // 스컹크가 도망갈 방향 랜덤하게 설정
            Vector3 escapeDirection = new Vector3(Random.Range(-1f, 1f), 0, Random.Range(-1f, 1f)).normalized;
            Vector3 escapeTarget = skunk.transform.position + escapeDirection * 15f;
            escapeTarget = FindValidPositionOnNavMesh(escapeTarget, 20f);
            
            // 스컹크 이동 시작
            skunk.agent.isStopped = false;
            skunk.agent.SetDestination(escapeTarget);
            skunk.GetComponent<PetAnimationController>().SetContinuousAnimation(PetAnimationController.PetAnimationType.Walk); // 걷기 애니메이션
            
            // 도망가는 동안 대기
            float escapeTimeout = 6f;
            startTime = Time.time;
            
            while (Time.time - startTime < escapeTimeout)
            {
                // 목적지에 가까워지면 중단
                if (!skunk.agent.pathPending && skunk.agent.remainingDistance < 1.0f)
                {
                    Debug.Log($"[RaccoonSkunkDefense] {skunk.petName}이(가) 안전하게 도망쳤습니다.");
                    break;
                }
                
                yield return null;
            }
            
            // 6. 상호작용 종료 - 스컹크가 한 번 더 뒤돌아보는 연출
            skunk.agent.isStopped = true;
            
            // 너구리 쪽 방향 확인
            // Vector3 directionToRaccoon = (raccoon.transform.position - skunk.transform.position).normalized;
            
            // 스컹크가 너구리 쪽을 바라봄
            Quaternion lookBackRotation = Quaternion.LookRotation(directionToRaccoon);
            skunk.transform.rotation = lookBackRotation;
            if (skunk.petModelTransform != null)
            {
                skunk.petModelTransform.rotation = lookBackRotation;
            }
            
            // 승리의 기쁨 표현 (점프 애니메이션)
            yield return StartCoroutine(skunk.GetComponent<PetAnimationController>().PlayAnimationWithCustomDuration(PetAnimationController.PetAnimationType.Jump, 2.0f, false, false));
            
            // 마지막으로 기본 애니메이션으로 전환
            skunk.GetComponent<PetAnimationController>().SetContinuousAnimation(0);
            
            // 추가 대기 시간
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
            raccoon.HideEmotion();
            skunk.HideEmotion();
            
            // 원래 상태로 복원
            raccoonState.Restore(raccoon);
            skunkState.Restore(skunk);
            
            // 애니메이션 원래대로
            raccoon.GetComponent<PetAnimationController>().StopContinuousAnimation();
            skunk.GetComponent<PetAnimationController>().StopContinuousAnimation();
            
            Debug.Log($"[RaccoonSkunkDefense] 상호작용 종료, 펫들의 상태가 원래대로 복원됨");
        }
    }
}