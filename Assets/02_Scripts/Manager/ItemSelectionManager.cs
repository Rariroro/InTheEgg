// ItemSelectionManager.cs
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

[System.Serializable]
public class ItemData
{
    public string itemType; // "meat", "fish", "fruit", "vegetable"
    public int count;
    
    public ItemData(string type, int cnt)
    {
        itemType = type;
        count = cnt;
    }
}

public class ItemSelectionManager : MonoBehaviour
{
    public static ItemSelectionManager Instance { get; private set; }
    
    // 선택된 아이템 데이터
    public List<ItemData> selectedItems = new List<ItemData>();
    
    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
            InitializeItems();
        }
        else
        {
            Destroy(gameObject);
        }
    }
    
    private void InitializeItems()
    {
        selectedItems.Clear();
        selectedItems.Add(new ItemData("meat", 0));
        selectedItems.Add(new ItemData("fish", 0));
        selectedItems.Add(new ItemData("fruit", 0));
        selectedItems.Add(new ItemData("vegetable", 0));
    }
    
    public void SetItemCount(string itemType, int count)
    {
        ItemData item = selectedItems.Find(x => x.itemType == itemType);
        if (item != null)
        {
            item.count = Mathf.Clamp(count, 0, 99);
        }
    }
    
    public int GetItemCount(string itemType)
    {
        ItemData item = selectedItems.Find(x => x.itemType == itemType);
        return item != null ? item.count : 0;
    }
    
    public int GetTotalItemCount()
    {
        int total = 0;
        foreach (var item in selectedItems)
        {
            total += item.count;
        }
        return total;
    }
    
    public void ClearSelectedItems()
    {
        InitializeItems();
    }
}