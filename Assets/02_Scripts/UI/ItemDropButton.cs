// Button.cs 수정 버전
using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine.EventSystems;

public class ItemDropButton : MonoBehaviour
{
    [Header("아이템 드롭 설정")]
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
    
    private Dictionary<string, GameObject> itemPrefabs = new Dictionary<string, GameObject>();
    private Dictionary<string, int> itemCounts = new Dictionary<string, int>();
    private Dictionary<string, Button> itemButtons = new Dictionary<string, Button>();
    
    private bool waitingForDropLocation = false;
    private string currentItemType = "";
    
    void Start()
    {
        // 아이템 프리팹 딕셔너리 초기화
        itemPrefabs["meat"] = meatPrefab;
        itemPrefabs["fish"] = fishPrefab;
        itemPrefabs["fruit"] = fruitPrefab;
        itemPrefabs["vegetable"] = vegetablePrefab;
        
        // ItemSelectionManager에서 아이템 정보 가져오기
        if (ItemSelectionManager.Instance != null)
        {
            foreach (var item in ItemSelectionManager.Instance.selectedItems)
            {
                if (item.count > 0)
                {
                    itemCounts[item.itemType] = item.count;
                    CreateItemButton(item.itemType, item.count);
                }
            }
        }
        
        // 피드백 텍스트 숨기기
        if (feedbackText != null)
        {
            feedbackText.gameObject.SetActive(false);
        }
    }
    
    void CreateItemButton(string itemType, int count)
    {
        if (itemButtonPrefab == null || itemButtonContainer == null)
        {
            Debug.LogError("아이템 버튼 프리팹 또는 컨테이너가 설정되지 않았습니다!");
            return;
        }
        
        GameObject buttonObj = Instantiate(itemButtonPrefab, itemButtonContainer);
        Button button = buttonObj.GetComponent<Button>();
        TMP_Text buttonText = buttonObj.GetComponentInChildren<TMP_Text>();
        
        // 버튼 색상 설정
        Image buttonImage = buttonObj.GetComponent<Image>();
        if (buttonImage != null)
        {
            switch (itemType)
            {
                case "meat": buttonImage.color = new Color(1f, 0.5f, 0.5f); break;
                case "fish": buttonImage.color = new Color(0.5f, 0.5f, 1f); break;
                case "fruit": buttonImage.color = new Color(0.5f, 1f, 0.5f); break;
                case "vegetable": buttonImage.color = new Color(1f, 1f, 0.5f); break;
            }
        }
        
        // 버튼 텍스트 설정
string displayName = itemType; // GetItemDisplayName(itemType) 대신
        if (buttonText != null)
        {
            buttonText.text = $"{displayName} ({count})";
        }
        
        // 버튼 이벤트 설정
        button.onClick.AddListener(() => OnItemButtonClicked(itemType));
        
        itemButtons[itemType] = button;
    }
    
   
    
    void OnItemButtonClicked(string itemType)
    {
        // 안전성 검사 추가
        if (string.IsNullOrEmpty(itemType))
        {
            Debug.LogError("아이템 타입이 비어있습니다!");
            return;
        }
        
        if (!itemCounts.ContainsKey(itemType))
        {
            Debug.LogError($"아이템 타입 '{itemType}'이 itemCounts에 없습니다!");
            return;
        }
        
        if (waitingForDropLocation)
        {
            // 이미 대기중이면 취소
            CancelDropMode();
        }
        else if (itemCounts[itemType] > 0)
        {
            // 드롭 모드 활성화
            waitingForDropLocation = true;
            currentItemType = itemType;
            
            Debug.Log($"아이템 드롭 모드 활성화: {itemType}");
            
            if (feedbackText != null)
            {
                feedbackText.text = "아이템을 놓을 곳을 선택하세요";
                feedbackText.gameObject.SetActive(true);
            }
        }
    }
    
