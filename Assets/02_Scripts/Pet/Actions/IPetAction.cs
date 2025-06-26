// IPetAction.cs
using UnityEngine;

/// <summary>
/// 펫의 모든 행동이 구현해야 하는 공통 인터페이스
/// </summary>
public interface IPetAction
{
    /// <summary>
    /// 이 행동을 지금 당장 수행하고 싶은 욕구/우선순위를 반환합니다. (0: 전혀 아님, 1: 매우 강함)
    /// </summary>
    float GetPriority();

    /// <summary>
    /// 행동이 시작될 때 한 번 호출됩니다. (상태 진입)
    /// </summary>
    void OnEnter();

    /// <summary>
    /// 행동이 활성화된 동안 매 프레임 호출됩니다. (상태 업데이트)
    /// </summary>
    void OnUpdate();

    /// <summary>
    /// 행동이 종료되거나 다른 행동으로 대체될 때 한 번 호출됩니다. (상태 탈출)
    /// </summary>
    void OnExit();
}