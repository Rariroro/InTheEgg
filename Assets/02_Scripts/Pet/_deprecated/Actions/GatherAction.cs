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

    // OnEnter 메서드를 아래와 같이 수정합니다.
public void OnEnter()
{
    // OnEnter 자체는 코루틴일 수 없으므로, PetController에서 코루틴을 시작하도록 합니다.
    _pet.StartCoroutine(EnterSequence());
}

private System.Collections.IEnumerator EnterSequence()
{
    Debug.Log($"{_pet.petName}: 모이기 행동 시작.");
    _hasArrived = false;
    _pet.isGathered = false; // 도착 상태 초기화

    // 나무에 올라가고 있었다면, 강제로 내려오게 합니다.
    if (_pet.isClimbingTree)
    {
        var treeClimber = _pet.GetComponent<PetTreeClimbingController>();
        treeClimber?.ForceCancelClimbing();

        // NavMeshAgent가 다시 활성화되고 안정될 시간을 벌어줍니다.
        yield return null; 
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

        if (_pet.animator) _pet.animator.SetInteger("animation", (int)PetAnimationController.PetAnimationType.Run); // 뛰기(Run) 애니메이션
    }
}


    public void OnUpdate()
    {    _pet.HandleRotation(); 

        if (_hasArrived || _agent == null || !_agent.enabled) return;

        // 목표 지점 도착 체크
        if (!_agent.pathPending && _agent.remainingDistance <= _agent.stoppingDistance)
        {
            _hasArrived = true;
            _pet.isGathered = true; // 도착 완료 플래그 설정
            _agent.isStopped = true;
            if (_pet.animator) _pet.animator.SetInteger("animation", (int)PetAnimationController.PetAnimationType.Idle); // 정지(Idle) 애니메이션

            // 카메라 바라보기 로직 시작
            _pet.StartCoroutine(LookAtCameraCoroutine());
        }
    }

    public void OnExit()
{
    Debug.Log($"{_pet.petName}: 모이기 행동 종료.");
    _pet.isGathered = false; 
    
    if (_agent != null && _agent.enabled)
    {
        _agent.speed = _pet.baseSpeed;
        _agent.angularSpeed = _pet.baseAngularSpeed;
        _agent.acceleration = _pet.baseAcceleration;
        _agent.stoppingDistance = _pet.baseStoppingDistance;
        _agent.isStopped = false;
    }

    // ★★★ 추가: 행동이 종료되면, 뛰기 애니메이션을 확실하게 중지하고 기본 상태로 돌립니다. ★★★
    _pet.GetComponent<PetAnimationController>()?.StopContinuousAnimation();

     // EnterSequence 코루틴이 실행 중일 수 있으므로 중지시킵니다.
    _pet.StopCoroutine(EnterSequence()); 
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