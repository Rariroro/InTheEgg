using System.Collections;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// 펫의 기본 움직임(Idle, Walk, Run, Jump, Rest, LookAround, Play)을
/// 성향(personality)별 가중치에 따라 결정하고 NavMesh를 이용해 이동합니다.
/// PetAnimationController를 이용해 애니메이션을 전환합니다.
/// </summary>
public class PetMovementController : MonoBehaviour
{
    // 외부 PetController 참조 (NavMeshAgent, baseSpeed, personality 등 포함)
    private PetController petController;

    // 현재 행동이 유지된 시간과 다음 행동 전환 시점
    private float behaviorTimer = 0f;
    private float nextBehaviorChange = 0f;

    // 현재 행동 상태 (초깃값: 걷기)
    private BehaviorState currentBehaviorState = BehaviorState.Walking;

    /// <summary>펫이 수행할 수 있는 행동 목록</summary>
    private enum BehaviorState
    {
        Idle,       // 가만히 서 있기
        Walking,    // 천천히 걷기
        Running,    // 빠르게 달리기
        Jumping,    // 점프
        Resting,    // 쉬기 (앉거나 누워있기)
        Looking,    // 주변 둘러보기
        Playing     // 간단한 놀이 동작
    }

    /// <summary>
    /// 성향(personality)별 행동 가중치와 지속시간, 속도 배율 값
    /// 선택 시 행동 확률과 이동 속도 등을 결정합니다.
    /// </summary>
    private class PersonalityBehavior
    {
        public float idleWeight;
        public float walkWeight;
        public float runWeight;
        public float jumpWeight;
        public float restWeight;
        public float lookWeight;
        public float playWeight;
        public float behaviorDuration;
        public float speedMultiplier;
    }

    // 현재 펫의 성향에 맞는 설정 저장용
    private PersonalityBehavior pb;

    /// <summary>
    /// 초기화: PetController 세팅, 물 영역(NavMeshArea) 비용 설정, 성향별 가중치 초기화
    /// </summary>
    public void Init(PetController controller)
    {
        petController = controller;
        // NavMesh의 "Water" 영역 비용 설정 (habitat이 물인지 여부에 따라 달리함)
        if (petController.agent != null)
        {
            int waterArea = NavMesh.GetAreaFromName("Water");
            if (petController.habitat == PetAIProperties.Habitat.Water)
                petController.agent.SetAreaCost(waterArea, 0.5f); // 물 속성 펫은 낮은 비용
            else
                petController.agent.SetAreaCost(waterArea, 10f);  // 물이 아닌 펫은 피하도록 높은 비용
        }

        // 성향별 행동 가중치, 지속시간, 속도 배율 값 초기화
        InitializePersonalityBehavior();

        // 약간 딜레이 후 첫 행동 결정
        StartCoroutine(DelayedStart());
    }

    /// <summary>
    /// 성향별 행동 가중치와 속도 설정
    /// Lazy, Shy, Brave, Playful의 경우를 나눠서 가중치를 부여합니다.
    /// </summary>
    private void InitializePersonalityBehavior()
    {
        pb = new PersonalityBehavior();
        switch (petController.personality)
        {
            case PetAIProperties.Personality.Lazy:
                pb.idleWeight = 3f; pb.walkWeight = 2f; pb.runWeight = 0.1f; pb.jumpWeight = 0.1f;
                pb.restWeight = 5f; pb.lookWeight = 2f; pb.playWeight = 0.1f;
                pb.behaviorDuration = 5f; pb.speedMultiplier = 0.7f;
                break;
            case PetAIProperties.Personality.Shy:
                pb.idleWeight = 2f; pb.walkWeight = 3f; pb.runWeight = 0.5f; pb.jumpWeight = 0.5f;
                pb.restWeight = 2f; pb.lookWeight = 4f; pb.playWeight = 0.5f;
                pb.behaviorDuration = 3f; pb.speedMultiplier = 0.8f;
                break;
            case PetAIProperties.Personality.Brave:
                pb.idleWeight = 1f; pb.walkWeight = 2f; pb.runWeight = 4f; pb.jumpWeight = 3f;
                pb.restWeight = 1f; pb.lookWeight = 1f; pb.playWeight = 2f;
                pb.behaviorDuration = 4f; pb.speedMultiplier = 1.2f;
                break;
            case PetAIProperties.Personality.Playful:
                pb.idleWeight = 0.5f; pb.walkWeight = 2f; pb.runWeight = 3f; pb.jumpWeight = 4f;
                pb.restWeight = 0.5f; pb.lookWeight = 1f; pb.playWeight = 5f;
                pb.behaviorDuration = 2f; pb.speedMultiplier = 1.1f;
                break;
        }
    }

    /// <summary>
    /// 시작 딜레이 후 행동 결정 메서드 호출
    /// </summary>
    private IEnumerator DelayedStart()
    {
        // 0.5초 후 다음 행동 결정
        yield return new WaitForSeconds(0.5f);
        DecideNextBehavior();
    }

