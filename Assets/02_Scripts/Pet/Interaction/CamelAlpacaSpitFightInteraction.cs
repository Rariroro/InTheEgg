// 낙타-알파카 침뱉기 싸움 상호작용 구현
using System.Collections;
using UnityEngine;

public class CamelAlpacaSpitFightInteraction : BasePetInteraction
{
    public override string InteractionName => "CamelAlpacaSpitFight";
    
    // 상호작용 유형 지정
    protected override InteractionType DetermineInteractionType()
    {
        // 기존 Fight 유형 활용
        return InteractionType.Fight;
    }
    
    // 이 상호작용이 가능한지 확인
    public override bool CanInteract(PetController pet1, PetController pet2)
    {
        PetType type1 = pet1.PetType;
        PetType type2 = pet2.PetType;
        
        // 낙타와 알파카 조합인지 확인
        return (type1 == PetType.Camel && type2 == PetType.Alpaca) || 
               (type1 == PetType.Alpaca && type2 == PetType.Camel);
    }
    
    // 위치와 회전을 고정하는 코루틴
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
    
    // 침 뱉기 효과 생성
    private IEnumerator CreateSpitEffect(Vector3 startPos, Vector3 endPos, float duration)
    {
        // 침 효과 표현 (게임 오브젝트 생성)
        GameObject spitObj = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        spitObj.transform.localScale = new Vector3(0.3f, 0.3f, 0.3f);
        
        // 물방울 느낌의 재질 설정
        Renderer renderer = spitObj.GetComponent<Renderer>();
        renderer.material.color = new Color(0.8f, 0.8f, 1.0f, 0.7f);
        
        // 콜라이더 제거 (물리 충돌 방지)
        Collider collider = spitObj.GetComponent<Collider>();
        if (collider != null)
        {
            Destroy(collider);
        }
        
        // 시작 위치 설정 (동물의 머리 앞쪽)
        spitObj.transform.position = startPos;
        
        // 침이 날아가는 애니메이션
        float elapsedTime = 0;
        Vector3 initialPosition = startPos;
        
        while (elapsedTime < duration)
        {
            // 포물선 경로 계산
            float normalizedTime = elapsedTime / duration;
            Vector3 currentPos = Vector3.Lerp(initialPosition, endPos, normalizedTime);
            
            // 중력 효과를 위한 포물선 궤적 추가
            float height = 2.0f * Mathf.Sin(normalizedTime * Mathf.PI);
            currentPos.y += height;
            
            spitObj.transform.position = currentPos;
            
            elapsedTime += Time.deltaTime;
            yield return null;
        }
        
        // 침이 목표에 도달하면 튀는 효과
        for (int i = 0; i < 5; i++)
        {
            GameObject splashParticle = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            splashParticle.transform.localScale = new Vector3(0.1f, 0.1f, 0.1f);
            splashParticle.transform.position = endPos;
            
            // 물방울 입자 재질 설정
            Renderer splashRenderer = splashParticle.GetComponent<Renderer>();
            splashRenderer.material.color = new Color(0.8f, 0.8f, 1.0f, 0.5f);
            
            // 콜라이더 제거
            Collider splashCollider = splashParticle.GetComponent<Collider>();
            if (splashCollider != null)
            {
                Destroy(splashCollider);
            }
            
            // 랜덤 방향으로 튀는 효과
            Vector3 randomDir = new Vector3(
                Random.Range(-1f, 1f),
                Random.Range(0.5f, 1.5f),
                Random.Range(-1f, 1f)
            ).normalized;
            
            // 입자 이동 애니메이션
            StartCoroutine(MoveAndFadeParticle(splashParticle, endPos, randomDir, 0.5f));
        }
        
        // 메인 침 오브젝트 제거
        Destroy(spitObj);
        
        yield return null;
    }
    
