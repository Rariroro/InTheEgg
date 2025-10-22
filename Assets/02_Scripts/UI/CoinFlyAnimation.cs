using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using System.Collections.Generic;
using TMPro;

/// <summary>
/// 코인이 획득 위치에서 UI로 날아가는 애니메이션을 관리하는 클래스
/// Unity Inspector에서 설정하거나 자동 생성됩니다
/// </summary>
public class CoinFlyAnimation : MonoBehaviour
{
    private static CoinFlyAnimation instance;
    public static CoinFlyAnimation Instance
    {
        get
        {
            if (instance == null)
            {
                // 씬에서 먼저 찾기 (프리팹으로 배치된 경우)
                instance = FindObjectOfType<CoinFlyAnimation>();

                // 없으면 자동 생성
                if (instance == null)
                {
                    GameObject go = new GameObject("CoinFlyAnimation");
                    instance = go.AddComponent<CoinFlyAnimation>();
                    DontDestroyOnLoad(go);
                }
            }
            return instance;
        }
    }

    [Header("코인 설정")]
    [Tooltip("코인 이미지 프리팹 (UI Image)")]
    public GameObject coinPrefab;

    [Tooltip("코인 스프라이트 (프리팹이 없을 때 사용)")]
    public Sprite coinSprite;

    [Tooltip("한 번에 생성할 코인 개수")]
    [Range(1, 10)]
    public int coinsPerAnimation = 5;

    [Tooltip("각 코인 생성 간격")]
    public float coinSpawnDelay = 0.1f;

    [Header("애니메이션 설정")]
    [Tooltip("코인이 날아가는 시간")]
    public float flyDuration = 1f;

    [Tooltip("곡선 경로의 높이")]
    public float curveHeight = 100f;

    [Tooltip("코인 시작 크기")]
    public float startScale = 1f;

    [Tooltip("코인 도착 크기")]
    public float endScale = 0.5f;

    [Tooltip("회전 속도")]
    public float rotationSpeed = 360f;

    [Header("효과")]
    [Tooltip("도착 시 펄스 크기")]
    [Range(1.1f, 2f)]
    public float pulseScale = 1.5f;

    [Tooltip("펄스 지속 시간")]
    [Range(0.1f, 1f)]
    public float pulseDuration = 0.4f;

    [Tooltip("코인 획득 사운드")]
    public AudioClip coinSound;

    [Tooltip("코인 도착 사운드")]
    public AudioClip arrivalSound;

    [Header("타겟 설정")]
    [Tooltip("코인이 날아갈 목표 UI - 코인 이미지 오브젝트")]
    public Image coinTargetImage;

    [Tooltip("코인이 날아갈 목표 UI - 텍스트 (이미지가 없을 때 대체용)")]
    public TMP_Text coinTargetText;

    // 활성 코인 애니메이션 추적
    private List<GameObject> activeCoins = new List<GameObject>();
    private Canvas uiCanvas;
    private RectTransform coinTargetUI;

