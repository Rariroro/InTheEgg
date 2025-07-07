using UnityEngine;

/// <summary>
/// 이 스크립트가 붙어있는 게임 오브젝트의 파티클 시스템이 끝나면 자동으로 오브젝트를 파괴합니다.
/// </summary>
[RequireComponent(typeof(ParticleSystem))]
public class ParticleAutoDestroy : MonoBehaviour
{
    private ParticleSystem ps;

    void Start()
    {
        ps = GetComponent<ParticleSystem>();

        // 파티클 시스템의 총 지속시간(duration + startLifetime) 후에 게임오브젝트를 파괴합니다.
        // main.duration은 파티클이 방출되는 시간, main.startLifetime은 각 파티클이 살아있는 최대 시간입니다.
        // Loop가 꺼져있을 때 가장 긴 파티클이 사라지는 시점에 맞춰 오브젝트가 파괴됩니다.
        Destroy(gameObject, ps.main.duration + ps.main.startLifetime.constantMax);
    }
}