    // 입자 이동 및 페이드 애니메이션
    private IEnumerator MoveAndFadeParticle(GameObject particle, Vector3 startPos, Vector3 direction, float duration)
    {
        Renderer renderer = particle.GetComponent<Renderer>();
        float elapsedTime = 0;
        
        while (elapsedTime < duration)
        {
            float normalizedTime = elapsedTime / duration;
            
            // 이동
            particle.transform.position = startPos + direction * normalizedTime * 1.0f;
            
            // 크기 감소
            particle.transform.localScale = Vector3.Lerp(
                new Vector3(0.1f, 0.1f, 0.1f),
                Vector3.zero,
                normalizedTime
            );
            
            // 투명도 감소
            Color color = renderer.material.color;
            color.a = Mathf.Lerp(0.5f, 0f, normalizedTime);
            renderer.material.color = color;
            
            elapsedTime += Time.deltaTime;
            yield return null;
        }
        
        // 입자 제거
        Destroy(particle);
    }
    
    // 상호작용 수행
    public override IEnumerator PerformInteraction(PetController pet1, PetController pet2)
    {
        Debug.Log($"[CamelAlpacaSpitFight] {pet1.petName}와(과) {pet2.petName} 사이의 침뱉기 싸움 시작!");
        
        // 낙타와 알파카 식별
        PetController camel = null;
        PetController alpaca = null;
        
        if (pet1.PetType == PetType.Camel && pet2.PetType == PetType.Alpaca)
        {
            camel = pet1;
            alpaca = pet2;
        }
        else
        {
            camel = pet2;
            alpaca = pet1;
        }
        
        Debug.Log($"[CamelAlpacaSpitFight] 낙타: {camel.petName}, 알파카: {alpaca.petName}");
        
        // 원래 상태 저장
        PetOriginalState camelState = new PetOriginalState(camel);
        PetOriginalState alpacaState = new PetOriginalState(alpaca);
        
        // 위치 고정 코루틴 참조 저장
        Coroutine fixPositionCoroutine = null;
        
        try
        {
            // 감정 표현 추가 - 화난 표정
            camel.ShowEmotion(EmotionType.Angry, 30f);
            alpaca.ShowEmotion(EmotionType.Angry, 30f);
            
            // 1. 두 동물이 서로 적당한 거리로 이동하도록 설정
            Vector3 fightSpot = FindInteractionSpot(camel, alpaca);
            Vector3 direction = (alpaca.transform.position - camel.transform.position).normalized;
            direction.y = 0; // 높이는 무시하고 수평면에서만 계산
            
            float fightDistance = 6f; // 침 뱉기 싸움을 위한 적절한 거리 설정
            
            // 낙타 위치 계산
            Vector3 camelTarget = fightSpot - direction * (fightDistance / 2);
            camelTarget = FindValidPositionOnNavMesh(camelTarget, 5f);
            
            // 알파카 위치 계산
            Vector3 alpacaTarget = fightSpot + direction * (fightDistance / 2);
            alpacaTarget = FindValidPositionOnNavMesh(alpacaTarget, 5f);
            
            // 공통 MoveToPositions 메소드 사용
            yield return StartCoroutine(MoveToPositions(camel, alpaca, camelTarget, alpacaTarget, 10f));
            
            // 2. 서로 정확하게 마주보도록 회전
            LookAtEachOther(camel, alpaca);
            Debug.Log($"[CamelAlpacaSpitFight] {camel.petName}와(과) {alpaca.petName}이(가) 서로 마주봄");
            
            // 위치 고정을 위한 변수 저장
            Vector3 camelFixedPos = camel.transform.position;
            Vector3 alpacaFixedPos = alpaca.transform.position;
            Quaternion camelFixedRot = camel.transform.rotation;
            Quaternion alpacaFixedRot = alpaca.transform.rotation;
            
            // 매 프레임마다 위치와 회전을 고정하는 코루틴 시작
            fixPositionCoroutine = StartCoroutine(
                FixPositionDuringInteraction(
                    camel, alpaca, 
                    camelFixedPos, alpacaFixedPos, 
                    camelFixedRot, alpacaFixedRot
                )
            );
            
            // 잠시 대기 - 긴장감 조성
            yield return new WaitForSeconds(1.0f);
            
            // 3. 낙타가 먼저 침 뱉기 공격
            Debug.Log($"[CamelAlpacaSpitFight] {camel.petName}이(가) 침을 뱉기 시작합니다!");
            
            // 낙타 침 뱉기 준비 애니메이션 (머리를 뒤로 젖히는 모션)
            yield return StartCoroutine(camel.GetComponent<PetAnimationController>().PlayAnimationWithCustomDuration(PetAnimationController.PetAnimationType.Attack, 1.0f, false, false));
            
            // 침 뱉기 위치 계산 (머리 앞쪽)
            Vector3 camelSpitStart = camel.transform.position + camel.transform.forward * 1.0f + Vector3.up * 2.0f;
            Vector3 alpacaHitPosition = alpaca.transform.position + Vector3.up * 2.0f; // 알파카 머리 위치 (알파카는 키가 큼)
            
            // 침 뱉기 효과 생성
            StartCoroutine(CreateSpitEffect(camelSpitStart, alpacaHitPosition, 0.8f));
            
            // 알파카가 맞는 애니메이션 (약간 지연 후)
            yield return new WaitForSeconds(0.8f);
            yield return StartCoroutine(alpaca.GetComponent<PetAnimationController>().PlayAnimationWithCustomDuration(PetAnimationController.PetAnimationType.Damage, 1.0f, false, false));
            
            // 알파카 반응 - 화남
            alpaca.ShowEmotion(EmotionType.Angry, 5f);
            yield return new WaitForSeconds(0.5f);
            
            // 4. 알파카의 반격 - 알파카도 침 뱉기
            Debug.Log($"[CamelAlpacaSpitFight] {alpaca.petName}이(가) 반격으로 침을 뱉습니다!");
            
            // 알파카 침 뱉기 준비 애니메이션
            yield return StartCoroutine(alpaca.GetComponent<PetAnimationController>().PlayAnimationWithCustomDuration(PetAnimationController.PetAnimationType.Attack, 1.0f, false, false));
            
            // 알파카 침 뱉기 위치 계산
            Vector3 alpacaSpitStart = alpaca.transform.position + alpaca.transform.forward * 0.8f + Vector3.up * 2.0f;
            Vector3 camelHitPosition = camel.transform.position + Vector3.up * 2.5f; // 낙타 머리 위치
            
            // 침 뱉기 효과 생성
            StartCoroutine(CreateSpitEffect(alpacaSpitStart, camelHitPosition, 0.8f));
            
            // 낙타가 맞는 애니메이션
            yield return new WaitForSeconds(0.8f);
            yield return StartCoroutine(camel.GetComponent<PetAnimationController>().PlayAnimationWithCustomDuration(PetAnimationController.PetAnimationType.Damage, 1.0f, false, false));
            
            // 낙타 반응 - 더 화남
            camel.ShowEmotion(EmotionType.Angry, 5f);
            yield return new WaitForSeconds(0.5f);
            
            // 5. 결투 진행 - 여러 번 침 뱉기
            int spitRounds = 3; // 추가 침 뱉기 라운드 수
            
            for (int round = 0; round < spitRounds; round++)
            {
                // 침 뱉는 주체 결정 (번갈아가며)
                PetController spitter = round % 2 == 0 ? camel : alpaca;
                PetController target = spitter == camel ? alpaca : camel;
                
                Debug.Log($"[CamelAlpacaSpitFight] 라운드 {round+1}: {spitter.petName}이(가) 침을 뱉습니다!");
                
                // 시작 및 타겟 위치 계산
                Vector3 spitStart = spitter.transform.position + 
                                   spitter.transform.forward * 0.8f + 
                                   Vector3.up * 2.0f;
                
                Vector3 targetPos = target.transform.position + 
                                   Vector3.up * (target == camel ? 2.5f : 2.0f);
                
                // 침 뱉기 애니메이션
                yield return StartCoroutine(spitter.GetComponent<PetAnimationController>().PlayAnimationWithCustomDuration(PetAnimationController.PetAnimationType.Attack, 0.8f, false, false));
                
                // 침 효과 생성
                StartCoroutine(CreateSpitEffect(spitStart, targetPos, 0.8f));
                
                // 맞는 애니메이션
                yield return new WaitForSeconds(0.8f);
                
                // 50% 확률로 회피 성공
                bool dodged = Random.value > 0.5f;
                
                if (dodged)
                {
                    // 회피 성공
                    Debug.Log($"[CamelAlpacaSpitFight] {target.petName}이(가) 침을 피했습니다!");
                    yield return StartCoroutine(target.GetComponent<PetAnimationController>().PlayAnimationWithCustomDuration(PetAnimationController.PetAnimationType.Jump, 1.0f, false, false));
                    target.ShowEmotion(EmotionType.Happy, 3f);
                    spitter.ShowEmotion(EmotionType.Surprised, 3f);
                }
                else
                {
                    // 맞음
                    yield return StartCoroutine(target.GetComponent<PetAnimationController>().PlayAnimationWithCustomDuration(PetAnimationController.PetAnimationType.Damage, 1.0f, false, false));
                    target.ShowEmotion(EmotionType.Angry, 3f);
                }
                
                yield return new WaitForSeconds(1.0f);
            }
            
            // 6. 최종 결정전 - 두 동물이 동시에 침 뱉기
            Debug.Log($"[CamelAlpacaSpitFight] 최종 결정전! 두 동물이 동시에 침을 뱉습니다!");
            
            // 두 동물 모두 공격 준비 애니메이션
            yield return StartCoroutine(PlaySimultaneousAnimations(camel, alpaca, PetAnimationController.PetAnimationType.Attack, PetAnimationController.PetAnimationType.Attack, 1.0f));
            
            // 동시에 침 뱉기 효과
            Vector3 finalCamelSpitStart = camel.transform.position + camel.transform.forward * 1.0f + Vector3.up * 2.0f;
            Vector3 finalAlpacaSpitStart = alpaca.transform.position + alpaca.transform.forward * 0.8f + Vector3.up * 2.0f;
            
            StartCoroutine(CreateSpitEffect(finalCamelSpitStart, alpacaHitPosition, 0.8f));
            StartCoroutine(CreateSpitEffect(finalAlpacaSpitStart, camelHitPosition, 0.8f));
            
            // 두 동물 모두 맞는 애니메이션
            yield return new WaitForSeconds(0.8f);
            yield return StartCoroutine(PlaySimultaneousAnimations(camel, alpaca, PetAnimationController.PetAnimationType.Damage, PetAnimationController.PetAnimationType.Damage, 1.0f));
            
            // 7. 승자 결정 (랜덤)
            PetController winner = Random.value > 0.5f ? camel : alpaca;
            PetController loser = winner == camel ? alpaca : camel;
            
            Debug.Log($"[CamelAlpacaSpitFight] 침 뱉기 대결 승자: {winner.petName}");
            
            // 8. 승자와 패자 애니메이션
            winner.ShowEmotion(EmotionType.Happy, 5f);
            loser.ShowEmotion(EmotionType.Angry, 5f);
            
            // 승자는 기뻐하는 애니메이션
            yield return StartCoroutine(winner.GetComponent<PetAnimationController>().PlayAnimationWithCustomDuration(PetAnimationController.PetAnimationType.Jump, 2.0f, false, false));
            
            // 패자는 실망하는 애니메이션
            yield return StartCoroutine(loser.GetComponent<PetAnimationController>().PlayAnimationWithCustomDuration(PetAnimationController.PetAnimationType.Eat, 2.0f, false, false));
            
            // 9. 상호작용 마무리
            yield return new WaitForSeconds(1.0f);
        }
        finally
        {
            // 위치 고정 코루틴 중지
            if (fixPositionCoroutine != null)
            {
                StopCoroutine(fixPositionCoroutine);
            }
            
            // 감정 말풍선 숨기기
            camel.HideEmotion();
            alpaca.HideEmotion();
            
            // 원래 상태로 복원
            camelState.Restore(camel);
            alpacaState.Restore(alpaca);
            
            // 애니메이션 원래대로
            camel.GetComponent<PetAnimationController>().StopContinuousAnimation();
            alpaca.GetComponent<PetAnimationController>().StopContinuousAnimation();
            
            Debug.Log($"[CamelAlpacaSpitFight] 상호작용 종료, 펫들의 상태가 원래대로 복원됨");
        }
    }
}