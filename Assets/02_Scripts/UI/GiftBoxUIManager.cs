using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using TMPro;
using System.Collections;

public class GiftBoxUIManager : MonoBehaviour
{
    [Header("UI 요소")]
    public GameObject giftButtonPrefab; // 선물 버튼 프리팹
    public Transform buttonContainer; // 버튼들이 배치될 부모 오브젝트
    
    [Header("카메라 설정")]
    public Camera mainCamera; // 메인 카메라
    public float cameraMoveDuration = 1f; // 카메라 이동 시간
    public float cameraZoomDistance = 10f; // 펫 선물 상자로부터의 카메라 거리
    public float cameraHeight = 8f; // 펫 카메라 높이
    
    [Header("환경 카메라 설정")]
    public float environmentCameraZoomDistance = 20f; // 환경 선물 상자로부터의 카메라 거리
    public float environmentCameraHeight = 15f; // 환경 카메라 높이
    
    // 선물 타입별 아이콘
    [System.Serializable]
    public class GiftTypeIcon
    {
        public string giftType; // "pet" 또는 "environment"
        public Sprite icon;
    }
    
    [Header("아이콘 설정")]
    public List<GiftTypeIcon> giftTypeIcons = new List<GiftTypeIcon>();
    
    // 활성화된 선물 상자들을 추적
    private Dictionary<string, List<GameObject>> activeGifts = new Dictionary<string, List<GameObject>>();
    private Dictionary<string, GameObject> giftButtons = new Dictionary<string, GameObject>();
    
    // 매니저 참조
    private PetManager petManager;
    private EnvironmentManager environmentManager;
    private TreasureHuntManager treasureHuntManager;
    private LegendaryPet.LegendaryPetManager legendaryPetManager;
    
    // 카메라 관련 변수
    private GameObject cameraParent; // CameraController 오브젝트 참조
    private CameraController cameraController; // CameraController 컴포넌트 참조
    private bool isCameraMoving = false;
    private bool originalLimitCameraMovement; // 원래 limitCameraMovement 값 저장

    // 버튼 클릭 직전 카메라 상태 저장
    private Vector3 lastCameraPosition;
    private Quaternion lastCameraRotation;
    private float lastCameraFOV;
    private Vector3 lastCameraParentPosition;
    
    // 싱글톤
    private static GiftBoxUIManager instance;
    public static GiftBoxUIManager Instance
    {
        get
        {
            if (instance == null)
            {
                instance = FindObjectOfType<GiftBoxUIManager>();
            }
            return instance;
        }
    }
    
    private void Awake()
    {
        instance = this;
        
        // CameraController의 자식 카메라 찾기
        if (mainCamera == null)
        {
            cameraController = FindObjectOfType<CameraController>();
            if (cameraController != null)
            {
                cameraParent = cameraController.gameObject;
                mainCamera = cameraController.GetComponentInChildren<Camera>();
                if (mainCamera == null)
                {
                    Debug.LogWarning("CameraController에서 자식 카메라를 찾을 수 없습니다. Camera.main 사용");
                    mainCamera = Camera.main;
                }
            }
            else
            {
                mainCamera = Camera.main;
            }
        }
    }
    
    private void Start()
    {
        // 매니저 찾기
        petManager = FindObjectOfType<PetManager>();
        environmentManager = FindObjectOfType<EnvironmentManager>();
        treasureHuntManager = TreasureHuntManager.Instance;
        legendaryPetManager = LegendaryPet.LegendaryPetManager.Instance;
        
        // 초기 카메라 상태를 lastCamera 변수에 저장 (첫 번째 버튼 클릭 전까지의 기본값)
        if (mainCamera != null)
        {
            lastCameraPosition = mainCamera.transform.position;
            lastCameraRotation = mainCamera.transform.rotation;
            lastCameraFOV = mainCamera.fieldOfView; // FOV 값 저장

            // 카메라 부모 오브젝트 위치도 저장
            if (cameraParent != null)
            {
                lastCameraParentPosition = cameraParent.transform.position;
            }
        }
        
        // 선물 타입별 리스트 초기화
        activeGifts["pet"] = new List<GameObject>();
        activeGifts["environment"] = new List<GameObject>();
        activeGifts["treasure"] = new List<GameObject>();  // 보물 타입 추가
        activeGifts["legendary"] = new List<GameObject>();  // 레전드 펫 타입 추가
        
        // 주기적으로 선물 상자 체크
        InvokeRepeating(nameof(UpdateGiftBoxes), 0.5f, 0.5f);
        
        // TreasureHuntManager의 이벤트 구독
        if (treasureHuntManager != null)
        {
            treasureHuntManager.OnDroppedTreasureUpdate += OnTreasureCountUpdated;
        }
    }
    
