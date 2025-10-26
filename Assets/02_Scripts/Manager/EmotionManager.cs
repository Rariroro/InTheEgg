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
    Disgusted,   // 역겨움
    Thought_Food_Meat,
    Thought_Food_Fish,
    Thought_Food_Grass,
    Thought_Food_Grain,
    Thought_Food_Fruit,
    Thought_Food_Vegetable,
    Thought_TresureHunt,  // 보물찾기 중 생각 (프리팹 이름과 일치)
    Tresure               // 보물 발견 (프리팹 이름과 일치)
}

// 감정 아이콘 관리 클래스
public class EmotionManager : MonoBehaviour
{
    // 싱글톤 패턴 구현
    public static EmotionManager Instance { get; private set; }

    [System.Serializable]
    public class EmotionAssetData
    {
        public EmotionType emotionType;
        public GameObject particlePrefab; // 파티클 효과용
    }

    [Header("Emotion Asset Settings")]
    [Tooltip("감정 타입별로 파티클(Prefab)을 설정합니다.")]
    public List<EmotionAssetData> emotionAssets = new List<EmotionAssetData>();

    private Dictionary<EmotionType, GameObject> emotionParticles = new Dictionary<EmotionType, GameObject>();

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
            InitializeEmotionAssets();
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private void InitializeEmotionAssets()
    {
        emotionParticles.Clear();
        foreach (var assetData in emotionAssets)
        {
            // 파티클 프리팹 캐싱
            if (assetData.particlePrefab != null)
            {
                emotionParticles[assetData.emotionType] = assetData.particlePrefab;
            }
        }
    }


    public GameObject ShowPetEmotion(PetController pet, EmotionType emotion, float duration = 10f)
    {
        if (pet == null) return null;

        // 파티클 프리팹이 있는지 확인
        if (emotionParticles.TryGetValue(emotion, out GameObject particlePrefab))
        {
            Vector3 spawnPosition;

            // 1순위: PetEmotionController의 emotionOrigin이 설정되어 있으면 그 위치를 사용합니다.
            var emotionController = pet.GetComponent<PetEmotionController>();
            if (emotionController != null && emotionController.GetEmotionOrigin() != null)
            {
                spawnPosition = emotionController.GetEmotionOrigin().position;
            }
            // 2순위: emotionOrigin이 없으면 기존처럼 콜라이더 기준으로 계산합니다.
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
            Transform parentTransform = pet.petModelTransform != null ? pet.petModelTransform : pet.transform;
            particleInstance.transform.SetParent(parentTransform);

            if (duration > 0)
            {
                Destroy(particleInstance, duration);
            }
            return particleInstance;
        }
        else
        {
            Debug.LogWarning($"[EmotionManager] EmotionType '{emotion}'에 할당된 파티클 프리팹이 없습니다.");
        }

        return null;
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
            // ShowPetEmotion은 GameObject를 반환합니다.
            GameObject emotionObject = ShowPetEmotion(pet, emotions[i], durations[i]);

            // 감정 표현이 지속 시간만큼 표시되도록 기다립니다.
            yield return new WaitForSeconds(durations[i]);

            // 파티클은 ShowPetEmotion 내부에서 자동으로 Destroy됩니다.
        }
    }
}