    void Update()
    {
        // UI 위에서의 터치 무시
        bool isOverUI = EventSystem.current.IsPointerOverGameObject();
        if (Input.touchCount > 0)
        {
            isOverUI = EventSystem.current.IsPointerOverGameObject(Input.GetTouch(0).fingerId);
        }
        
        if (isOverUI) return;
        
        // 드롭 위치 선택 대기중일 때
        if (waitingForDropLocation && Input.GetMouseButtonDown(0))
        {
            LayerMask terrainMask = LayerMask.GetMask("Terrain");
            Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
            RaycastHit hit;
            
            if (Physics.Raycast(ray, out hit, Mathf.Infinity, terrainMask))
            {
                DropItem(hit.point);
            }
        }
    }
    
   // DropItem 메서드 수정 (아이템 생성 부분)
void DropItem(Vector3 dropPosition)
{
    Debug.Log($"[DropItem] currentItemType='{currentItemType}'");

    // (기존 검증 코드는 동일)
    if (string.IsNullOrEmpty(currentItemType) 
        || !itemPrefabs.ContainsKey(currentItemType) 
        || !itemCounts.ContainsKey(currentItemType))
    {
        Debug.LogError($"DropItem 방어: '{currentItemType}'이 유효하지 않음");
        CancelDropMode();
        return;
    }

    string itemTypeToProcess = currentItemType;

    GameObject prefab;
    if (!itemPrefabs.TryGetValue(itemTypeToProcess, out prefab) || prefab == null)
    {
        Debug.LogError($"아이템 프리팹이 없거나 할당되지 않음: '{itemTypeToProcess}'");
        CancelDropMode();
        return;
    }

    // (3) 아이템 생성 처리
    Vector3 spawnPosition = dropPosition + Vector3.up * dropHeight;
    GameObject item = Instantiate(prefab, spawnPosition, Quaternion.identity);
    
    // ★ 추가: Food 태그 설정 및 PetFeedingController 캐시에 등록
    item.tag = "Food";
    PetFeedingController.RegisterFoodItem(item);
    
    // ★ 추가: 아이템이 삭제될 때 캐시에서도 제거되도록 컴포넌트 추가
    FoodItem foodComponent = item.AddComponent<FoodItem>();
    foodComponent.Initialize();
    
    Rigidbody rb = item.GetComponent<Rigidbody>();
    if (rb == null) rb = item.AddComponent<Rigidbody>();
    rb.mass = 1f;
    rb.linearDamping = 0.5f;
    rb.angularDamping = 0.5f;
    rb.AddTorque(Random.insideUnitSphere * dropForce, ForceMode.Impulse);

    // (나머지 코드는 동일)
    if (itemCounts.ContainsKey(itemTypeToProcess))
    {
        itemCounts[itemTypeToProcess]--;
        UpdateItemButton(itemTypeToProcess);
        Debug.Log($"아이템 드롭 완료: {itemTypeToProcess}, 남은 개수: {itemCounts[itemTypeToProcess]}");
    }
    else
    {
        Debug.LogError($"개수 감소 도중 '{itemTypeToProcess}' 키를 찾을 수 없음");
        CancelDropMode();
        return;
    }

    CancelDropMode();

    if (itemCounts.ContainsKey(itemTypeToProcess) && itemCounts[itemTypeToProcess] <= 0)
    {
        if (itemButtons.ContainsKey(itemTypeToProcess))
        {
            Destroy(itemButtons[itemTypeToProcess].gameObject);
            itemButtons.Remove(itemTypeToProcess);
        }
        itemCounts.Remove(itemTypeToProcess);
        Debug.Log($"아이템 '{itemTypeToProcess}' 개수가 0이므로 버튼과 딕셔너리에서 제거함");
    }
}

    
    void UpdateItemButton(string itemType)
    {
        if (itemButtons.ContainsKey(itemType))
        {
            TMP_Text buttonText = itemButtons[itemType].GetComponentInChildren<TMP_Text>();
            if (buttonText != null)
            {
string displayName = itemType; // GetItemDisplayName(itemType) 대신
                buttonText.text = $"{displayName} ({itemCounts[itemType]})";
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
        
        Debug.Log("아이템 드롭 모드 취소됨");
    }
    
    // ESC 키로 취소할 수 있도록 추가
    void LateUpdate()
    {
        if (waitingForDropLocation && Input.GetKeyDown(KeyCode.Escape))
        {
            CancelDropMode();
        }
    }
}