    /// <summary>
    /// 매 프레임 호출: 행동 타이머 갱신, 행동 전환 판단, 움직임 처리
    /// </summary>
    public void UpdateMovement()
    {
        // 모여있는 상태(집합)이면 이동 로직 건너뜀
        if (petController.isGathered) return;
        // NavMeshAgent가 유효하지 않거나 NavMesh 위에 없으면 건너뜀
        if (petController.agent == null || !petController.agent.isOnNavMesh) return;

        // 행동 유지 시간 증가
        behaviorTimer += Time.deltaTime;
        // 설정된 지속 시간을 넘겼으면 다음 행동 결정
        if (behaviorTimer >= nextBehaviorChange)
            DecideNextBehavior();

        // agent가 멈춰있지 않을 때만 실제 이동 처리
        if (!petController.agent.isStopped)
        {
            switch (currentBehaviorState)
            {
                case BehaviorState.Walking:
                    HandleWalking();  // 도착 시 다음 목적지 설정
                    break;
                case BehaviorState.Running:
                    HandleRunning();  // 도착 시 다음 목적지 설정
                    break;
            }
        }

        // 모델 위치를 NavMeshAgent와 동기화 시켜 충돌이나 튕김 방지
        if (petController.petModelTransform != null)
            petController.petModelTransform.position = transform.position;
    }

    /// <summary>
    /// 다음 행동을 랜덤으로 결정하고 SetBehavior 호출
    /// </summary>
    private void DecideNextBehavior()
    {
        // 타이머 초기화
        behaviorTimer = 0f;
        // 전체 가중치 합계를 통한 확률 계산
        float total = pb.idleWeight + pb.walkWeight + pb.runWeight + pb.jumpWeight + pb.restWeight + pb.lookWeight + pb.playWeight;
        float rand = Random.Range(0f, total);
        float sum = 0f;

        sum += pb.idleWeight;
        if (rand < sum)        { SetBehavior(BehaviorState.Idle); return; }

        sum += pb.walkWeight;
        if (rand < sum)        { SetBehavior(BehaviorState.Walking); return; }

        sum += pb.runWeight;
        if (rand < sum)        { SetBehavior(BehaviorState.Running); return; }

        sum += pb.jumpWeight;
        if (rand < sum)        { SetBehavior(BehaviorState.Jumping); return; }

        sum += pb.restWeight;
        if (rand < sum)        { SetBehavior(BehaviorState.Resting); return; }

        sum += pb.lookWeight;
        if (rand < sum)        { SetBehavior(BehaviorState.Looking); return; }

        SetBehavior(BehaviorState.Playing);
    }

    /// <summary>
    /// 실제 행동 상태(state)를 세팅하고 애니메이션 및 agent 설정 수행
    /// </summary>
    private void SetBehavior(BehaviorState state)
    {
        currentBehaviorState = state;
        // 다음 행동 전환 시점(가중치 기반 지속시간 +/- 랜덤)
        nextBehaviorChange = pb.behaviorDuration + Random.Range(-1f, 1f);

        var animCtrl = petController.GetComponent<PetAnimationController>();
        // 일단 이동을 멈추고(Idle, Resting 등 처리)
        petController.agent.isStopped = true;

        switch (state)
        {
            case BehaviorState.Idle:
                animCtrl.SetContinuousAnimation(0); // idle 애니메이션
                break;

            case BehaviorState.Walking:
                petController.agent.isStopped = false; // NavMeshAgent 이동 허용
                petController.agent.speed = petController.baseSpeed * pb.speedMultiplier;
                SetRandomDestination();                    // 새로운 목표 지점 설정
                animCtrl.SetContinuousAnimation(1);      // walk 애니메이션
                break;

            case BehaviorState.Running:
                petController.agent.isStopped = false;
                petController.agent.speed = petController.baseSpeed * pb.speedMultiplier * 1.5f;
                SetRandomDestination();                    // 새로운 목표 지점 설정
                animCtrl.SetContinuousAnimation(2);      // run 애니메이션
                break;

            case BehaviorState.Jumping:
                StartCoroutine(PerformJump());             // 점프 코루틴 실행
                break;

            case BehaviorState.Resting:
                animCtrl.SetContinuousAnimation(5);      // rest 애니메이션
                break;

            case BehaviorState.Looking:
                StartCoroutine(LookAround());             // 둘러보기 코루틴 실행
                break;

            case BehaviorState.Playing:
                StartCoroutine(PerformPlay());            // 놀이 코루틴 실행
                break;
        }
    }

    /// <summary>
    /// 걷기 상태에서 목표 지점에 도달했는지 확인하고, 도달 시 SetRandomDestination 호출
    /// </summary>
    private void HandleWalking()
    {
        if (!petController.agent.pathPending && petController.agent.remainingDistance < 1f)
            SetRandomDestination();
    }

