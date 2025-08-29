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
    [SerializeField] private bool isCollectable = true;
    [SerializeField] private bool isCarried = false;
    [SerializeField] private PetController carryingPet;
    
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
        treasureCollider.isTrigger = true;
        
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
        if (!isCarried)
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
    /// 펫이 보물을 들기 시작
    /// </summary>
    public void StartCarrying(PetController pet)
    {
        isCarried = true;
        carryingPet = pet;
        isCollectable = false;
        
        // 움직임 효과 중지
        StopAllCoroutines();
        
        // 콜라이더를 트리거로 변경
        if (treasureCollider != null)
        {
            treasureCollider.isTrigger = true;
        }
    }
    
    /// <summary>
    /// 보물을 수집 가능 상태로 설정
    /// </summary>
    public void EnableCollection()
    {
        isCollectable = true;
    }
    
    /// <summary>
    /// 터치 또는 클릭 감지
    /// </summary>
    private void OnMouseDown()
    {
        if (!isCollectable || !isCarried) return;
        
        TryCollect();
    }
    
    /// <summary>
    /// 트리거 충돌 감지 (선택사항)
    /// </summary>
    private void OnTriggerEnter(Collider other)
    {
        // 플레이어 태그나 특정 조건으로 수집 가능
        if (!isCollectable || !isCarried) return;
        
        if (other.CompareTag("Player"))
        {
            TryCollect();
        }
    }
    
    /// <summary>
    /// 보물 수집 시도
    /// </summary>
    private void TryCollect()
    {
        if (!isCollectable || carryingPet == null) return;
        
        // 매니저에 수집 알림
        if (TreasureHuntManager.Instance != null && parentSpot != null)
        {
            TreasureHuntManager.Instance.CollectTreasure(parentSpot, carryingPet);
        }
        
        // 수집 효과
        PlayCollectEffect();
        
        // 이 오브젝트는 매니저에서 제거됨
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