using System.Collections;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// 펫의 입력 처리를 담당하는 컨트롤러 클래스
/// 플레이어의 터치/클릭 입력을 받아 펫 선택, 들기/놓기, 특수 애니메이션 등을 처리합니다
/// </summary>
public class PetInputController : PetControllerBase
{
    // ===== 이름 표시 관련 필드 =====
    private TextMesh nameText;           // 펫 이름을 표시하는 3D 텍스트 메시
    private GameObject nameTextObject;    // 이름 텍스트를 담는 게임 오브젝트

    // ===== 선택 상태 관리 =====
    private float selectionTimer = 0f;   // 펫이 선택된 상태에서 경과한 시간 (3초 후 자동 해제용)

    // ===== 터치 카운트 시스템 (특수 애니메이션 트리거) =====
    private int touchCount = 0;          // 연속 터치 횟수 카운터
    private float lastTouchTime = 0f;    // 마지막 터치 시간 (리셋 타이머용)
    private float touchResetTime = 10f;  // 터치 카운트를 리셋하는 시간 간격
    private int maxTouchCount = 10;      // 최대 터치 횟수 (이 수에 도달하면 죽는 애니메이션 재생)
    private bool isProcessingSpecialAnimation = false;  // 특수 애니메이션 처리 중 플래그
    public bool IsProcessingSpecialAnimation => isProcessingSpecialAnimation;

    // ===== 홀드(들기) 시스템 관련 필드 =====
    private float holdTimer = 0f;        // 홀드 버튼을 누른 시간 측정
    private float holdThreshold = 0.5f;  // 홀드로 인식되는 최소 시간 (0.5초)
    private float holdHeight = 6f;       // 펫을 들어올리는 높이
    private Vector3 initialTouchPosition; // 터치 시작 위치
    private Vector3 lastTouchPosition;    // 마지막 터치 위치
    private float edgeScrollThreshold = 200f; // 화면 가장자리 스크롤 활성화 영역 크기(픽셀)
    private float edgeScrollSpeed = 10f;      // 화면 가장자리 스크롤 속도
    private Vector3 targetPosition;           // 펫을 이동시킬 목표 위치
    private float dropLerpSpeed = 5f;         // 펫을 놓을 때 보간 속도 (미사용)

    private int terrainLayer;             // 지형 레이어 마스크
    private bool isTouchingPet = false;   // 현재 펫을 터치 중인지 여부

    /// <summary>
    /// 컨트롤러 초기화 시 호출되는 메서드
    /// </summary>
    protected override void OnInitialize()
    {
        CreateNameText();  // 펫 이름 표시용 3D 텍스트 생성
        terrainLayer = LayerMask.GetMask("Terrain");  // 지형 레이어 마스크 설정
    }

    /// <summary>
    /// 펫 이름을 표시할 3D 텍스트 생성
    /// 선택 시에만 표시되며, 애정도에 따라 색상이 변경됩니다
    /// </summary>
    private void CreateNameText()
    {
        // 펫 모델이 존재하는 경우에만 생성
        if (petController.petModelTransform != null)
        {
            // 이름 텍스트 오브젝트 생성
            nameTextObject = new GameObject("NameText");

            // 펫의 자식으로 설정하고 머리 위 3유닛 위치에 배치
            nameTextObject.transform.SetParent(petController.transform);
            nameTextObject.transform.localPosition = Vector3.up * 3f;
            nameTextObject.transform.localRotation = Quaternion.identity;

            // TextMesh 컴포넌트 추가 및 설정
            nameText = nameTextObject.AddComponent<TextMesh>();
            nameText.text = petController.petName;
            nameText.fontSize = 20;
            nameText.alignment = TextAlignment.Center;
            nameText.anchor = TextAnchor.LowerCenter;
            nameText.color = Color.white;

            // Billboard 컴포넌트 추가 (항상 카메라를 향하도록)
            nameTextObject.AddComponent<Billboard>();

            // 초기에는 비활성화 (선택 시에만 표시)
            nameTextObject.SetActive(false);
        }
    }


    private void Update()
    {
        HandleInput();
    }