    /// <summary>
    /// 뛰기 상태에서 목표 지점에 도달했는지 확인하고, 도달 시 SetRandomDestination 호출
    /// </summary>
    private void HandleRunning()
    {
        if (!petController.agent.pathPending && petController.agent.remainingDistance < 1f)
            SetRandomDestination();
    }

    /// <summary>
    /// 점프 행동 코루틴: 잠깐 대기 후 점프 애니메이션 실행
    /// </summary>
    private IEnumerator PerformJump()
    {
        yield return new WaitForSeconds(0.2f);
        yield return StartCoroutine(
            petController.GetComponent<PetAnimationController>()
                         .PlayAnimationWithCustomDuration(3, 1f, true, false));
    }

    /// <summary>
    /// 둘러보기 행동 코루틴: 걷기 애니메이션으로 좌우 회전 수행
    /// </summary>
    private IEnumerator LookAround()
    {
        var animCtrl = petController.GetComponent<PetAnimationController>();
        animCtrl.SetContinuousAnimation(1); // 걷기 애니메이션

        for (int i = 0; i < 2; i++)
        {
            // 90도 방향으로 서서히 회전
            float t = 0f;
            var start = petController.petModelTransform.rotation;
            var end = start * Quaternion.Euler(0, 90, 0);
            while (t < 1f)
            {
                t += Time.deltaTime;
                petController.petModelTransform.rotation = Quaternion.Slerp(start, end, t);
                yield return null;
            }
            yield return new WaitForSeconds(0.5f);

            // 반대 방향으로 회전
            t = 0f;
            start = petController.petModelTransform.rotation;
            end = start * Quaternion.Euler(0, -180, 0);
            while (t < 1f)
            {
                t += Time.deltaTime;
                petController.petModelTransform.rotation = Quaternion.Slerp(start, end, t);
                yield return null;
            }
            yield return new WaitForSeconds(0.5f);
        }

        // 둘러보기 종료 후 애니메이션 정지
        animCtrl.StopContinuousAnimation();
    }

    /// <summary>
    /// 놀이 행동 코루틴: 세 가지 유형(Spin, JumpSequence, ShortRun)
    /// </summary>
    private IEnumerator PerformPlay()
    {
        var animCtrl = petController.GetComponent<PetAnimationController>();
        int type = Random.Range(0, 3);

        switch (type)
        {
            case 0:
                // 제자리 Spin
                petController.agent.isStopped = true;
                animCtrl.SetContinuousAnimation(2); // run처럼 회전용 애니메이션 재활용
                yield return new WaitForSeconds(3f);
                animCtrl.StopContinuousAnimation();
                break;

            case 1:
                // 연속 점프
                for (int i = 0; i < 3; i++)
                    yield return StartCoroutine(
                        animCtrl.PlayAnimationWithCustomDuration(3, 0.8f, true, false));
                break;

            case 2:
                // 짧은 달리기 후 정지
                petController.agent.isStopped = false;
                petController.agent.speed = petController.baseSpeed * 2f;
                animCtrl.SetContinuousAnimation(2);
                SetRandomDestination();
                yield return new WaitForSeconds(2f);
                petController.agent.isStopped = true;
                animCtrl.StopContinuousAnimation();
                yield return new WaitForSeconds(0.5f);
                break;
        }

        // 놀이 종료 후 기본 속도 및 이동 허용 상태로 복귀
        petController.agent.isStopped = false;
        petController.agent.speed = petController.baseSpeed * pb.speedMultiplier;
    }

    /// <summary>
    /// NavMesh 위에서 랜덤 목표 지점 생성 및 설정
    /// 방향 전환 시에도 걷기/뛰기 애니메이션을 유지하기 위해 호출 위치에서 애니메이션 유지
    /// </summary>
    public void SetRandomDestination()
    {
        if (petController.agent == null || !petController.agent.isOnNavMesh) return;

        int water = NavMesh.GetAreaFromName("Water");
        int mask = NavMesh.AllAreas;
        if (petController.habitat != PetAIProperties.Habitat.Water)
            mask &= ~(1 << water); // 물이 아닌 펫은 물 영역 제외

        Vector3 dir = Random.insideUnitSphere * 30f + transform.position;
        if (NavMesh.SamplePosition(dir, out var hit, 30f, mask))
        {
            petController.agent.SetDestination(hit.position);

            var animCtrl = petController.GetComponent<PetAnimationController>();
            // 현재 상태가 걷기/뛰기면, 새로운 목적지 설정 후에도 애니메이션 재보강
            if (currentBehaviorState == BehaviorState.Walking)
                animCtrl.SetContinuousAnimation(1);
            else if (currentBehaviorState == BehaviorState.Running)
                animCtrl.SetContinuousAnimation(2);
        }
    }
}
