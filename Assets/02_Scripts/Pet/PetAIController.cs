using UnityEngine;
using UnityEngine.AI;
using System;
using Random = UnityEngine.Random;
using System.Collections;


//안쓰는 코드 
// PetAIController 클래스는 애완동물의 인공지능 동작을 제어합니다.
public class PetAIController : MonoBehaviour
{
    [Header("Pet Properties")]
    public float speed = 3.5f; // 애완동물의 이동 속도
    public float rotationSpeed = 5f; // 회전 속도를 조절하기 위한 변수

    // 성격을 나타내는 열거형
    public enum Personality { Shy, Brave, Lazy, Playful }
    // 식단 유형을 나타내는 열거형
    public enum DietType { Carnivore, Herbivore, Omnivore }
    // 서식지를 나타내는 열거형
    public enum Habitat { Water, Forest, Field }

    public Personality personality; // 애완동물의 성격
    public DietType dietType; // 애완동물의 식단 유형
    public Habitat habitat; // 애완동물의 서식지
    [Range(0, 100)]
    public float affection; // 애완동물의 호감도
    [Range(0, 100)]
    public float hunger; // 애완동물의 배고픔 정도

    [Header("Pet Information")]
    public string petName = "Buddy"; // 애완동물의 이름
    public DateTime birthday = DateTime.Now; // 애완동물의 생일

    public LayerMask hitLayers; // 레이캐스트에 사용할 레이어 마스크 (Inspector에서 설정)



    private NavMeshAgent agent; // 네비게이션 에이전트 컴포넌트
    private float wanderTimer; // 방황 시간을 추적하는 타이머
    private Transform petModelTransform; // 애완동물 모델의 Transform
    private TextMesh nameText; // 애완동물의 이름을 표시하는 TextMesh
    private PetBillboard nameBillboard; // 이름 표시를 위한 빌보드 스크립트
    private bool isSelected = false; // 애완동물이 선택되었는지 여부
    private float selectionTimer = 0f; // 선택 시간 타이머


    private Animator animator; // 애니메이터 컴포넌트


    private float detectionRadius = 100f; // 음식 감지 반경
    private float eatingDistance = 4f; // 음식을 먹을 수 있는 거리
    private GameObject targetFood; // 목표 음식 오브젝트
    private bool isEating = false; // 현재 먹고 있는지 여부를 나타내는 변수


    private Vector3 lastPosition;
    private Vector3 smoothVelocity;
    public float smoothTime = 0.3f; // 속도 평활화를 위한 시간



    private int touchCount = 0;
    private float lastTouchTime = 0f;
    private float touchResetTime = 5f; // 터치 횟수를 리셋할 시간
    private int maxTouchCount = 10; // 애니메이션 변경을 위한 최대 터치 횟수
    private bool isSpecialAnimationPlaying = false;
    //private bool isPlayingSpecialAnimation = false; // 특별한 애니메이션 재생 중인지 여부
    private Coroutine currentAnimation;

    private void Start()
    {
        // NavMeshAgent 컴포넌트를 가져오고 속도를 설정
        agent = GetComponent<NavMeshAgent>();
        agent.speed = speed;

        // 애완동물 모델을 찾고 이름 텍스트를 생성하며 랜덤한 목적지를 설정
        FindPetModel();
        CreateNameText();
        SetRandomDestination();

        // 애니메이터 컴포넌트를 가져옴
        animator = petModelTransform.GetComponent<Animator>();
        if (animator == null)
        {
            Debug.LogWarning("Animator component not found on the pet model.");
        }
    }

    // 애완동물 모델의 Transform을 찾는 함수
    private void FindPetModel()
    {
        // 자식이 있는지 확인하고 첫 번째 자식을 애완동물 모델로 설정
        if (transform.childCount > 0)
        {
            petModelTransform = transform.GetChild(0);
        }

        // 여전히 모델을 찾지 못했다면 자식 중 Renderer를 가진 오브젝트를 찾아 Transform을 설정
        if (petModelTransform == null)
        {
            Renderer modelRenderer = GetComponentInChildren<Renderer>();
            if (modelRenderer != null)
            {
                petModelTransform = modelRenderer.transform;
            }
        }

        // 모델을 찾지 못한 경우 경고 메시지 출력
        if (petModelTransform == null)
        {
            Debug.LogWarning("Pet model not found. The pet may not display correctly.");
        }
    }

