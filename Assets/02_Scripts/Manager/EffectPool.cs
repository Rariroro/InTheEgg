using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 이펙트 오브젝트 풀링을 관리하는 싱글톤 매니저
/// 파티클 이펙트를 재사용하여 메모리 사용량을 줄이고 성능을 향상시킵니다
/// </summary>
public class EffectPool : MonoBehaviour
{
    private static EffectPool instance;
    public static EffectPool Instance
    {
        get
        {
            if (instance == null)
            {
                instance = FindObjectOfType<EffectPool>();
                if (instance == null)
                {
                    GameObject poolObject = new GameObject("EffectPool");
                    instance = poolObject.AddComponent<EffectPool>();
                    DontDestroyOnLoad(poolObject);
                }
            }
            return instance;
        }
    }

    [System.Serializable]
    public class PoolItem
    {
        [Tooltip("풀 아이템의 고유 이름")]
        public string name;

        [Tooltip("풀링할 프리팹")]
        public GameObject prefab;

        [Tooltip("초기 풀 크기")]
        public int poolSize = 10;

        [Tooltip("풀이 비었을 때 자동으로 확장할지 여부")]
        public bool autoExpand = true;
    }

    [Header("Pool Settings")]
    [SerializeField] private List<PoolItem> poolItems = new List<PoolItem>();

    private Dictionary<string, Queue<GameObject>> pools;
    private Dictionary<string, PoolItem> poolItemLookup;
    private Transform poolParent;

