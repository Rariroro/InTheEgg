# 펫 상호작용 시스템 리팩토링 계획

## 현재 시스템 분석 완료
- **거리 기반 체크**: 0.5초마다 모든 펫 쌍 거리 계산 (O(n²) 복잡도)
- **공간 분할 그리드**: 20f 크기 셀로 최적화 시도했으나 여전히 성능 문제
- **상호작용 흐름**: PetInteractionManager → BasePetInteraction → 각 Interaction 구현체

## 콜라이더 기반 새로운 시스템 설계

### 1. **PetInteractionDetector** (새 컴포넌트)
- PetController에 추가될 콜라이더 기반 감지 시스템
- SphereCollider를 Trigger로 사용 (반경 5f)
- OnTriggerEnter/Stay/Exit로 근처 펫 실시간 추적
- HashSet으로 범위 내 펫 목록 관리

### 2. **PetInteractionManager** (간소화)
- 거리 계산 로직 완전 제거
- 콜라이더 이벤트 기반으로 상호작용 관리
- 쿨다운과 상태 체크만 담당
- 싱글톤 패턴 유지

### 3. **코드 구조 개선**
```
Manager/
  └── PetInteractionManager.cs (간소화)
Pet/
  ├── Core/
  │   └── PetInteractionDetector.cs (새 파일)
  └── Interaction/ (변경 없음)
      ├── BasePetInteraction.cs
      └── 각 상호작용 구현체들...
```

## 주요 변경사항

### 1. **PetInteractionDetector.cs 생성**
- 콜라이더 이벤트 처리
- 근처 펫 목록 관리
- 상호작용 가능 체크 및 시작

### 2. **PetInteractionManager.cs 수정**
- 공간 분할 그리드 제거
- 거리 계산 코루틴 제거
- 상호작용 쿨다운/상태 관리만 유지

### 3. **PetController.cs 수정**
- PetInteractionDetector 컴포넌트 추가
- Awake()에서 초기화

## 성능 개선 예상
- O(n²) → O(1) 복잡도 (물리 엔진이 처리)
- CPU 사용량 대폭 감소
- 실시간 반응성 향상

## 구현 순서
1. PetInteractionDetector.cs 생성
2. PetInteractionManager.cs 간소화
3. PetController.cs에 Detector 통합
4. 테스트 및 디버깅