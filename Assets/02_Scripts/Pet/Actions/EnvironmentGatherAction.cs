// EnvironmentGatherAction.cs (축하 로직이 포함된 완성 버전)

using System.Collections;
using UnityEngine;
using UnityEngine.AI;

public class EnvironmentGatherAction : IPetAction
{
    private readonly PetController _pet;
    private readonly NavMeshAgent _agent;
    private bool _isGathering; // 행동이 진행 중인지 추적

    // 환경 모임 시 적용할 속도 배율
    private const float SPEED_MULTIPLIER = 3f;
    private const float CELEBRATION_DURATION = 5f; // 축하 시간

    public EnvironmentGatherAction(PetController pet)
    {
        _pet = pet;
        _agent = pet.agent;
    }

    public float GetPriority()
    {
        // PetController의 isAttractedToEnvironment 플래그로 우선순위를 결정합니다.
        return _pet.isAttractedToEnvironment ? 15.0f : 0f;
    }

    public void OnEnter()
    {
        _isGathering = true;
        _pet.StartCoroutine(EnterSequence());
    }

    private IEnumerator EnterSequence()
    {
        // Debug.Log($"{_pet.petName}: 환경에 이끌려 모이기 시작.");

        if (_pet.isClimbingTree)
        {
            var treeClimber = _pet.GetComponent<PetTreeClimbingController>();
            if (treeClimber != null)
            {
                treeClimber.ForceCancelClimbing();
                yield return new WaitForSeconds(0.5f); // 나무에서 내려온 후 NavMeshAgent가 안정화될 시간
            }
        }
        
        if (_agent != null && _agent.enabled)
        {
            _agent.speed = _pet.baseSpeed * SPEED_MULTIPLIER;
            _agent.acceleration = _pet.baseAcceleration * SPEED_MULTIPLIER;
            _agent.SetDestination(_pet.environmentTargetPosition);
            _agent.isStopped = false;
            if (_pet.animator) _pet.animator.SetInteger("animation", 2);
        }
    }

    public void OnUpdate()
    {
        if (!_isGathering) return;

        _pet.HandleRotation();

        // 도착 체크 (거리를 넉넉하게 2.5f로 설정)
        if (_agent != null && _agent.enabled && !_agent.pathPending && _agent.remainingDistance <= 2.5f)
        {
            _isGathering = false; // 더 이상 이동 업데이트는 하지 않음
            _pet.StartCoroutine(CelebrateArrivalCoroutine());
        }
    }

    // ★★★ CelebratePet 로직을 이 Action 내부로 가져왔습니다. ★★★
    private IEnumerator CelebrateArrivalCoroutine()
    {
        // 1. 도착 후 정지 및 방향 전환
        if (_agent != null && _agent.enabled)
        {
             _agent.isStopped = true;
             // 환경의 중심을 바라보게 할 수 있습니다. (카메라 대신)
             Vector3 lookDirection = (_pet.environmentTargetPosition - _pet.transform.position).normalized;
             lookDirection.y = 0;
             if(lookDirection != Vector3.zero)
             {
                _pet.transform.rotation = Quaternion.LookRotation(lookDirection);
             }
        }

        // 2. 축하 애니메이션 및 이모티콘 표시 (기존 CelebratePet 로직)
        _pet.ShowEmotion(EmotionType.Happy, CELEBRATION_DURATION);
        float celebrationTime = 0f;

        while (celebrationTime < CELEBRATION_DURATION)
        {
            if (_pet == null) yield break;

            if (_pet.animator != null)
            {
                int randomAnimation = Random.Range(0, 2); // 2가지 행동만 사용
                switch (randomAnimation)
                {
                    case 0:
                        _pet.animator.SetInteger("animation", 3); // 점프
                        yield return new WaitForSeconds(1f);
                        break;
                    case 1:
                        _pet.animator.SetInteger("animation", 2); // 달리기 제스처
                        yield return new WaitForSeconds(0.5f);
                        if(_pet.animator != null) _pet.animator.SetInteger("animation", 0);
                        yield return new WaitForSeconds(0.3f);
                        break;
                }
            }
            celebrationTime += 1.5f;
            yield return new WaitForSeconds(0.5f);
        }

        // 3. 모든 행동 종료 후 상태 초기화
        if (_pet == null) yield break;
        
        _pet.HideEmotion();
        
        if (_pet.animator != null)
        {
            _pet.animator.SetInteger("animation", 0);
        }
        
        // 중요: 모든 행동이 끝났으므로, 펫이 다른 행동을 할 수 있도록 상태를 해제합니다.
        _pet.isAttractedToEnvironment = false;
    }

    public void OnExit()
    {
        // 다른 고순위 행동(예: 플레이어 모이기)에 의해 중단될 경우 호출됨
        _isGathering = false;
        _pet.isAttractedToEnvironment = false;

        // 펫의 이동 속도를 원래대로 복구
        if (_agent != null && _agent.enabled)
        {
            _agent.speed = _pet.baseSpeed;
            _agent.acceleration = _pet.baseAcceleration;
            _agent.isStopped = false;
        }

        _pet.StopAllCoroutines(); // 이 Action에서 실행한 모든 코루틴 중지
    }
}