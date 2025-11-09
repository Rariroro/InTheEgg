using System.Collections.Generic;
using UnityEngine;

namespace LegendaryPet
{
    /// <summary>
    /// 레전드 펫 선택을 관리하는 싱글톤 매니저
    /// PetSelectionManager와 동일한 구조로 작동
    ///
    /// [폴더 위치 참고]
    /// 이 파일은 Manager 폴더가 아닌 LegendaryPet 폴더에 위치합니다.
    /// 이유: LegendaryPet 네임스페이스 통일성 유지 및 LegendaryPetManager와의 강한 결합
    /// </summary>
    public class LegendaryPetSelectionManager : MonoBehaviour
    {
        // 싱글톤 패턴 구현
        public static LegendaryPetSelectionManager Instance { get; private set; }

        // 선택된 레전드 펫 ID 목록
        public List<string> selectedLegendaryPetIds = new List<string>();
        
        // 최초 등장으로 선택된 레전드 펫 ID 목록
        public List<string> firstAppearanceLegendaryIds = new List<string>();

        private void Awake()
        {
            // 싱글톤 처리
            if (Instance == null)
            {
                Instance = this;
                DontDestroyOnLoad(gameObject);
            }
            else
            {
                Destroy(gameObject);
            }
        }

        // 레전드 펫 ID 추가 메서드
        public void AddLegendaryPet(string legendaryPetId)
        {
            if (!selectedLegendaryPetIds.Contains(legendaryPetId))
            {
                selectedLegendaryPetIds.Add(legendaryPetId);
        // Debug.Log($"[LegendaryPetSelection] 레전드 펫 추가: {legendaryPetId}, 현재 선택된 레전드 펫: {selectedLegendaryPetIds.Count}개");
            }
        }

        // 레전드 펫 ID 제거 메서드
        public void RemoveLegendaryPet(string legendaryPetId)
        {
            if (selectedLegendaryPetIds.Contains(legendaryPetId))
            {
                selectedLegendaryPetIds.Remove(legendaryPetId);
                // 최초 등장 목록에서도 제거
                if (firstAppearanceLegendaryIds.Contains(legendaryPetId))
                {
                    firstAppearanceLegendaryIds.Remove(legendaryPetId);
                }
        // Debug.Log($"[LegendaryPetSelection] 레전드 펫 제거: {legendaryPetId}, 현재 선택된 레전드 펫: {selectedLegendaryPetIds.Count}개");
            }
        }

        // 최초 등장 레전드 펫 추가/제거 메서드
        public void SetLegendaryPetFirstAppearance(string legendaryPetId, bool isFirstAppearance)
        {
            if (isFirstAppearance)
            {
                if (!firstAppearanceLegendaryIds.Contains(legendaryPetId))
                {
                    firstAppearanceLegendaryIds.Add(legendaryPetId);
        // Debug.Log($"[LegendaryPetSelection] 레전드 펫 최초 등장 설정: {legendaryPetId}");
                }
            }
            else
            {
                if (firstAppearanceLegendaryIds.Contains(legendaryPetId))
                {
                    firstAppearanceLegendaryIds.Remove(legendaryPetId);
        // Debug.Log($"[LegendaryPetSelection] 레전드 펫 최초 등장 해제: {legendaryPetId}");
                }
            }
        }

        // 레전드 펫이 최초 등장인지 확인하는 메서드
        public bool IsLegendaryPetFirstAppearance(string legendaryPetId)
        {
            return firstAppearanceLegendaryIds.Contains(legendaryPetId);
        }

        // 선택된 모든 레전드 펫 초기화
        public void ClearSelectedLegendaryPets()
        {
            selectedLegendaryPetIds.Clear();
            firstAppearanceLegendaryIds.Clear();
        // Debug.Log("[LegendaryPetSelection] 선택된 모든 레전드 펫 초기화");
        }

        // 레전드 펫이 선택되었는지 확인
        public bool HasSelectedLegendaryPets()
        {
            return selectedLegendaryPetIds.Count > 0;
        }

        // 선택된 레전드 펫 수 반환
        public int GetSelectedCount()
        {
            return selectedLegendaryPetIds.Count;
        }
    }
}