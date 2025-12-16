// ItemDropButton.cs - 최종 수정 버전
using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using TMPro;
using UnityEngine.EventSystems;

public class ItemDropButton : MonoBehaviour
{
    [Header("UI 및 드롭 설정")]
    public Transform itemButtonContainer;
    public GameObject itemButtonPrefab;
    public TMP_Text feedbackText;
    public float dropHeight = 10f;
    public float dropForce = 5f;

    [Header("아이템 프리팹")]
    public GameObject meatPrefab;
    public GameObject fishPrefab;
    public GameObject fruitPrefab;
    public GameObject vegetablePrefab;
    public GameObject GrainPrefab;
    public GameObject GrassPrefab;
    public GameObject hayPrefab;

    // 아이템 타입별 아이콘
    [System.Serializable]
    public class ItemTypeIcon
    {
        public string itemType;
        public Sprite icon;
    }

    [Header("아이콘 설정")]
    public List<ItemTypeIcon> itemTypeIcons = new List<ItemTypeIcon>();

    [Header("가방 버튼")]
    public Button bagButton;
    public Image bagBackground;           // Background 이미지
    public Sprite bagBackgroundEmpty;     // 투명 스프라이트 (아이템 없을 때)
    public Sprite bagBackgroundFilled;    // 색상 있는 스프라이트 (아이템 있을 때)
    public TMP_Text bagCountText;         // 가방의 CountBackground/CountText

    private Dictionary<string, GameObject> itemPrefabs = new Dictionary<string, GameObject>();
    private Dictionary<string, int> itemCounts = new Dictionary<string, int>();
    private Dictionary<string, Button> itemButtons = new Dictionary<string, Button>();

    private bool waitingForDropLocation = false;
    private string currentItemType = "";
    private bool isItemListVisible = false;

    void Start()
    {
        // 아이템 프리팹 딕셔너리 초기화
        itemPrefabs["meat"] = meatPrefab;
        itemPrefabs["fish"] = fishPrefab;
        itemPrefabs["fruit"] = fruitPrefab;
        itemPrefabs["vegetable"] = vegetablePrefab;
        itemPrefabs["Grain"] = GrainPrefab;
        itemPrefabs["Grass"] = GrassPrefab;
        itemPrefabs["hay"] = hayPrefab;

        // 가방 버튼 클릭 이벤트 연결
        if (bagButton != null)
        {
            bagButton.onClick.AddListener(ToggleBag);
        }

        // ItemButtonContainer 초기에 비활성화
        if (itemButtonContainer != null)
        {
            itemButtonContainer.gameObject.SetActive(false);
        }

        // Flutter 연동 환경인지 체크 (FlutterModeManager가 존재하면 Flutter 빌드)
        if (FlutterModeManager.Instance != null)
        {
            // Flutter 환경: 이벤트 구독 (데이터가 오면 로드)
            FlutterModeManager.Instance.OnFlutterDataReceived += OnFlutterDataReceived;

            // 이미 초기화되어 있으면 바로 로드 (늦게 Start된 경우)
            if (FlutterModeManager.Instance.IsInitialized)
            {
                LoadItemsFromSelectionManager();
            }
        }
        else
        {
            // 순수 Unity 빌드 모드: 기존 로직 (PetChoice 씬에서 선택한 데이터 사용)
            LoadItemsFromSelectionManager();
        }

        // 초기 가방 상태 업데이트
        UpdateBagState();

        if (feedbackText != null)
        {
            feedbackText.gameObject.SetActive(false);
        }
    }

    void OnDestroy()
    {
        if (FlutterModeManager.Instance != null)
        {
            FlutterModeManager.Instance.OnFlutterDataReceived -= OnFlutterDataReceived;
        }
    }

    private void OnFlutterDataReceived()
    {
        LoadItemsFromSelectionManager();
    }

