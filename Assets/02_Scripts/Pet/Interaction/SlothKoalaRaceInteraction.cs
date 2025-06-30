// 나무늘보-코알라 달리기 상호작용 구현 (개선 버전)
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SlothKoalaRaceInteraction : BasePetInteraction
{
    public override string InteractionName => "SlothKoalaRace";
    
    // 상호작용 유형 지정
    protected override InteractionType DetermineInteractionType()
    {
        return InteractionType.SlothKoalaRace;
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
        
        // 나무늘보와 코알라 경주
        if ((type1 == PetType.Sloth && type2 == PetType.Koala) || 
            (type1 == PetType.Koala && type2 == PetType.Sloth))
        {
            return true;
        }
        
        return false;
    }
    
    // 상호작용 수행
    protected override IEnumerator PerformInteraction(PetController pet1, PetController pet2)
    {
        Debug.Log($"[SlothKoalaRace] {pet1.petName}와(과) {pet2.petName}의 달리기 시합이 시작됩니다!");
        
        // 나무늘보와 코알라 식별
        PetController sloth = null;
        PetController koala = null;
        
        // 펫 타입에 따라 나무늘보와 코알라 식별
        if (pet1.PetType == PetType.Sloth && pet2.PetType == PetType.Koala)
        {
            sloth = pet1;
            koala = pet2;
        }
        else if (pet2.PetType == PetType.Sloth && pet1.PetType == PetType.Koala)
        {
            sloth = pet2;
            koala = pet1;
        }
        else
        {
            // 나무늘보나 코알라가 아닌 경우, 무작위로 역할 배정
            Debug.Log("[SlothKoalaRace] 나무늘보와 코알라가 아닌 펫들입니다. 역할을 임의로 배정합니다.");
            if (Random.value > 0.5f)
            {
                sloth = pet1;
                koala = pet2;
            }
            else
            {
                sloth = pet2;
                koala = pet1;
            }
        }
        
        Debug.Log($"[SlothKoalaRace] 나무늘보 역할: {sloth.petName}, 코알라 역할: {koala.petName}");
        
        // 원래 상태 저장 (개선된 헬퍼 클래스 사용)
        PetOriginalState slothState = new PetOriginalState(sloth);
        PetOriginalState koalaState = new PetOriginalState(koala);
        
        // 관중을 위한 변수들
        List<PetController> spectators = new List<PetController>();
        List<Vector3> spectatorOriginalPositions = new List<Vector3>();
        List<Quaternion> spectatorOriginalRotations = new List<Quaternion>();
        List<bool> spectatorOriginalAgentStates = new List<bool>();
        
        // 위치 고정 코루틴 참조 저장
        Coroutine fixPositionCoroutine = null;
        
        try
        {
            // 감정 표현 추가 - 둘 다 놀라는 표정
            sloth.ShowEmotion(EmotionType.Race, 30f);
            koala.ShowEmotion(EmotionType.Race, 30f);
            
            // 1. 출발 지점 준비 - 두 펫을 나란히 배치
            Vector3 startPosition = (sloth.transform.position + koala.transform.position) / 2;
            Vector3 raceDirection = Random.insideUnitSphere;
            raceDirection.y = 0;
            raceDirection.Normalize();
            
            // 공통 메소드 사용해서 출발선 위치 계산
            Vector3 slothStartPos, koalaStartPos;
            CalculateStartPositions(sloth, koala, out slothStartPos, out koalaStartPos, 3f);
            
            // NavMesh 보정
            slothStartPos = FindValidPositionOnNavMesh(slothStartPos, 5f);
            koalaStartPos = FindValidPositionOnNavMesh(koalaStartPos, 5f);
            
            // 공통 MoveToPositions 메소드 사용
            yield return StartCoroutine(MoveToPositions(sloth, koala, slothStartPos, koalaStartPos, 10f));
            
            // 2. 결승선 위치 설정 (50 유닛 거리 - 느린 경주이므로 거리 축소)
            Vector3 finishLine = startPosition + raceDirection * 50f;
            finishLine = FindValidPositionOnNavMesh(finishLine, 60f);
            
            Debug.Log($"[SlothKoalaRace] 결승선 위치: {finishLine}, 거리: {Vector3.Distance(startPosition, finishLine)}");
            
            // 3. 주변의 관중 동물들 찾기
            FindSpectators(sloth, koala, startPosition, spectators, spectatorOriginalPositions, spectatorOriginalRotations, spectatorOriginalAgentStates);
            
            // 관중들에게 감정 표현 적용
            foreach (var spectator in spectators)
            {
                spectator.ShowEmotion(EmotionType.Cheer, 60f);
            }
            
            // 4. 출발 준비 - 결승선 방향으로 회전
            Vector3 dirToFinish = (finishLine - startPosition).normalized;
            dirToFinish.y = 0; // 수평 방향만 고려
            Quaternion targetRotation = Quaternion.LookRotation(dirToFinish);
            
           // 두 펫을 결승선 방향으로 회전
            sloth.transform.rotation = targetRotation;
            koala.transform.rotation = targetRotation;
            if (sloth.petModelTransform != null)
                sloth.petModelTransform.rotation = targetRotation;
            if (koala.petModelTransform != null)
                koala.petModelTransform.rotation = targetRotation;
            
            // 위치 고정 설정 (출발 준비 중)
            Vector3 slothFixedPos = sloth.transform.position;
            Vector3 koalaFixedPos = koala.transform.position;
            Quaternion slothFixedRot = sloth.transform.rotation;
            Quaternion koalaFixedRot = koala.transform.rotation;
            
            // 위치 고정 코루틴 시작
            fixPositionCoroutine = StartCoroutine(
                FixPositionDuringInteraction(
                    sloth, koala, 
                    slothFixedPos, koalaFixedPos, 
                    slothFixedRot, koalaFixedRot
                )
            );
            
            // 관중들 위치시키기
            PositionSpectators(spectators, startPosition, finishLine);
            
            // 5. 경주 시작 연출 - 응원
            // 관중들이 응원 (점프 애니메이션)
            foreach (var spectator in spectators)
            {
                StartCoroutine(spectator.GetComponent<PetAnimationController>().PlayAnimationWithCustomDuration(PetAnimationController.PetAnimationType.Jump, 1.5f, false, false));
            }
            
            // 잠시 대기 (긴장감 조성)
            yield return new WaitForSeconds(2.0f);
            
            // 위치 고정 코루틴 중지 (경주 시작을 위해)
            if (fixPositionCoroutine != null)
            {
                StopCoroutine(fixPositionCoroutine);
                fixPositionCoroutine = null;
            }
            
            // 6. 경주 시작 세팅
            // 매우 느린 속도로 설정 (둘 다 느린 동물이므로)
            sloth.agent.speed = slothState.originalSpeed * 0.5f;
            sloth.agent.acceleration = slothState.originalAcceleration * 0.5f;
            koala.agent.speed = koalaState.originalSpeed * 0.45f; // 약간 차이
            koala.agent.acceleration = koalaState.originalAcceleration * 0.5f;
            
            // 애니메이션 컨트롤러 참조
            PetAnimationController slothAnimController = sloth.GetComponent<PetAnimationController>();
            PetAnimationController koalaAnimController = koala.GetComponent<PetAnimationController>();
            
            // 달리기 애니메이션 시작 - 두 동물 모두 걷기 애니메이션 사용 (느리므로)
            slothAnimController.SetContinuousAnimation(PetAnimationController.PetAnimationType.Walk); // 걷기 애니메이션
            koalaAnimController.SetContinuousAnimation(PetAnimationController.PetAnimationType.Walk); // 걷기 애니메이션
            
            // 결승선으로 이동 시작
            sloth.agent.isStopped = false;
            koala.agent.isStopped = false;
            sloth.agent.SetDestination(finishLine);
            koala.agent.SetDestination(finishLine);
            
            Debug.Log("[SlothKoalaRace] 경주 시작! 둘 다 느리게 출발합니다.");
            
            // 7. 경주 진행
            bool slothFinished = false;
            bool koalaFinished = false;
            
            float raceStartTime = Time.time;
            float totalRaceDistance = Vector3.Distance(startPosition, finishLine);
            float spectatorInterestTimer = 0f; // 관중의 관심도 타이머
            float interestThreshold1 = 6f; // 첫 번째 관심 감소 시점
            float interestThreshold2 = 15f; // 두 번째 관심 감소 시점
            bool spectatorInterestPhase1 = false;
            bool spectatorInterestPhase2 = false;
            
            while (!slothFinished && !koalaFinished)
            {
                // 현재 각 펫의 경주 진행 상황 계산
                float slothProgress = 0f;
                float koalaProgress = 0f;
                
                if (!sloth.agent.pathPending)
                {
                    float slothProjectedDistance = Vector3.Dot(sloth.transform.position - startPosition, dirToFinish);
                    slothProgress = Mathf.Clamp01(slothProjectedDistance / totalRaceDistance);
                }
                
                if (!koala.agent.pathPending)
                {
                    float koalaProjectedDistance = Vector3.Dot(koala.transform.position - startPosition, dirToFinish);
                    koalaProgress = Mathf.Clamp01(koalaProjectedDistance / totalRaceDistance);
                }
                
                // 관중의 관심도 관리
                spectatorInterestTimer += Time.deltaTime;
                
                // 첫 번째 관심 단계 - 점프 애니메이션에서 먹기 애니메이션으로 전환
                if (!spectatorInterestPhase1 && spectatorInterestTimer >= interestThreshold1)
                {
                    spectatorInterestPhase1 = true;
                    Debug.Log("[SlothKoalaRace] 관중들이 지루해하기 시작합니다...");
                    
                    // 감정 표현 업데이트 - 관중들이 지루해짐
                    foreach (var spectator in spectators)
                    {
                        spectator.ShowEmotion(EmotionType.Sleepy, 60f);
                        StartCoroutine(spectator.GetComponent<PetAnimationController>().PlayAnimationWithCustomDuration(PetAnimationController.PetAnimationType.Eat, 3.0f, false, false));
                    }
                }
                
                // 두 번째 관심 단계 - 먹기 애니메이션에서 쉬기 애니메이션으로 전환
                if (!spectatorInterestPhase2 && spectatorInterestTimer >= interestThreshold2)
                {
                    spectatorInterestPhase2 = true;
                    Debug.Log("[SlothKoalaRace] 관중들이 완전히 지루해합니다...");
                    
                    // 감정 표현 업데이트 - 관중들이 잠을 참을 수 없음
                    foreach (var spectator in spectators)
                    {
                        spectator.ShowEmotion(EmotionType.Sleepy, 60f);
                        StartCoroutine(spectator.GetComponent<PetAnimationController>().PlayAnimationWithCustomDuration(PetAnimationController.PetAnimationType.Rest, 10.0f, false, false));
                    }
                }
                
                // 랜덤으로 속도 미세 조정 (매우 작은 차이로 경쟁하도록)
                if (Random.value < 0.03f) // 3% 확률로 속도 조정
                {
                    if (Random.value > 0.5f)
                    {
                        sloth.agent.speed = slothState.originalSpeed * Random.Range(0.48f, 0.52f);
                    }
                    else
                    {
                        koala.agent.speed = koalaState.originalSpeed * Random.Range(0.43f, 0.47f);
                    }
                }
                
                // 나무늘보가 결승선에 도착했는지 확인
                if (!slothFinished && !sloth.agent.pathPending && sloth.agent.remainingDistance < 0.5f)
                {
                    slothFinished = true;
                    Debug.Log($"[SlothKoalaRace] 나무늘보({sloth.petName})가 결승선에 도착했습니다!");
                    
                    // // 감정 표현 업데이트 - 승리 표정
                    // sloth.ShowEmotion(EmotionType.Victory, 15f);
                }
                
                // 코알라가 결승선에 도착했는지 확인
                if (!koalaFinished && !koala.agent.pathPending && koala.agent.remainingDistance < 0.5f)
                {
                    koalaFinished = true;
                    Debug.Log($"[SlothKoalaRace] 코알라({koala.petName})가 결승선에 도착했습니다!");
                    
                    // // 감정 표현 업데이트 - 승리 표정
                    // koala.ShowEmotion(EmotionType.Victory, 15f);
                }
                
                // 경주 진행 상황 로깅 (디버깅용)
                if (Time.frameCount % 120 == 0) // 약 2초마다 로깅
                {
                    Debug.Log($"[SlothKoalaRace] 진행 상황 - 나무늘보: {slothProgress:P0}, 코알라: {koalaProgress:P0}");
                }
                
                // 경주 시간이 너무 길어지면 랜덤으로 승자 결정 (최대 1분)
                if (Time.time - raceStartTime > 60f)
                {
                    Debug.LogWarning("[SlothKoalaRace] 경주 시간이 너무 길어져 랜덤으로 승자를 결정합니다.");
                    
                    // 랜덤으로 승자 결정
                    if (Random.value > 0.5f)
                    {
                        slothFinished = true;
                        koalaFinished = false;
                        // 나무늘보를 결승선으로 이동
                        sloth.transform.position = finishLine;
                    }
                    else
                    {
                        koalaFinished = true;
                        slothFinished = false;
                        // 코알라를 결승선으로 이동
                        koala.transform.position = finishLine;
                    }
                    break;
                }
                
                yield return null;
            }
            
            // 8. 승자 결정 및 축하
            PetController winner = slothFinished && !koalaFinished ? sloth : koala;
            PetController loser = winner == sloth ? koala : sloth;
            
            Debug.Log($"[SlothKoalaRace] 경주 결과: {winner.petName}의 승리!");
            
            // // 승자 감정 표현 (승리)
            // winner.ShowEmotion(EmotionType.Victory, 10f);
            
            // // 패자 감정 표현 (패배)
            // loser.ShowEmotion(EmotionType.Defeat, 10f);
            
            // 승자 축하 (점프 애니메이션)
            yield return StartCoroutine(winner.GetComponent<PetAnimationController>().PlayAnimationWithCustomDuration(PetAnimationController.PetAnimationType.Jump, 2.0f, false, false));
            
            // 패자 실망 (앉기 애니메이션)
            yield return StartCoroutine(loser.GetComponent<PetAnimationController>().PlayAnimationWithCustomDuration(PetAnimationController.PetAnimationType.Eat, 2.0f, false, false));
            
            // 관중 반응 (아직 잠들지 않은 관중만)
            if (spectatorInterestTimer < interestThreshold2)
            {
                foreach (var spectator in spectators)
                {
                    // 50% 확률로 점프 애니메이션 (축하)
                    if (Random.value > 0.5f)
                    {
                        spectator.ShowEmotion(EmotionType.Happy, 5f);
                        StartCoroutine(spectator.GetComponent<PetAnimationController>().PlayAnimationWithCustomDuration(PetAnimationController.PetAnimationType.Jump, 1.5f, false, false));
                    }
                }
            }
            
            // 잠시 대기
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
            sloth.HideEmotion();
            koala.HideEmotion();
            
            // 관중들 감정 말풍선 숨기기
            foreach (var spectator in spectators)
            {
                spectator.HideEmotion();
            }
            
            // 원래 상태로 복원
            slothState.Restore(sloth);
            koalaState.Restore(koala);
            
            // 애니메이션 원래대로
            sloth.GetComponent<PetAnimationController>().StopContinuousAnimation();
            koala.GetComponent<PetAnimationController>().StopContinuousAnimation();
            
            // 관중들 원래 상태로 복원
            RestoreSpectators(spectators, spectatorOriginalPositions, spectatorOriginalRotations, spectatorOriginalAgentStates);
        }
    }
    
    // 주변 관중을 찾는 함수
    private void FindSpectators(PetController pet1, PetController pet2, Vector3 centerPosition,
                               List<PetController> spectators, List<Vector3> originalPositions,
                               List<Quaternion> originalRotations, List<bool> originalAgentStates)
    {
        Debug.Log("[SlothKoalaRace] 관중 펫을 찾습니다.");
        
        // 모든 펫 컨트롤러 찾기
        PetController[] allPets = GameObject.FindObjectsOfType<PetController>();
        
        // 참가자 두 마리를 제외한 펫들 중에서 선택
        foreach (var pet in allPets)
        {
            // 경주 참가자가 아니고, 다른 상호작용 중이 아니며, 중심 위치에서 일정 거리 내에 있는 경우
            if (pet != pet1 && pet != pet2 && !pet.isInteracting &&
                Vector3.Distance(pet.transform.position, centerPosition) < 20f)
            {
                // 최대 2마리만 선택
                if (spectators.Count < 2)
                {
                    spectators.Add(pet);
                    originalPositions.Add(pet.transform.position);
                    originalRotations.Add(pet.transform.rotation);
                    originalAgentStates.Add(pet.agent.isStopped);
                    
                    // 관중 펫 이동 중지
                    pet.agent.isStopped = true;
                    pet.isInteracting = true; // 다른 상호작용 방지
                    
                    Debug.Log($"[SlothKoalaRace] 관중으로 {pet.petName}을(를) 선택했습니다.");
                }
                else
                {
                    break;
                }
            }
        }
        
        // 관중이 없는 경우 로그 출력
        if (spectators.Count == 0)
        {
            Debug.Log("[SlothKoalaRace] 주변에 관중으로 적합한 펫이 없습니다.");
        }
    }
    
    // 관중을 관람 위치에 배치하는 함수
    private void PositionSpectators(List<PetController> spectators, Vector3 startPosition, Vector3 finishLine)
    {
        if (spectators.Count == 0) return;
        
        // 경주 방향 계산
        Vector3 raceDirection = (finishLine - startPosition).normalized;
        raceDirection.y = 0;
        
        // 수직 방향 계산 (경주 트랙 옆쪽으로 배치하기 위함)
        Vector3 perpDirection = Vector3.Cross(Vector3.up, raceDirection).normalized;
        
        // 경주 중간 지점
        Vector3 midPoint = (startPosition + finishLine) / 2;
        
        // 각 관중의 위치 설정
        for (int i = 0; i < spectators.Count; i++)
        {
            // 중간 지점에서 트랙 옆으로 배치, 첫 번째와 두 번째 관중은 서로 반대편에 배치
            float sideOffset = 5f; // 트랙에서 떨어진 거리
            float posOffset = (i == 0) ? -5f : 5f; // 중간 지점 기준 앞뒤 위치 조정
            
            // 위치 계산
            Vector3 spectatorPos = midPoint + (perpDirection * sideOffset * (i == 0 ? 1 : -1)) + (raceDirection * posOffset);
            
            // NavMesh 보정
            spectatorPos = FindValidPositionOnNavMesh(spectatorPos, 10f);
            
            // 관중 이동 및 회전 (경주를 바라보도록)
            spectators[i].transform.position = spectatorPos;
            spectators[i].transform.rotation = Quaternion.LookRotation(-perpDirection * (i == 0 ? 1 : -1));
            
            // petModelTransform도 회전
            if (spectators[i].petModelTransform != null)
            {
                spectators[i].petModelTransform.rotation = spectators[i].transform.rotation;
            }
            
            Debug.Log($"[SlothKoalaRace] 관중 {spectators[i].petName}을(를) 위치시켰습니다: {spectatorPos}");
        }
    }
    
    // 관중을 원래 상태로 복원하는 함수
    private void RestoreSpectators(List<PetController> spectators, List<Vector3> originalPositions,
                                  List<Quaternion> originalRotations, List<bool> originalAgentStates)
    {
        for (int i = 0; i < spectators.Count; i++)
        {
            // 애니메이션 초기화
            spectators[i].GetComponent<PetAnimationController>().StopContinuousAnimation();
            
            // 원래 위치로 복원하지 않고 현재 위치에서 움직임 재개
            // spectators[i].transform.position = originalPositions[i];
            // spectators[i].transform.rotation = originalRotations[i];
            
            // NavMeshAgent 상태 복원
            spectators[i].agent.isStopped = originalAgentStates[i];
            
            // 상호작용 상태 복원
            spectators[i].isInteracting = false;
            
            Debug.Log($"[SlothKoalaRace] 관중 {spectators[i].petName}의 상태를 복원했습니다.");
        }
    }
}