using UnityEngine;
using System.Collections;

/// <summary>
/// 보물 아이템을 제어하는 컴포넌트
/// 터치 감지, 시각 효과, 수집 처리를 담당
/// </summary>
public class TreasureController : MonoBehaviour
{
    [Header("시각 효과")]
    [Tooltip("반짝이는 효과의 속도")]
    public float sparkleSpeed = 2f;
    
    [Tooltip("위아래 움직임 속도")]
    public float floatSpeed = 1f;
    
    [Tooltip("위아래 움직임 범위")]
    public float floatAmplitude = 0.2f;
    
    [Tooltip("회전 속도")]
    public float rotationSpeed = 50f;
    
    [Header("수집 효과")]
    [Tooltip("수집 시 파티클 효과 프리팹")]
    public GameObject collectEffectPrefab;
    
    [Tooltip("수집 시 사운드")]
    public AudioClip collectSound;
    
    [Header("상태")]
    [SerializeField] private TreasureSpot parentSpot;
    [SerializeField] private bool isCollectable = false;  // 기본값을 false로 변경 (펫이 찾기 전에는 수집 불가)
    [SerializeField] private bool isCarried = false;
    [SerializeField] private bool isDropped = false;  // 펫이 내려놓은 상태
    [SerializeField] private PetController carryingPet;
    
    // Public 속성
    public bool IsCarried => isCarried;
    public bool IsDropped => isDropped;
    public PetController CarryingPet => carryingPet;
    
    private Vector3 startPosition;
    private float timeOffset;
    private Renderer treasureRenderer;
    private Material treasureMaterial;
    private Color originalColor;
    
    // 터치 감지용 콜라이더
    private Collider treasureCollider;
    
    private void Awake()
    {
        // 컴포넌트 캐싱
        treasureRenderer = GetComponent<Renderer>();
        if (treasureRenderer != null)
        {
            treasureMaterial = treasureRenderer.material;
            
            // Material이 _Color 속성을 가지고 있는지 확인
            if (treasureMaterial.HasProperty("_Color"))
            {
                originalColor = treasureMaterial.color;
            }
            else
            {
                // _Color 속성이 없으면 기본 흰색 사용
                originalColor = Color.white;
                Debug.LogWarning($"Material '{treasureMaterial.name}' doesn't have '_Color' property. Using white as default.", gameObject);
            }
        }
        
        treasureCollider = GetComponent<Collider>();
        if (treasureCollider == null)
        {
            // 콜라이더가 없으면 추가
            treasureCollider = gameObject.AddComponent<BoxCollider>();
        }
        // isTrigger를 false로 설정하여 Raycast가 감지할 수 있도록 함
        treasureCollider.isTrigger = false;
        
        // 초기에는 콜라이더 비활성화 (펫이 찾기 전에는 클릭 불가)
        treasureCollider.enabled = false;
        
        // 레이어 설정 (DroppedItem 레이어 사용)
        gameObject.layer = LayerMask.NameToLayer("DroppedItem");
        
        startPosition = transform.position;
        timeOffset = Random.Range(0f, Mathf.PI * 2f);
    }
    
    private void Start()
    {
        // 반짝이는 효과 시작
        if (!isCarried)
        {
            StartCoroutine(SparkleEffect());
        }
    }
    
    private void Update()
    {
        // 들고 있지 않고, 내려놓은 상태도 아닐 때만 애니메이션
        if (!isCarried && !isDropped)
        {
            // 위아래 움직임
            float yOffset = Mathf.Sin((Time.time + timeOffset) * floatSpeed) * floatAmplitude;
            transform.position = startPosition + Vector3.up * yOffset;
            
            // 회전
            transform.Rotate(Vector3.up, rotationSpeed * Time.deltaTime);
        }
    }
    
    /// <summary>
    /// 이 보물이 속한 스팟 설정
    /// </summary>
    public void SetSpot(TreasureSpot spot)
    {
        parentSpot = spot;
    }
    
    /// <summary>
    /// 초기 상태 설정 (처음 생성 시 회전 여부 등 설정)
    /// </summary>
    public void SetInitialState(bool shouldRotate = false)
    {
        if (!shouldRotate)
        {
            isDropped = true;  // 회전하지 않도록 설정
            // 시작 위치 저장
            startPosition = transform.position;
        }
    }
    