    private void LoadItemsFromSelectionManager()
    {
        // 기존 버튼들 제거 (중복 방지)
        foreach (var button in itemButtons.Values)
        {
            if (button != null)
            {
                Destroy(button.gameObject);
            }
        }
        itemButtons.Clear();
        itemCounts.Clear();

        // 새로 로드
        if (ItemSelectionManager.Instance != null)
        {
            foreach (var item in ItemSelectionManager.Instance.selectedItems)
            {
                if (item.count > 0 && itemPrefabs.ContainsKey(item.itemType))
                {
                    itemCounts[item.itemType] = item.count;
                    CreateItemButton(item.itemType, item.count);
                }
            }
        }
        UpdateBagState();
    }

    void CreateItemButton(string itemType, int count)
    {
        if (itemButtonPrefab == null || itemButtonContainer == null) return;

        GameObject buttonObj = Instantiate(itemButtonPrefab, itemButtonContainer);
        Button button = buttonObj.GetComponent<Button>();

        // 아이콘 설정
        var icon = itemTypeIcons.Find(x => x.itemType == itemType);
        if (icon != null && icon.icon != null)
        {
            Image iconImage = buttonObj.transform.Find("Icon")?.GetComponent<Image>();
            if (iconImage != null)
            {
                iconImage.sprite = icon.icon;
            }
        }

        // 카운트 설정
        TMP_Text countText = buttonObj.transform.Find("CountBackground/CountText")?.GetComponent<TMP_Text>();
        if (countText != null)
        {
            countText.text = count.ToString();
        }

        button.onClick.AddListener(() => OnItemButtonClicked(itemType));
        itemButtons[itemType] = button;

        // 가방 상태 업데이트
        UpdateBagState();
    }

    void OnItemButtonClicked(string itemType)
    {
        if (string.IsNullOrEmpty(itemType) || !itemCounts.ContainsKey(itemType)) return;

        if (waitingForDropLocation)
        {
            CancelDropMode();
        }
        else if (itemCounts[itemType] > 0)
        {
            waitingForDropLocation = true;
            currentItemType = itemType;

            if (feedbackText != null)
            {
                feedbackText.text = "아이템을 놓을 곳을 선택하세요";
                feedbackText.gameObject.SetActive(true);
            }
        }
    }

    void Update()
    {
        if (EventSystem.current.IsPointerOverGameObject()) return;
        
        if (waitingForDropLocation && Input.GetMouseButtonDown(0))
        {
            Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
            if (Physics.Raycast(ray, out RaycastHit hit, Mathf.Infinity, LayerMask.GetMask("Terrain")))
            {
                DropItem(hit.point);
            }
        }
        
        if (waitingForDropLocation && Input.GetKeyDown(KeyCode.Escape))
        {
            CancelDropMode();
        }
    }

    void DropItem(Vector3 dropPosition)
    {
        if (!itemPrefabs.TryGetValue(currentItemType, out GameObject prefab) || prefab == null)
        {
            CancelDropMode();
            return;
        }

        Vector3 spawnPosition = dropPosition + Vector3.up * dropHeight;
        
        // ★★★★★ 코드 대폭 간소화 ★★★★★
        // 프리팹을 생성하기만 하면 됩니다.
        // FoodItem 컴포넌트 추가, foodType 설정, Initialize 호출 등의 코드가 모두 필요 없어졌습니다.
        GameObject item = Instantiate(prefab, spawnPosition, Quaternion.identity);

        // Rigidbody에 물리 효과만 적용 (프리팹에 Rigidbody가 이미 있어야 함)
        Rigidbody rb = item.GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.AddTorque(Random.insideUnitSphere * dropForce, ForceMode.Impulse);
        }
        
        // 개수 차감 및 UI 업데이트
        itemCounts[currentItemType]--;
        UpdateItemButton(currentItemType);

        if (itemCounts[currentItemType] <= 0)
        {
            Destroy(itemButtons[currentItemType].gameObject);
            itemButtons.Remove(currentItemType);
            itemCounts.Remove(currentItemType);
        }

        // 가방 상태 업데이트
        UpdateBagState();

