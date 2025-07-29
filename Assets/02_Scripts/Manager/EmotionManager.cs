using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// 감정 타입 열거형
public enum EmotionType
{
    Race,
    Fight,
    Friend,
    Happy,      // 행복
    Sad,        // 슬픔
    Angry,      // 화남
    Surprised,  // 놀람
    Love,       // 사랑
    Sleepy,     // 졸림
    Hungry,     // 배고픔
    Scared,  // 무서움
    Cheer,
    Confused,   // 혼란
    Victory,    // 승리
    Joke,
    Defeat,      // 패배
    Sleep,       // 잠자기
    Dizzy,      // 어지러움/스컹크 방구 맞음
    Disgusted   // 역겨움

}

// 감정 아이콘 관리 클래스
public class EmotionManager : MonoBehaviour
{
    // 싱글톤 패턴 구현
    public static EmotionManager Instance { get; private set; }

    // ▼▼▼ [수정] 데이터 클래스 이름 변경 및 파티클 프리팹 필드 추가 ▼▼▼
    [System.Serializable]
    public class EmotionAssetData // EmotionIconData -> EmotionAssetData
    {
        public EmotionType emotionType;
        public Sprite iconSprite;       // 스프라이트 아이콘용
        public GameObject particlePrefab; // 파티클 효과용
    }

    [Header("Emotion Asset Settings")]
    [Tooltip("감정 타입별로 아이콘(Sprite) 또는 파티클(Prefab)을 설정합니다. 둘 중 하나만 사용해야 합니다.")]
    public List<EmotionAssetData> emotionAssets = new List<EmotionAssetData>(); // emotionIcons -> emotionAssets

    // ▼▼▼ [수정] 파티클 프리팹 캐싱을 위한 딕셔너리 추가 ▼▼▼
    private Dictionary<EmotionType, Sprite> emotionSprites = new Dictionary<EmotionType, Sprite>();
    private Dictionary<EmotionType, GameObject> emotionParticles = new Dictionary<EmotionType, GameObject>();