    /// <summary>
    /// 플레이어 입력을 처리하는 메인 메서드
    /// 매 프레임 호출되며 터치/클릭 입력을 감지하고 처리합니다
    /// </summary>
    public void HandleInput()
    {
        // ===== 탈진 상태 체크 =====
        // 펫이 탈진 상태면 들기 강제 해제
        if (petController.State.IsExhausted)
        {
            // 들고 있던 펫을 즉시 놓기
            if (petController.State.IsHolding)
            {
                ForceStopHolding();
            }
            // 탈진 상태에서는 더 이상의 입력 처리 없음
            // (선택은 유지할 수 있지만 들기는 불가능)
        }

        // ===== 모이기 명령 중 입력 차단 =====
        // 펫이 모이기 명령을 수행 중이면 모든 입력을 무시
        if (petController.State.CurrentStatus == PetStatus.GatheringInProgress ||
            petController.State.CurrentStatus == PetStatus.GatheredWaiting)
        {
            // 들고 있던 상태면 강제로 놓기
            if (petController.State.IsHolding)
            {
                ForceStopHolding();
            }

            // 선택 상태도 해제
            if (petController.State.IsSelected)
            {
                Deselect();
            }

            return;  // 더 이상의 입력 처리 없음
        }

        // ===== 터치 카운트 리셋 =====
        // 10초 이상 터치가 없으면 카운트를 0으로 리셋
        if (Time.time - lastTouchTime > touchResetTime)
            touchCount = 0;

        // ===== 마우스/터치 다운 이벤트 =====
        if (Input.GetMouseButtonDown(0))
        {
            // 터치 시작 위치 저장
            initialTouchPosition = Input.mousePosition;
            lastTouchPosition = initialTouchPosition;

            // 터치 위치에서 레이캐스트 수행
            Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
            RaycastHit hit;

            // Water 레이어를 제외한 모든 레이어와 충돌 검사
            // (물 속에서도 펫을 선택할 수 있도록)
            int layerMask = ~LayerMask.GetMask("Water");

            if (Physics.Raycast(ray, out hit, Mathf.Infinity, layerMask))
            {
                // 터치한 오브젝트가 이 펫인지 확인
                PetController hitPet = hit.collider.GetComponent<PetController>();
                if (hitPet == petController ||
                    (hit.collider.transform.IsChildOf(petController.transform) && petController.State.IsInWater))
                {
                    // 이 펫을 터치한 경우: 홀드 타이머 시작
                    isTouchingPet = true;
                    holdTimer = 0f;
                }
                else if (petController.State.IsSelected)
                {
                    // 다른 곳을 터치했고 이 펫이 선택된 상태면 선택 해제
                    Deselect();
                }
            }
        }
        // ===== 마우스/터치 홀드 이벤트 =====
        else if (Input.GetMouseButton(0) && isTouchingPet)
        {
            holdTimer += Time.deltaTime;  // 홀드 시간 누적

            // 0.5초 이상 홀드하면 들기 시작
            if (!petController.State.IsHolding && holdTimer >= holdThreshold)
            {
                StartHolding();
            }
            // 이미 들고 있는 상태면 이동 처리
            else if (petController.State.IsHolding)
            {
                HandleHoldingMovement();
            }
        }
        // ===== 마우스/터치 업 이벤트 =====
        else if (Input.GetMouseButtonUp(0))
        {
            // 들고 있던 펫을 놓기
            if (petController.State.IsHolding)
            {
                Debug.Log("HandleInput() / StopHolding()");
                StopHolding();
            }
            // 짧은 터치였다면 선택/특수 동작 처리
            else if (isTouchingPet)
            {
                HandleShortTouch();
            }

            isTouchingPet = false;  // 터치 상태 리셋
            holdTimer = 0f;         // 홀드 타이머 리셋
        }

        // ===== 선택 상태 처리 =====
        if (petController.State.IsSelected && !petController.State.IsHolding)
        {
            // 3초 후 자동 선택 해제
            selectionTimer += Time.deltaTime;
            if (selectionTimer >= 3f)
            {
                Deselect();
            }

            // 이름 색상 업데이트 (애정도에 따라 변경)
            if (nameTextObject != null && nameTextObject.activeSelf)
            {
                UpdateNameColor();
            }

            // 선택된 펫을 카메라 방향으로 회전
            // (나무 타기 중이거나 특수 애니메이션 중에는 회전하지 않음)
            if (Camera.main != null && !petController.State.IsClimbingTree && !isProcessingSpecialAnimation)
            {
                Vector3 directionToCamera = Camera.main.transform.position - petController.transform.position;
                directionToCamera.y = 0;  // Y축 회전만 적용

                if (directionToCamera != Vector3.zero)
                {
                    Quaternion targetRotation = Quaternion.LookRotation(directionToCamera);
                    // 부드러운 회전 적용 (선택 시에는 2배 빠르게)
                    petController.transform.rotation = Quaternion.Slerp(
                        petController.transform.rotation,
                        targetRotation,
                        petController.Movement.rotationSmoothness * 2f * Time.deltaTime
                    );
                }
            }
        }
    }