    // 애완동물의 이름을 표시하는 텍스트를 생성하는 함수
    private void CreateNameText()
    {
        if (petModelTransform != null)
        {
            // 새로운 게임 오브젝트를 생성하여 이름 텍스트로 사용
            GameObject textObject = new GameObject("NameText");
            textObject.transform.SetParent(transform);
            textObject.transform.localPosition = Vector3.up * 3f; // 애완동물 위에 위치시킴
            textObject.transform.localRotation = Quaternion.identity;

            // TextMesh 컴포넌트를 추가하고 속성 설정
            nameText = textObject.AddComponent<TextMesh>();
            nameText.text = petName;
            nameText.fontSize = 20;
            nameText.alignment = TextAlignment.Center;
            nameText.anchor = TextAnchor.LowerCenter;
            nameText.color = Color.white;

            // 빌보드 스크립트를 추가하여 항상 카메라를 향하도록 설정
            nameBillboard = textObject.AddComponent<PetBillboard>();
            nameBillboard.mainTransform = transform;

            // 처음에는 이름 태그를 숨김
            textObject.SetActive(false);
        }
    }

    private void Update()
    {

        if (isSpecialAnimationPlaying)
            return;
        // 시간에 따라 배고픔 증가
        hunger += Time.deltaTime * 0.1f;
        hunger = Mathf.Clamp(hunger, 0f, 100f);
        //  if (Input.GetKeyDown(KeyCode.E))
        //     {
        //                 Debug.Log("4");

        //         animator.SetInteger("animation", 4);
        //     }
        // 터치 횟수 리셋 로직
        if (Time.time - lastTouchTime > touchResetTime)
        {
            touchCount = 0;
        }

        if (isSelected)
        {
            // 선택된 상태에서는 선택 시간 타이머 증가
            selectionTimer += Time.deltaTime;
            if (selectionTimer >= 3f)
            {
                // 3초가 지나면 선택 해제
                Deselect();
            }
        }
        else if (!agent.pathPending && agent.remainingDistance < 0.1f)
        {
            // 경로를 따라 이동 중이 아니고 목적지에 도착했을 때
            wanderTimer += Time.deltaTime;
            if (wanderTimer >= GetWanderInterval())
            {
                // 방황 시간이 지나면 새로운 랜덤 목적지 설정
                SetRandomDestination();
                wanderTimer = 0;
            }
        }
        // 부드러운 속도 계산
        Vector3 currentVelocity = (transform.position - lastPosition) / Time.deltaTime;
        smoothVelocity = Vector3.Lerp(smoothVelocity, currentVelocity, Time.deltaTime / smoothTime);
        lastPosition = transform.position;

        if (petModelTransform != null)
        {
            // 애완동물 모델의 위치를 에이전트의 위치와 동기화
            petModelTransform.position = transform.position;

            // 특별한 애니메이션이 재생 중이지 않을 때만 애니메이션을 변경
            if (!isSpecialAnimationPlaying)
            {
                if (smoothVelocity.magnitude > 1f) // 최소 속도 임계값
                {
                    // 부드러운 회전 적용
                    Quaternion targetRotation = Quaternion.LookRotation(smoothVelocity);
                    petModelTransform.rotation = Quaternion.Slerp(petModelTransform.rotation, targetRotation, Time.deltaTime * rotationSpeed);

                    // 이동 중일 때 걷는 애니메이션 설정
                    if (animator != null)
                    {
                        animator.SetInteger("animation", 1);
                    }
                }
                else
                {
                    // 이동하지 않을 때 대기 애니메이션 설정
                    if (animator != null)
                    {
                        animator.SetInteger("animation", 0);
                    }
                }
            }
            // else
            // {
            //     // 애완동물이 먹고 있을 때는 음식 쪽을 바라보도록 회전 (선택 사항)
            //     if (targetFood != null)
            //     {
            //         Vector3 directionToFood = targetFood.transform.position - petModelTransform.position;
            //         directionToFood.y = 0; // 수평 회전만 고려

            //         if (directionToFood != Vector3.zero)
            //         {
            //             Quaternion targetRotation = Quaternion.LookRotation(directionToFood);
            //             petModelTransform.rotation = Quaternion.Slerp(
            //                 petModelTransform.rotation,
            //                 targetRotation,
            //                 Time.deltaTime * rotationSpeed
            //             );
            //         }
            //     }
            // }
        }

        // 마우스 클릭 또는 터치 감지
        if (Input.GetMouseButtonDown(0))
        {
            // Debug.Log("Mouse clicked");
            HandleSelection();
        }

        // 배고프고 목표 음식이 없고 현재 먹고 있지 않을 때만 음식 감지
        if (hunger > 50 && targetFood == null && !isEating)
        {
            DetectFood();
        }

        // 목표 음식이 있고 현재 먹고 있지 않을 때 처리
        if (targetFood != null && !isEating)
        {
            // 음식으로 이동
            agent.SetDestination(targetFood.transform.position);

            // 음식과의 거리 확인
            if (Vector3.Distance(transform.position, targetFood.transform.position) <= eatingDistance)
            {
                StartCoroutine(EatFood());
            }
        }
    }
    // 애완동물 선택을 처리하는 함수
    private void HandleSelection()
    {
        // 화면에서 마우스 위치를 기반으로 레이 생성
        Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
        RaycastHit hit;

        // 레이캐스트 실행하여 결과를 didHit에 저장
        bool didHit = Physics.Raycast(ray, out hit, Mathf.Infinity, hitLayers);

        // Debug.Log($"Raycast hit something: {didHit}");

        if (didHit)
        {
            // Debug.Log($"Hit object: {hit.collider.gameObject.name} on layer {hit.collider.gameObject.layer}");
            // Debug.Log($"This gameObject: {gameObject.name} on layer {gameObject.layer}");

            if (hit.collider.gameObject == gameObject)
            {
                // 이 애완동물을 클릭한 경우 선택
                // Debug.Log("Hit this pet!");
                Select();
            }
            else if (isSelected)
            {
                // 다른 오브젝트를 클릭했을 때 선택 해제
                // Debug.Log("Deselecting");
                Deselect();
            }
        }
        else
        {
            // Debug.Log("Raycast didn't hit anything");
            if (isSelected)
            {
                // 아무것도 클릭하지 않았을 때 선택 해제
                //Deselect();
            }
        }
    }

