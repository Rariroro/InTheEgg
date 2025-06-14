using System.Collections;
using UnityEngine;
using UnityEngine.AI;

// PetInteractionController 클래스는 펫과의 상호작용(터치, 드래그, 홀드 등)을 처리하는 클래스입니다.
public class PetInteractionController : MonoBehaviour
{
    // 펫 컨트롤러, 이름 텍스트, 이름 텍스트 오브젝트에 대한 참조를 저장합니다.
    private PetController petController;
    private TextMesh nameText;
    private GameObject nameTextObject;

    // 펫 선택 관련 변수
    private float selectionTimer = 0f;   // 펫 선택 후 경과 시간을 측정하는 타이머
    private bool isSelected = false;     // 펫이 현재 선택되었는지 여부

    // 터치 횟수 관련 변수
    private int touchCount = 0;         // 플레이어가 펫을 터치한 횟수
    private float lastTouchTime = 0f;    // 마지막 터치 시간
    private float touchResetTime = 5f;  // 터치 횟수를 초기화하기 위한 시간 간격
    private int maxTouchCount = 10;    // 특별한 애니메이션을 트리거하기 위한 최대 터치 횟수

    // 펫 들기 관련 변수
    private bool isHolding = false;       // 펫을 현재 들고 있는지 여부
    private float holdTimer = 0f;        // 펫을 길게 누르고 있는 시간을 측정하는 타이머
    private float holdThreshold = 0.5f;  // 펫을 들기 위한 최소 홀드 시간 (길게 누르기 인식 시간)
    private float holdHeight = 6f;       // 펫을 들었을 때 지면으로부터의 높이
    private Vector3 initialTouchPosition; // 초기 터치 위치
    private Vector3 lastTouchPosition;    //마지막 터치 위치
    private float edgeScrollThreshold = 200f;  // 화면 가장자리 스크롤을 위한 임계값
    private float edgeScrollSpeed = 10f;       // 화면 가장자리 스크롤 속도
    private Vector3 targetPosition;      // 펫을 놓을 목표 위치
    private float dropLerpSpeed = 5f;   // 펫을 내려놓을 때 부드러운 이동을 위한 보간 속도

    private int terrainLayer;             // Terrain 레이어 마스크
    private bool isTouchingPet = false;   // 현재 펫을 터치하고 있는지 여부

    // PetController 초기화 함수.
    public void Init(PetController controller)
    {
        petController = controller;
        CreateNameText(); // 펫 이름 텍스트 생성
        terrainLayer = LayerMask.GetMask("Terrain"); // "Terrain" 레이어의 마스크를 가져옵니다.
    }

    // 펫 이름 텍스트를 생성하는 함수입니다.
    private void CreateNameText()
    {
        // 펫의 petModelTransform이 null이 아닌 경우에만 이름 텍스트를 생성합니다.
        if (petController.petModelTransform != null)
        {
            // "NameText"라는 이름의 새 GameObject를 생성합니다.
            nameTextObject = new GameObject("NameText");
            // 펫 오브젝트를 부모로 설정하고, 로컬 위치와 회전을 설정합니다.
            nameTextObject.transform.SetParent(petController.transform);
            nameTextObject.transform.localPosition = Vector3.up * 3f; // 펫 위쪽으로 3 유닛 위에 위치
            nameTextObject.transform.localRotation = Quaternion.identity;

            // TextMesh 컴포넌트를 추가하고 설정을 합니다.
            nameText = nameTextObject.AddComponent<TextMesh>();
            nameText.text = petController.petName; // 텍스트는 펫의 이름
            nameText.fontSize = 20;             // 글꼴 크기
            nameText.alignment = TextAlignment.Center; // 텍스트 정렬
            nameText.anchor = TextAnchor.LowerCenter;    // 텍스트 앵커
            nameText.color = Color.white;              // 텍스트 색상

            // Billboard 컴포넌트를 추가합니다. (카메라를 항상 바라보도록)
            var billboard = nameTextObject.AddComponent<Billboard>();
            billboard.mainTransform = petController.transform;

            // 초기에는 이름 텍스트를 비활성화합니다.
            nameTextObject.SetActive(false);
        }
    }