    // 선물 상자 등록
    public void RegisterGiftBox(GameObject giftBox, string giftType)
    {
        if (!activeGifts.ContainsKey(giftType))
        {
            activeGifts[giftType] = new List<GameObject>();
        }
        
        activeGifts[giftType].Add(giftBox);
        UpdateGiftButton(giftType);
    }
    
    // 선물 상자 제거
    public void UnregisterGiftBox(GameObject giftBox, string giftType)
    {
        if (activeGifts.ContainsKey(giftType))
        {
            activeGifts[giftType].Remove(giftBox);
            UpdateGiftButton(giftType);
        }
    }
    
    // 선물 상자 업데이트 (매니저에서 직접 가져오기)
    private void UpdateGiftBoxes()
    {
        // 펫 선물 상자 업데이트
        if (petManager != null)
        {
            var petGifts = GetPetGiftBoxes();
            activeGifts["pet"].Clear();
            activeGifts["pet"].AddRange(petGifts);
            
            
            UpdateGiftButton("pet");
        }
        
        // 환경 선물 상자 업데이트
        if (environmentManager != null)
        {
            var envGifts = GetEnvironmentGiftBoxes();
            activeGifts["environment"].Clear();
            activeGifts["environment"].AddRange(envGifts);
            
            
            UpdateGiftButton("environment");
        }
        
        // 보물 업데이트
        if (treasureHuntManager != null)
        {
            var treasures = GetDroppedTreasures();
            activeGifts["treasure"].Clear();
            activeGifts["treasure"].AddRange(treasures);
            
            UpdateGiftButton("treasure");
        }
        
        // 레전드 펫 선물 상자 업데이트
        if (legendaryPetManager != null)
        {
            var legendaryGifts = GetLegendaryGiftBoxes();
            activeGifts["legendary"].Clear();
            activeGifts["legendary"].AddRange(legendaryGifts);
            
            UpdateGiftButton("legendary");
        }
    }
    
    // PetManager에서 선물 상자 가져오기
    private List<GameObject> GetPetGiftBoxes()
    {
        if (petManager != null)
        {
            return petManager.GetPendingGiftList();
        }
        
        return new List<GameObject>();
    }
    
    // EnvironmentManager에서 선물 상자 가져오기
    private List<GameObject> GetEnvironmentGiftBoxes()
    {
        if (environmentManager != null)
        {
            return environmentManager.GetPendingGiftList();
        }
        
        return new List<GameObject>();
    }
    
    // TreasureHuntManager에서 내려놓은 보물 가져오기
    private List<GameObject> GetDroppedTreasures()
    {
        if (treasureHuntManager != null)
        {
            return treasureHuntManager.GetDroppedTreasureList();
        }
        
        return new List<GameObject>();
    }
    
    // LegendaryPetManager에서 선물 상자 가져오기
    private List<GameObject> GetLegendaryGiftBoxes()
    {
        if (legendaryPetManager != null)
        {
            return legendaryPetManager.GetPendingGiftList();
        }
        
        return new List<GameObject>();
    }
    
    // 보물 개수 업데이트 이벤트 핸들러
    private void OnTreasureCountUpdated(int count)
    {
        // 보물 업데이트
        var treasures = GetDroppedTreasures();
        activeGifts["treasure"].Clear();
        activeGifts["treasure"].AddRange(treasures);
        
        UpdateGiftButton("treasure");
    }
    