    [Header("Bubble Settings")]
    public GameObject emotionBubblePrefab;
    private Queue<EmotionBubble> bubblePool = new Queue<EmotionBubble>();
    private int poolSize = 10;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
            InitializeEmotionAssets(); // 메서드 이름 변경
            InitializeBubblePool();
        }
        else
        {
            Destroy(gameObject);
        }
    }

    // ▼▼▼ [수정] 스프라이트와 파티클을 모두 초기화하도록 로직 변경 ▼▼▼
    private void InitializeEmotionAssets()
    {
        emotionSprites.Clear();
        emotionParticles.Clear();
        foreach (var assetData in emotionAssets)
        {
            // 파티클과 스프라이트가 동시에 할당된 경우 경고
            if (assetData.particlePrefab != null && assetData.iconSprite != null)
            {
                Debug.LogWarning($"[EmotionManager] EmotionType '{assetData.emotionType}'에 파티클과 스프라이트가 모두 할당되었습니다. 파티클이 우선 적용됩니다.");
            }

            // 파티클 프리팹 캐싱
            if (assetData.particlePrefab != null)
            {
                emotionParticles[assetData.emotionType] = assetData.particlePrefab;
            }
            // 스프라이트 아이콘 캐싱
            else if (assetData.iconSprite != null)
            {
                emotionSprites[assetData.emotionType] = assetData.iconSprite;
            }
        }
    }

    // 말풍선 풀 초기화
    private void InitializeBubblePool()
    {
        if (emotionBubblePrefab == null)
        {
            Debug.LogError("말풍선 프리팹이 설정되지 않았습니다!");
            return;
        }

        // 캔버스 찾기 또는 생성
        Canvas worldCanvas = FindObjectOfType<Canvas>();
        if (worldCanvas == null || worldCanvas.renderMode != RenderMode.WorldSpace)
        {
            GameObject canvasObj = new GameObject("WorldSpaceCanvas");
            worldCanvas = canvasObj.AddComponent<Canvas>();
            worldCanvas.renderMode = RenderMode.WorldSpace;

            // 캔버스 스케일러 추가
            CanvasScaler scaler = canvasObj.AddComponent<CanvasScaler>();
            scaler.dynamicPixelsPerUnit = 100f;

            // 그래픽 레이캐스터 추가
            canvasObj.AddComponent<UnityEngine.UI.GraphicRaycaster>();

            // 캔버스 크기 설정
            RectTransform canvasRect = worldCanvas.GetComponent<RectTransform>();
            canvasRect.sizeDelta = new Vector2(800, 600);

            // 캔버스 위치 및 회전 설정
            canvasRect.position = Vector3.zero;
            canvasRect.rotation = Quaternion.identity;
        }

        // 말풍선 풀 생성
        for (int i = 0; i < poolSize; i++)
        {
            GameObject bubbleObj = Instantiate(emotionBubblePrefab, worldCanvas.transform);
            EmotionBubble bubble = bubbleObj.GetComponent<EmotionBubble>();

            if (bubble == null)
            {
                Debug.LogError("말풍선 프리팹에 EmotionBubble 컴포넌트가 없습니다!");
                continue;
            }

            bubbleObj.SetActive(false);
            bubblePool.Enqueue(bubble);
        }
    }

    // 감정 말풍선 가져오기
    private EmotionBubble GetEmotionBubble()
    {
        // 풀에 말풍선이 있는 경우 재사용
        if (bubblePool.Count > 0)
        {
            return bubblePool.Dequeue();
        }

        // 풀이 비었으면 새로 생성
        if (emotionBubblePrefab != null)
        {
            Canvas worldCanvas = FindObjectOfType<Canvas>();
            if (worldCanvas != null)
            {
                GameObject bubbleObj = Instantiate(emotionBubblePrefab, worldCanvas.transform);
                return bubbleObj.GetComponent<EmotionBubble>();
            }
        }

        Debug.LogWarning("말풍선을 생성할 수 없습니다. 프리팹을 확인하세요.");
        return null;
    }

    // 말풍선 풀에 반환
    public void ReturnBubbleToPool(EmotionBubble bubble)
    {
        if (bubble != null)
        {
            bubble.gameObject.SetActive(false);
            bubblePool.Enqueue(bubble);
        }
    }

    // EmotionManager.cs

