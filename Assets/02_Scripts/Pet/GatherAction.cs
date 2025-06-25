// GatherAction.cs

using UnityEngine;
using UnityEngine.AI;

public class GatherAction : IPetAction
{
    private PetController _pet;
    private NavMeshAgent _agent;

    private bool _hasArrived = false;

    // 모이기 행동 시 적용할 속도 배율 (중앙에서 관리)
    private const float SPEED_MULTIPLIER = 4f;
    private const float ANGULAR_SPEED_MULTIPLIER = 4f;
    private const float ACCELERATION_MULTIPLIER = 4f;
    private const float STOPPING_DISTANCE_MULTIPLIER = 3f;

    public GatherAction(PetController pet)
    {
        _pet = pet;
        _agent = pet.agent;
    }

    public float GetPriority()
    {
        // PetController의 isGathering 플래그로 우선순위를 결정합니다.
        // isInteracting 보다도 높은 최상위 우선순위
        return _pet.isGathering ? 20.0f : 0f;
    }

    public void OnEnter()
    {
        Debug.Log($"{_pet.petName}: 모이기 행동 시작.");
        _hasArrived = false;
        _pet.isGathered = false; // 도착 상태 초기화

        // 나무에 올라가고 있었다면, 강제로 내려오게 합니다.
        if (_pet.isClimbingTree)
        {
            var movementController = _pet.GetComponent<PetMovementController>();
            if (movementController != null)
            {
                movementController.ForceCancelClimbing();
            }
            // ForceCancelClimbing()이 NavMeshAgent를 재활성화하는데 시간이 걸릴 수 있으므로
            // 다음 로직이 안전하게 실행되도록 코루틴으로 처리하거나, 약간의 지연을 주는 것이 좋습니다.
            // 이 예제에서는 즉시 실행하지만, 문제가 발생하면 지연 처리를 고려해야 합니다.
        }
        
        if (_agent != null && _agent.enabled)
        {
            // ★★★ 모이기 행동의 실제 로직을 여기서 책임집니다. ★★★
            _agent.speed = _pet.baseSpeed * SPEED_MULTIPLIER;
            _agent.angularSpeed = _pet.baseAngularSpeed * ANGULAR_SPEED_MULTIPLIER;
            _agent.acceleration = _pet.baseAcceleration * ACCELERATION_MULTIPLIER;
            _agent.stoppingDistance = _pet.baseStoppingDistance * STOPPING_DISTANCE_MULTIPLIER;
            
            _agent.SetDestination(_pet.gatherTargetPosition);
            _agent.isStopped = false;

            if (_pet.animator) _pet.animator.SetInteger("animation", 2); // 뛰기(Run) 애니메이션
        }
    }

    public void OnUpdate()
    {
        if (_hasArrived || _agent == null || !_agent.enabled) return;

        // 목표 지점 도착 체크
        if (!_agent.pathPending && _agent.remainingDistance <= _agent.stoppingDistance)
        {
            _hasArrived = true;
            _pet.isGathered = true; // 도착 완료 플래그 설정
            _agent.isStopped = true;
            if (_pet.animator) _pet.animator.SetInteger("animation", 0); // 정지(Idle) 애니메이션

            // 카메라 바라보기 로직 시작
            _pet.StartCoroutine(LookAtCameraCoroutine());
        }
    }

    public void OnExit()
    {
        Debug.Log($"{_pet.petName}: 모이기 행동 종료.");
        _pet.isGathered = false; // 모이기 상태 완전 종료
        
        // 행동이 끝날 때, 펫의 속도를 원래대로 복구합니다.
        if (_agent != null && _agent.enabled)
        {
            _agent.speed = _pet.baseSpeed;
            _agent.angularSpeed = _pet.baseAngularSpeed;
            _agent.acceleration = _pet.baseAcceleration;
            _agent.stoppingDistance = _pet.baseStoppingDistance;
            
            // 다른 행동으로 바로 이어질 수 있도록 isStopped를 false로 설정
            _agent.isStopped = false;
        }

        // 카메라 바라보기 코루틴 중단
        _pet.StopCoroutine(LookAtCameraCoroutine());
    }

    private System.Collections.IEnumerator LookAtCameraCoroutine()
    {
        if (Camera.main == null) yield break;
        
        // 현재 위치를 계속 유지하도록 isStopped를 true로 유지
        if(_agent != null && _agent.enabled) _agent.isStopped = true;

        Vector3 directionToCamera = Camera.main.transform.position - _pet.transform.position;
        directionToCamera.y = 0;
        Quaternion targetRotation = Quaternion.LookRotation(directionToCamera);

        // 펫이 여전히 모이기 상태일 때만 카메라를 바라봅니다.
        while (_pet.isGathering && Quaternion.Angle(_pet.transform.rotation, targetRotation) > 1.0f)
        {
            _pet.transform.rotation = Quaternion.Slerp(_pet.transform.rotation, targetRotation, _pet.rotationSpeed * Time.deltaTime);
            yield return null;
        }
    }
}