    // 선물 버튼 업데이트
    private void UpdateGiftButton(string giftType)
    {
        int count = activeGifts[giftType].Count;
        
        // 선물이 없으면 버튼 제거
        if (count == 0)
        {
            if (giftButtons.ContainsKey(giftType))
            {
                Destroy(giftButtons[giftType]);
                giftButtons.Remove(giftType);
            }
            return;
        }
        
        // 버튼이 없으면 생성
        if (!giftButtons.ContainsKey(giftType))
        {
            CreateGiftButton(giftType);
        }
        
        // 카운트 업데이트
        UpdateButtonCount(giftType, count);
    }
    
    // 선물 버튼 생성
    private void CreateGiftButton(string giftType)
    {
        if (giftButtonPrefab == null || buttonContainer == null) return;
        
        GameObject buttonObj = Instantiate(giftButtonPrefab, buttonContainer);
        giftButtons[giftType] = buttonObj;
        
        // 아이콘 설정
        var icon = giftTypeIcons.Find(x => x.giftType == giftType);
        if (icon != null && icon.icon != null)
        {
            Image iconImage = buttonObj.transform.Find("Icon")?.GetComponent<Image>();
            if (iconImage != null)
            {
                iconImage.sprite = icon.icon;
            }
        }
        
        // 버튼 클릭 이벤트
        Button button = buttonObj.GetComponent<Button>();
        if (button != null)
        {
            string type = giftType; // 클로저용
            button.onClick.AddListener(() => OnGiftButtonClicked(type));
        }
        
    }
    
    // 버튼 카운트 업데이트
    private void UpdateButtonCount(string giftType, int count)
    {
        if (!giftButtons.ContainsKey(giftType)) return;
        
        GameObject buttonObj = giftButtons[giftType];
        // CountBackground의 자식인 CountText 찾기
        TMP_Text countText = buttonObj.transform.Find("CountBackground/CountText")?.GetComponent<TMP_Text>();
        
        if (countText != null)
        {
            countText.text = count.ToString();
        }
        
        // 개수가 0이면 CountBackground 숨기기
        Transform countBg = buttonObj.transform.Find("CountBackground");
        if (countBg != null)
        {
            countBg.gameObject.SetActive(count > 0);
        }
    }
    
    // 선물 버튼 클릭 시
    private void OnGiftButtonClicked(string giftType)
    {
        if (isCameraMoving) return;

        List<GameObject> gifts = activeGifts[giftType];
        if (gifts.Count == 0) return;

        // 유효한 선물 찾기
        GameObject targetGift = null;
        foreach (var gift in gifts)
        {
            if (gift != null)
            {
                targetGift = gift;
                break;
            }
        }

        if (targetGift != null)
        {
            // 버튼 클릭 직전의 카메라 상태 저장
            if (mainCamera != null)
            {
                lastCameraPosition = mainCamera.transform.position;
                lastCameraRotation = mainCamera.transform.rotation;
                lastCameraFOV = mainCamera.fieldOfView;

                if (cameraParent != null)
                {
                    lastCameraParentPosition = cameraParent.transform.position;
                }
            }

            StartCoroutine(MoveCameraToGift(targetGift, giftType));
        }
    }
    