// ▼▼▼ ShowPetEmotion 메서드를 아래와 같이 수정합니다 ▼▼▼
public GameObject ShowPetEmotion(PetController pet, EmotionType emotion, float duration = 10f)
{
    if (pet == null) return null;

    // 1. 파티클 프리팹이 있는지 먼저 확인
    if (emotionParticles.TryGetValue(emotion, out GameObject particlePrefab))
    {
        // ▼▼▼ [수정] 파티클 생성 위치 결정 로직 ▼▼▼
        Vector3 spawnPosition;

        // 1순위: PetEmotionController의 emotionOrigin이 설정되어 있으면 그 위치를 사용합니다.
        var emotionController = pet.GetComponent<PetEmotionController>();
        if (emotionController != null && emotionController.GetEmotionOrigin() != null)
        {
            spawnPosition = emotionController.GetEmotionOrigin().position;
        }
        // 2순위: emotionOrigin이 없으면 기존처럼 콜라이더 기준으로 계산합니다. (하위 호환성)
        else
        {
            Transform targetTransform = pet.petModelTransform != null ? pet.petModelTransform : pet.transform;
            spawnPosition = targetTransform.position;
            Collider petCollider = pet.GetComponent<Collider>();
            if (petCollider != null)
            {
                spawnPosition.y += petCollider.bounds.size.y;
            }
            else
            {
                spawnPosition.y += 1.5f; // 콜라이더가 없을 경우 기본 높이
            }
        }

        GameObject particleInstance = Instantiate(particlePrefab, spawnPosition, particlePrefab.transform.rotation);

        // 파티클이 펫을 따라다니도록 부모 설정
        // 부모를 pet.emotionOrigin 이나 targetTransform으로 설정할 수 있습니다.
        // 여기서는 기존 로직을 유지하여 petModelTransform을 부모로 설정합니다.
        Transform parentTransform = pet.petModelTransform != null ? pet.petModelTransform : pet.transform;
        particleInstance.transform.SetParent(parentTransform);
        // ▲▲▲ 여기까지 수정 ▲▲▲

        if (duration > 0)
        {
            Destroy(particleInstance, duration);
        }
        return particleInstance;
    }
    // 2. 파티클이 없으면 스프라이트 아이콘 확인
    else if (emotionSprites.TryGetValue(emotion, out Sprite emotionSprite))
    {
        EmotionBubble bubble = GetEmotionBubble();
        if (bubble != null)
        {
            // ▼▼▼ [수정] 말풍선 타겟 결정 로직 ▼▼▼
            // 1순위: PetEmotionController의 emotionOrigin이 설정되어 있으면 그것을 타겟으로 설정합니다.
            var emotionController = pet.GetComponent<PetEmotionController>();
            if (emotionController != null && emotionController.GetEmotionOrigin() != null)
            {
                bubble.SetTargetPet(emotionController.GetEmotionOrigin());
            }
            // 2순위: 없으면 기존처럼 petModelTransform을 타겟으로 설정합니다.
            else
            {
                Transform targetTransform = pet.petModelTransform != null ? pet.petModelTransform : pet.transform;
                bubble.SetTargetPet(targetTransform);
            }
            // ▲▲▲ 여기까지 수정 ▲▲▲
            bubble.ShowEmotion(emotionSprite, duration);
            return bubble.gameObject;
        }
    }
    else
    {
        Debug.LogWarning($"[EmotionManager] EmotionType '{emotion}'에 할당된 에셋(파티클/스프라이트)이 없습니다.");
    }

    return null;
}

    // 일정 시간 후 말풍선 풀에 반환
    private System.Collections.IEnumerator ReturnBubbleAfterDelay(EmotionBubble bubble, float delay)
    {
        yield return new WaitForSeconds(delay);
        ReturnBubbleToPool(bubble);
    }

    // 여러 감정 아이콘을 연속으로 표시하는 방법
    public void ShowEmotionSequence(PetController pet, EmotionType[] emotions, float[] durations)
    {
        if (emotions.Length != durations.Length)
        {
            Debug.LogError("감정과 지속 시간 배열의 길이가 일치해야 합니다.");
            return;
        }

        StartCoroutine(PlayEmotionSequence(pet, emotions, durations));
    }

    // 감정 시퀀스 코루틴
    // EmotionManager.cs - PlayEmotionSequence 코루틴
    private System.Collections.IEnumerator PlayEmotionSequence(PetController pet, EmotionType[] emotions, float[] durations)
    {
        for (int i = 0; i < emotions.Length; i++)
        {
            // 1. ShowPetEmotion은 이제 GameObject를 반환합니다.
            GameObject emotionObject = ShowPetEmotion(pet, emotions[i], durations[i]);

            // 2. 감정 표현이 지속 시간만큼 표시되도록 기다립니다.
            yield return new WaitForSeconds(durations[i]);

            // 3. 생성된 오브젝트가 말풍선(EmotionBubble)인 경우에만 풀에 반환합니다.
            if (emotionObject != null && emotionObject.TryGetComponent<EmotionBubble>(out EmotionBubble bubble))
            {
                // TryGetComponent가 성공하면 bubble 변수에 컴포넌트가 담깁니다.
                ReturnBubbleToPool(bubble);
            }
            // 파티클의 경우, ShowPetEmotion 내부에서 자동으로 Destroy되므로 여기서 추가 처리할 필요가 없습니다.
        }
    }
}