    void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
        DontDestroyOnLoad(gameObject);
        InitializePools();
    }

    /// <summary>
    /// 모든 풀을 초기화합니다
    /// </summary>
    private void InitializePools()
    {
        pools = new Dictionary<string, Queue<GameObject>>();
        poolItemLookup = new Dictionary<string, PoolItem>();

        // 풀 오브젝트들을 정리할 부모 오브젝트 생성
        poolParent = new GameObject("PooledObjects").transform;
        poolParent.SetParent(transform);

        foreach (var item in poolItems)
        {
            if (item.prefab == null)
            {
                Debug.LogError($"[EffectPool] Pool item '{item.name}'의 프리팹이 null입니다!");
                continue;
            }

            CreatePool(item);
        }

        Debug.Log($"[EffectPool] {pools.Count}개의 풀이 초기화되었습니다.");
    }

    /// <summary>
    /// 특정 아이템에 대한 풀을 생성합니다
    /// </summary>
    private void CreatePool(PoolItem item)
    {
        var queue = new Queue<GameObject>();
        poolItemLookup[item.name] = item;

        // 풀 아이템별 부모 오브젝트 생성
        Transform itemParent = new GameObject($"Pool_{item.name}").transform;
        itemParent.SetParent(poolParent);

        for (int i = 0; i < item.poolSize; i++)
        {
            GameObject obj = CreatePooledObject(item.prefab, itemParent);
            queue.Enqueue(obj);
        }

        pools[item.name] = queue;
        Debug.Log($"[EffectPool] '{item.name}' 풀 생성 완료 (크기: {item.poolSize})");
    }

    /// <summary>
    /// 풀링된 오브젝트를 생성합니다
    /// </summary>
    private GameObject CreatePooledObject(GameObject prefab, Transform parent)
    {
        GameObject obj = Instantiate(prefab, parent);
        obj.SetActive(false);

        // PooledObject 컴포넌트 추가 (자동 반환용)
        if (obj.GetComponent<PooledObject>() == null)
        {
            obj.AddComponent<PooledObject>();
        }

        return obj;
    }

    /// <summary>
    /// 풀에서 오브젝트를 가져옵니다
    /// </summary>
    /// <param name="effectName">가져올 이펙트 이름</param>
    /// <returns>활성화된 게임 오브젝트</returns>
    public GameObject Get(string effectName)
    {
        if (!pools.ContainsKey(effectName))
        {
            Debug.LogError($"[EffectPool] '{effectName}' 풀이 존재하지 않습니다!");
            return null;
        }

        GameObject obj = null;

        if (pools[effectName].Count > 0)
        {
            obj = pools[effectName].Dequeue();
        }
        else if (poolItemLookup.ContainsKey(effectName) && poolItemLookup[effectName].autoExpand)
        {
            // 풀이 비었고 자동 확장이 켜져있으면 새로 생성
            Debug.LogWarning($"[EffectPool] '{effectName}' 풀이 비었습니다. 새 오브젝트를 생성합니다.");
            Transform itemParent = poolParent.Find($"Pool_{effectName}");
            obj = CreatePooledObject(poolItemLookup[effectName].prefab, itemParent);
        }
        else
        {
            Debug.LogError($"[EffectPool] '{effectName}' 풀이 비었고 자동 확장이 비활성화되어 있습니다!");
            return null;
        }

        if (obj != null)
        {
            obj.SetActive(true);
            obj.transform.SetParent(null); // 풀 부모에서 분리

            // PooledObject 컴포넌트에 풀 이름 설정
            PooledObject pooledObj = obj.GetComponent<PooledObject>();
            if (pooledObj != null)
            {
                pooledObj.poolName = effectName;
            }
        }

        return obj;
    }

    /// <summary>
    /// 오브젝트를 풀로 반환합니다
    /// </summary>
    /// <param name="obj">반환할 오브젝트</param>
    /// <param name="effectName">반환할 풀 이름</param>
    public void Return(GameObject obj, string effectName)
    {
        if (obj == null) return;

        if (!pools.ContainsKey(effectName))
        {
            Debug.LogError($"[EffectPool] '{effectName}' 풀이 존재하지 않습니다! 오브젝트를 파괴합니다.");
            Destroy(obj);
            return;
        }

        // 오브젝트 초기화
        obj.SetActive(false);
        obj.transform.SetParent(poolParent.Find($"Pool_{effectName}"));
        obj.transform.position = Vector3.zero;
        obj.transform.rotation = Quaternion.identity;
        obj.transform.localScale = Vector3.one;

        // 파티클 시스템 초기화
        ParticleSystem ps = obj.GetComponent<ParticleSystem>();
        if (ps != null)
        {
            ps.Clear();
            ps.Stop();
        }

        pools[effectName].Enqueue(obj);
    }

    /// <summary>
    /// 지정된 시간 후 오브젝트를 자동으로 반환합니다
    /// </summary>
    public void ReturnAfterDelay(GameObject obj, string effectName, float delay)
    {
        StartCoroutine(ReturnAfterDelayCoroutine(obj, effectName, delay));
    }

    private System.Collections.IEnumerator ReturnAfterDelayCoroutine(GameObject obj, string effectName, float delay)
    {
        yield return new WaitForSeconds(delay);
        Return(obj, effectName);
    }

    /// <summary>
    /// 런타임에 새로운 풀을 추가합니다
    /// </summary>
    public void AddPool(string name, GameObject prefab, int poolSize = 10, bool autoExpand = true)
    {
        if (pools.ContainsKey(name))
        {
            Debug.LogWarning($"[EffectPool] '{name}' 풀이 이미 존재합니다!");
            return;
        }

        PoolItem newItem = new PoolItem
        {
            name = name,
            prefab = prefab,
            poolSize = poolSize,
            autoExpand = autoExpand
        };

        poolItems.Add(newItem);
        CreatePool(newItem);
    }

    /// <summary>
    /// 특정 풀의 현재 사용 가능한 오브젝트 수를 반환합니다
    /// </summary>
    public int GetAvailableCount(string effectName)
    {
        if (!pools.ContainsKey(effectName))
            return 0;

        return pools[effectName].Count;
    }

    /// <summary>
    /// 특정 이름의 풀이 존재하는지 확인합니다
    /// </summary>
    public bool HasPool(string effectName)
    {
        return pools != null && pools.ContainsKey(effectName);
    }

    /// <summary>
    /// 모든 풀의 상태를 디버그 로그로 출력합니다
    /// </summary>
    public void PrintPoolStatus()
    {
        Debug.Log("=== Effect Pool Status ===");
        foreach (var kvp in pools)
        {
            Debug.Log($"Pool '{kvp.Key}': {kvp.Value.Count} available");
        }
    }

    private void OnDestroy()
    {
        if (instance == this)
        {
            instance = null;
        }
    }
}

/// <summary>
/// 풀링된 오브젝트를 자동으로 관리하는 컴포넌트
/// </summary>
public class PooledObject : MonoBehaviour
{
    public string poolName;
    private float autoReturnTime = 0f;

    /// <summary>
    /// 자동 반환 시간을 설정합니다
    /// </summary>
    public void SetAutoReturn(float time)
    {
        autoReturnTime = time;
        if (autoReturnTime > 0)
        {
            Invoke(nameof(AutoReturn), autoReturnTime);
        }
    }

    private void AutoReturn()
    {
        if (EffectPool.Instance != null && !string.IsNullOrEmpty(poolName))
        {
            EffectPool.Instance.Return(gameObject, poolName);
        }
    }

    private void OnDisable()
    {
        CancelInvoke();
    }
}