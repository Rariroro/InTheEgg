# InTheEgg - AI 협업 가이드
- 한글로 소통

## 프로젝트 개요
Unity 기반의 펫 시뮬레이션 게임으로, 다양한 동물 펫들이 AI를 통해 자율적으로 행동하고 상호작용하는 프로젝트입니다.

### 게임 플로우
1. **PetChoice 씬**: 플레이어가 펫, 환경, 아이템을 선택하는 초기 설정 화면
2. **PetVillage 씬**: 선택한 펫들이 AI로 자율 행동하며 생활하는 메인 게임 씬

### 주요 특징
- 다중 펫 선택 시스템 (토글 기반 UI)
- 환경 커스터마이징 (다양한 환경 오브젝트 선택 가능)
- 아이템 시스템 (펫들이 상호작용할 수 있는 아이템)

## 기술 스택
- **Unity Version**: 6000.0.32
- **Render Pipeline**: URP (Universal Render Pipeline)
- **주요 시스템**:
  - NavMesh 기반 AI 네비게이션
  - 행동 기반 AI 시스템 (Action-based AI)
  - 감정 표현 시스템
  - 펫 간 상호작용 시스템

## 프로젝트 구조
```
Assets/
├── 01_Scenes/          # 게임 씬 파일들
├── 02_Scripts/         # 모든 C# 스크립트
│   ├── Pet/           # 펫 관련 핵심 스크립트
│   ├── Manager/       # 게임 매니저들
│   ├── UI/            # UI 관련 스크립트
│   │   ├── PetSelectionUI.cs      # 펫 선택 UI
│   │   ├── EnvironmentSelectionUI.cs # 환경 선택 UI
│   │   └── ItemSelectionUI.cs     # 아이템 선택 UI
│   └── Environment/   # 환경 상호작용
├── 03_Prefabs/        # 프리팹 파일들
├── 04_Arts/           # 아트 리소스
└── 08_ThirdParty/     # 서드파티 에셋
```

## 코드 스타일 가이드

- 우선순위 기반의 상태 머신(Priority-Based State Machine) , PetController.cs에 모든 행동을 결정하는 중앙 통제 메서드를 고려

### 명명 규칙
- **Classes**: PascalCase (예: `PetController`)
- **Public 변수**: PascalCase (예: `PetType`)
- **Private 변수**: camelCase 또는 _camelCase (예: `_currentAction`)
- **메서드**: PascalCase (예: `UpdateAI()`)
- **상수**: UPPER_SNAKE_CASE (예: `SLEEPY_EMOTION_INTERVAL`)

### 주석 규칙
- 한글 주석 사용 가능
- 복잡한 로직은 반드시 주석 추가
- TODO 주석 형식: `// TODO: 설명`

## 주요 시스템 설명

### 1. Pet AI 시스템
- **핵심 클래스**: `PetController.cs`
- **행동 시스템**: `IPetAction` 인터페이스 기반
- **우선순위**: 각 Action은 `GetPriority()` 메서드로 우선순위 반환

### 2. 감정 표현 시스템
- **매니저**: `EmotionManager.cs`
- **감정 타입**: `EmotionType` enum
- **표현 방식**: 말풍선(EmotionBubble) 또는 파티클

### 3. 상호작용 시스템
- **매니저**: `PetInteractionManager.cs`
- **상호작용 타입**: 머리 박치기, 경주, 싸움 등

## 빌드 및 테스트

### 빌드 설정
- **iOS**: Bundle ID: `com.eunga0110.InTheEgg`
- **Android**: 최소 SDK 23
- **Scripting Backend**: IL2CPP

### 테스트 방법
1. Unity Editor에서 Play Mode 테스트
2. Device Simulator로 모바일 테스트
3. 실제 디바이스 빌드 및 테스트

## 디버깅 팁

### 로그 확인
- 펫 AI 상태: `[AI]` 태그로 로그 출력
- 상호작용: `[Interaction]` 태그
- 에러: `[PetController]` 등 클래스명 태그

### 일반적인 문제 해결
1. **NavMesh 문제**: 펫이 움직이지 않을 때
   - Window > AI > Navigation에서 NavMesh 베이크 확인
   - `agent.isOnNavMesh` 체크

2. **애니메이션 문제**: 애니메이션이 재생되지 않을 때
   - Animator Controller 연결 확인
   - Animation 파라미터 이름 확인

3. **상호작용 문제**: 펫들이 상호작용하지 않을 때
   - PetInteractionManager 인스턴스 확인
   - 충돌체(Collider) 설정 확인

## 자주 사용하는 명령어

### Git 관련
```bash
# 현재 브랜치 상태 확인
git status

# 변경사항 확인
git diff

# 최근 커밋 로그
git log --oneline -10
```

### Unity 관련
- 캐시 클리어: `Library` 폴더 삭제 후 재시작
- 프로젝트 설정 리셋: `ProjectSettings` 폴더 백업 후 재설정

## 추가 참고사항

### 성능 최적화
- 펫이 많을 때는 AI 업데이트 간격(`_aiUpdateInterval`) 조정
- LOD 그룹 사용으로 원거리 펫 최적화
- 오브젝트 풀링으로 감정 표현 오브젝트 재사용

### 확장 가능한 부분
1. 새로운 펫 행동 추가: `IPetAction` 구현
2. 새로운 감정 타입: `EmotionType` enum 확장
3. 새로운 상호작용: `BasePetInteraction` 상속

## 연락처 및 도움말
- 프로젝트 관련 질문이나 버그 리포트는 이슈 트래커를 사용해주세요.
- AI 도구 사용 시 이 문서를 참고하여 프로젝트 컨텍스트를 이해할 수 있습니다.

## AI 도구 사용 가이드
- **중요**: 이 프로젝트에서 AI 도구를 사용할 때는 반드시 한글로 답변해야 합니다.
- 코드 주석도 한글로 작성합니다.
- 기술적인 용어는 필요시 영어 병기 가능합니다.