    // 펄스 효과 동시 실행 방지
    private bool isPulsingTarget = false;

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }
        instance = this;

        // Canvas 찾기
        GameObject canvasObject = GameObject.Find("Canvas");
        if (canvasObject != null)
        {
            uiCanvas = canvasObject.GetComponent<Canvas>();
        }

        // 타겟 설정 (Inspector에서 설정되지 않은 경우 자동으로 찾기)
        SetupTargets();
    }

    /// <summary>
    /// 타겟 자동 설정
    /// </summary>
    private void SetupTargets()
    {
        // 코인 이미지가 설정되어 있으면 우선 사용
        if (coinTargetImage != null)
        {
            coinTargetUI = coinTargetImage.GetComponent<RectTransform>();
        }
        // 텍스트가 설정되어 있으면 사용
        else if (coinTargetText != null)
        {
            coinTargetUI = coinTargetText.GetComponent<RectTransform>();
        }
    }

    /// <summary>
    /// 코인 UI 타겟 설정
    /// </summary>
    public void SetCoinTarget(RectTransform target)
    {
        coinTargetUI = target;
    }

    /// <summary>
    /// Image 컴포넌트로부터 타겟 설정 (권장)
    /// </summary>
    public void SetCoinTargetFromImage(Image coinImage)
    {
        if (coinImage != null)
        {
            coinTargetImage = coinImage;
            coinTargetUI = coinImage.GetComponent<RectTransform>();
        }
    }

    /// <summary>
    /// TMP_Text 컴포넌트로부터 자동으로 타겟 설정
    /// </summary>
    public void SetCoinTargetFromText(TMP_Text coinText)
    {
        if (coinText != null)
        {
            coinTargetText = coinText;
            coinTargetUI = coinText.GetComponent<RectTransform>();
        }
    }

    /// <summary>
    /// 월드 좌표에서 코인 애니메이션 시작
    /// </summary>
    public void PlayCoinAnimation(Vector3 worldPosition, int coinAmount = 0, System.Action onComplete = null)
    {
        if (uiCanvas == null)
        {
            GameObject canvasObject = GameObject.Find("Canvas");
            if (canvasObject != null)
            {
                uiCanvas = canvasObject.GetComponent<Canvas>();
            }

            if (uiCanvas == null)
            {
                Debug.LogWarning("Canvas를 찾을 수 없습니다!");
                onComplete?.Invoke();
                return;
            }
        }

        // 타겟 UI가 설정되지 않았으면 TreasureHuntManager에서 가져오기
        if (coinTargetUI == null && TreasureHuntManager.Instance != null)
        {
            var coinText = TreasureHuntManager.Instance.totalCoinsText;
            if (coinText != null)
            {
                SetCoinTargetFromText(coinText);
            }
        }

        if (coinTargetUI == null)
        {
            Debug.LogWarning("코인 UI 타겟이 설정되지 않았습니다!");
            onComplete?.Invoke();
            return;
        }

        // 코인 개수 결정
        int numCoins = coinAmount > 0 ? Mathf.Min(coinAmount / 10, 10) : coinsPerAnimation;
        numCoins = Mathf.Max(1, numCoins); // 최소 1개

        StartCoroutine(SpawnCoinsSequentially(worldPosition, numCoins, onComplete));
    }

    /// <summary>
    /// 스크린 좌표에서 코인 애니메이션 시작 (팝업용)
    /// </summary>
    public void PlayCoinAnimationFromScreen(Vector3 screenPosition, int coinAmount = 0, System.Action onComplete = null)
    {
        if (uiCanvas == null || coinTargetUI == null)
        {
            Debug.LogWarning("Canvas 또는 코인 타겟이 설정되지 않았습니다!");
            onComplete?.Invoke();
            return;
        }

        // 코인 개수 결정
        int numCoins = coinAmount > 0 ? Mathf.Min(coinAmount / 20, 8) : coinsPerAnimation;
        numCoins = Mathf.Max(1, numCoins);

        StartCoroutine(SpawnCoinsFromScreenPosition(screenPosition, numCoins, onComplete));
    }

    /// <summary>
    /// 순차적으로 코인 생성 및 애니메이션
    /// </summary>
    private IEnumerator SpawnCoinsSequentially(Vector3 worldPosition, int count, System.Action onComplete)
    {
        // 월드 좌표를 스크린 좌표로 변환
        Vector3 screenPos = Camera.main.WorldToScreenPoint(worldPosition);

        for (int i = 0; i < count; i++)
        {
            CreateAndAnimateCoin(screenPos);

            if (i < count - 1)
            {
                yield return new WaitForSeconds(coinSpawnDelay);
            }
        }

        // 코인이 거의 도착할 때 콜백 실행 (90% 지점)
        // 이렇게 하면 코인이 도착하면서 텍스트가 바로 변하기 시작
        float waitTime = flyDuration * 0.9f;
        yield return new WaitForSeconds(waitTime);

        onComplete?.Invoke();

        // 나머지 애니메이션 완료 대기
        yield return new WaitForSeconds(flyDuration * 0.1f);
    }

    /// <summary>
    /// 스크린 좌표에서 코인 생성
    /// </summary>
    private IEnumerator SpawnCoinsFromScreenPosition(Vector3 screenPos, int count, System.Action onComplete)
    {
        for (int i = 0; i < count; i++)
        {
            // 약간의 랜덤 오프셋 추가
            Vector3 randomOffset = new Vector3(
                Random.Range(-30f, 30f),
                Random.Range(-30f, 30f),
                0
            );

            CreateAndAnimateCoin(screenPos + randomOffset);

            if (i < count - 1)
            {
                yield return new WaitForSeconds(coinSpawnDelay * 0.5f); // 팝업에서는 더 빠르게
            }
        }

        // 코인이 거의 도착할 때 콜백 실행 (90% 지점)
        float waitTime = flyDuration * 0.9f;
        yield return new WaitForSeconds(waitTime);

        onComplete?.Invoke();

        // 나머지 애니메이션 완료 대기
        yield return new WaitForSeconds(flyDuration * 0.1f);
    }

    /// <summary>
    /// 개별 코인 생성 및 애니메이션
    /// </summary>
    private void CreateAndAnimateCoin(Vector3 screenStartPos)
    {
        GameObject coin = CreateCoinUI();
        if (coin == null) return;

        RectTransform coinRect = coin.GetComponent<RectTransform>();
        coinRect.position = screenStartPos;

        // 시작 크기 설정
        coinRect.localScale = Vector3.one * startScale;

        // 애니메이션 시작
        StartCoroutine(AnimateCoin(coinRect, screenStartPos));

        // 사운드 재생
        if (coinSound != null && Camera.main != null)
        {
            AudioSource.PlayClipAtPoint(coinSound, Camera.main.transform.position, 0.5f);
        }
    }

    /// <summary>
    /// 코인 UI 오브젝트 생성
    /// </summary>
    private GameObject CreateCoinUI()
    {
        GameObject coin = null;

        // 프리팹이 있으면 사용
        if (coinPrefab != null)
        {
            coin = Instantiate(coinPrefab, uiCanvas.transform);
        }
        // 없으면 동적 생성
        else if (coinSprite != null)
        {
            coin = new GameObject("CoinFly");
            coin.transform.SetParent(uiCanvas.transform, false);

            Image img = coin.AddComponent<Image>();
            img.sprite = coinSprite;
            img.preserveAspect = true;

            RectTransform rect = coin.GetComponent<RectTransform>();
            rect.sizeDelta = new Vector2(50, 50); // 기본 크기
        }
        else
        {
            // 스프라이트도 없으면 기본 원 생성
            coin = new GameObject("CoinFly");
            coin.transform.SetParent(uiCanvas.transform, false);

            Image img = coin.AddComponent<Image>();
            img.color = new Color(1f, 0.85f, 0f); // 금색

            RectTransform rect = coin.GetComponent<RectTransform>();
            rect.sizeDelta = new Vector2(30, 30);
        }

        activeCoins.Add(coin);
        return coin;
    }

    /// <summary>
    /// 코인 애니메이션 (베지어 곡선 경로)
    /// </summary>
    private IEnumerator AnimateCoin(RectTransform coin, Vector3 startPos)
    {
        Vector3 endPos = coinTargetUI.position;
        float elapsed = 0f;

        // 랜덤 제어점 (곡선 경로용)
        Vector3 controlPoint = startPos + new Vector3(
            Random.Range(-curveHeight, curveHeight),
            curveHeight,
            0
        );

        while (elapsed < flyDuration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / flyDuration;

            // 이징 함수 (가속 후 감속)
            float easedT = t * t * (3f - 2f * t);

            // 베지어 곡선 계산
            Vector3 p0 = startPos;
            Vector3 p1 = controlPoint;
            Vector3 p2 = endPos;

            Vector3 position = CalculateQuadraticBezier(p0, p1, p2, easedT);
            coin.position = position;

            // 크기 변화
            float scale = Mathf.Lerp(startScale, endScale, easedT);
            coin.localScale = Vector3.one * scale;

            // 회전
            coin.rotation = Quaternion.Euler(0, 0, elapsed * rotationSpeed);

            yield return null;
        }

        // 최종 위치 설정
        coin.position = endPos;

        // 도착 효과
        yield return PlayArrivalEffect(coin);

        // 정리
        activeCoins.Remove(coin.gameObject);
        Destroy(coin.gameObject);
    }

    /// <summary>
    /// 2차 베지어 곡선 계산
    /// </summary>
    private Vector3 CalculateQuadraticBezier(Vector3 p0, Vector3 p1, Vector3 p2, float t)
    {
        float u = 1 - t;
        float tt = t * t;
        float uu = u * u;

        Vector3 p = uu * p0;
        p += 2 * u * t * p1;
        p += tt * p2;

        return p;
    }

    /// <summary>
    /// 도착 시 펄스 효과
    /// </summary>
    private IEnumerator PlayArrivalEffect(RectTransform coin)
    {
        // 코인 이미지에 펄스 효과 적용 (이미지가 있으면 우선)
        RectTransform targetForPulse = null;
        Vector3 originalScale = Vector3.one;
        string targetType = "None";

        if (coinTargetImage != null)
        {
            targetForPulse = coinTargetImage.GetComponent<RectTransform>();
            originalScale = targetForPulse.localScale;
            targetType = "Image";
            Debug.Log($"[CoinFlyAnimation] 펄스 효과 시작 - 타겟: 코인 이미지, 원래 크기: {originalScale}, 펄스 크기: {pulseScale}, 지속시간: {pulseDuration}초");
        }
        else if (coinTargetUI != null)
        {
            targetForPulse = coinTargetUI;
            originalScale = targetForPulse.localScale;
            targetType = "UI (Text or other)";
            Debug.Log($"[CoinFlyAnimation] 펄스 효과 시작 - 타겟: UI 요소, 원래 크기: {originalScale}, 펄스 크기: {pulseScale}, 지속시간: {pulseDuration}초");
        }
        else
        {
            Debug.LogWarning("[CoinFlyAnimation] 펄스 효과 타겟이 설정되지 않음!");
            yield break;
        }

        // 이미 펄스 중이면 대기 (동시 실행 방지)
        if (isPulsingTarget)
        {
            Debug.Log("[CoinFlyAnimation] 이미 펄스 효과 실행 중, 스킵");
            yield break;
        }

        if (targetForPulse != null)
        {
            isPulsingTarget = true;

            // 도착 사운드
            if (arrivalSound != null && Camera.main != null)
            {
                AudioSource.PlayClipAtPoint(arrivalSound, Camera.main.transform.position, 0.3f);
            }

            // 펄스 애니메이션 (커졌다가 원래 크기로)
            float elapsed = 0f;
            Debug.Log($"[CoinFlyAnimation] 펄스 애니메이션 시작 - {targetType}");

            while (elapsed < pulseDuration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / pulseDuration;

                // 사인 곡선으로 부드럽게 커졌다가 작아짐
                float scale = 1f + (pulseScale - 1f) * Mathf.Sin(t * Mathf.PI);
                targetForPulse.localScale = originalScale * scale;

                // 중간 지점에서 로그 출력 (디버깅용)
                if (t > 0.45f && t < 0.55f)
                {
                    Debug.Log($"[CoinFlyAnimation] 펄스 중간 - 현재 크기: {targetForPulse.localScale}, 스케일 계수: {scale}");
                }

                yield return null;
            }

            // 원래 크기로 복원
            targetForPulse.localScale = originalScale;
            isPulsingTarget = false;

            Debug.Log($"[CoinFlyAnimation] 펄스 효과 완료 - 최종 크기: {targetForPulse.localScale}");
        }
    }

    /// <summary>
    /// 모든 활성 코인 정리
    /// </summary>
    public void ClearAllCoins()
    {
        foreach (var coin in activeCoins)
        {
            if (coin != null)
            {
                Destroy(coin);
            }
        }
        activeCoins.Clear();
    }

    private void OnDestroy()
    {
        ClearAllCoins();

        if (instance == this)
        {
            instance = null;
        }
    }
}