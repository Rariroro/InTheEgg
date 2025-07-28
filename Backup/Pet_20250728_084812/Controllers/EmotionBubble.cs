using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class EmotionBubble : MonoBehaviour
{
    [Header("말풍선 설정")]
    public Transform targetPet;           // 따라다닐 펫의 Transform
    public Vector3 offset = new Vector3(0, 3f, 0);  // 펫으로부터의 위치 오프셋
    public float followSpeed = 10f;       // 따라다니는 속도
    public float lifetimeSeconds = 3f;    // 자동으로 사라지는 시간 (0이면 수동으로 비활성화해야 함)
    
    [Header("말풍선 UI 요소")]
    public Image bubbleBackground;        // 말풍선 배경 이미지
    public Image emotionIcon;             // 감정 아이콘 이미지
    public TextMeshProUGUI bubbleText;    // 말풍선 텍스트 (옵션)
    
    private Canvas canvas;                
    private CanvasScaler canvasScaler;
    private bool autoHide = true;         // 자동 숨김 여부
    
    private void Awake()
    {
        canvas = GetComponentInParent<Canvas>();
        canvasScaler = GetComponentInParent<CanvasScaler>();
        
        // 기본적으로 비활성화
        gameObject.SetActive(false);
    }
    
    // 말풍선 표시 메서드
    public void ShowEmotion(Sprite emotionSprite,  float duration = -1f)
    {
        // 감정 아이콘 설정
        if (emotionIcon != null && emotionSprite != null)
        {
            emotionIcon.sprite = emotionSprite;
            emotionIcon.enabled = true;
        }
        
       
        
        // 기존 코루틴 중지하고 새로 시작
        StopAllCoroutines();
        
        // 말풍선 활성화
        gameObject.SetActive(true);
        
        // 지속 시간 설정 (기본값 사용 또는 전달된 값 사용)
        float finalDuration = (duration > 0) ? duration : lifetimeSeconds;
        
        // 자동 숨김 설정된 경우 코루틴 시작
        if (autoHide && finalDuration > 0)
        {
            StartCoroutine(HideAfterDelay(finalDuration));
        }
    }
    
    // 일정 시간 후 말풍선 숨기기
    private IEnumerator HideAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);
        gameObject.SetActive(false);
    }
    
    // 말풍선 즉시 숨기기
    public void HideEmotion()
    {
        StopAllCoroutines();
        gameObject.SetActive(false);
    }
    
    private void LateUpdate()
    {
        if (targetPet == null || !gameObject.activeSelf) 
            return;
            
        // 1. 위치 업데이트 - 펫 위치 + 오프셋에 말풍선 위치
        Vector3 targetPosition = targetPet.position + offset;
        transform.position = Vector3.Lerp(transform.position, targetPosition, Time.deltaTime * followSpeed);
        
        // 2. 회전 업데이트 - 항상 카메라를 향하도록
        if (Camera.main != null)
        {
            transform.LookAt(transform.position + Camera.main.transform.rotation * Vector3.forward,
                Camera.main.transform.rotation * Vector3.up);
        }
    }
    
    // 타겟 펫 설정 메서드
    public void SetTargetPet(Transform pet)
    {
        targetPet = pet;
        if (pet != null)
            transform.position = pet.position + offset; // 초기 위치 설정
    }
}