    private void Select()
    {
        Debug.Log("Pet selected");
        isSelected = true;
        selectionTimer = 0f;
        agent.isStopped = true;

        touchCount++;
        lastTouchTime = Time.time;
  
        if (touchCount >= maxTouchCount)
        {
            StartCoroutine(PlaySpecialAnimation(8)); // 10번 이상 터치 시 애니메이션 8
            touchCount = 0;
        }
        else if (touchCount >= 5)
        {
            StartCoroutine(PlaySpecialAnimation(6)); // 5-9번 터치 시 애니메이션 6
          
        }
        else
        {
            if (nameText != null)
            {
                nameText.gameObject.SetActive(true);
            }
            StartCoroutine(WaitForStopAndLookAtCamera());
        }
    }

    private IEnumerator PlaySpecialAnimation(int animationNumber)
    {
        isSpecialAnimationPlaying = true;

        if (animator != null)
        {
            animator.SetInteger("animation", animationNumber);
            Debug.Log($"{petName}이(가) 특별한 반응을 보입니다! (애니메이션 {animationNumber})");

            // 애니메이션의 길이만큼 대기
            yield return new WaitForSeconds(animator.GetCurrentAnimatorStateInfo(0).length);

            // 애니메이션이 끝나면 기본 상태로 돌아감
            animator.SetInteger("animation", 0);
        }
        else
        {
            // 애니메이터가 없는 경우 기본 대기 시간
            yield return new WaitForSeconds(2f);
        }

        isSpecialAnimationPlaying = false;
        // 현재 애니메이션 코루틴 참조 제거
        agent.isStopped = false; // 에이전트 이동 재개
    }
    // 에이전트가 멈출 때까지 기다린 후 카메라를 향하도록 하는 코루틴
    private IEnumerator WaitForStopAndLookAtCamera()
    {
        // 에이전트의 속도가 거의 0이 될 때까지 대기
        while (agent.velocity.magnitude > 0.01f)
        {
            yield return null;
        }

        StartCoroutine(SmoothLookAtCamera()); // 카메라를 향하도록 회전하는 코루틴 시작
    }

    // 애완동물이 부드럽게 카메라를 바라보도록 회전시키는 코루틴
    private IEnumerator SmoothLookAtCamera()
    {
        if (Camera.main != null && petModelTransform != null)
        {
            // 카메라 방향을 계산
            Vector3 directionToCamera = Camera.main.transform.position - petModelTransform.position;
            directionToCamera.y = 0; // y축 회전을 무시하여 수평 회전만 고려

            if (directionToCamera != Vector3.zero)
            {
                // 목표 회전값 계산
                Quaternion targetRotation = Quaternion.LookRotation(directionToCamera);

                // 현재 회전에서 목표 회전까지 부드럽게 회전
                while (Quaternion.Angle(petModelTransform.rotation, targetRotation) > 0.01f)
                {
                    petModelTransform.rotation = Quaternion.Slerp(
                        petModelTransform.rotation,
                        targetRotation,
                        Time.deltaTime * rotationSpeed
                    );
                    yield return null;
                }
            }
        }
    }

    // 애완동물의 선택을 해제하는 함수
    private void Deselect()
    {
        Debug.Log("Pet deselected");
        isSelected = false;
        agent.isStopped = false; // 에이전트 재개

        // 이름 태그를 숨김
        if (nameText != null)
        {
            nameText.gameObject.SetActive(false);
        }
        StopAllCoroutines(); // 모든 코루틴 중지
        SetRandomDestination(); // 새로운 목적지 설정하여 이동 재개
    }

