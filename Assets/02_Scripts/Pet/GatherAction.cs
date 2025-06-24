// GatherAction.cs
using UnityEngine;
using UnityEngine.AI;

public class GatherAction : IPetAction
{
    private PetController _pet;
    private NavMeshAgent _agent;

    private Vector3 _targetPosition;
    private bool _hasArrived = false;

    public GatherAction(PetController pet)
    {
        _pet = pet;
        _agent = pet.agent;
    }

    public float GetPriority()
    {
        // PetController의 isGathering 플래그로 우선순위를 결정합니다.
        return _pet.isGathering ? 20.0f : 0f; // isInteracting 보다도 높은 최상위 우선순위
    }

    public void OnEnter()
    {
        // Debug.Log($"{_pet.petName}: 모이기 행동 시작.");
        _hasArrived = false;
        _pet.isGathered = false; // 도착 상태 초기화

        // PetGatheringButton에서 설정해준 목표 위치로 이동 시작
        // (이 부분은 PetGatheringButton에서 이 Action으로 로직을 옮겨와야 함)
        // PetController에 목표 위치를 저장할 변수(예: public Vector3 gatherTargetPosition)가 필요합니다.
        _targetPosition = _pet.gatherTargetPosition; // 예시 변수
        
        if (_agent != null && _agent.enabled)
        {
             // PetGatheringButton에서 이미 속도 등을 설정했으므로 그대로 사용하거나, 여기서 다시 설정할 수 있습니다.
            _agent.SetDestination(_targetPosition);
            _agent.isStopped = false;
            if (_pet.animator) _pet.animator.SetInteger("animation", 2); // Run
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
            if (_pet.animator) _pet.animator.SetInteger("animation", 0); // Idle

            // 카메라 바라보기 로직 (PetGatheringButton의 코루틴 로직을 여기로 가져옴)
            _pet.StartCoroutine(LookAtCameraCoroutine());
        }
    }

    public void OnExit()
    {
        // Debug.Log($"{_pet.petName}: 모이기 행동 종료.");
        _pet.isGathered = false; // 모이기 상태 완전 종료
        
        // PetGatheringButton의 CancelGathering에서 하던 속도 원복 로직을 여기서 처리
         if (_agent != null)
        {
            _agent.speed = _pet.baseSpeed;
            _agent.angularSpeed = _pet.baseAngularSpeed;
            _agent.acceleration = _pet.baseAcceleration;
            _agent.stoppingDistance = _pet.baseStoppingDistance;
            _agent.isStopped = false;
        }
    }

    private System.Collections.IEnumerator LookAtCameraCoroutine()
    {
        // PetGatheringButton에 있던 카메라 바라보기 로직...
        if (Camera.main == null) yield break;
        
        Vector3 directionToCamera = Camera.main.transform.position - _pet.transform.position;
        directionToCamera.y = 0;
        Quaternion targetRotation = Quaternion.LookRotation(directionToCamera);

        while (Quaternion.Angle(_pet.transform.rotation, targetRotation) > 1.0f)
        {
            // 모으기가 취소되면 코루틴 중단
            if (!_pet.isGathering) yield break;
            
            _pet.transform.rotation = Quaternion.Slerp(_pet.transform.rotation, targetRotation, _pet.rotationSpeed * Time.deltaTime);
            yield return null;
        }
    }
}