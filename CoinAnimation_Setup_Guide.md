# 코인 애니메이션 설정 가이드

## 개요
보물찾기에서 코인을 획득할 때 코인이 날아가는 애니메이션 효과를 설정하는 방법입니다.

## 1. CoinFlyAnimation 설정

### 방법 1: 프리팹으로 설정 (권장)
1. **Hierarchy**에서 빈 GameObject 생성: `Create Empty > CoinFlyAnimation`
2. **CoinFlyAnimation.cs** 스크립트 추가
3. **Inspector**에서 설정:
   - **코인 설정**
     - `Coin Prefab`: 코인 UI 프리팹 (없으면 자동으로 기본 원 생성)
     - `Coin Sprite`: 코인 이미지 스프라이트
     - `Coins Per Animation`: 5 (한 번에 날아갈 코인 개수)
     - `Coin Spawn Delay`: 0.1 (각 코인 생성 간격)

   - **애니메이션 설정**
     - `Fly Duration`: 1 (코인이 날아가는 시간)
     - `Curve Height`: 100 (곡선 높이)
     - `Start Scale`: 1 (시작 크기)
     - `End Scale`: 0.5 (도착 크기)
     - `Rotation Speed`: 360 (회전 속도)

   - **효과**
     - `Pulse Scale`: 1.5 (펄스 크기 - 1.1~2.0 권장)
     - `Pulse Duration`: 0.4 (펄스 시간 - 0.3~0.5초 권장)
     - `Coin Sound`: 코인 획득 사운드
     - `Arrival Sound`: 도착 사운드

   - **타겟 설정** (중요!)
     - `Coin Target Image`: Canvas에서 코인 이미지 UI 오브젝트 드래그
     - `Coin Target Text`: 코인 텍스트 (이미지가 없을 때 대체용)

4. GameObject를 **Prefab**으로 저장: `Assets/03_Prefabs/Managers/CoinFlyAnimation.prefab`

### 방법 2: 자동 생성
- 아무 설정 없이도 코드가 자동으로 CoinFlyAnimation을 생성합니다
- 하지만 코인 이미지나 사운드 등을 설정할 수 없으므로 권장하지 않습니다

## 2. TreasureHuntManager 설정

1. **PetVillage 씬**에서 `TreasureHuntManager` 오브젝트 선택
2. **Inspector**에서 다음 필드 설정:
   - `Total Coins Text`: 코인 수를 표시하는 텍스트 UI (코인 개수 표시용)

## 3. 코인 UI 구조 예시

```
Canvas
├── TopUI
│   └── CoinPanel
│       ├── CoinImage (Image 컴포넌트) ← CoinFlyAnimation의 Coin Target Image에 연결
│       └── CoinText (TMP_Text) ← TreasureHuntManager의 Total Coins Text에 연결
```

## 4. 코인 이미지 만들기

1. **Canvas** 아래에 UI > Image 생성
2. 이름을 `CoinImage`로 변경
3. **Sprite**에 코인 이미지 할당
4. 적절한 위치에 배치 (보통 화면 상단)
5. **CoinFlyAnimation**의 `Coin Target Image` 필드에 드래그 (중요!)

## 5. 테스트 방법

1. 보물찾기 시작
2. 펫이 보물을 찾아서 내려놓을 때까지 대기
3. 보물을 터치/클릭 → 코인이 보물 위치에서 코인 UI로 날아감
4. 모든 보물을 찾으면 성공 팝업 표시
5. 팝업의 닫기 버튼 클릭 → 보너스 코인이 화면 중앙에서 코인 UI로 날아감

## 6. 문제 해결

### 코인이 날아가지 않는 경우
- `Coin Target Image` 또는 `Coin Target Text`가 CoinFlyAnimation에 설정되었는지 확인
- Canvas가 씬에 존재하는지 확인
- Console에서 에러 메시지 확인

### 코인이 이상한 곳으로 날아가는 경우
- 코인 UI 오브젝트의 Anchor 설정 확인
- Canvas의 Render Mode 확인 (Screen Space - Overlay 권장)

### 펄스 효과가 보이지 않는 경우
- Console에서 `[CoinFlyAnimation] 펄스 효과 시작` 로그 확인
- 로그에 "타겟: 코인 이미지"가 표시되는지 확인
- `Coin Target Image`가 CoinFlyAnimation에 설정되었는지 확인
- `Pulse Scale`을 1.5 이상으로 증가
- `Pulse Duration`을 0.4초 이상으로 증가

### 펄스 효과가 텍스트에 적용되는 경우
- `Coin Target Image`가 CoinFlyAnimation에 제대로 설정되었는지 확인
- 이미지 오브젝트가 없으면 생성하여 연결

## 7. 커스터마이징

### 코인 개수 조절
- `Coins Per Animation`: 더 많은 코인이 날아가게 하려면 값 증가

### 애니메이션 속도
- `Fly Duration`: 값을 줄이면 더 빠르게 날아감
- `Coin Spawn Delay`: 값을 줄이면 코인이 더 빠르게 연속 생성

### 경로 모양
- `Curve Height`: 값을 늘리면 더 큰 포물선을 그리며 날아감

### 펄스 효과 조절
- `Pulse Scale`: 1.5 권장 (1.1~2.0 범위, 클수록 더 크게 커짐)
- `Pulse Duration`: 0.4초 권장 (0.1~1.0 범위, 길수록 천천히 효과 적용)
- 여러 코인이 동시에 도착하면 첫 번째 코인만 펄스 효과 적용 (중복 방지)

## 8. 설정 요약

### 필수 설정 (한 곳에서만!)
**CoinFlyAnimation 컴포넌트**:
- `Coin Target Image`: 코인 이미지 UI 오브젝트 연결 (코인이 날아갈 목표)
- `Coin Sprite`: 날아갈 코인 스프라이트 (선택사항)
- `Pulse Scale`: 1.5 (권장)
- `Pulse Duration`: 0.4 (권장)

**TreasureHuntManager 컴포넌트**:
- `Total Coins Text`: 코인 개수 표시 텍스트 UI (표시용만)

### 주요 개선사항
1. **중복 제거**: CoinFlyAnimation에서만 타겟 설정 (이전에는 두 곳에서 설정)
2. **싱글톤 개선**: 프리팹 기반으로도 사용 가능
3. **펄스 효과**: 코인 이미지에 적용 (커졌다가 원래 크기로)
4. **팝업 이벤트**: 닫기 버튼 클릭을 더 안정적으로 감지

## 9. 코드 사용 예시

```csharp
// 월드 좌표에서 코인 애니메이션 시작 (보물 수집)
CoinFlyAnimation.Instance.PlayCoinAnimation(treasurePosition, coinAmount, () => {
    // 애니메이션 완료 후 실행할 코드
});

// 스크린 좌표에서 코인 애니메이션 시작 (팝업)
Vector3 screenCenter = new Vector3(Screen.width / 2f, Screen.height / 2f, 0);
CoinFlyAnimation.Instance.PlayCoinAnimationFromScreen(screenCenter, bonusAmount, () => {
    // 애니메이션 완료 후 실행할 코드
});
```