using System.Collections;
using UnityEngine;

public class ChameleonCamouflageInteraction : BasePetInteraction
{
    public override string InteractionName => "ChameleonCamouflage";
    
    // Inspector에서 직접 할당할 수 있는 필드 추가
    [SerializeField] private Material transparentMaterial;
    
    // 기존 메서드 유지
    protected override InteractionType DetermineInteractionType()
    {
        return InteractionType.ChameleonCamouflage;
    }
    
    // 카멜레온 투명화 효과를 위한 메서드 (수정)
    private IEnumerator CamouflageEffect(PetController chameleon, float duration)
    {
        // 카멜레온 모델 참조
        if (chameleon.petModelTransform == null)
        {
            Debug.LogWarning("[ChameleonCamouflage] 카멜레온 모델을 찾을 수 없습니다!");
            yield break;
        }
        
        // 투명 마테리얼 확인
        if (transparentMaterial == null)
        {
            Debug.LogError("[ChameleonCamouflage] Transparent 마테리얼이 할당되지 않았습니다! Inspector에서 할당해주세요.");
            yield break;
        }
        
        // 카멜레온 모델의 모든 렌더러 컴포넌트 찾기
        Renderer[] renderers = chameleon.petModelTransform.GetComponentsInChildren<Renderer>();
        
        if (renderers.Length == 0)
        {
            Debug.LogWarning("[ChameleonCamouflage] 카멜레온 모델에 렌더러가 없습니다!");
            yield break;
        }
        
        // 원래 마테리얼 저장
        Material[][] originalMaterials = new Material[renderers.Length][];
        
        for (int i = 0; i < renderers.Length; i++)
        {
            // 원본 마테리얼 배열 복사
            originalMaterials[i] = renderers[i].materials;
        }
        
        // 투명화 효과 - 투명 마테리얼로 교체
        Debug.Log("[ChameleonCamouflage] 카멜레온이 투명 마테리얼로 교체됩니다.");
        
        for (int i = 0; i < renderers.Length; i++)
        {
            // 현재 사용 중인 마테리얼 수만큼 새 마테리얼 배열 생성
            Material[] newMaterials = new Material[renderers[i].materials.Length];
            for (int j = 0; j < newMaterials.Length; j++)
            {
                // 모든 마테리얼을 투명 마테리얼로 교체
                newMaterials[j] = transparentMaterial;
            }
            
            // 새 마테리얼 배열 적용
            renderers[i].materials = newMaterials;
        }
        
        // 위장 상태 유지
        yield return new WaitForSeconds(duration);
        
        // 원래 마테리얼로 복원
        for (int i = 0; i < renderers.Length; i++)
        {
            renderers[i].materials = originalMaterials[i];
        }
        
        Debug.Log("[ChameleonCamouflage] 카멜레온이 원래 마테리얼로 돌아왔습니다!");
    }
    

    
    // 이 상호작용이 가능한지 확인하는 메서드
    public override bool CanInteract(PetController pet1, PetController pet2)
    {
        // 카멜레온과 육식/잡식 동물 조합 찾기
        bool hasChameleon = pet1.PetType == PetType.Chameleon || pet2.PetType == PetType.Chameleon;
        
        // 카멜레온이 없으면 상호작용 불가
        if (!hasChameleon) return false;
        
        // 카멜레온이 아닌 상대 펫 찾기
        PetController otherPet = pet1.PetType == PetType.Chameleon ? pet2 : pet1;
        
        // 육식/잡식 여부 확인 (PetAIProperties.DietType 사용)
bool isPredator = (otherPet.diet & (PetAIProperties.DietaryFlags.Meat | PetAIProperties.DietaryFlags.Fish)) != 0;                         
        
        return hasChameleon && isPredator;
    }
    