    // 카메라를 선물 상자로 이동
    private IEnumerator MoveCameraToGift(GameObject gift, string giftType)
    {
        isCameraMoving = true;

        // CameraController의 limitCameraMovement 비활성화
        if (cameraController != null)
        {
            originalLimitCameraMovement = cameraController.limitCameraMovement;
            cameraController.limitCameraMovement = false;
        }

        Vector3 startPos = mainCamera.transform.position;
        Quaternion startRot = mainCamera.transform.rotation;
        float startFOV = mainCamera.fieldOfView; // 현재 FOV 값 저장
        Vector3 startParentPos = cameraParent != null ? cameraParent.transform.position : Vector3.zero;

        // 목표 위치 계산 (선물 타입에 따라 다른 거리 사용)
        Vector3 giftPos = gift.transform.position;
        float zoomDistance = (giftType == "environment") ? environmentCameraZoomDistance : cameraZoomDistance;
        float height = (giftType == "environment") ? environmentCameraHeight : cameraHeight;
        Vector3 offset = new Vector3(0, height, -zoomDistance);
        Vector3 targetPos = giftPos + offset;

        // 선물을 바라보는 회전 계산
        Quaternion targetRot = Quaternion.LookRotation(giftPos - targetPos);

        // 카메라 부모를 선물 위치로 이동시킬 목표 위치 (선물의 x, z 좌표만 사용)
        Vector3 targetParentPos = new Vector3(giftPos.x, startParentPos.y, giftPos.z);

        float elapsed = 0f;
        while (elapsed < cameraMoveDuration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / cameraMoveDuration;

            // 부드러운 이동 (Ease In Out)
            t = t * t * (3f - 2f * t);

            // 카메라 부모 오브젝트도 부드럽게 이동
            if (cameraParent != null)
            {
                cameraParent.transform.position = Vector3.Lerp(startParentPos, targetParentPos, t);
            }

            mainCamera.transform.position = Vector3.Lerp(startPos, targetPos, t);
            mainCamera.transform.rotation = Quaternion.Slerp(startRot, targetRot, t);
            mainCamera.fieldOfView = Mathf.Lerp(startFOV, 60f, t); // FOV를 60으로 부드럽게 변경

            yield return null;
        }

        // 최종 위치 설정
        if (cameraParent != null)
        {
            cameraParent.transform.position = targetParentPos;
        }
        mainCamera.transform.position = targetPos;
        mainCamera.transform.rotation = targetRot;
        mainCamera.fieldOfView = 60f; // 최종 FOV 값 설정

        // 3초 후 버튼 클릭 직전 위치로 복귀
        yield return new WaitForSeconds(3f);

        // 버튼 클릭 직전 위치로 복귀 (저장된 last 위치 사용)
        yield return MoveCameraToPosition(lastCameraPosition, lastCameraRotation, lastCameraFOV, lastCameraParentPosition);

        // CameraController의 limitCameraMovement 원래 값으로 복원
        if (cameraController != null)
        {
            cameraController.limitCameraMovement = originalLimitCameraMovement;
        }

        isCameraMoving = false;
    }
    
    // 카메라를 특정 위치로 이동 (오버로드 - 부모 위치 없이)
    private IEnumerator MoveCameraToPosition(Vector3 targetPos, Quaternion targetRot, float targetFOV)
    {
        yield return MoveCameraToPosition(targetPos, targetRot, targetFOV, Vector3.zero, false);
    }

    // 카메라를 특정 위치로 이동 (부모 위치 포함)
    private IEnumerator MoveCameraToPosition(Vector3 targetPos, Quaternion targetRot, float targetFOV, Vector3 targetParentPos, bool moveParent = true)
    {
        Vector3 startPos = mainCamera.transform.position;
        Quaternion startRot = mainCamera.transform.rotation;
        float startFOV = mainCamera.fieldOfView;
        Vector3 startParentPos = cameraParent != null ? cameraParent.transform.position : Vector3.zero;

        float elapsed = 0f;
        while (elapsed < cameraMoveDuration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / cameraMoveDuration;

            // 부드러운 이동
            t = t * t * (3f - 2f * t);

            // 카메라 부모 오브젝트도 부드럽게 이동 (필요한 경우)
            if (moveParent && cameraParent != null)
            {
                cameraParent.transform.position = Vector3.Lerp(startParentPos, targetParentPos, t);
            }

            mainCamera.transform.position = Vector3.Lerp(startPos, targetPos, t);
            mainCamera.transform.rotation = Quaternion.Slerp(startRot, targetRot, t);
            mainCamera.fieldOfView = Mathf.Lerp(startFOV, targetFOV, t); // FOV도 부드럽게 변경

            yield return null;
        }

        // 최종 위치 설정
        if (moveParent && cameraParent != null)
        {
            cameraParent.transform.position = targetParentPos;
        }
        mainCamera.transform.position = targetPos;
        mainCamera.transform.rotation = targetRot;
        mainCamera.fieldOfView = targetFOV; // 최종 FOV 값 설정
    }
    
    private void OnDestroy()
    {
        CancelInvoke(nameof(UpdateGiftBoxes));
        
        // 이벤트 구독 해제
        if (treasureHuntManager != null)
        {
            treasureHuntManager.OnDroppedTreasureUpdate -= OnTreasureCountUpdated;
        }
    }
}