    // 사용자 입력을 처리하는 함수입니다.
    public void HandleInput()
    {
        // 모이기 중이거나 이미 모였을 때는 일반적인 상호작용을 제한
        if (petController.isGathering || petController.isGathered)
        {
            // 펫을 들고 있었다면 강제로 놓기
            if (isHolding)
            {
                ForceStopHolding();
            }

            // 선택되어 있었다면 선택 해제
            if (isSelected)
            {
                Deselect();
            }

            return; // 모이기 중에는 새로운 상호작용 입력을 받지 않음
        }

        // 터치 카운트 리셋 (마지막 터치 이후 일정 시간이 지나면 터치 카운트 초기화)
        if (Time.time - lastTouchTime > touchResetTime)
            touchCount = 0;

        // 마우스 왼쪽 버튼을 눌렀을 때
        if (Input.GetMouseButtonDown(0))
        {
            // 초기 터치 위치와 마지막 터치 위치를 기록합니다.
            initialTouchPosition = Input.mousePosition;
            lastTouchPosition = initialTouchPosition;

            // 화면에서 마우스 위치로 레이를 발사합니다.
            Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
            RaycastHit hit;

            // 레이와 충돌하는 오브젝트가 있는지 확인합니다.
            if (Physics.Raycast(ray, out hit, Mathf.Infinity))
            {
                // 충돌한 오브젝트가 펫인 경우
                if (hit.collider.gameObject == petController.gameObject)
                {
                    isTouchingPet = true; // 펫 터치 상태를 true로 설정
                    holdTimer = 0f;        // 홀드 타이머 초기화
                }
                // 펫이 선택된 상태에서 다른 곳을 터치하면 선택 해제
                else if (isSelected)
                {
                    Deselect();
                }
            }
        }
        // 마우스 왼쪽 버튼을 누르고 있고, 펫을 터치하고 있는 경우
        else if (Input.GetMouseButton(0) && isTouchingPet)
        {
            holdTimer += Time.deltaTime; // 홀드 타이머 증가

            // 펫을 아직 들고 있지 않고, 홀드 타이머가 임계값을 초과하면 펫 들기 시작
            if (!isHolding && holdTimer >= holdThreshold)
            {
                StartHolding(); // 펫 들기 시작
            }
            // 펫을 들고 있는 경우, 펫 이동 처리
            else if (isHolding)
            {
                HandleHoldingMovement(); // 펫 들고 이동
            }
        }
        // 마우스 왼쪽 버튼을 뗐을 때
        else if (Input.GetMouseButtonUp(0))
        {
            // 펫을 들고 있었다면 놓기
            if (isHolding)
            {
                StopHolding(); // 펫 놓기
            }
            // 펫을 짧게 터치한 경우
            else if (isTouchingPet)
            {
                HandleShortTouch(); // 짧은 터치 처리
            }

            isTouchingPet = false; // 펫 터치 상태 해제
            holdTimer = 0f;       // 홀드 타이머 초기화
        }

        // 펫이 선택되었고, 들고 있지 않은 상태에서 일정 시간이 지나면 선택 해제
        if (isSelected && !isHolding)
        {
            selectionTimer += Time.deltaTime;
            if (selectionTimer >= 3f)
            {
                Deselect();
            }
        }
    }

    private void HandleShortTouch()
    {
        // 화면 터치 위치에서 레이캐스트
        Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
        RaycastHit hit;
        bool didHit = Physics.Raycast(ray, out hit, Mathf.Infinity);

        if (didHit)
        {
            // 펫 카메라 모드가 활성화된 상태라면
            if (PetCameraSwitcher.Instance != null && PetCameraSwitcher.Instance.petCameraModeActivated)
            {
                // 터치한 오브젝트가 이 펫이라면
                if (hit.collider.gameObject == petController.gameObject)
                {
                    // 펫의 3D모델의 자식 중 "CameraPoint"를 찾습니다.
                    Transform cameraPoint = petController.petModelTransform.Find("CameraPoint");
                    if (cameraPoint != null)
                    {
                        PetCameraSwitcher.Instance.SwitchToPetCamera(cameraPoint);
                    }
                    return; // 일반 선택 로직을 실행하지 않음
                }
            }

            // 일반 터치(펫 선택) 처리
            if (hit.collider.gameObject == petController.gameObject)
            {
                Select();
            }
            else if (isSelected)
            {
                Deselect();
            }
        }
    }

