// 싸우기 상호작용 구현 (개선 버전)
using System.Collections;
using UnityEngine;

public class FightInteraction : BasePetInteraction
{
    public override string InteractionName => "Fight";
    // 상호작용 유형 지정
    protected override InteractionType DetermineInteractionType()
    {
        return InteractionType.Fight;
    }
    // 이 상호작용이 가능한지 확인
    public override bool CanInteract(PetController pet1, PetController pet2)
    {
        // 토끼와 거북이는 싸우지 않고 경주함
        if ((pet1.PetType == PetType.Rabbit && pet2.PetType == PetType.Turtle) ||
            (pet1.PetType == PetType.Turtle && pet2.PetType == PetType.Rabbit))
        {
            return false;
        }

        // 사자와 호랑이는 싸움
        if ((pet1.PetType == PetType.Leopard && pet2.PetType == PetType.Tiger) ||
            (pet1.PetType == PetType.Tiger && pet2.PetType == PetType.Leopard))
        {
            return true;
        }

        // 기타 조건 추가 가능
        return false;
    }

    // 상호작용 수행
    protected override IEnumerator PerformInteraction(PetController pet1, PetController pet2)
    {
        Debug.Log($"[Fight] {pet1.petName}와(과) {pet2.petName}의 싸움 상호작용 시작");

        // 원래 상태 저장 (개선된 헬퍼 클래스 사용)
        PetOriginalState pet1State = new PetOriginalState(pet1);
        PetOriginalState pet2State = new PetOriginalState(pet2);

        try
        {

            // 감정 표현 (화난 표정)
            pet1.ShowEmotion(EmotionType.Fight,30f);
            pet2.ShowEmotion(EmotionType.Fight,30f);
            // 1. 두 펫이 서로 적당한 거리로 이동하도록 설정
            Vector3 fightSpot = FindInteractionSpot(pet1, pet2);
            Vector3 direction = (pet2.transform.position - pet1.transform.position).normalized;
            direction.y = 0; // 높이는 무시하고 수평면에서만 계산

            float fightDistance = 5f; // 싸움을 위한 적절한 거리 설정

            // 첫 번째 펫 위치 계산
            Vector3 pet1Target = fightSpot - direction * (fightDistance / 2);
            pet1Target = FindValidPositionOnNavMesh(pet1Target, 5f);

            // 두 번째 펫 위치 계산
            Vector3 pet2Target = fightSpot + direction * (fightDistance / 2);
            pet2Target = FindValidPositionOnNavMesh(pet2Target, 5f);

            // 공통 MoveToPositions 메소드 사용
            yield return StartCoroutine(MoveToPositions(pet1, pet2, pet1Target, pet2Target, 10f));

            // 2. 서로 정확하게 마주보도록 회전
            LookAtEachOther(pet1, pet2);
            Debug.Log($"[Fight] {pet1.petName}와(과) {pet2.petName}이(가) 서로 마주봄");

  
            

            // 위치 고정을 위한 변수 저장
            Vector3 pet1FixedPos = pet1.transform.position;
            Vector3 pet2FixedPos = pet2.transform.position;
            Quaternion pet1FixedRot = pet1.transform.rotation;
            Quaternion pet2FixedRot = pet2.transform.rotation;

            // 매 프레임마다 위치와 회전을 고정하는 코루틴 시작
            StartCoroutine(FixPositionDuringInteraction(pet1, pet2, pet1FixedPos, pet2FixedPos, pet1FixedRot, pet2FixedRot));

            // 잠시 대기
            yield return new WaitForSeconds(1.0f);

            // 3. 두 펫이 서로 공격 애니메이션 재생 (애니메이션 6번) - 2초간 재생, 이동 재개 안 함
            Debug.Log($"[Fight] {pet1.petName}와(과) {pet2.petName}이(가) 공격 시작");

            // 공통 PlaySimultaneousAnimations 메소드 사용
            yield return StartCoroutine(PlaySimultaneousAnimations(pet1, pet2, PetAnimationController.PetAnimationType.Attack, PetAnimationController.PetAnimationType.Damage, 2.0f));
            yield return StartCoroutine(PlaySimultaneousAnimations(pet2, pet1, PetAnimationController.PetAnimationType.Attack, PetAnimationController.PetAnimationType.Damage, 2.0f));
            yield return StartCoroutine(PlaySimultaneousAnimations(pet1, pet2, PetAnimationController.PetAnimationType.Attack, PetAnimationController.PetAnimationType.Damage, 2.0f));

            // 4. 랜덤으로 승자 결정
            PetController winner = DetermineWinner(pet1, pet2, 0.5f);
            PetController loser = winner == pet1 ? pet2 : pet1;

            // 애니메이션 재생
            yield return StartCoroutine(
                PlayWinnerLoserAnimations(winner, loser, PetAnimationController.PetAnimationType.Jump, PetAnimationController.PetAnimationType.Die)
            );

            // 5. 패자는 "죽는" 애니메이션 (애니메이션 8번)
            yield return StartCoroutine(loser.GetComponent<PetAnimationController>().PlayAnimationWithCustomDuration(PetAnimationController.PetAnimationType.Die, 2.0f, false, false));
            // 6. 원래 상태로 복귀 (애니메이션 0번)
            pet1.GetComponent<PetAnimationController>().SetContinuousAnimation(0);
            pet2.GetComponent<PetAnimationController>().SetContinuousAnimation(0);

            // 추가 대기 시간 (효과적인 연출을 위해)
            yield return new WaitForSeconds(2.0f);
        }
        finally
        {
                 // 감정 말풍선 숨기기
            pet1.HideEmotion();
            pet2.HideEmotion();
            // 마지막에 NavMeshAgent의 상태를 원래대로 복원 (예외 발생해도 실행됨)
            pet1State.Restore(pet1);
            pet2State.Restore(pet2);

            Debug.Log($"[Fight] 원래 에이전트 상태 복원 완료");
        }
    }
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
}