    /// <summary>
    /// 펫이 보물을 들기 시작
    /// </summary>
    public void StartCarrying(PetController pet)
    {
        isCarried = true;
        carryingPet = pet;
        isCollectable = false;
        
        // 움직임 효과 중지
        StopAllCoroutines();
        
        // 펫이 들고 있는 동안에는 콜라이더를 비활성화
        if (treasureCollider != null)
        {
            treasureCollider.enabled = false;
        }
    }
    
    /// <summary>
    /// 보물을 수집 가능 상태로 설정
    /// </summary>
    public void EnableCollection()
    {
        Debug.Log($"[TreasureController] EnableCollection - 이전 상태: isCollectable={isCollectable}, isCarried={isCarried}, carryingPet={carryingPet?.petName}");
        
        isCollectable = true;
        isCarried = false;  // 펫이 더 이상 들고 있지 않음
        isDropped = true;   // 펫이 내려놓은 상태로 표시
        // carryingPet은 유지! (누가 찾았는지 기억하기 위해)
        
        // TreasureSpot과의 연결 해제 (보물찾기 종료 시 삭제되지 않도록)
        if (parentSpot != null)
        {
            Debug.Log($"[TreasureController] TreasureSpot과 연결 해제: {parentSpot.name}");
            parentSpot = null;
        }
        
        // 콜라이더 다시 활성화 (Raycast 감지를 위해)
        if (treasureCollider != null)
        {
            treasureCollider.enabled = true;
            treasureCollider.isTrigger = false; // Raycast가 감지할 수 있도록
        }
        
        // 내려놓은 위치를 새로운 시작 위치로 설정 (원래 스팟으로 돌아가지 않도록)
        startPosition = transform.position;
        
        // TreasureHuntManager에 내려놓은 보물 등록
        if (TreasureHuntManager.Instance != null)
        {
            TreasureHuntManager.Instance.RegisterDroppedTreasure(this);
        }
        
        Debug.Log($"[TreasureController] EnableCollection - 현재 상태: isCollectable={isCollectable}, isCarried={isCarried}, isDropped={isDropped}, carryingPet={carryingPet?.petName}");
        Debug.Log($"[TreasureController] Collider 상태: enabled={treasureCollider?.enabled}, isTrigger={treasureCollider?.isTrigger}");
        
        // 빙글빙글 도는 효과 재시작
        StartCoroutine(SparkleEffect());
    }
    
    /// <summary>
    /// 터치 또는 클릭 감지 (Unity의 기본 OnMouseDown)
    /// </summary>
    private void OnMouseDown()
    {
        Debug.Log($"[TreasureController] OnMouseDown - isCollectable={isCollectable}, isCarried={isCarried}, isDropped={isDropped}");
        
        // 펫이 찾아서 내려놓은 보물만 수집 가능
        if (!isCollectable || !isDropped)
        {
            Debug.Log($"[TreasureController] 아직 펫이 찾지 않은 보물입니다. 수집 불가!");
            return;
        }
        
        Debug.Log($"[TreasureController] OnMouseDown 클릭 감지! 수집 시도");
        TryCollect();
    }
    
    /// <summary>
    /// CameraController에서 직접 호출하는 클릭 처리
    /// </summary>
    public void OnTreasureClicked()
    {
        Debug.Log($"[TreasureController] OnTreasureClicked - isCollectable={isCollectable}, isCarried={isCarried}, isDropped={isDropped}");
        
        // 펫이 찾아서 내려놓은 보물만 수집 가능
        if (!isCollectable || !isDropped)
        {
            Debug.Log($"[TreasureController] 아직 펫이 찾지 않은 보물입니다. 수집 불가!");
            return;
        }
        
        Debug.Log($"[TreasureController] Raycast로 클릭 감지! 수집 시도");
        TryCollect();
    }
    
    /// <summary>
    /// 트리거 충돌 감지 (선택사항)
    /// </summary>
    private void OnTriggerEnter(Collider other)
    {
        // 플레이어 태그나 특정 조건으로 수집 가능
        if (!isCollectable) return;
        
        if (other.CompareTag("Player"))
        {
            Debug.Log($"[TreasureController] Player 트리거 감지! 수집 시도");
            TryCollect();
        }
    }
    
