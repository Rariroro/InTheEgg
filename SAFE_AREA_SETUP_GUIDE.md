# iOS Safe Area 설정 가이드

iOS의 Dynamic Island(다이나믹 아일랜드), 노치, 화면 모서리 때문에 UI가 가려지는 문제를 해결하기 위한 가이드입니다.

## 문제 상황
- **iPhone 14 Pro 이상**: Dynamic Island 영역에 UI가 가려짐
- **iPhone X ~ 13**: 노치에 UI가 가려짐
- **모든 최신 iPhone**: 둥근 화면 모서리에 UI가 잘림

## 해결 방법

### 1단계: SafeAreaAdapter 스크립트 확인
이미 생성되었습니다: `/Assets/02_Scripts/UI/SafeAreaAdapter.cs`

### 2단계: Unity 에디터에서 Canvas 구조 수정

#### PetVillage 씬 (메인 게임 씬)
1. **PetVillage.unity** 씬을 엽니다
2. Hierarchy에서 **Canvas** 오브젝트를 찾습니다
3. Canvas를 우클릭 → Create Empty → 이름을 **"SafeArea"**로 변경
4. SafeArea 오브젝트를 선택합니다
5. Inspector에서 **Add Component** → `SafeAreaAdapter` 스크립트 추가
6. SafeArea의 RectTransform 설정:
   - **Anchors**: Stretch (0,0 ~ 1,1) - 스크립트가 자동 설정함
   - **Pivot**: (0.5, 0.5)
7. Canvas 아래의 **모든 UI 요소들**을 SafeArea 안으로 드래그해서 이동:
   - PetGatheringButton
   - ItemDropButton
   - TreasureHuntButton
   - 기타 모든 UI 오브젝트들

#### PetChoice 씬 (펫 선택 씬)
1. **PetChoice.unity** 씬을 엽니다
2. 위와 동일한 방법으로 Canvas 아래에 SafeArea 생성
3. SafeAreaAdapter 스크립트 추가
4. 모든 UI 요소를 SafeArea 안으로 이동

### 3단계: Canvas 프리팹 수정 (있는 경우)
`/Assets/03_Prefabs/UI/Canvas.prefab`를 열고 동일하게 적용합니다.

## 구조 예시

```
Canvas (Canvas 컴포넌트)
└── SafeArea (SafeAreaAdapter 스크립트 추가됨)
    ├── PetGatheringButton
    ├── ItemDropButton
    ├── TreasureHuntButton
    ├── BagButton
    └── 기타 모든 UI 오브젝트들
```

## 테스트 방법

### Unity 에디터에서 테스트
1. Game 뷰에서 **Free Aspect** 드롭다운 클릭
2. **iPhone 14 Pro**, **iPhone X**, **iPhone 15 Pro Max** 등 선택
3. Safe Area가 자동으로 적용되는지 확인
4. Console에서 `[SafeAreaAdapter] Safe Area 적용:` 로그 확인

### iOS 실제 기기에서 테스트
1. Xcode로 빌드
2. 실제 iPhone에서 실행
3. 상단 UI가 Dynamic Island/노치를 피하는지 확인
4. 화면을 회전해도 Safe Area가 자동으로 조정되는지 확인

## 주의사항

### ✅ 해야 할 것
- **모든 중요한 UI**는 SafeArea 안에 배치
- **Canvas Scaler**는 그대로 유지 (SafeAreaAdapter와 호환됨)
- 여러 씬이 있다면 **모든 씬의 Canvas**에 적용

### ❌ 하지 말아야 할 것
- SafeArea 밖에 중요한 버튼이나 텍스트 배치하지 않기
- Canvas에 SafeAreaAdapter를 추가하지 말 것 (Canvas의 **자식 오브젝트**에 추가)
- Safe Area를 감싸는 배경 이미지는 Canvas 바로 아래에 두기 (SafeArea 밖에)

## 배경 이미지 처리

게임 배경이나 전체 화면 이미지는 SafeArea **밖에** 두어야 합니다:

```
Canvas
├── BackgroundImage (전체 화면 배경 - SafeArea 밖)
└── SafeArea (SafeAreaAdapter 추가)
    ├── Button1
    └── Button2
```

이렇게 하면:
- **배경 이미지**: 화면 전체를 채움 (노치/Dynamic Island 포함)
- **UI 요소**: Safe Area 안에만 표시됨 (가려지지 않음)

## Apple 가이드라인 준수

Apple은 앱이 노치나 Dynamic Island를 검은 막대로 가리는 것을 금지합니다.
이 솔루션은 Apple의 가이드라인을 완전히 준수합니다.

## 지원 버전

- **Unity**: 2017.2.1 이상 (Screen.safeArea API 지원)
- **iOS**: iPhone X 이상 모든 기기
- **Dynamic Island**: iPhone 14 Pro 이상 자동 지원

## 문제 해결

### UI가 여전히 가려져요
1. SafeArea 오브젝트에 SafeAreaAdapter가 추가되었는지 확인
2. UI 요소들이 정말로 SafeArea의 **자식 오브젝트**인지 확인
3. Console에서 Safe Area 로그가 출력되는지 확인

### 화면 회전 시 UI가 이상해요
SafeAreaAdapter는 매 프레임 Screen.safeArea를 확인하므로 자동으로 조정됩니다.
문제가 있다면 SafeArea 오브젝트의 Anchors 설정을 확인하세요.

### 에디터에서는 정상인데 실제 기기에서 문제가 있어요
1. iOS 빌드 설정에서 **Auto Graphics API** 확인
2. Xcode에서 **Launch Screen** 설정 확인
3. 실제 기기의 iOS 버전이 최신인지 확인

## 추가 기능

### 런타임에서 Safe Area 강제 갱신
```csharp
var safeAreaAdapter = GetComponent<SafeAreaAdapter>();
safeAreaAdapter.RefreshSafeArea();
```

### 다른 스크립트에서 Safe Area 정보 접근
```csharp
Rect safeArea = Screen.safeArea;
Debug.Log($"Safe Area: {safeArea}");
```

---

문제가 해결되지 않으면 Unity의 Screen.safeArea API 문서를 참고하세요:
https://docs.unity3d.com/ScriptReference/Screen-safeArea.html