        CancelDropMode();
    }

    void UpdateItemButton(string itemType)
    {
        if (itemButtons.TryGetValue(itemType, out Button button))
        {
            GameObject buttonObj = button.gameObject;
            TMP_Text countText = buttonObj.transform.Find("CountBackground/CountText")?.GetComponent<TMP_Text>();

            if (countText != null)
            {
                countText.text = itemCounts[itemType].ToString();
            }

            // 개수가 0이면 CountBackground 숨기기
            Transform countBg = buttonObj.transform.Find("CountBackground");
            if (countBg != null)
            {
                countBg.gameObject.SetActive(itemCounts[itemType] > 0);
            }
        }
    }

    void CancelDropMode()
    {
        waitingForDropLocation = false;
        currentItemType = "";

        if (feedbackText != null)
        {
            feedbackText.gameObject.SetActive(false);
        }
    }

    void ToggleBag()
    {
        if (!HasAnyItems()) return; // 아이템 없으면 무시

        isItemListVisible = !isItemListVisible;
        StartCoroutine(AnimateItemContainer(isItemListVisible));
    }

    void UpdateBagState()
    {
        bool hasItems = HasAnyItems();
        int totalItemCount = GetTotalItemCount();

        // Background 스프라이트 변경
        if (bagBackground != null)
        {
            bagBackground.sprite = hasItems ? bagBackgroundFilled : bagBackgroundEmpty;
        }

        // 버튼 활성화/비활성화
        if (bagButton != null)
        {
            bagButton.interactable = hasItems;
        }

        // 전체 아이템 개수 표시
        if (bagCountText != null)
        {
            bagCountText.text = totalItemCount.ToString();
        }

        // CountBackground 표시/숨김
        if (bagCountText != null && bagCountText.transform.parent != null)
        {
            bagCountText.transform.parent.gameObject.SetActive(totalItemCount > 0);
        }

        // 아이템 없으면 리스트 닫기
        if (!hasItems && isItemListVisible)
        {
            isItemListVisible = false;
            if (itemButtonContainer != null)
            {
                itemButtonContainer.gameObject.SetActive(false);
            }
        }
    }

    bool HasAnyItems()
    {
        if (itemCounts.Count == 0) return false;

        foreach (var count in itemCounts.Values)
        {
            if (count > 0) return true;
        }

        return false;
    }

    int GetTotalItemCount()
    {
        int total = 0;
        foreach (var count in itemCounts.Values)
        {
            total += count;
        }
        return total;
    }

    System.Collections.IEnumerator AnimateItemContainer(bool show)
    {
        if (itemButtonContainer == null) yield break;

        RectTransform rect = itemButtonContainer.GetComponent<RectTransform>();
        CanvasGroup canvasGroup = itemButtonContainer.GetComponent<CanvasGroup>();

        if (canvasGroup == null)
            canvasGroup = itemButtonContainer.gameObject.AddComponent<CanvasGroup>();

        itemButtonContainer.gameObject.SetActive(true);

        float duration = 0.3f;
        float elapsed = 0f;

        // 시작/끝 스케일 (아래에서 위로 펼쳐지는 효과)
        Vector3 startScale = show ? new Vector3(1, 0, 1) : Vector3.one;
        Vector3 endScale = show ? Vector3.one : new Vector3(1, 0, 1);

        float startAlpha = show ? 0f : 1f;
        float endAlpha = show ? 1f : 0f;

        // Pivot을 (0, 0)으로 설정하면 아래에서 위로 펼쳐짐
        Vector2 originalPivot = rect.pivot;
        rect.pivot = new Vector2(0, 0);

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / duration;
            t = t * t * (3f - 2f * t); // Smooth curve

            rect.localScale = Vector3.Lerp(startScale, endScale, t);
            canvasGroup.alpha = Mathf.Lerp(startAlpha, endAlpha, t);

            yield return null;
        }

        rect.localScale = endScale;
        canvasGroup.alpha = endAlpha;
        rect.pivot = originalPivot;

        if (!show)
        {
            itemButtonContainer.gameObject.SetActive(false);
        }
    }
}