    /// <summary>
    /// 보물 수집 시도
    /// </summary>
    private void TryCollect()
    {
        Debug.Log($"[TreasureController] TryCollect - isCollectable={isCollectable}, carryingPet={carryingPet?.petName}, isDropped={isDropped}");
        
        if (!isCollectable) return;
        
        // 펫이 찾은 보물인 경우 (carryingPet이 있음)
        if (carryingPet != null)
        {
            Debug.Log($"[TreasureController] {carryingPet.petName}이(가) 찾은 보물을 수집합니다.");
            
            // 이 펫의 점프 상태를 종료시킴
            // TreasureHuntActivity가 보물 삭제를 감지하고 자동으로 종료됨
            // 여기서는 추가 작업 필요 없음
        }
        // 플레이어가 직접 찾은 보물이거나, 펫이 내려놓은 보물인 경우
        else if (isDropped)
        {
            Debug.Log("[TreasureController] 플레이어가 펫이 찾아놓은 보물을 수집합니다.");
            // 가장 가까운 펫을 찾아서 보상 제공 (선택사항)
            FindNearestPetForReward();
        }
        else
        {
            Debug.Log("[TreasureController] 플레이어가 직접 보물을 발견했습니다.");
        }
        
        // 매니저에 수집 알림 (펫 상태 리셋은 매니저에서 처리)
        if (TreasureHuntManager.Instance != null)
        {
            // 내려놓은 보물 리스트에서 제거
            TreasureHuntManager.Instance.UnregisterDroppedTreasure(this);
            
            // parentSpot이 null이어도 수집 가능하도록 처리
            TreasureHuntManager.Instance.CollectTreasure(parentSpot, carryingPet);
        }
        
        // 수집 효과
        PlayCollectEffect();
        
        // 오브젝트 제거
        Destroy(gameObject, 0.1f);
    }
    
    /// <summary>
    /// 가장 가까운 펫을 찾아서 보상 제공 (선택사항)
    /// </summary>
    private void FindNearestPetForReward()
    {
        if (carryingPet != null) return; // 이미 펫이 지정된 경우
        
        PetController[] pets = FindObjectsByType<PetController>(FindObjectsSortMode.None);
        float minDistance = float.MaxValue;
        PetController nearestPet = null;
        
        foreach (var pet in pets)
        {
            if (pet == null) continue;
            float distance = Vector3.Distance(transform.position, pet.transform.position);
            if (distance < minDistance)
            {
                minDistance = distance;
                nearestPet = pet;
            }
        }
        
        if (nearestPet != null && minDistance < 10f) // 10미터 이내의 펫에게만 보상
        {
            carryingPet = nearestPet;
            Debug.Log($"[TreasureController] 가장 가까운 펫 {carryingPet.petName}에게 보상을 제공합니다.");
        }
    }
    
    /// <summary>
    /// 수집 효과 재생
    /// </summary>
    private void PlayCollectEffect()
    {
        // 파티클 효과
        if (collectEffectPrefab != null)
        {
            GameObject effect = Instantiate(collectEffectPrefab, transform.position, Quaternion.identity);
            Destroy(effect, 2f);
        }
        
        // 사운드 효과
        if (collectSound != null)
        {
            AudioSource.PlayClipAtPoint(collectSound, transform.position);
        }
    }
    
    /// <summary>
    /// 반짝이는 효과
    /// </summary>
    private IEnumerator SparkleEffect()
    {
        if (treasureMaterial == null) yield break;
        
        while (!isCarried)
        {
            float emission = Mathf.PingPong(Time.time * sparkleSpeed, 1f);
            Color emissionColor = originalColor * Mathf.LinearToGammaSpace(emission);
            
            // Emission 색상 설정 (URP/Lit 셰이더 기준)
            treasureMaterial.SetColor("_EmissionColor", emissionColor);
            
            // Standard 셰이더인 경우
            if (treasureMaterial.HasProperty("_EmissionColor"))
            {
                treasureMaterial.EnableKeyword("_EMISSION");
            }
            
            yield return null;
        }
        
        // 원래 색상으로 복원
        if (treasureMaterial != null)
        {
            treasureMaterial.SetColor("_EmissionColor", Color.black);
        }
    }
    
    private void OnDestroy()
    {
        // 머티리얼 정리
        if (treasureMaterial != null)
        {
            treasureMaterial.SetColor("_EmissionColor", Color.black);
        }
    }
}