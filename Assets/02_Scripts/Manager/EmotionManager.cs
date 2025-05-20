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
    Defeat      // 패배
}

// 감정 아이콘 관리 클래스
public class EmotionManager : MonoBehaviour
{
    // 싱글톤 패턴 구현
    public static EmotionManager Instance { get; private set; }
    
    [System.Serializable]
    public class EmotionIconData
    {
        public EmotionType emotionType;
        public Sprite iconSprite;
    }
    
    // Inspector에서 설정 가능한 감정 아이콘 목록
    public List<EmotionIconData> emotionIcons = new List<EmotionIconData>();
    
    // 감정 아이콘 캐싱용 딕셔너리
    private Dictionary<EmotionType, Sprite> emotionSprites = new Dictionary<EmotionType, Sprite>();
    
    // 말풍선 프리팹
    public GameObject emotionBubblePrefab;
    
    // 감정 말풍선 풀
    private Queue<EmotionBubble> bubblePool = new Queue<EmotionBubble>();
    private int poolSize = 10;  // 기본 풀 크기
    
    private void Awake()
    {
        // 싱글톤 패턴 적용
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
            
            // 아이콘 딕셔너리 초기화
            InitializeEmotionIcons();
            
            // 말풍선 풀 초기화
            InitializeBubblePool();
        }
        else
        {
            Destroy(gameObject);
        }
    }
    
    // 감정 아이콘 초기화
    private void InitializeEmotionIcons()
    {
        emotionSprites.Clear();
        foreach (var iconData in emotionIcons)
        {
            if (iconData.iconSprite != null)
            {
                emotionSprites[iconData.emotionType] = iconData.iconSprite;
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
    
    // 감정 표현 보여주기 (PetController에서 호출)
    public EmotionBubble ShowPetEmotion(PetController pet, EmotionType emotion, float duration = 10f)
    {
        if (pet == null)
        {
            Debug.LogWarning("펫이 null입니다. 감정을 표시할 수 없습니다.");
            return null;
        }
        
        // 해당 감정 아이콘 찾기
        Sprite emotionSprite = null;
        if (!emotionSprites.TryGetValue(emotion, out emotionSprite))
        {
            Debug.LogWarning($"감정 타입 '{emotion}'에 대한 스프라이트를 찾을 수 없습니다.");
        }
        
        // 말풍선 가져오기
        EmotionBubble bubble = GetEmotionBubble();
        if (bubble != null)
        {
            // 타겟 펫 설정
            Transform targetTransform = pet.petModelTransform != null ? pet.petModelTransform : pet.transform;
            bubble.SetTargetPet(targetTransform);
            
            // 감정 표시
            bubble.ShowEmotion(emotionSprite, duration);
            
            // 지정된 시간 후에 풀에 반환
            if (duration > 0)
            {
                StartCoroutine(ReturnBubbleAfterDelay(bubble, duration));
            }
            
            return bubble;
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
    private System.Collections.IEnumerator PlayEmotionSequence(PetController pet, EmotionType[] emotions, float[] durations)
    {
        for (int i = 0; i < emotions.Length; i++)
        {
            EmotionBubble bubble = ShowPetEmotion(pet, emotions[i], durations[i]);
            yield return new WaitForSeconds(durations[i]);
            
            if (bubble != null)
            {
                ReturnBubbleToPool(bubble);
            }
        }
    }
}