    // 상호작용 수행
    protected override IEnumerator PerformInteraction(PetController pet1, PetController pet2)
    {
        Debug.Log($"[ChameleonCamouflage] {pet1.petName}와(과) {pet2.petName} 사이의 카멜레온 위장 상호작용 시작!");
        
        // 카멜레온과 포식자 식별
        PetController chameleon = null;
        PetController predator = null;
        
        if (pet1.PetType == PetType.Chameleon)
        {
            chameleon = pet1;
            predator = pet2;
        }
        else
        {
            chameleon = pet2;
            predator = pet1;
        }
        
        Debug.Log($"[ChameleonCamouflage] 카멜레온: {chameleon.petName}, 포식자: {predator.petName}");
        
        // 원래 상태 저장 (개선된 헬퍼 클래스 사용)
        PetOriginalState chameleonState = new PetOriginalState(chameleon);
        PetOriginalState predatorState = new PetOriginalState(predator);
        
        // 위치 고정 코루틴 참조 저장
        Coroutine fixPositionCoroutine = null;
        
        try
        {
            // 감정 표현 추가 - 카멜레온은 두려움, 포식자는 배고픔
            chameleon.ShowEmotion(EmotionType.Scared, 30f);
            predator.ShowEmotion(EmotionType.Hungry, 30f);
            
            // 1. 포식자가 카멜레온에게 접근
            float initialDistance = Vector3.Distance(predator.transform.position, chameleon.transform.position);
            Vector3 approachTarget = chameleon.transform.position;
            
            predator.agent.isStopped = false;
            predator.agent.speed = predatorState.originalSpeed * 1.5f; // 빠르게 접근
            predator.agent.acceleration = predatorState.originalAcceleration * 1.5f;
            predator.agent.SetDestination(approachTarget);
            
            // 달리기 애니메이션 설정
            predator.GetComponent<PetAnimationController>().SetContinuousAnimation(PetAnimationController.PetAnimationType.Run); // 달리기
            
            Debug.Log($"[ChameleonCamouflage] {predator.petName}이(가) {chameleon.petName}에게 접근합니다!");
            
            // 카멜레온은 긴장하며 대기
            chameleon.agent.isStopped = true;
            
            // 위험 감지 거리
            float detectionDistance = 10f;
            bool dangerDetected = false;
            
            // 포식자가 접근하는 동안 대기
            while (!dangerDetected)
            {
                float currentDistance = Vector3.Distance(predator.transform.position, chameleon.transform.position);
                
                // 위험 거리에 도달했는지 확인
                if (currentDistance <= detectionDistance)
                {
                    dangerDetected = true;
                    Debug.Log($"[ChameleonCamouflage] 카멜레온이 위험을 감지했습니다! 거리: {currentDistance}");
                }
                
                // 시간 초과 예방 (최대 10초)
                if (Time.time - Time.time > 10f)
                {
                    Debug.Log("[ChameleonCamouflage] 시간 초과! 위험 감지 처리.");
                    dangerDetected = true;
                }
                
                yield return null;
            }
            
            // 2. 카멜레온이 위장(투명화) 시작
            Debug.Log($"[ChameleonCamouflage] {chameleon.petName}이(가) 위장을 시작합니다!");
            
            // 카멜레온 감정 업데이트
            chameleon.ShowEmotion(EmotionType.Surprised, 5f);
            
            // 투명화 코루틴 시작 (별도 코루틴으로 처리)
            StartCoroutine(CamouflageEffect(chameleon, 5f));
            
            // 카멜레온이 위장하는 동안의 애니메이션 (다른 애니메이션으로 대체 가능)
            yield return StartCoroutine(chameleon.GetComponent<PetAnimationController>().PlayAnimationWithCustomDuration(PetAnimationController.PetAnimationType.Eat, 2.0f, false, false));
            
            // 3. 포식자가 멈추고 혼란스러워함
            predator.agent.isStopped = true;
            predator.GetComponent<PetAnimationController>().StopContinuousAnimation();
            
            Debug.Log($"[ChameleonCamouflage] {predator.petName}이(가) 혼란스러워합니다!");
            
            // 포식자 감정 업데이트
            predator.ShowEmotion(EmotionType.Confused, 15f);
            
            // 혼란스러운 애니메이션 (고개를 두리번거리는 등)
            yield return StartCoroutine(predator.GetComponent<PetAnimationController>().PlayAnimationWithCustomDuration(PetAnimationController.PetAnimationType.Jump, 2.0f, false, false));
            
            // 제자리에서 혼란스럽게 회전
            float confusionTime = 3.0f;
            float rotationSpeed = 60.0f;
            float elapsedTime = 0f;
            
            while (elapsedTime < confusionTime)
            {
                predator.transform.Rotate(Vector3.up, rotationSpeed * Time.deltaTime);
                elapsedTime += Time.deltaTime;
                yield return null;
            }
            
            // 4. 포식자가 다른 방향으로 이동
            Debug.Log($"[ChameleonCamouflage] {predator.petName}이(가) 목표물을 잃고 다른 방향으로 이동합니다.");
            
            // 랜덤한 다른 방향 찾기
            Vector3 escapeDirection = Random.insideUnitSphere;
            escapeDirection.y = 0; // y축 무시
            escapeDirection.Normalize();
            
            Vector3 newTarget = predator.transform.position + escapeDirection * 20f;
            newTarget = FindValidPositionOnNavMesh(newTarget, 25f);
            
            // 포식자 이동 재개
            predator.agent.isStopped = false;
            predator.agent.SetDestination(newTarget);
            predator.GetComponent<PetAnimationController>().SetContinuousAnimation(PetAnimationController.PetAnimationType.Walk); // 걷기 애니메이션
            
            // 포식자가 충분히 멀어질 때까지 대기
            float safeDistance = 15f;
            float maxWaitTime = 10f;
            float waitStartTime = Time.time;
            bool predatorFarEnough = false;
            
            while (Time.time - waitStartTime < maxWaitTime && !predatorFarEnough)
            {
                float currentDistance = Vector3.Distance(predator.transform.position, chameleon.transform.position);
                
                if (currentDistance >= safeDistance)
                {
                    predatorFarEnough = true;
                    Debug.Log($"[ChameleonCamouflage] 포식자가 충분히 멀어졌습니다. 거리: {currentDistance}");
                }
                
                yield return null;
            }
            
            // 5. 카멜레온이 원래 색으로 돌아오고 안심함
            chameleon.ShowEmotion(EmotionType.Happy, 5f);
            
            // 안심하는 애니메이션
            yield return StartCoroutine(chameleon.GetComponent<PetAnimationController>().PlayAnimationWithCustomDuration(PetAnimationController.PetAnimationType.Jump, 2.0f, false, false));
            
            Debug.Log($"[ChameleonCamouflage] {chameleon.petName}이(가) 안전함을 느끼고 안심합니다.");
            
            // 잠시 대기
            yield return new WaitForSeconds(2f);
        }
        finally
        {
            // 감정 말풍선 숨기기
            chameleon.HideEmotion();
            predator.HideEmotion();
            
            // 원래 상태로 복원
            chameleonState.Restore(chameleon);
            predatorState.Restore(predator);
            
            // 애니메이션 원래대로
            chameleon.GetComponent<PetAnimationController>().StopContinuousAnimation();
            predator.GetComponent<PetAnimationController>().StopContinuousAnimation();
            
            Debug.Log($"[ChameleonCamouflage] 상호작용 종료, 펫들의 상태가 원래대로 복원됨");
        }
    }
}