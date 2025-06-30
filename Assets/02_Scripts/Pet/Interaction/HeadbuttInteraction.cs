// 박치기 상호작용 구현 - 염소-양, 황소-버팔로, 코뿔소-들소 조합 지원
using System.Collections;
using UnityEngine;

public class HeadbuttInteraction : BasePetInteraction
{
    public override string InteractionName => "Headbutt";
    
    // 상호작용 유형 지정 - 기존의 Fight 타입 활용
    protected override InteractionType DetermineInteractionType()
    {
        return InteractionType.Fight;
    }
    
    // 이 상호작용이 가능한지 확인
    public override bool CanInteract(PetController pet1, PetController pet2)
    {
        // 염소와 양, 황소와 버팔로, 코뿔소와 들소 조합 확인
        return (pet1.PetType == PetType.Goat && pet2.PetType == PetType.Sheep) || 
               (pet1.PetType == PetType.Sheep && pet2.PetType == PetType.Goat) ||
               (pet1.PetType == PetType.Bull && pet2.PetType == PetType.Buffalo) ||
               (pet1.PetType == PetType.Buffalo && pet2.PetType == PetType.Bull) ||
               (pet1.PetType == PetType.Rhino && pet2.PetType == PetType.Bison) ||
               (pet1.PetType == PetType.Bison && pet2.PetType == PetType.Rhino);
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
    
    // 마주본 채로 뒤로 이동하는 메서드 추가
    private IEnumerator MoveBackward(PetController pet, Vector3 targetPos, float duration)
    {
        // 원래 위치 저장
        Vector3 startPos = pet.transform.position;
        float elapsedTime = 0f;
        
        // NavMeshAgent 임시 비활성화 (방향 전환 방지)
        bool wasAgentEnabled = pet.agent.enabled;
        pet.agent.enabled = false;
        
        // 후진 애니메이션 (걷기 애니메이션 사용)
        pet.GetComponent<PetAnimationController>().SetContinuousAnimation(PetAnimationController.PetAnimationType.Walk);
        
        // 원래 방향 유지하며 뒤로 이동
        Quaternion originalRotation = pet.transform.rotation;
        
        while (elapsedTime < duration)
        {
            // 시간에 따른 이동 보간
            float t = elapsedTime / duration;
            pet.transform.position = Vector3.Lerp(startPos, targetPos, t);
            
            // 회전 유지 (마주본 상태 유지)
            pet.transform.rotation = originalRotation;
            if (pet.petModelTransform != null)
                pet.petModelTransform.rotation = originalRotation;
            
            elapsedTime += Time.deltaTime;
            yield return null;
        }
        
        // 최종 위치 설정
        pet.transform.position = targetPos;
        
        // NavMeshAgent 원래 상태로 복원
        pet.agent.enabled = wasAgentEnabled;
        if (pet.agent.enabled && !pet.agent.isOnNavMesh)
        {
            // NavMesh에 없는 경우 재배치 시도
            UnityEngine.AI.NavMeshHit hit;
            if (UnityEngine.AI.NavMesh.SamplePosition(pet.transform.position, out hit, 1f, UnityEngine.AI.NavMesh.AllAreas))
            {
                pet.transform.position = hit.position;
            }
        }
    }
    
    // 두 펫의 조합 유형을 결정하는 메서드 추가
    private enum HeadbuttPair { GoatSheep, BullBuffalo, RhinoBison, Other }
    
    private HeadbuttPair GetPairType(PetController pet1, PetController pet2)
    {
        if ((pet1.PetType == PetType.Goat && pet2.PetType == PetType.Sheep) ||
            (pet1.PetType == PetType.Sheep && pet2.PetType == PetType.Goat))
        {
            return HeadbuttPair.GoatSheep;
        }
        else if ((pet1.PetType == PetType.Bull && pet2.PetType == PetType.Buffalo) ||
                 (pet1.PetType == PetType.Buffalo && pet2.PetType == PetType.Bull))
        {
            return HeadbuttPair.BullBuffalo;
        }
        else if ((pet1.PetType == PetType.Rhino && pet2.PetType == PetType.Bison) ||
                 (pet1.PetType == PetType.Bison && pet2.PetType == PetType.Rhino))
        {
            return HeadbuttPair.RhinoBison;
        }
        
        return HeadbuttPair.Other;
    }
    
    // 상호작용 수행
    protected override IEnumerator PerformInteraction(PetController pet1, PetController pet2)
    {
        // 상호작용 유형 확인
        HeadbuttPair pairType = GetPairType(pet1, pet2);
        
        if (pairType == HeadbuttPair.GoatSheep)
        {
            Debug.Log($"[Headbutt] {pet1.petName}와(과) {pet2.petName}의 염소-양 박치기 상호작용 시작!");
        }
        else if (pairType == HeadbuttPair.BullBuffalo)
        {
            Debug.Log($"[Headbutt] {pet1.petName}와(과) {pet2.petName}의 황소-버팔로 박치기 상호작용 시작!");
        }
        else if (pairType == HeadbuttPair.RhinoBison)
        {
            Debug.Log($"[Headbutt] {pet1.petName}와(과) {pet2.petName}의 코뿔소-들소 박치기 상호작용 시작!");
        }
        else
        {
            Debug.Log($"[Headbutt] {pet1.petName}와(과) {pet2.petName}의 박치기 상호작용 시작!");
        }
        
        // 펫 식별 (첫 번째와 두 번째 펫으로 구분)
        PetController firstPet = null;
        PetController secondPet = null;
        
        // 염소-양 경우
        if (pairType == HeadbuttPair.GoatSheep)
        {
            if (pet1.PetType == PetType.Goat)
            {
                firstPet = pet1; // 염소
                secondPet = pet2; // 양
            }
            else
            {
                firstPet = pet2; // 염소
                secondPet = pet1; // 양
            }
        }
        // 황소-버팔로 경우
        else if (pairType == HeadbuttPair.BullBuffalo)
        {
            if (pet1.PetType == PetType.Bull)
            {
                firstPet = pet1; // 황소
                secondPet = pet2; // 버팔로
            }
            else
            {
                firstPet = pet2; // 황소
                secondPet = pet1; // 버팔로
            }
        }
        // 코뿔소-들소 경우
        else if (pairType == HeadbuttPair.RhinoBison)
        {
            if (pet1.PetType == PetType.Rhino)
            {
                firstPet = pet1; // 코뿔소
                secondPet = pet2; // 들소
            }
            else
            {
                firstPet = pet2; // 코뿔소
                secondPet = pet1; // 들소
            }
        }
        else
        {
            // 기타 경우 (발생하지 않아야 함)
            firstPet = pet1;
            secondPet = pet2;
        }
        
        // 원래 상태 저장
        PetOriginalState firstPetState = new PetOriginalState(firstPet);
        PetOriginalState secondPetState = new PetOriginalState(secondPet);
        
        // 위치 고정 코루틴 참조 저장
        Coroutine fixPositionCoroutine = null;
        
        try
        {
            // 감정 표현 추가 - 둘 다 도전적인 표정
            firstPet.ShowEmotion(EmotionType.Angry, 30f);
            secondPet.ShowEmotion(EmotionType.Angry, 30f);
            
            // 1. 박치기 위치 찾기 - 정확한 중간 지점
            Vector3 headbuttSpot = (firstPet.transform.position + secondPet.transform.position) / 2;
            headbuttSpot = FindValidPositionOnNavMesh(headbuttSpot, 10f);
            
            // 2. 서로 마주 보는 위치로 이동
            Vector3 direction = (secondPet.transform.position - firstPet.transform.position).normalized;
            direction.y = 0; // 높이는 무시
            
            // 동물 크기에 따른 박치기 거리 조정
            float headbuttDistance = 8f; // 기본 거리 (염소-양)
            
            // 황소-버팔로 또는 코뿔소-들소는 더 큰 동물이므로 거리 증가
            if (pairType == HeadbuttPair.BullBuffalo || pairType == HeadbuttPair.RhinoBison)
            {
                headbuttDistance = 12f;
            }
            
            // 첫 번째 펫 위치 계산
            Vector3 firstPetPos = headbuttSpot - direction * (headbuttDistance / 2);
            firstPetPos = FindValidPositionOnNavMesh(firstPetPos, 15f);
            
            // 두 번째 펫 위치 계산
            Vector3 secondPetPos = headbuttSpot + direction * (headbuttDistance / 2);
            secondPetPos = FindValidPositionOnNavMesh(secondPetPos, 15f);
            
            // 공통 MoveToPositions 메소드 사용
            yield return StartCoroutine(MoveToPositions(firstPet, secondPet, firstPetPos, secondPetPos, 10f));
            
            // 3. 서로 정확하게 마주보도록 회전
            LookAtEachOther(firstPet, secondPet);
            Debug.Log($"[Headbutt] {firstPet.petName}와(과) {secondPet.petName}이(가) 서로 마주봄");
            
            // 위치 고정을 위한 변수 저장
            Vector3 firstPetFixedPos = firstPet.transform.position;
            Vector3 secondPetFixedPos = secondPet.transform.position;
            Quaternion firstPetFixedRot = firstPet.transform.rotation;
            Quaternion secondPetFixedRot = secondPet.transform.rotation;
            
            // 초기 위치 고정
            fixPositionCoroutine = StartCoroutine(
                FixPositionDuringInteraction(
                    firstPet, secondPet, 
                    firstPetFixedPos, secondPetFixedPos, 
                    firstPetFixedRot, secondPetFixedRot
                )
            );
            
            // 잠시 대기 (긴장감 형성)
            yield return new WaitForSeconds(1.0f);
            
            // 박치기 상호작용 반복
            int headbuttCount = Random.Range(3, 6); // 3~5회 반복 (기본 - 염소-양)
            
            // 황소-버팔로 또는 코뿔소-들소는 더 강력하고 느리므로 횟수 감소
            if (pairType == HeadbuttPair.BullBuffalo || pairType == HeadbuttPair.RhinoBison)
            {
                headbuttCount = Random.Range(2, 4); // 2~3회 반복
            }
            
            for (int i = 0; i < headbuttCount; i++)
            {
                // 위치 고정 코루틴 중지 (이동을 위해)
                if (fixPositionCoroutine != null)
                {
                    StopCoroutine(fixPositionCoroutine);
                    fixPositionCoroutine = null;
                }
                
                // 4. 박치기 준비 - 뒤로 물러남
                Debug.Log($"[Headbutt] {i+1}번째 박치기 준비. 뒤로 물러납니다.");
                
                // 후진 거리 조정 (동물 크기에 따라)
                float backupDistance = 3.0f; // 기본 (염소-양)
                if (pairType == HeadbuttPair.BullBuffalo || pairType == HeadbuttPair.RhinoBison)
                {
                    backupDistance = 5.0f; // 큰 동물들은 더 길게
                }
                
                // 현재 방향을 유지하면서 뒤로 물러나는 위치 계산 (기존 방향 유지)
                Vector3 firstPetPrepPos = firstPet.transform.position - firstPet.transform.forward * backupDistance;
                Vector3 secondPetPrepPos = secondPet.transform.position - secondPet.transform.forward * backupDistance;
                
                // NavMesh 확인
                firstPetPrepPos = FindValidPositionOnNavMesh(firstPetPrepPos, 8f);
                secondPetPrepPos = FindValidPositionOnNavMesh(secondPetPrepPos, 8f);
                
                // 마주본 상태로 뒤로 걷기 (새로운 메서드 사용)
                float backupDuration = 1.0f; // 뒤로 이동하는 데 걸리는 시간 (더 자연스러운 속도)
                
                // 두 펫이 동시에 뒤로 이동하도록 병렬 실행
                StartCoroutine(MoveBackward(firstPet, firstPetPrepPos, backupDuration));
                yield return StartCoroutine(MoveBackward(secondPet, secondPetPrepPos, backupDuration));
                
                // 이동 종료 후 서로 마주보기 확인 (이미 마주보고 있겠지만 안전을 위해)
                LookAtEachOther(firstPet, secondPet);
                
                // 준비 위치에서 약간 대기 (긴장감)
                yield return new WaitForSeconds(0.5f);
                
                // 5. 박치기 실행 - 서로 달려들어 충돌
                Debug.Log($"[Headbutt] {i+1}번째 박치기 시작!");
                
                // 달리기 애니메이션
                firstPet.GetComponent<PetAnimationController>().SetContinuousAnimation(PetAnimationController.PetAnimationType.Run); // 달리기
                secondPet.GetComponent<PetAnimationController>().SetContinuousAnimation(PetAnimationController.PetAnimationType.Run); // 달리기
                
                // 동물 크기에 따른 속도 조정
                float speedMultiplier = 2.5f; // 기본 (염소-양)
                float accelerationMultiplier = 2.0f;
                
                // 황소-버팔로, 코뿔소-들소는 더 느리지만 더 강력하게
                if (pairType == HeadbuttPair.BullBuffalo || pairType == HeadbuttPair.RhinoBison)
                {
                    speedMultiplier = 2.0f;
                    accelerationMultiplier = 1.5f;
                }
                
                // 정확한 충돌 지점 계산 (두 펫 사이의 중간 지점)
                Vector3 collisionPoint = (firstPet.transform.position + secondPet.transform.position) / 2;
                collisionPoint = FindValidPositionOnNavMesh(collisionPoint, 5f);
                
                // 원래 위치로 빠르게 이동 시작
                firstPet.agent.speed = firstPetState.originalSpeed * speedMultiplier;
                secondPet.agent.speed = secondPetState.originalSpeed * speedMultiplier;
                firstPet.agent.acceleration = firstPetState.originalAcceleration * accelerationMultiplier;
                secondPet.agent.acceleration = secondPetState.originalAcceleration * accelerationMultiplier;
                
                firstPet.agent.isStopped = false;
                secondPet.agent.isStopped = false;
                firstPet.agent.SetDestination(collisionPoint);
                secondPet.agent.SetDestination(collisionPoint);
                
                // 충돌 거리 계산
                float collisionDistance = 1.5f; // 기본 충돌 거리 (염소-양)
                
                // 황소-버팔로, 코뿔소-들소는 더 큰 동물이므로 충돌 거리 증가
                if (pairType == HeadbuttPair.BullBuffalo || pairType == HeadbuttPair.RhinoBison)
                {
                    collisionDistance = 2.5f;
                }
                
                float chargeTimeout = 2.0f; // 최대 대기 시간
                float chargeStartTime = Time.time;
                bool collision = false;
                
                while (Time.time - chargeStartTime < chargeTimeout && !collision)
                {
                    // 두 펫 모두 중간 지점까지의 거리 계산
                    float distanceToCollision1 = Vector3.Distance(firstPet.transform.position, collisionPoint);
                    float distanceToCollision2 = Vector3.Distance(secondPet.transform.position, collisionPoint);
                    
                    // 두 펫 모두 충돌 지점에 충분히 가까워졌는지 확인
                    if (distanceToCollision1 <= collisionDistance && distanceToCollision2 <= collisionDistance)
                    {
                        collision = true;
                        Debug.Log($"[Headbutt] 충돌 발생! 거리: {distanceToCollision1}/{distanceToCollision2}");
                    }
                    
                    yield return null;
                }
                
                // 충돌 효과
                firstPet.agent.isStopped = true;
                secondPet.agent.isStopped = true;
                
                // 충돌 애니메이션 (공격/충격 애니메이션)
                // 공격 애니메이션 사용 (6번)
                yield return StartCoroutine(PlaySimultaneousAnimations(firstPet, secondPet, PetAnimationController.PetAnimationType.Attack, PetAnimationController.PetAnimationType.Attack, 1.0f));
                
                // 뒤로 밀려나는 효과 - 펫 유형에 따라 조정
                float knockbackDistance = 1.5f; // 기본 (염소-양)
                if (pairType == HeadbuttPair.BullBuffalo || pairType == HeadbuttPair.RhinoBison)
                {
                    knockbackDistance = 2.5f; // 더 큰 동물은 더 강력한 충격
                }
                
                // 뒤로 밀려나는 위치 계산
                Vector3 firstPetKnockbackPos = firstPet.transform.position - firstPet.transform.forward * knockbackDistance;
                Vector3 secondPetKnockbackPos = secondPet.transform.position - secondPet.transform.forward * knockbackDistance;
                
                // NavMesh 확인
                firstPetKnockbackPos = FindValidPositionOnNavMesh(firstPetKnockbackPos, 5f);
                secondPetKnockbackPos = FindValidPositionOnNavMesh(secondPetKnockbackPos, 5f);
                
                // 천천히 뒤로 밀림
                float knockbackTime = 0.5f;
                float knockbackElapsed = 0f;
                Vector3 firstPetStartPos = firstPet.transform.position;
                Vector3 secondPetStartPos = secondPet.transform.position;
                
                // 방향 저장 (뒤로 밀리는 동안에도 마주보게)
                Quaternion firstPetDir = firstPet.transform.rotation;
                Quaternion secondPetDir = secondPet.transform.rotation;
                
                while (knockbackElapsed < knockbackTime)
                {
                    firstPet.transform.position = Vector3.Lerp(firstPetStartPos, firstPetKnockbackPos, knockbackElapsed / knockbackTime);
                    secondPet.transform.position = Vector3.Lerp(secondPetStartPos, secondPetKnockbackPos, knockbackElapsed / knockbackTime);
                    
                    // 회전 유지 (계속 마주보게 유지)
                    firstPet.transform.rotation = firstPetDir;
                    secondPet.transform.rotation = secondPetDir;
                    if (firstPet.petModelTransform != null) firstPet.petModelTransform.rotation = firstPetDir;
                    if (secondPet.petModelTransform != null) secondPet.petModelTransform.rotation = secondPetDir;
                    
                    knockbackElapsed += Time.deltaTime;
                    yield return null;
                }
                
                // 마지막 위치 고정
                firstPet.transform.position = firstPetKnockbackPos;
                secondPet.transform.position = secondPetKnockbackPos;
                
                // 잠시 대기 (다음 박치기 준비)
                yield return new WaitForSeconds(0.7f);
                
                // 원래 위치 갱신 (현재 위치가 새로운 시작점)
                firstPetFixedPos = firstPet.transform.position;
                secondPetFixedPos = secondPet.transform.position;
                
                // 다시 서로 마주보기
                LookAtEachOther(firstPet, secondPet);
                firstPetFixedRot = firstPet.transform.rotation;
                secondPetFixedRot = secondPet.transform.rotation;
            }
            
            // 6. 승자 결정 (동물 타입에 따라 확률 조정)
            Debug.Log("[Headbutt] 박치기 대결 종료. 승자를 결정합니다.");
            
            // 각 동물 유형에 따른 승리 확률 조정
            float winProbability = 0.5f; // 기본 50/50 확률 (염소-양)
            
            // 황소-버팔로 경우
            if (pairType == HeadbuttPair.BullBuffalo)
            {
                // 황소가 firstPet인 경우 55% 확률로 승리
                winProbability = (firstPet.PetType == PetType.Bull) ? 0.55f : 0.45f;
            }
            // 코뿔소-들소 경우
            else if (pairType == HeadbuttPair.RhinoBison)
            {
                // 코뿔소가 firstPet인 경우 60% 확률로 승리
                winProbability = (firstPet.PetType == PetType.Rhino) ? 0.6f : 0.4f;
            }
            
            // 승자 결정
            PetController winner = DetermineWinner(firstPet, secondPet, winProbability);
            PetController loser = winner == firstPet ? secondPet : firstPet;
            
            Debug.Log($"[Headbutt] 승자: {winner.petName}!");
            
            // 감정 표현 업데이트
            winner.ShowEmotion(EmotionType.Victory, 5f);
            loser.ShowEmotion(EmotionType.Defeat, 5f);
            
            // 승리/패배 애니메이션
            yield return StartCoroutine(
                PlayWinnerLoserAnimations(winner, loser, PetAnimationController.PetAnimationType.Jump, PetAnimationController.PetAnimationType.Damage)
            );
            
            // 7. 마무리 - 기본 애니메이션으로 전환
            firstPet.GetComponent<PetAnimationController>().SetContinuousAnimation(0);
            secondPet.GetComponent<PetAnimationController>().SetContinuousAnimation(0);
            
            // 대기 시간 추가
            yield return new WaitForSeconds(2.0f);
        }
        finally
        {
            // 위치 고정 코루틴 중지
            if (fixPositionCoroutine != null)
            {
                StopCoroutine(fixPositionCoroutine);
            }
            
            // 감정 말풍선 숨기기
            firstPet.HideEmotion();
            secondPet.HideEmotion();
            
            // 원래 상태로 복원
            firstPetState.Restore(firstPet);
            secondPetState.Restore(secondPet);
            
            // 애니메이션 원래대로
            firstPet.GetComponent<PetAnimationController>().StopContinuousAnimation();
            secondPet.GetComponent<PetAnimationController>().StopContinuousAnimation();
            
            Debug.Log($"[Headbutt] 상호작용 종료, 펫들의 상태가 원래대로 복원됨");
        }
    }
}