    /// <summary>
    /// 짧은 터치(탭) 처리
    /// 펫 선택, 카메라 전환, 특수 애니메이션 트리거 등을 처리
    /// </summary>
    private void HandleShortTouch()
    {
        // 애니메이션이 잠긴 상태거나 특수 애니메이션 처리 중이면 무시
        if (petController.State.IsAnimationLocked || isProcessingSpecialAnimation)
        {
            // 입력을 무시하고 종료
            return;
        }


        Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
        RaycastHit hit;
        bool didHit = Physics.Raycast(ray, out hit, Mathf.Infinity);

        if (didHit)
        {

            if (PetCameraSwitcherButton.Instance != null && PetCameraSwitcherButton.Instance.petCameraModeActivated)
            {

                if (hit.collider.gameObject == petController.gameObject)
                {

                    Transform cameraPoint = petController.petModelTransform.Find("CameraPoint");
                    if (cameraPoint != null)
                    {
                        PetCameraSwitcherButton.Instance.SwitchToPetCamera(cameraPoint);
                    }
                    return;
                }
            }


            if (hit.collider.gameObject == petController.gameObject)
            {

                if (petController.State.IsInteracting && petController.State.InteractionLogic != null)
                {
                    ForceStopInteraction();
                }
                Select();
            }
            else if (petController.State.IsSelected && !isProcessingSpecialAnimation)
            {

                Deselect();
            }
        }
    }