    // 펫을 들고 이동하는 함수 (수정된 카메라 이동 속도 적용)
    private void HandleHoldingMovement()
    {
        Vector3 currentTouchPosition = Input.mousePosition; // 현재 터치 위치

        // 현재 터치 위치에서 레이를 발사하여 지형과 충돌 체크
        Ray ray = Camera.main.ScreenPointToRay(currentTouchPosition);
        RaycastHit terrainHit;
        NavMeshHit navHit;
        if (Physics.Raycast(ray, out terrainHit, Mathf.Infinity, terrainLayer))
        {
            if (NavMesh.SamplePosition(terrainHit.point, out navHit, 10f, NavMesh.AllAreas))
            {
                float targetHeight = terrainHit.point.y + holdHeight;
                targetPosition = new Vector3(navHit.position.x, targetHeight, navHit.position.z);
                petController.transform.position = targetPosition;

                if (petController.petModelTransform != null && Camera.main != null)
                {
                    Vector3 directionToCamera = Camera.main.transform.position - petController.petModelTransform.position;
                    directionToCamera.y = 0;
                    if (directionToCamera != Vector3.zero)
                    {
                        Quaternion targetRotation = Quaternion.LookRotation(directionToCamera);
                        petController.petModelTransform.rotation = Quaternion.Lerp(
                            petController.petModelTransform.rotation,
                            targetRotation,
                            Time.deltaTime * petController.rotationSpeed
                        );
                    }
                }
            }
        }

        // 화면 가장자리 스크롤 처리 (이전보다 2배 빠르게)
        Vector2 screenPosition = currentTouchPosition;
        Vector3 cameraMovement = Vector3.zero;

        if (screenPosition.x < edgeScrollThreshold)
            cameraMovement.x = -1;
        else if (screenPosition.x > Screen.width - edgeScrollThreshold)
            cameraMovement.x = 1;
        if (screenPosition.y < edgeScrollThreshold)
            cameraMovement.z = -1;
        else if (screenPosition.y > Screen.height - edgeScrollThreshold)
            cameraMovement.z = 1;

        if (cameraMovement != Vector3.zero)
        {
            float fastEdgeScrollSpeed = edgeScrollSpeed * 2f; // 두 배 빠르게
            Camera.main.transform.parent.Translate(cameraMovement * fastEdgeScrollSpeed * Time.deltaTime, Space.World);
        }
    }

    // PetInteractionController.cs 내의 수정이 필요한 부분
    private void StartHolding()
    {
        isHolding = true;
        petController.StopMovement();

        // ★ 나무 올라가기 상태 초기화 개선
        if (petController.isClimbingTree)
        {
            // PetMovementController의 나무 관련 코루틴만 중단
            var movementController = petController.GetComponent<PetMovementController>();
            movementController?.StopTreeClimbing();

            // 나무 올라가기 관련 상태 초기화
            petController.isClimbingTree = false;
            petController.currentTree = null;

            Debug.Log($"{petController.petName}: 나무에서 강제로 내려옴 (플레이어가 잡음)");
        }

        // NavMeshAgent 비활성화
        if (petController.agent != null)
        {
            petController.agent.enabled = false;
            // 현재 회전값 저장
            if (petController.petModelTransform != null)
            {
                petController.petModelTransform.rotation = petController.transform.rotation;
            }
        }

        // CameraController 비활성화
        CameraController camController = FindObjectOfType<CameraController>();
        if (camController != null)
        {
            camController.enabled = false;
        }

        // NavMesh 상의 가까운 지점을 찾고 펫 위치 보정
        NavMeshHit hit;
        if (NavMesh.SamplePosition(petController.transform.position, out hit, 10f, NavMesh.AllAreas))
        {
            Vector3 surfacePoint = hit.position;
            targetPosition = new Vector3(surfacePoint.x, surfacePoint.y + holdHeight, surfacePoint.z);
            petController.transform.position = targetPosition;
        }

        if (nameTextObject != null)
            nameTextObject.SetActive(false);
    }

    private void StopHolding()
    {
        if (petController.animator != null)
        {
            petController.animator.SetInteger("animation", 0);
        }

        isHolding = false;

        // 현재 회전값 저장
        Quaternion currentRotation = petController.petModelTransform != null
            ? petController.petModelTransform.rotation
            : petController.transform.rotation;

        // 화면 터치 위치 기준으로 Terrain에 레이캐스트하여 펫 놓기
        Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
        RaycastHit hit;
        if (Physics.Raycast(ray, out hit, Mathf.Infinity, terrainLayer))
        {
            StartCoroutine(SmoothlyPlacePet(hit.point, currentRotation));
        }
        else
        {
            Vector3 groundPoint = new Vector3(
                petController.transform.position.x,
                0,
                petController.transform.position.z
            );
            StartCoroutine(SmoothlyPlacePet(groundPoint, currentRotation));
        }
    }

