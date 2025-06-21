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
        itemPrefabs["Grain"] = GrainPrefab;
        itemPrefabs["Grass"] = GrassPrefab;
        itemPrefabs["hay"] = hayPrefab;

        // ItemSelectionManager에서 아이템 정보 가져오기
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

        if (feedbackText != null)
        {
            feedbackText.gameObject.SetActive(false);
        }
    }

    void CreateItemButton(string itemType, int count)
    {
        if (itemButtonPrefab == null || itemButtonContainer == null) return;

        GameObject buttonObj = Instantiate(itemButtonPrefab, itemButtonContainer);
        Button button = buttonObj.GetComponent<Button>();
        TMP_Text buttonText = buttonObj.GetComponentInChildren<TMP_Text>();
        Image buttonImage = buttonObj.GetComponent<Image>();

        if (buttonImage != null)
        {
             switch (itemType)
            {
                case "meat": buttonImage.color = new Color(1f, 0.5f, 0.5f); break;
                case "fish": buttonImage.color = new Color(0.5f, 0.5f, 1f); break;
                case "fruit": buttonImage.color = new Color(0.5f, 1f, 0.5f); break;
                case "vegetable": buttonImage.color = new Color(1f, 1f, 0.5f); break;
                case "Grain": buttonImage.color = new Color(0.8f, 0.7f, 0.5f); break;
                case "Grass": buttonImage.color = new Color(0.2f, 0.8f, 0.2f); break;
                case "hay": buttonImage.color = new Color(0.7f, 0.6f, 0.2f); break;
            }
        }
        
        if (buttonText != null)
        {
            buttonText.text = $"{itemType} ({count})";
        }
        
        button.onClick.AddListener(() => OnItemButtonClicked(itemType));
        itemButtons[itemType] = button;
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

        CancelDropMode();
    }

    void UpdateItemButton(string itemType)
    {
        if (itemButtons.TryGetValue(itemType, out Button button))
        {
            TMP_Text buttonText = button.GetComponentInChildren<TMP_Text>();
            if (buttonText != null)
            {
                buttonText.text = $"{itemType} ({itemCounts[itemType]})";
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
}