    // 랜덤한 목적지를 설정하는 함수
    private void SetRandomDestination()
    {
        // 현재 위치를 기준으로 반지름 30f 내의 랜덤한 방향 계산
        Vector3 randomDirection = Random.insideUnitSphere * 30f;
        randomDirection += transform.position;

        NavMeshHit hit;
        Vector3 finalPosition = Vector3.zero;

        // NavMesh 상에서 유효한 위치를 샘플링
        if (NavMesh.SamplePosition(randomDirection, out hit, 10f, NavMesh.AllAreas))
        {
            finalPosition = hit.position;
        }
        agent.SetDestination(finalPosition); // 에이전트의 목적지 설정
    }

    // 애완동물의 성격에 따라 방황 간격을 얻는 함수
    private float GetWanderInterval()
    {
        switch (personality)
        {
            case Personality.Lazy: return Random.Range(5f, 10f);
            case Personality.Shy: return Random.Range(3f, 7f);
            case Personality.Brave: return Random.Range(2f, 5f);
            case Personality.Playful: return Random.Range(1f, 3f);
            default: return 3f;
        }
    }

    private void DetectFood()
    {
        // 주변의 모든 "Food" 태그를 가진 오브젝트 찾기
        GameObject[] foodObjects = GameObject.FindGameObjectsWithTag("Food");

        GameObject nearestFood = null;
        float nearestDistance = float.MaxValue;

        foreach (GameObject food in foodObjects)
        {
            float distance = Vector3.Distance(transform.position, food.transform.position);
            if (distance < nearestDistance && distance <= detectionRadius)
            {
                nearestFood = food;
                nearestDistance = distance;
            }
        }

        if (nearestFood != null)
        {
            targetFood = nearestFood;
            // 음식을 향해 이동 시작
            agent.SetDestination(targetFood.transform.position);
            Debug.Log($"{petName}이(가) 음식을 발견하고 접근 중입니다.");
        }
    }
    private IEnumerator EatFood()
    {
        agent.isStopped = true; // NavMeshAgent 정지

        // 배고픔 감소
        hunger -= 30;
        hunger = Mathf.Clamp(hunger, 0f, 100f);

        Debug.Log($"{petName}이(가) 음식을 먹기 시작했습니다. 현재 배고픔: {hunger}");

        yield return StartCoroutine(PlaySpecialAnimation(4)); // 먹기 애니메이션 (인덱스 4) 시작

        if (targetFood != null)
        {
            Destroy(targetFood);
            targetFood = null;
            Debug.Log($"{petName}이(가) 음식을 다 먹었습니다.");
        }

        // 새로운 목적지 설정 (선택적)
        SetRandomDestination();
    }
    // private void EatFood()
    // {
    //   agent.isStopped = true; // NavMeshAgent 정지

    //     // 배고픔 감소
    //     hunger -= 30;
    //     hunger = Mathf.Clamp(hunger, 0f, 100f);

    //     Debug.Log($"{petName}이(가) 음식을 먹기 시작했습니다. 현재 배고픔: {hunger}");

    //     StartCoroutine(PlaySpecialAnimation(4)); // 먹기 애니메이션 (인덱스 4) 시작

    //     StartCoroutine(RemoveFoodAfterAnimation());
    // }

    // private IEnumerator RemoveFoodAfterAnimation()
    // {
    //   // 애니메이션이 끝날 때까지 대기
    //     while (isSpecialAnimationPlaying)
    //     {
    //         yield return null;
    //     }

    //     if (targetFood != null)
    //     {
    //         Destroy(targetFood);
    //         targetFood = null;
    //         Debug.Log($"{petName}이(가) 음식을 다 먹었습니다.");
    //     }

    //     // 새로운 목적지 설정 (선택적)
    //     SetRandomDestination();
    // }
}

// 기존 'Billboard'와 이름 충돌을 피하기 위해 클래스 이름을 'PetBillboard'로 변경
public class PetBillboard : MonoBehaviour
{
    public Transform mainTransform; // 메인 오브젝트(애완동물)의 Transform
    private Vector3 offset; // 애완동물과의 위치 차이

    void Start()
    {
        offset = transform.position - mainTransform.position; // 초기 오프셋 계산
    }

    void LateUpdate()
    {
        // 카메라를 향하도록 회전
        transform.LookAt(transform.position + Camera.main.transform.rotation * Vector3.forward,
            Camera.main.transform.rotation * Vector3.up);

        // 부드러운 위치 업데이트
        Vector3 targetPosition = mainTransform.position + offset;
        transform.position = Vector3.Lerp(transform.position, targetPosition, Time.deltaTime * 10f);
    }
}
