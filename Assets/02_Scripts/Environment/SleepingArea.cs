// SleepingArea.cs - 새로운 스크립트
using UnityEngine;

/// <summary>
/// 펫들의 수면 공간을 정의하는 컴포넌트입니다.
/// 이 공간이 어떤 서식지(Habitat)에 해당하는지 지정합니다.
/// </summary>
public class SleepingArea : MonoBehaviour
{
    [Tooltip("이 수면 공간이 해당하는 펫의 서식지(Habitat) 종류를 선택하세요.")]
    public PetAIProperties.Habitat habitatType;
}