// ItemSelectionUI.cs
using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using TMPro;

public class ItemSelectionUI : MonoBehaviour
{
    [Header("아이템 UI 프리팹")]
    public GameObject itemUIprefab; // 각 아이템의 UI 프리팹
    public Transform itemContainer; // UI가 배치될 부모 오브젝트
    public TMP_Text totalItemsText; // 총 아이템 개수 표시
    
    [System.Serializable]
    public class ItemUIData
    {
        public string itemType;
        public Color itemColor;
    }
    
    [Header("아이템 설정")]
    public List<ItemUIData> itemConfigs = new List<ItemUIData>()
    {
        new ItemUIData { itemType = "meat", itemColor = Color.red },
        new ItemUIData { itemType = "fish",  itemColor = Color.blue },
        new ItemUIData { itemType = "fruit",  itemColor = Color.green },
        new ItemUIData { itemType = "vegetable",  itemColor = Color.yellow }
    };
    
    private Dictionary<string, TMP_Text> countTexts = new Dictionary<string, TMP_Text>();
    
    void Start()
    {
        // ItemSelectionManager 초기화
        if (ItemSelectionManager.Instance == null)
        {
            GameObject managerObj = new GameObject("ItemSelectionManager");
            managerObj.AddComponent<ItemSelectionManager>();
        }
        
        // 이전에 선택된 아이템 초기화
        ItemSelectionManager.Instance.ClearSelectedItems();
        
        // UI 생성
        CreateItemUIs();
        
        // 총 개수 업데이트
        UpdateTotalText();
    }
    
    void CreateItemUIs()
    {
        foreach (var config in itemConfigs)
        {
            GameObject itemUI = Instantiate(itemUIprefab, itemContainer);
            
            // 아이템 이름 설정
            TMP_Text nameText = itemUI.transform.Find("ItemName").GetComponent<TMP_Text>();
            if (nameText != null)
            {
                nameText.text = config.itemType; 
                nameText.color = config.itemColor;
            }
            
            // 카운트 텍스트 참조 저장
            TMP_Text countText = itemUI.transform.Find("CountText").GetComponent<TMP_Text>();
            if (countText != null)
            {
                countText.text = "0";
                countTexts[config.itemType] = countText;
            }
            
            // 버튼 이벤트 설정
            Button plusButton = itemUI.transform.Find("PlusButton").GetComponent<Button>();
            Button minusButton = itemUI.transform.Find("MinusButton").GetComponent<Button>();
            
            string itemType = config.itemType; // 클로저용 변수
            
            if (plusButton != null)
            {
                plusButton.onClick.AddListener(() => OnPlusButtonClicked(itemType));
            }
            
            if (minusButton != null)
            {
                minusButton.onClick.AddListener(() => OnMinusButtonClicked(itemType));
            }
        }
    }
    
    void OnPlusButtonClicked(string itemType)
    {
        int currentCount = ItemSelectionManager.Instance.GetItemCount(itemType);
        if (currentCount < 99)
        {
            ItemSelectionManager.Instance.SetItemCount(itemType, currentCount + 1);
            UpdateItemCountUI(itemType);
            UpdateTotalText();
        }
    }
    
    void OnMinusButtonClicked(string itemType)
    {
        int currentCount = ItemSelectionManager.Instance.GetItemCount(itemType);
        if (currentCount > 0)
        {
            ItemSelectionManager.Instance.SetItemCount(itemType, currentCount - 1);
            UpdateItemCountUI(itemType);
            UpdateTotalText();
        }
    }
    
    void UpdateItemCountUI(string itemType)
    {
        if (countTexts.ContainsKey(itemType))
        {
            int count = ItemSelectionManager.Instance.GetItemCount(itemType);
            countTexts[itemType].text = count.ToString();
        }
    }
    
    void UpdateTotalText()
    {
        if (totalItemsText != null)
        {
            int total = ItemSelectionManager.Instance.GetTotalItemCount();
            totalItemsText.text = $"총 아이템: {total}개";
        }
    }
}