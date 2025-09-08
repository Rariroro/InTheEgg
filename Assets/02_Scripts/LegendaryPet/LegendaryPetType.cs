using System;

namespace LegendaryPet
{
    /// <summary>
    /// 레전드 펫의 종류를 정의하는 열거형
    /// </summary>
    public enum LegendaryPetType
    {
        Dragon,    // 용
        Phoenix,   // 불사조
        Unicorn,   // 유니콘
        Griffin,   // 그리핀
        Pegasus,   // 페가수스
        Cerberus,  // 케르베로스
        Sphinx,    // 스핑크스
        Hydra      // 히드라
    }

    /// <summary>
    /// 레전드 펫의 특성을 정의하는 구조체
    /// </summary>
    [Serializable]
    public struct LegendaryPetTraits
    {
        public float moveSpeed;        // 이동 속도
        public float glowIntensity;     // 발광 강도
        public float particleAmount;    // 파티클 양
        public bool canFly;            // 비행 가능 여부
        public bool hasAura;           // 오라 효과 여부
        
        public static LegendaryPetTraits GetDefault(LegendaryPetType type)
        {
            var traits = new LegendaryPetTraits();
            
            switch (type)
            {
                case LegendaryPetType.Dragon:
                    traits.moveSpeed = 5f;
                    traits.glowIntensity = 1.5f;
                    traits.particleAmount = 100f;
                    traits.canFly = true;
                    traits.hasAura = true;
                    break;
                    
                case LegendaryPetType.Phoenix:
                    traits.moveSpeed = 6f;
                    traits.glowIntensity = 2f;
                    traits.particleAmount = 150f;
                    traits.canFly = true;
                    traits.hasAura = true;
                    break;
                    
                case LegendaryPetType.Unicorn:
                    traits.moveSpeed = 7f;
                    traits.glowIntensity = 1.2f;
                    traits.particleAmount = 80f;
                    traits.canFly = false;
                    traits.hasAura = true;
                    break;
                    
                case LegendaryPetType.Griffin:
                    traits.moveSpeed = 5.5f;
                    traits.glowIntensity = 1f;
                    traits.particleAmount = 60f;
                    traits.canFly = true;
                    traits.hasAura = false;
                    break;
                    
                case LegendaryPetType.Pegasus:
                    traits.moveSpeed = 8f;
                    traits.glowIntensity = 1.3f;
                    traits.particleAmount = 70f;
                    traits.canFly = true;
                    traits.hasAura = true;
                    break;
                    
                case LegendaryPetType.Cerberus:
                    traits.moveSpeed = 4.5f;
                    traits.glowIntensity = 0.8f;
                    traits.particleAmount = 50f;
                    traits.canFly = false;
                    traits.hasAura = false;
                    break;
                    
                case LegendaryPetType.Sphinx:
                    traits.moveSpeed = 3.5f;
                    traits.glowIntensity = 1.1f;
                    traits.particleAmount = 40f;
                    traits.canFly = false;
                    traits.hasAura = true;
                    break;
                    
                case LegendaryPetType.Hydra:
                    traits.moveSpeed = 3f;
                    traits.glowIntensity = 0.9f;
                    traits.particleAmount = 90f;
                    traits.canFly = false;
                    traits.hasAura = false;
                    break;
                    
                default:
                    traits.moveSpeed = 4f;
                    traits.glowIntensity = 1f;
                    traits.particleAmount = 50f;
                    traits.canFly = false;
                    traits.hasAura = false;
                    break;
            }
            
            return traits;
        }
    }
}