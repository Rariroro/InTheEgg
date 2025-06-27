// Pet.zip/ClimbTreeAction.cs

using UnityEngine;

public class ClimbTreeAction : IPetAction
{
    private readonly PetController _pet;
    private readonly PetTreeClimbingController _climbingController;
    private readonly PetAnimationController _animController; // ★ 애니메이션 컨트롤러 참조 추가

    public ClimbTreeAction(PetController pet, PetTreeClimbingController climbingController)
    {
        _pet = pet;
        _climbingController = climbingController;
        _animController = pet.GetComponent<PetAnimationController>(); // ★ 초기화
    }

    public float GetPriority()
    {
        // 1. 이미 나무에 오르기 시작했거나, 나무 위에 있다면 최상위 우선순위를 가짐
        if (_pet.isClimbingTree || _climbingController.IsSearchingForTree())
        {
            // ★★★ 수정: SelectedAction(5.0f)보다 높은 우선순위로 변경 ★★★
            // 이렇게 하면 펫이 나무 위에 있을 때 선택되어도 ClimbTreeAction 상태가 유지됩니다.
            return 6.0f;
        }

        // 2. 'Tree' 서식지 펫이 아니면 실행하지 않음
        if (_pet.habitat != PetAIProperties.Habitat.Tree) return 0f;

        // 3. 다른 중요한 행동(식사, 수면 등)을 하면 실행하지 않음
        if (_pet.hunger > 70f || _pet.sleepiness > 70f)
        {
            return 0f;
        }

        // 4. 설정된 확률(treeClimbChance)에 따라 우선순위를 가끔씩 높게 줌
        if (Random.value < _pet.treeClimbChance * 0.1f)
        {
            return 0.3f;
        }

        return 0f;
    }

    public void OnEnter()
    {
        // Debug.Log($"{_pet.petName}: 나무 오르기 행동 시작.");
        // ★★★ 수정: 이미 나무 위에 있는 상태에서 이 액션이 다시 시작될 수 있으므로,
        // isClimbingTree가 false일 때만 새로 나무를 찾도록 합니다.
        if (!_pet.isClimbingTree)
        {
            _pet.StartCoroutine(_climbingController.SearchAndClimbTreeRegularly());
        }
    }

    public void OnUpdate()
    {
        // ★★★ 핵심 수정: OnUpdate 로직 추가 ★★★
        // 이 액션이 활성화된 상태(즉, 나무 위에 있을 때) 펫이 선택되었다면,
        // 나무 위에서 카메라를 바라보도록 처리합니다.
        if (_pet.isClimbingTree && _pet.isSelected)
        {
            // 1. 카메라 바라보기
            if (Camera.main != null)
            {
                Vector3 directionToCamera = Camera.main.transform.position - _pet.transform.position;
                directionToCamera.y = 0; // 펫이 위아래로 기울지 않도록 Y축 고정

                if (directionToCamera != Vector3.zero)
                {
                    Quaternion targetRotation = Quaternion.LookRotation(directionToCamera);
                    // 나무 위에 있을 때는 NavMeshAgent가 비활성화되어 있으므로, transform을 직접 회전시켜도 안전합니다.
                    _pet.transform.rotation = Quaternion.Slerp(
                        _pet.transform.rotation,
                        targetRotation,
                        _pet.rotationSpeed * Time.deltaTime
                    );
                }
            }

            // 2. 애니메이션 처리
            // 나무 위에서 선택되었을 때는 '휴식' 애니메이션을 멈추고 '기본(Idle)' 자세를 취하게 합니다.
            _animController?.SetContinuousAnimation(PetAnimationController.PetAnimationType.Idle); // Idle 애니메이션
        }
    }

    public void OnExit()
    {
        // Debug.Log($"{_pet.petName}: 나무 오르기 행동 중단.");
        // 다른 고순위 행동(예: 모이기 명령)에 의해 중단될 경우,
        // 나무타기 상태를 강제로 취소합니다. (이 로직은 그대로 유지)
        _climbingController.ForceCancelClimbing();
    }
}