    /// <summary>
    /// 펫을 들고 있는 동안의 이동 처리
    /// 마우스/터치 위치를 따라 펫을 이동시키고, 화면 가장자리에서 카메라 스크롤
    /// </summary>
    private void HandleHoldingMovement()
    {
        Vector3 currentTouchPosition = Input.mousePosition;

        // 마우스 위치에서 지형으로 레이캐스트
        Ray ray = Camera.main.ScreenPointToRay(currentTouchPosition);
        RaycastHit terrainHit;
        NavMeshHit navHit;
        if (Physics.Raycast(ray, out terrainHit, Mathf.Infinity, terrainLayer))
        {
            // NavMesh 상의 유효한 위치 찾기
            if (NavMesh.SamplePosition(terrainHit.point, out navHit, 10f, NavMesh.AllAreas))
            {
                // 지형 위 6유닛 높이에 펫 배치
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
                            Time.deltaTime * petController.Movement.rotationSmoothness
                        );

                        petController.transform.rotation = Quaternion.Lerp(
                            petController.transform.rotation,
                            targetRotation,
                            Time.deltaTime * petController.Movement.rotationSmoothness
                        );
                    }
                }
            }
        }

        // ===== 화면 가장자리 카메라 스크롤 =====
        // 마우스가 화면 가장자리(200픽셀 이내)에 있으면 카메라 이동
        Vector2 screenPosition = currentTouchPosition;
        Vector3 cameraMovement = Vector3.zero;

        // 좌우 가장자리 체크
        if (screenPosition.x < edgeScrollThreshold)
            cameraMovement.x = -1;  // 왼쪽으로 이동
        else if (screenPosition.x > Screen.width - edgeScrollThreshold)
            cameraMovement.x = 1;   // 오른쪽으로 이동

        // 상하 가장자리 체크
        if (screenPosition.y < edgeScrollThreshold)
            cameraMovement.z = -1;  // 아래쪽으로 이동
        else if (screenPosition.y > Screen.height - edgeScrollThreshold)
            cameraMovement.z = 1;   // 위쪽으로 이동

        // 카메라 이동 적용
        if (cameraMovement != Vector3.zero)
        {
            // 펫을 들고 있을 때는 2배 속도로 스크롤
            float fastEdgeScrollSpeed = edgeScrollSpeed * 2f;
            Camera.main.transform.parent.Translate(cameraMovement * fastEdgeScrollSpeed * Time.deltaTime, Space.World);
        }
    }



    /// <summary>
    /// 펫 들기 시작
    /// 현재 진행 중인 모든 활동을 중단하고 펫을 들어올립니다
    /// </summary>
    private void StartHolding()
    {
        // ===== 진행 중인 활동 강제 중단 =====

        // 다른 펫과 상호작용 중이면 강제 중단
        if (petController.State.IsInteracting && petController.State.InteractionLogic != null)
        {
            ForceStopInteraction();
        }

        // 나무 타기 중이면 강제로 내리기
        if (petController.State.IsClimbingTree)
        {
            var treeClimbingController = petController.GetComponent<PetTreeClimbingController>();
            if (treeClimbingController != null)
            {
                treeClimbingController.ForceCancelClimbing();
            }
        }


        petController.State.SetPlayerControl(holding: true, selected: petController.State.IsSelected);

        if (petController.animator != null)
        {
            petController.animator.speed = 3.0f;
        }

        var animController = petController.GetComponent<PetAnimationController>();
        if (animController != null)
        {
            animController.SetContinuousAnimation(PetAnimationController.PetAnimationType.Run);
        }

        if (petController.agent != null && petController.agent.enabled)
        {
            petController.agent.enabled = false;
        }

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

    /// <summary>
    /// 펫 놓기 처리
    /// 현재 마우스 위치의 지형에 펫을 부드럽게 내려놓습니다
    /// </summary>
    private void StopHolding()
    {
        // ===== 홀드 상태 해제 =====
        // State의 IsHolding을 false로 설정
        petController.State.UpdateHoldingState(false);

        if (petController.animator != null)
        {
            petController.animator.speed = 1.0f;
            petController.animator.SetInteger("animation", 0);
        }


        var animController = petController.GetComponent<PetAnimationController>();
        if (animController != null)
        {
            animController.StopContinuousAnimation();
        }

        Quaternion currentRotation = petController.petModelTransform != null
            ? petController.petModelTransform.rotation
            : petController.transform.rotation;


        Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
        RaycastHit hit;
        if (Physics.Raycast(ray, out hit, Mathf.Infinity, terrainLayer))
        {





            // 물 영역 체크
            NavMeshHit navHit;
            if (NavMesh.SamplePosition(hit.point, out navHit, 1f, NavMesh.AllAreas))
            {
                int waterArea = NavMesh.GetAreaFromName("Water");
                if (waterArea != -1 && (1 << waterArea) == navHit.mask)
                {
                    // 물 영역이면 드롭 다이빙 시퀀스 실행
                    Debug.Log($"StopHolding() - 물 영역 감지! 다이빙 시퀀스 시작");

                    // 물 표면 높이 가져오기
                    float waterSurfaceY = hit.point.y;  // 기본값
                    GameObject waterObj = GameObject.FindWithTag("Water");
                    if (waterObj != null)
                    {
                        var waterTrigger = waterObj.GetComponent<WaterZoneTrigger>();
                        if (waterTrigger != null)
                        {
                            waterSurfaceY = waterTrigger.WaterSurfaceY;
                        }
                        else
                        {
                            waterSurfaceY = waterObj.transform.position.y;
                        }
                    }

                    // PetWaterBehaviorController에 드롭 준비 알림
                    var waterController = petController.GetComponent<PetWaterBehaviorController>();
                    if (waterController != null)
                    {
                        waterController.PrepareForDrop();
                    }

                    // 물 표면 높이를 사용하여 드롭 다이빙
                    Vector3 dropPoint = hit.point;
                    dropPoint.y = waterSurfaceY;
                    StartCoroutine(DropDivingSequence(dropPoint, currentRotation));
                    return;
                }
            }

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

    /// <summary>
    /// 펫 들기 강제 중단
    /// 탈진 상태나 모이기 명령 등으로 인해 강제로 펫을 놓아야 할 때 사용
    /// </summary>
    private void ForceStopHolding()
    {
        if (!petController.State.IsHolding) return;  // 들고 있지 않으면 무시
        petController.State.UpdateHoldingState(false);  // 홀드 상태 해제


        if (petController.animator != null)
        {
            petController.animator.speed = 1.0f;
            petController.animator.SetInteger("animation", 0);
        }


        var animController = petController.GetComponent<PetAnimationController>();
        if (animController != null)
        {
            animController.StopContinuousAnimation();
        }


        isTouchingPet = false;
        holdTimer = 0f;


        if (petController.agent != null)
        {
            petController.agent.enabled = true;
        }


        NavMeshHit navHit;
        if (NavMesh.SamplePosition(petController.transform.position, out navHit, 10f, NavMesh.AllAreas))
        {
            petController.transform.position = navHit.position;
        }

        if (nameTextObject != null)
            nameTextObject.SetActive(false);


    }

    /// <summary>
    /// 펫을 부드럽게 지면에 내려놓는 코루틴
    /// 0.8초에 걸쳐 현재 위치에서 목표 지점으로 중력을 적용하여 낙하
    /// 물 지역에 놓을 경우 물 튀김 효과 생성
    /// </summary>
    /// <param name="groundPoint">펫을 놓을 지면 위치</param>
    /// <param name="originalRotation">펫의 원래 회전값</param>
    private IEnumerator SmoothlyPlacePet(Vector3 groundPoint, Quaternion originalRotation)
    {
        // 나무 타기 상태 해제 (혹시 남아있을 수 있음)
        petController.State.UpdateTreeClimbingState(false);

        // 시작 위치와 목표 위치 설정
        Vector3 startPosition = petController.transform.position;
        Vector3 targetPosition = groundPoint;

        // 자유낙하 시뮬레이션 설정
        float fallDuration = 0.8f;  // 낙하 시간
        float fallProgress = 0f;
        float gravity = -9.8f * 2f;  // 중력 가속도 (2배속)
        float initialVelocityY = 0f;  // 초기 수직 속도

        if (petController.petModelTransform != null)
        {
            petController.petModelTransform.rotation = originalRotation;
        }

        // 자유낙하 애니메이션
        while (fallProgress < 1f)
        {
            fallProgress += Time.deltaTime / fallDuration;
            
            // 수평 이동 (선형 보간)
            Vector3 currentPos = Vector3.Lerp(startPosition, targetPosition, fallProgress);
            
            // 수직 이동 (중력 가속도 적용)
            float t = fallProgress * fallDuration;
            currentPos.y = startPosition.y + initialVelocityY * t + 0.5f * gravity * t * t;
            
            // 지면에 도달했는지 체크
            if (currentPos.y <= groundPoint.y)
            {
                currentPos.y = groundPoint.y;
                petController.transform.position = currentPos;
                break;
            }
            
            petController.transform.position = currentPos;

            if (petController.petModelTransform != null)
            {
                petController.petModelTransform.rotation = originalRotation;
            }

            yield return null;
        }


        petController.transform.position = groundPoint;


        if (petController.petModelTransform != null)
        {
            petController.petModelTransform.rotation = originalRotation;
            petController.transform.rotation = originalRotation;

            petController.petModelTransform.localPosition = Vector3.zero;

        }

        yield return new WaitForSeconds(0.1f);


        if (petController.agent != null)
        {
            petController.agent.enabled = true;
            petController.agent.Warp(groundPoint);
            petController.agent.updateRotation = true;
        }

        CompletePetPlacement();
    }

    /// <summary>
    /// 드롭 다이빙 시퀀스 코루틴
    /// 펫을 물에 떨어뜨렸을 때 다이빙 효과를 연출합니다
    /// </summary>
    private IEnumerator DropDivingSequence(Vector3 dropPoint, Quaternion originalRotation)
    {
        // 홀드 상태 해제
        petController.State.UpdateHoldingState(false);

        // NavMesh 에이전트 비활성화 (점프 중에는 직접 제어)
        if (petController.agent != null && petController.agent.enabled)
        {
            petController.agent.enabled = false;
        }

        // 카메라 초점 해제 (펫 카메라 모드가 활성화된 경우 메인 카메라로 복귀)
        if (PetCameraSwitcherButton.Instance != null &&
            PetCameraSwitcherButton.Instance.petCameraModeActivated)
        {
            PetCameraSwitcherButton.Instance.SwitchBackToMainCamera();
        }

        // 점프 시작 위치와 목표 위치 설정
        Vector3 jumpStartPosition = petController.transform.position;
        Vector3 jumpTargetPosition = dropPoint;

        // 수면 높이 설정 (dropPoint의 y값)
        float waterSurfaceY = dropPoint.y;

        // 자유낙하 시뮬레이션 (포물선 운동)
        float fallDuration = 0.8f;  // 낙하 시간
        float fallProgress = 0f;
        float gravity = -9.8f * 2f;  // 중력 가속도 (2배속)
        float initialVelocityY = 0f;  // 초기 수직 속도

        // 자유낙하 애니메이션
        while (fallProgress < 1f)
        {
            fallProgress += Time.deltaTime / fallDuration;

            // 수평 이동 (선형 보간)
            Vector3 currentPos = Vector3.Lerp(jumpStartPosition, jumpTargetPosition, fallProgress);

            // 수직 이동 (중력 가속도 적용)
            float t = fallProgress * fallDuration;
            currentPos.y = jumpStartPosition.y + initialVelocityY * t + 0.5f * gravity * t * t;

            // 수면에 도달했는지 체크
            if (currentPos.y <= waterSurfaceY)
            {
                currentPos.y = waterSurfaceY;
                petController.transform.position = currentPos;
                break;
            }

            petController.transform.position = currentPos;

            // 낙하 방향으로 회전
            Vector3 direction = jumpTargetPosition - jumpStartPosition;
            direction.y = 0;
            if (direction != Vector3.zero)
            {
                petController.transform.rotation = Quaternion.LookRotation(direction);
            }

            yield return null;
        }

        // ===== 물 착수 =====
        Debug.Log($"{petController.petName}: 물에 착수!");

        // 물 입수 시작 이벤트
        var waterController = petController.GetComponent<PetWaterBehaviorController>();
        if (waterController != null)
        {
            // 다이빙 시퀀스 시작 (물튀김 효과, 잠수 깊이 설정)
            // 수면 높이를 전달하여 정확한 위치 설정
            waterController.StartDivingSequence(waterSurfaceY);
        }

        // 펫 감정 표현 (행복)
        petController.ShowEmotion(EmotionType.Happy);

        // 3초간 물속에서 다이빙
        yield return new WaitForSeconds(3f);

        // NavMesh 에이전트 재활성화
        if (!petController.State.IsHolding && petController.agent != null && !petController.agent.enabled)
        {
            petController.agent.enabled = true;
            petController.agent.Warp(petController.transform.position);
        }

        // 펫 배치 완료 처리
        CompletePetPlacement();

        Debug.Log($"{petController.petName}: 드롭 다이빙 시퀀스 완료!");
    }

    /// <summary>
    /// 펫 이름 색상을 애정도에 따라 업데이트
    /// 애정도가 높을수록 따뜻한 색상으로 변경됩니다
    /// </summary>
    private void UpdateNameColor()
    {
        if (nameText == null) return;

        float affection = petController.Needs.Affection;
        Color nameColor;

        // 애정도에 따른 색상 설정
        if (affection <= petController.Needs.LowAffectionThreshold)  // 매우 낮음 (30 이하)
        {
            // 흰색: 경계하는 상태
            nameColor = Color.white;
        }
        else if (affection <= 50f)  // 낮음 (30-50)
        {
            // 노란색: 보통 상태
            nameColor = Color.yellow;
        }
        else if (affection <= petController.Needs.HighAffectionThreshold)  // 중간 (50-75)
        {
            // 주황색: 친근한 상태
            nameColor = new Color(1f, 0.5f, 0f);
        }
        else  // 높음 (75 이상)
        {
            // 분홍색: 매우 친근한 상태
            nameColor = new Color(1f, 0.4f, 0.7f);
        }

        nameText.color = nameColor;
    }



    /// <summary>
    /// 펫 배치 완료 처리
    /// 펫을 놓은 후 최종 정리 작업을 수행합니다
    /// </summary>
    private void CompletePetPlacement()
    {
        // ===== 상태 플래그 정리 =====
        // 홀드 상태와 나무 타기 상태 모두 해제
        petController.State.UpdateHoldingState(false);
        petController.State.UpdateTreeClimbingState(false);


        if (petController.petModelTransform != null)
        {
            // 물 속에 있지 않을 때만 위치 리셋
            if (!petController.State.IsInWater)
            {
                petController.petModelTransform.localPosition = Vector3.zero;
            }
        }

        var treeClimbingController = petController.GetComponent<PetTreeClimbingController>();
        if (treeClimbingController != null)
        {
            treeClimbingController.ForceCancelClimbing();
        }


        petController.ResumeMovement();



        Deselect();
    }


    /// <summary>
    /// 펫 선택 처리
    /// 애정도에 따라 다른 반응을 보이며, 터치 횟수에 따라 특수 애니메이션 재생
    /// </summary>
    private void Select()
    {
        // 애니메이션이 잠긴 상태거나 특수 애니메이션 처리 중이면 선택 불가
        // (단, 탈진 상태는 예외로 선택 가능)
        if ((petController.State.IsAnimationLocked && !petController.State.IsExhausted) || isProcessingSpecialAnimation)
        {
            return;
        }


        petController.State.SetPlayerControl(holding: false, selected: true);
        selectionTimer = 0f;


        if (petController.AI != null)
        {
            petController.AI.InterruptAndResetAI();
        }


        var moveController = petController.GetComponent<PetMovementController>();
        moveController?.ForceStopCurrentBehavior();


        if (!petController.State.IsClimbingTree)
        {
            if (petController.movementController != null)
            {
                petController.movementController.StopMovement();
            }
        }


        var animController = petController.GetComponent<PetAnimationController>();
        animController?.SetContinuousAnimation((int)PetAnimationController.PetAnimationType.Idle);


        if (nameTextObject != null)
        {
            nameTextObject.SetActive(true);
            UpdateNameColor();
        }


        float affection = petController.Needs.Affection;

        if (affection <= petController.Needs.LowAffectionThreshold)
        {

            petController.ShowEmotion(EmotionType.Surprised, 2f);
            StartCoroutine(RunAwayAfterSelect());
        }
        else if (affection >= petController.Needs.HighAffectionThreshold)
        {

            StartCoroutine(ShowLoveAndJump());
        }
        else
        {

            touchCount++;
            lastTouchTime = Time.time;

            if (touchCount >= maxTouchCount)
            {
                isProcessingSpecialAnimation = true;
                StartCoroutine(PlayDieAnimationAndReset(animController));
            }
            else if (touchCount >= 5)
            {
                StartCoroutine(AttackAfterDelay());
            }

        }
    }

    /// <summary>
    /// 펫 선택 해제
    /// 선택 상태를 해제하고 정상 활동으로 복귀시킵니다
    /// </summary>
    private void Deselect()
    {
        // 선택 상태 플래그 해제
        petController.State.UpdateSelectedState(false);

        if (!petController.State.IsHolding)
        {
            if (nameTextObject != null)
                nameTextObject.SetActive(false);



            if (!isProcessingSpecialAnimation)
            {

                StopAllCoroutines();
            }


            if (petController.State.IsClimbingTree)
            {
                var animController = petController.GetComponent<PetAnimationController>();
                animController?.SetContinuousAnimation(PetAnimationController.PetAnimationType.Rest);
            }
            else
            {

                petController.ResumeMovement();
            }
        }
    }

    /// <summary>
    /// 진행 중인 상호작용 강제 중단
    /// 펫을 들거나 선택할 때 다른 펫과의 상호작용을 중단시킵니다
    /// </summary>
    private void ForceStopInteraction()
    {
        if (petController.State.InteractionLogic != null)
        {
            // 상호작용 로직의 모든 코루틴 중단
            var interactionLogic = petController.State.InteractionLogic;
            interactionLogic.StopAllCoroutines();


            if (petController.State.InteractionPartner != null)
            {
                var partner = petController.State.InteractionPartner;


                if (partner.State.InteractionLogic != null)
                {
                    partner.State.InteractionLogic.StopAllCoroutines();
                }


                if (PetInteractionManager.Instance != null)
                {
                    PetInteractionManager.Instance.NotifyInteractionEnded(petController, partner);
                }
            }


        }
    }

    /// <summary>
    /// 딜레이 후 공격 애니메이션 재생
    /// 터치를 5회 이상 했을 때 실행됩니다
    /// </summary>
    private IEnumerator AttackAfterDelay()
    {
        // 특수 애니메이션 처리 플래그 설정
        isProcessingSpecialAnimation = true;

        // 0.5초 대기 후 공격 애니메이션 재생
        yield return new WaitForSeconds(0.5f);


        if (petController.State.IsSelected)
        {
            var animController = petController.GetComponent<PetAnimationController>();
            if (animController != null)
            {
                yield return StartCoroutine(animController.PlaySpecialAnimation(PetAnimationController.PetAnimationType.Attack, false));
            }
        }


        isProcessingSpecialAnimation = false;
    }

    /// <summary>
    /// 죽는 애니메이션 재생 후 리셋
    /// 터치를 10회 이상 했을 때 실행됩니다
    /// </summary>
    private IEnumerator PlayDieAnimationAndReset(PetAnimationController animController)
    {
        // 죽는 애니메이션 재생 (회전 포함)
        yield return StartCoroutine(animController.PlaySpecialAnimation(PetAnimationController.PetAnimationType.Die, true));
        touchCount = 0;  // 터치 카운트 리셋
        isProcessingSpecialAnimation = false;  // 특수 애니메이션 플래그 해제
    }

    /// <summary>
    /// 선택 후 도망가기 행동
    /// 애정도가 낮을 때 (30 이하) 펫이 플레이어로부터 도망갑니다
    /// </summary>
    private IEnumerator RunAwayAfterSelect()
    {
        // 특수 애니메이션 처리 중 플래그 설정
        isProcessingSpecialAnimation = true;

        // 애니메이터 속도 정상화
        if (petController.animator != null)
        {
            petController.animator.speed = 1.0f;
        }


        Vector3 cameraPosition = Camera.main.transform.position;
        cameraPosition.y = petController.transform.position.y;


        Vector3 awayFromCamera = (petController.transform.position - cameraPosition).normalized;


        float randomAngle = UnityEngine.Random.Range(-90f, 90f);


        Vector3 runDirection = Quaternion.Euler(0, randomAngle, 0) * awayFromCamera;
        Vector3 runTarget = petController.transform.position + runDirection * 10f;


        UnityEngine.AI.NavMeshHit hit;
        if (UnityEngine.AI.NavMesh.SamplePosition(runTarget, out hit, 10f, UnityEngine.AI.NavMesh.AllAreas))
        {
            runTarget = hit.position;
        }


        yield return new WaitForSeconds(0.2f);


        if (petController.agent != null && petController.agent.enabled && runDirection != Vector3.zero)
        {

            petController.agent.updateRotation = true;


            var animController = petController.GetComponent<PetAnimationController>();
            animController?.SetContinuousAnimation(PetAnimationController.PetAnimationType.Run);


            float accelerationTime = 0.5f;
            float accelerationElapsed = 0f;


            petController.agent.SetDestination(runTarget);


            while (accelerationElapsed < accelerationTime)
            {
                accelerationElapsed += Time.deltaTime;
                float t = accelerationElapsed / accelerationTime;


                petController.agent.speed = Mathf.Lerp(petController.baseSpeed, petController.baseSpeed * 2f, t);

                yield return null;
            }


            petController.agent.speed = petController.baseSpeed * 2f;


            float elapsedTime = 0f;
            while (petController.agent.pathPending || petController.agent.remainingDistance > 1f)
            {
                elapsedTime += Time.deltaTime;
                if (elapsedTime > 3f) break;
                yield return null;
            }


            petController.agent.speed = petController.baseSpeed;
            animController?.StopContinuousAnimation();


            if (petController.animator != null)
            {
                petController.animator.speed = 1.0f;
            }
        }


        Deselect();


        isProcessingSpecialAnimation = false;
    }

    /// <summary>
    /// 사랑 표현과 점프 애니메이션
    /// 애정도가 높을 때 (75 이상) 펫이 기뻐하며 점프합니다
    /// </summary>
    private IEnumerator ShowLoveAndJump()
    {
        // 애니메이터 속도 정상화
        if (petController.animator != null)
        {
            petController.animator.speed = 1.0f;
        }

        // 하트 이모티콘 표시 (3초간)
        petController.ShowEmotion(EmotionType.Love, 3f);


        yield return new WaitForSeconds(0.3f);

        var animController = petController.GetComponent<PetAnimationController>();
        if (animController != null)
        {

            for (int i = 0; i < 2; i++)
            {
                yield return StartCoroutine(animController.PlayAnimationWithCustomDuration(
                    PetAnimationController.PetAnimationType.Jump, 0.8f, true, false));
                yield return new WaitForSeconds(0.2f);
            }
        }


    }
}