    // 모이기 중에 강제로 펫 들기 중단
    private void ForceStopHolding()
    {
        if (!isHolding) return;

        if (petController.animator != null)
        {
            petController.animator.SetInteger("animation", 0);
        }

        isHolding = false;
        isTouchingPet = false;
        holdTimer = 0f;

        // NavMeshAgent 재활성화
        if (petController.agent != null)
        {
            petController.agent.enabled = true;
        }

        // CameraController 재활성화
        CameraController camController = FindObjectOfType<CameraController>();
        if (camController != null)
        {
            camController.enabled = true;
        }

        // 펫을 지면에 즉시 배치
        NavMeshHit navHit;
        if (NavMesh.SamplePosition(petController.transform.position, out navHit, 10f, NavMesh.AllAreas))
        {
            petController.transform.position = navHit.position;
        }

        if (nameTextObject != null)
            nameTextObject.SetActive(false);

        Debug.Log($"{petController.petName}의 들기가 모이기 명령으로 인해 중단되었습니다.");
    }

    private IEnumerator SmoothlyPlacePet(Vector3 groundPoint, Quaternion originalRotation)
    {
        Vector3 startPosition = petController.transform.position;
        float startY = startPosition.y;
        float targetY = groundPoint.y;

        float duration = 0.8f;
        float elapsed = 0f;

        Vector3 horizontalStart = new Vector3(startPosition.x, startY, startPosition.z);
        Vector3 horizontalEnd = new Vector3(groundPoint.x, startY, groundPoint.z);

        // 시작할 때 원래 회전값 유지
        if (petController.petModelTransform != null)
        {
            petController.petModelTransform.rotation = originalRotation;
        }

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / duration;

            // 이징 함수 적용
            float easeT = 1f - Mathf.Pow(1f - t, 3f);

            // 수평 이동과 수직 하강을 분리하여 계산
            Vector3 horizontalPosition = Vector3.Lerp(horizontalStart, horizontalEnd, easeT);
            float currentY = Mathf.Lerp(startY, targetY, easeT);

            Vector3 newPosition = new Vector3(
                horizontalPosition.x,
                currentY,
                horizontalPosition.z
            );

            petController.transform.position = newPosition;

            // 회전값 유지
            if (petController.petModelTransform != null)
            {
                petController.petModelTransform.rotation = originalRotation;
            }

            yield return null;
        }

        // 최종 위치 설정
        petController.transform.position = groundPoint;

        // 최종 회전값 설정
        if (petController.petModelTransform != null)
        {
            petController.petModelTransform.rotation = originalRotation;
            petController.transform.rotation = originalRotation;
        }

        yield return new WaitForSeconds(0.1f); // 약간의 지연 추가

        // NavMeshAgent 재활성화
        if (petController.agent != null)
        {
            petController.agent.enabled = true;
            petController.agent.updateRotation = true;
        }

        // CameraController 재활성화
        CameraController camController = FindObjectOfType<CameraController>();
        if (camController != null)
        {
            camController.enabled = true;
        }

