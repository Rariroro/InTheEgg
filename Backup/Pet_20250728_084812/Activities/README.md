# Pet Activities 구조

Pet의 모든 활동(Activity)들을 카테고리별로 정리한 폴더입니다.

## 폴더 구조

### Basic/
기본적인 일상 활동들
- **WanderActivity**: 자유롭게 돌아다니기
- **SelectedActivity**: 선택되었을 때 카메라 바라보기
- **ClimbTreeActivity**: 나무 오르기 (Tree 서식지 펫 전용)

### Needs/
욕구 관련 활동들
- **EatActivity**: 먹이 찾아 먹기
- **SleepActivity**: 잠자리 찾아 잠자기
- **ExhaustedActivity**: 탈진 상태 (배고픔 100 이상)

### Emergency/
긴급 상황 대응 활동들
- **BeeEscapeActivity**: 벌 공격 시 도망가기

### Social/
사회적 상호작용 활동들
- **GatherActivity**: 플레이어 명령으로 모이기
- **InteractWithPetActivity**: 다른 펫과 상호작용

### Environment/
환경 관련 활동들
- **EnvironmentGatherActivity**: 새 환경이 생성될 때 축하하러 모이기

## Activity 우선순위 체계

1. **긴급 상황** (50~100)
   - BeeEscapeActivity: 100 (벌 공격 중)
   - ExhaustedActivity: 50 (탈진)

2. **플레이어 명령** (20~30)
   - SelectedActivity: 30 (선택됨, 일반)
   - GatherActivity: 20 (모이기 명령)

3. **환경 이벤트** (15)
   - EnvironmentGatherActivity: 15

4. **자율 행동** (0.1~6)
   - ClimbTreeActivity: 6 (나무 위)
   - SelectedActivity: 5.5 (선택됨, 나무 위)
   - EatActivity: 2 (먹는 중)
   - SleepActivity: 2 (자는 중)
   - InteractWithPetActivity: 1.5
   - EatActivity: 0.2~1.2 (배고픔 정도)
   - SleepActivity: 0.2~1.2 (졸림 정도)
   - ClimbTreeActivity: 0.3 (시작 확률)
   - WanderActivity: 0.1 (기본)