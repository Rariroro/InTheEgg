using UnityEngine;

/// <summary>
/// 펫 컨트롤러들의 공통 인터페이스
/// 모든 펫 관련 컨트롤러는 이 인터페이스를 구현해야 합니다.
/// </summary>
public interface IPetController
{
    /// <summary>
    /// 컨트롤러 초기화
    /// PetController의 Awake()에서 호출됩니다.
    /// </summary>
    /// <param name="petController">부모 PetController 참조</param>
    void Init(PetController petController);
    
    /// <summary>
    /// 컨트롤러가 활성화되어 있는지 여부
    /// </summary>
    bool IsActive { get; }
}

/// <summary>
/// 펫 컨트롤러 기본 구현을 제공하는 추상 클래스
/// </summary>
public abstract class PetControllerBase : MonoBehaviour, IPetController
{
    protected PetController petController;
    protected bool isInitialized = false;
    
    public virtual bool IsActive => isInitialized && enabled;
    
    public virtual void Init(PetController controller)
    {
        petController = controller;
        isInitialized = true;
        OnInitialize();
    }
    
    /// <summary>
    /// 파생 클래스에서 추가 초기화 로직을 구현할 수 있습니다.
    /// </summary>
    protected virtual void OnInitialize()
    {
        // 파생 클래스에서 필요시 오버라이드
    }
}