# PreferredZone 시스템 설정 가이드

## 개요
펫들이 자신의 서식지 타입에 맞는 구역을 선호하여 더 오래 머물고, 자주 방문하도록 하는 시스템입니다.

## Unity 에디터 설정 방법

### 1. PreferredZone GameObject 생성

#### 원형 구역 (Sphere) - 물 구역 예시
1. Hierarchy에서 빈 GameObject 생성
2. 이름을 "PreferredZone_Water"로 변경
3. **PreferredZone** 컴포넌트 추가
4. Inspector에서 설정:
   - **구역 모양**: Sphere
   - **서식지 타입**: Water
   - **구역 반경**: 15-20 (연못 크기에 맞게)
   - **행동 지속 시간 배율**: 1.5
5. 연못/호수 중앙에 배치

#### 사각형 구역 (Box) - 울타리 구역 예시
1. Hierarchy에서 빈 GameObject 생성
2. 이름을 "PreferredZone_Fence"로 변경
3. **PreferredZone** 컴포넌트 추가
4. Inspector에서 설정:
   - **구역 모양**: Box
   - **서식지 타입**: Fence
5. **Box Collider 추가** 버튼 클릭 (자동으로 추가됨)
6. Box Collider 크기 조정:
   - Size: (30, 5, 20) - 울타리 영역에 맞게
7. 울타리 영역에 배치

#### 숲 구역 (Forest Zone)
1. GameObject 생성 → "PreferredZone_Forest"
2. **PreferredZone** 컴포넌트 추가
3. 설정:
   - **Habitat Type**: Forest
   - **Zone Radius**: 10-15
   - **Behavior Duration Multiplier**: 1.5
4. 나무가 많은 지역에 배치

#### 들판 구역 (Field Zone)
1. GameObject 생성 → "PreferredZone_Field"
2. **PreferredZone** 컴포넌트 추가
3. 설정:
   - **Habitat Type**: Field
   - **Zone Radius**: 20-30 (넓은 영역)
   - **Behavior Duration Multiplier**: 1.5
4. 열린 공간에 배치

#### 나무 구역 (Tree Zone)
1. GameObject 생성 → "PreferredZone_Tree"
2. **PreferredZone** 컴포넌트 추가
3. 설정:
   - **Habitat Type**: Tree
   - **Zone Radius**: 5-8 (작은 영역)
   - **Behavior Duration Multiplier**: 2.0 (나무 오르기 펫은 더 오래)
4. 큰 나무 주변에 배치

### 2. 선택적 설정 - 성격 기반 선호

게으른 펫을 위한 휴식 구역 예시:
1. GameObject 생성 → "PreferredZone_RestArea"
2. **PreferredZone** 컴포넌트 추가
3. 설정:
   - **Habitat Type**: Field (기본값)
   - **Use Personality Preference**: ✅ 체크
   - **Preferred Personalities**: Lazy 추가
   - **Zone Radius**: 10
   - **Behavior Duration Multiplier**: 2.0
5. 편안한 장소에 배치

### 3. Gizmo로 구역 확인

1. Scene 뷰에서 **Gizmos** 버튼이 켜져 있는지 확인
2. PreferredZone GameObject 선택 시:
   - 초록색 반투명 구체로 구역 범위 표시
   - 상단에 "Preferred Zone: [타입]" 라벨 표시

### 4. 테스트 방법

#### Play Mode에서 확인
1. Play 버튼 클릭
2. Scene 뷰에서 관찰할 펫 선택
3. 확인 사항:
   - 물고기/오리 → PreferredZone_Water 주변에 자주 이동
   - 원숭이 → PreferredZone_Tree 선호
   - 토끼/햄스터 → PreferredZone_Field 선호

#### 실시간 조정
Play Mode 중에도 조정 가능:
- **Zone Radius**: 구역 크기 실시간 변경
- **Behavior Duration Multiplier**: 머무는 시간 조정
- 위치 이동: Transform 컴포넌트로 구역 이동

### 5. 디버그 로그 활성화

코드의 주석 처리된 Debug.Log를 활성화하려면:
1. `WanderActivity.cs` 열기
2. 다음 라인들의 주석 제거:
   - 259번 줄: 선호 구역 내 행동 시간 증가 로그
   - 406번 줄: 선호 구역 방향 이동 로그
   - 532번 줄: 가장 가까운 선호 구역 변경 로그

### 6. 여러 구역 배치 팁

- **중첩 가능**: 구역들이 겹쳐도 됨
- **다양한 크기**: 환경에 맞게 크기 조정
- **밀도 조절**: 너무 많으면 펫이 혼란스러워할 수 있음
- **권장 개수**: 씬당 3-5개 정도가 적당

## 작동 원리

1. **펫의 행동 결정**:
   - 70% 확률로 선호 구역 방향 이동
   - 30% 확률로 일반 랜덤 이동

2. **구역 내 행동**:
   - 행동 지속 시간이 설정된 배율만큼 증가
   - 예: 1.5배면 평소보다 50% 더 오래 머묾

3. **우선순위**:
   - 가장 가까운 선호 구역을 자동 선택
   - 펫의 서식지 타입과 일치하는 구역만 고려

## 확장 아이디어

1. **시간대별 선호도**: 낮/밤에 따라 다른 구역 선호
2. **계절별 변화**: 계절에 따라 구역 속성 변경
3. **특수 이벤트**: 특정 구역에서만 발생하는 상호작용

## 문제 해결

### 펫이 구역을 무시하는 경우
- PreferredZone의 Habitat Type이 펫의 habitat과 일치하는지 확인
- Zone Radius가 너무 작지 않은지 확인
- NavMesh가 구역까지 연결되어 있는지 확인

### 펫이 구역에서 나가지 않는 경우
- Behavior Duration Multiplier가 너무 높지 않은지 확인 (2.0 이하 권장)
- 다른 Activity의 우선순위 확인

---

*구현 완료: PreferredZone.cs, WanderActivity.cs 수정*