        CompletePetPlacement();
    }

    // 펫 배치를 완료하는 함수
    private void CompletePetPlacement()
    {
        isHolding = false;  // 펫을 들고 있지 않은 상태로 설정

        // ★ 나무 상태 최종 확인 및 초기화
        if (petController.isClimbingTree)
        {
            petController.isClimbingTree = false;
            petController.currentTree = null;
            Debug.Log($"{petController.petName}: 나무 상태 최종 초기화");
        }

        petController.ResumeMovement(); // 펫의 움직임을 다시 시작합니다.
                                        // 펫에게 랜덤한 목적지를 설정합니다.
        petController.GetComponent<PetMovementController>().SetRandomDestination();
        Deselect();       // 펫 선택 해제
    }

    // 펫을 선택하는 함수
    private void Select()
    {
        petController.isSelected = true;  // PetController에 선택 상태 알림

        isSelected = true;          // 펫이 선택된 상태로 설정
        selectionTimer = 0f;       // 선택 타이머 초기화
        petController.StopMovement(); // 펫의 움직임을 멈춥니다.
                                      // ★ 추가: 쉬고 있는 상태라면 애니메이션 중단
          // ★ 추가: 모든 상태에서 애니메이션을 Idle로 전환
    var animController = petController.GetComponent<PetAnimationController>();
    animController?.StopContinuousAnimation();
    
    // 즉시 Idle 애니메이션으로 전환
    if (petController.animator != null)
    {
        petController.animator.SetInteger("animation", 0);
    }

        touchCount++;             // 터치 횟수 증가
        lastTouchTime = Time.time; // 마지막 터치 시간 업데이트

        // 터치 횟수에 따라 특별한 애니메이션을 재생합니다.
        if (touchCount >= maxTouchCount)
        {
            // 최대 터치 횟수에 도달하면 8번 애니메이션 재생
            StartCoroutine(TriggerSpecialAnimation(8));
            touchCount = 0; // 터치 횟수 초기화
        }
        else if (touchCount >= 5)
        {
            // 5번 이상 터치하면 6번 애니메이션 재생
            StartCoroutine(TriggerSpecialAnimation(6));
        }
        else
        {
            // 이름 텍스트 오브젝트가 있으면 활성화
            if (nameTextObject != null)
                nameTextObject.SetActive(true);

            // 펫을 들고 있지 않으면, 펫이 멈추고 카메라를 바라보도록 하는 코루틴 시작
            if (!isHolding)
                StartCoroutine(WaitForStopAndLookAtCamera());
        }
    }

    // 펫 선택을 해제하는 함수
    private void Deselect()
{
    petController.isSelected = false;
    isSelected = false;

    if (!isHolding)
    {
        if (nameTextObject != null)
            nameTextObject.SetActive(false);

        StopAllCoroutines();
        
        // ★ 수정: 애니메이션 정상화 후 이동 시작하도록 코루틴 사용
        StartCoroutine(DelayedMovementResume());
    }
}
// ★ 새로운 코루틴 추가: 애니메이션 정상화 후 이동 재개
private IEnumerator DelayedMovementResume()
{
    // 먼저 회전을 정면으로 리셋 (옵션)
    if (petController.agent != null && petController.agent.enabled)
    {
        // 현재 보고 있는 방향을 유지하면서 부드럽게 전환
        yield return new WaitForSeconds(0.1f);
    }
    
    // 이동 재개
    petController.ResumeMovement();
    
    // 약간의 지연 후 새 목적지 설정
    yield return new WaitForSeconds(0.2f);
    
    var movementController = petController.GetComponent<PetMovementController>();
    movementController?.SetRandomDestination();
}
    // 특별한 애니메이션을 재생하는 코루틴
    private IEnumerator TriggerSpecialAnimation(int animationNumber)
    {
        // 펫의 PetAnimationController를 통해 특별한 애니메이션을 재생하고, 애니메이션이 끝날 때까지 기다립니다.
        yield return StartCoroutine(petController.GetComponent<PetAnimationController>().PlaySpecialAnimation(animationNumber));
    }

    // 펫이 멈추고 카메라를 바라볼 때까지 기다리는 코루틴
   private IEnumerator WaitForStopAndLookAtCamera()
{
    // ★ 추가: 즉시 Idle 애니메이션으로 전환
    if (petController.animator != null)
    {
        petController.animator.SetInteger("animation", 0);
    }
    
    // 펫의 에이전트 속도가 0.01f보다 클 동안 대기
    while (petController.agent != null && 
           petController.agent.enabled && 
           petController.agent.velocity.magnitude > 0.01f)
    {
        yield return null;
    }

    // 펫이 멈추면 부드럽게 카메라를 바라보도록 하는 코루틴 시작
    yield return StartCoroutine(SmoothLookAtCamera());
}
    // 펫이 부드럽게 카메라를 바라보도록 하는 코루틴
    private IEnumerator SmoothLookAtCamera()
    {
        if (Camera.main != null && petController.petModelTransform != null)
        {
            // 카메라에서 펫으로의 방향 벡터 계산
            Vector3 directionToCamera = Camera.main.transform.position - petController.transform.position;
            directionToCamera.y = 0; // Y축 회전은 무시

            if (directionToCamera != Vector3.zero)
            {
                // 목표 회전값 계산
                Quaternion targetRotation = Quaternion.LookRotation(directionToCamera);

                // 부모 오브젝트(PetController)를 회전시킴
                while (Quaternion.Angle(petController.transform.rotation, targetRotation) > 1f && isSelected)
                {
                    petController.transform.rotation = Quaternion.Slerp(
                        petController.transform.rotation,
                        targetRotation,
                        Time.deltaTime * petController.rotationSpeed * 2f // 조금 더 빠르게
                    );
                    yield return null;
                }
            }
        }
    }
}