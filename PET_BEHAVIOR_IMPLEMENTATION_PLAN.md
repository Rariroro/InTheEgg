# 기존 구현을 활용한 펫 행동 다양화 기획

## 📋 현재 구현된 시스템

### **Activities (활동)**
1. **WanderActivity** - 배회 활동
2. **DivingActivity** - 다이빙 (Playful 전용)
3. **ClimbTreeActivity** - 나무 오르기
4. **SleepActivity** - 수면
5. **EatActivity** - 먹이 활동
6. **ExhaustedActivity** - 탈진 상태
7. **BeeEscapeActivity** - 벌 도망
8. **ButterflyPlayActivity** - 나비와 놀기
9. **EnvironmentGatherActivity** - 환경 오브젝트 모이기
10. **GatherActivity** - 모이기 명령
11. **InteractWithPetActivity** - 펫 간 상호작용

### **Interactions (상호작용)**
1. **ChaseAndRunInteraction** - 추격전
2. **RaceInteraction** - 경주
3. **FightInteraction** - 싸움
4. **WalkTogetherInteraction** - 함께 걷기
5. **RestAndSleepTogetherInteraction** - 함께 쉬기
6. **RideAndWalkInteraction** - 타고 걷기
7. **HeadbuttInteraction** - 박치기
8. **SlothKoalaRaceInteraction** - 느림보 경주
9. **CamelAlpacaSpitFightInteraction** - 침 뱉기 싸움
10. **ChameleonCamouflageInteraction** - 카멜레온 위장
11. **PredatorMoleInteraction** - 두더지 땅굴 도망
12. **PredatorPossumPrankInteraction** - 주머니쥐 죽은 척
13. **SkunkDefenseInteraction** - 스컹크 방어

---

## 🎯 성격 × 식성 × 서식지별 Activity 우선순위 조정

### **1. Lazy (게으른) 조합**

#### **Lazy + Carnivore (육식)**
**Activity 우선순위:**
- SleepActivity: 8.0 (매우 높음)
- RestAndSleepTogetherInteraction: 7.0
- WanderActivity: 2.0 (매우 낮음, 느린 속도)
- EatActivity: 5.0 (배고플 때만 활발)
- ChaseAndRunInteraction: 1.0 (거의 참여 안 함)

**특별 행동 패턴:**
- 사냥은 매복 위주 (최소 에너지 소비)
- 다른 펫이 잡은 먹이 뺏기 선호
- Tree 서식지: 나무 위에서 대기, 아래 지나가는 먹이만 노림
- Water 서식지: 물가에서 물고기 떠오르길 기다림
- 하루 20시간 이상 휴식

#### **Lazy + Herbivore (초식)**
**Activity 우선순위:**
- SleepActivity: 9.0
- RestAndSleepTogetherInteraction: 8.0
- EatActivity: 6.0 (한 자리에서 오래 먹기)
- WanderActivity: 1.5 (최소 이동)
- SlothKoalaRaceInteraction: 7.0 (느림보 경주 선호)

**특별 행동 패턴:**
- 이동하지 않고 주변 풀만 먹기
- Water 서식지: 물가에 누워서 물 마시기
- Forest 서식지: 떨어진 열매만 먹기
- 다른 펫이 가져다주는 음식 선호
- 하루 2-3번만 이동

#### **Lazy + Omnivore (잡식)**
**Activity 우선순위:**
- SleepActivity: 7.5
- EatActivity: 5.5 (가장 가까운 먹이)
- EnvironmentGatherActivity: 4.0 (환경 오브젝트 근처만)
- WanderActivity: 2.5

**특별 행동 패턴:**
- 최소 노력으로 다양한 먹이 섭취
- Fence 서식지: 인간이 주는 먹이 기다리기
- 썩은 음식도 개의치 않음
- 다른 펫 먹이 훔치기

---

### **2. Shy (소심한) 조합**

#### **Shy + Carnivore**
**Activity 우선순위:**
- ClimbTreeActivity: 7.0 (높은 곳에서 관찰)
- PredatorMoleInteraction: 8.0 (땅굴 숨기)
- PredatorPossumPrankInteraction: 8.0 (죽은 척)
- ChaseAndRunInteraction: 3.0 (피하는 쪽 선호)
- WanderActivity: 4.0 (조심스러운 이동)

**특별 행동 패턴:**
- 기습 사냥만 시도
- Water 서식지: 물속에서 숨어서 사냥
- Forest 서식지: 덤불 뒤에서 매복
- 야간 활동 선호
- 실패 시 즉시 도망

#### **Shy + Herbivore**
**Activity 우선순위:**
- BeeEscapeActivity: 9.0 (위험 회피 최우선)
- ClimbTreeActivity: 6.0 (안전한 높은 곳)
- WalkTogetherInteraction: 5.0 (그룹 안전)
- EatActivity: 4.0 (혼자 있을 때만)
- WanderActivity: 3.0 (다른 펫 피해서)

**특별 행동 패턴:**
- 다른 펫 15m 이내 접근 시 자동 회피
- Tree 서식지: 나무 꼭대기에서만 활동
- Field 서식지: 가장자리에서만 풀 뜯기
- 새벽/저녁 활동 증가
- 경계하며 먹기 (2초 먹고 1초 주변 확인)

#### **Shy + Omnivore**
**Activity 우선순위:**
- ChameleonCamouflageInteraction: 9.0 (위장 최우선)
- SkunkDefenseInteraction: 8.0 (방어 메커니즘)
- ButterflyPlayActivity: 3.0 (혼자 놀기)
- EatActivity: 5.0 (몰래 먹기)

**특별 행동 패턴:**
- 새벽/저녁에만 활발
- 다른 펫 소리에 민감 반응
- 숨은 장소에서만 식사
- 도망 경로 확보 후 활동

---

### **3. Brave (용감한) 조합**

#### **Brave + Carnivore**
**Activity 우선순위:**
- ChaseAndRunInteraction: 9.0 (추격자 역할)
- FightInteraction: 8.0 (도전적)
- RaceInteraction: 7.0 (경쟁 좋아함)
- DivingActivity: 6.0 (위험한 다이빙)
- WanderActivity: 5.0 (빠른 속도, 넓은 범위)

**특별 행동 패턴:**
- 자신보다 큰 먹이도 도전
- Water 서식지: 깊은 물까지 추격
- Field 서식지: 개방된 곳에서 당당히 사냥
- 영역 표시 행동
- 알파 포지션 추구

#### **Brave + Herbivore**
**Activity 우선순위:**
- HeadbuttInteraction: 7.0 (정면 대결)
- RaceInteraction: 6.0 (속도 경쟁)
- WalkTogetherInteraction: 5.0 (리더 역할)
- ClimbTreeActivity: 8.0 (위험한 높이 도전)

**특별 행동 패턴:**
- 포식자 앞에서도 도망가지 않음
- Tree 서식지: 가장 높은 가지까지 올라감
- Fence 서식지: 울타리 넘어가기 시도
- 무리의 리더 역할
- 위험 지역 탐험

#### **Brave + Omnivore**
**Activity 우선순위:**
- 모든 Interaction: 6.0~8.0 (도전적)
- DivingActivity: 8.0
- EnvironmentGatherActivity: 7.0 (새로운 환경 탐험)
- WanderActivity: 6.0 (탐험 모드)

**특별 행동 패턴:**
- 미지의 영역 적극 탐험
- 다른 펫의 먹이도 뺏기
- 인간 영역 침범
- 새로운 것 먼저 시도
- 위험한 곳도 탐색

---

### **4. Playful (장난스러운) 조합**

#### **Playful + Carnivore**
**Activity 우선순위:**
- ChaseAndRunInteraction: 8.0 (놀이처럼)
- RaceInteraction: 9.0 (재미로 경주)
- DivingActivity: 10.0 (전용 활동)
- ButterflyPlayActivity: 7.0
- RideAndWalkInteraction: 6.0

**특별 행동 패턴:**
- 사냥도 놀이처럼 즐김
- Water 서식지: 물고기와 장난치며 사냥
- Tree 서식지: 나무 타며 곡예 사냥
- 먹이와 술래잡기
- 잡았다 놓아주기 반복

#### **Playful + Herbivore**
**Activity 우선순위:**
- ButterflyPlayActivity: 9.0
- RaceInteraction: 8.0
- SlothKoalaRaceInteraction: 7.0 (재미있는 느림보 경주)
- WalkTogetherInteraction: 6.0 (놀이 상대 찾기)
- DivingActivity: 8.0

**특별 행동 패턴:**
- 먹으면서도 장난치기
- Field 서식지: 뛰어다니며 풀 뜯기
- Forest 서식지: 낙엽 던지며 놀기
- 음식으로 장난치기
- 다른 펫과 놀이 유도

#### **Playful + Omnivore**
**Activity 우선순위:**
- DivingActivity: 10.0 (최우선)
- ButterflyPlayActivity: 9.0
- 모든 Interaction: 7.0~8.0 (다양한 놀이)
- WanderActivity: 6.0 (지그재그, 랜덤 점프)

**특별 행동 패턴:**
- 모든 활동을 놀이로 변환
- 먹이 찾기도 게임처럼
- Fence 서식지: 울타리 미로 놀이
- 공중제비, 회전 등 묘기
- 다른 펫 놀리기

---

## 🌟 **특수 조합 시너지 (레전더리 조합)**

### **1. "나무늘보" - Lazy + Herbivore + Tree**
- 나무에서 하루 22시간 이상 체류
- 이동 속도 30% (극도로 느림)
- 나무 위에서 잠자기, 먹기 모두 해결
- 다른 펫이 와도 반응 없음
- 비 와도 움직이지 않음

### **2. "수중 사냥꾼" - Brave + Carnivore + Water**
- 물속 이동 속도 150%
- 잠수 시간 3배
- 물고기 사냥 성공률 90%
- 육지 동물도 물로 유인
- 다이빙 높이 2배

### **3. "서커스 곡예사" - Playful + Omnivore + Field**
- 연속 점프 5회
- 공중 3회전
- 달리면서 백플립
- 다른 펫 위로 점프
- 관객(다른 펫) 모으기

### **4. "그림자 암살자" - Shy + Carnivore + Forest**
- 완벽한 은신 (투명도 80%)
- 소리 없는 이동
- 기습 성공률 95%
- 야간 시야 2배
- 한 번에 치명타

### **5. "태양의 왕" - Lazy + Carnivore + Field**
- 언덕 꼭대기 독점
- 다른 펫이 먹이 가져다줌
- 포효로 영역 선포
- 하루 1번만 사냥
- 위엄 오라 발산

### **6. "숲의 수호자" - Brave + Herbivore + Forest**
- 약한 펫 자동 보호
- 포식자 추방
- 열매 나눠주기
- 치유 능력
- 숲 전체 감시

### **7. "물의 광대" - Playful + Omnivore + Water**
- 백덤블링 다이빙
- 물 분수 쇼
- 물방울 저글링
- 수중 댄스
- 물싸움 시작

### **8. "조용한 관찰자" - Shy + Omnivore + Tree**
- 모든 펫 행동 추적
- 위험 경고 신호
- 정보 수집
- 은밀한 이동
- 360도 시야

### **9. "철벽 수비수" - Brave + Herbivore + Fence**
- 울타리 영역 수호
- 침입자 격퇴
- 순찰 경로 고정
- 경고음 발산
- 절대 후퇴 없음

### **10. "유령" - Shy + Herbivore + Forest**
- 발소리 없음
- 순간 사라짐
- 안개 속 은신
- 흔적 남기지 않음
- 목격자 없음

---

## 🎮 **복합 시스템 상호작용**

### **시간대별 활동 변화**

#### **새벽 (5:00-7:00)**
- **Shy + Herbivore**: 활동 피크 (안전한 시간)
- **Lazy 전체**: 아직 수면 중
- **Brave + Carnivore**: 마지막 사냥

#### **아침 (7:00-10:00)**
- **Playful 전체**: 에너지 최고조
- **Herbivore 전체**: 아침 식사
- **Lazy**: 겨우 일어남

#### **낮 (10:00-15:00)**
- **Lazy 전체**: 낮잠 시간
- **Water 서식지**: 물놀이 피크
- **Tree 서식지**: 그늘에서 휴식

#### **오후 (15:00-18:00)**
- **Brave 전체**: 탐험 시간
- **Carnivore**: 사냥 준비
- **Playful**: 두 번째 활동 피크

#### **저녁 (18:00-20:00)**
- **Shy 전체**: 두 번째 활동 시간
- **모든 펫**: 둥지 귀환
- **Herbivore**: 마지막 식사

#### **밤 (20:00-5:00)**
- **Carnivore**: 야간 사냥
- **Shy + Carnivore**: 가장 활발
- **대부분**: 수면

### **날씨별 행동 변화**

#### **맑음** ☀️
- **Field 서식지**: +30% 활동량
- **Lazy**: 일광욕 모드
- **Water 서식지**: -10% (너무 더움)

#### **비** 🌧️
- **Water 서식지**: +50% 행복도
- **Tree 서식지**: 나무 아래 대피
- **Playful**: 빗물 놀이

#### **바람** 💨
- **Playful**: 바람 타기 놀이
- **Shy**: 불안감 증가
- **Tree 서식지**: 나무 꽉 잡기

#### **안개** 🌫️
- **Shy**: 대담해짐 (시야 차단)
- **Carnivore**: 사냥 성공률 +30%
- **Herbivore**: 경계 레벨 최대

#### **눈** ❄️
- **Lazy**: 동면 모드
- **Playful**: 눈싸움, 눈사람
- **Water 서식지**: 얼음 위 스케이팅

---

## 🔄 **그룹 다이나믹스**

### **같은 식성 그룹**

#### **초식 동물 무리 (3+ 마리)**
- 원형 방어 진형
- 교대로 경계
- 함께 이동
- 위험 신호 공유
- 새끼 보호

#### **육식 동물 팩 (2+ 마리)**
- 협동 사냥
- 영역 분할
- 서열 정리
- 먹이 순서
- 라이벌 관계

#### **잡식 동물 커뮤니티**
- 정보 교환
- 먹이 위치 공유
- 유연한 관계
- 상황별 동맹

### **다른 식성 상호작용**

#### **포식자-피식자**
- 자연스러운 추격전
- 도망 경로 학습
- 은신처 기억
- 경고 신호

#### **공생 관계**
- **Tree Herbivore + Ground Carnivore**: 층 분리
- **Water Omnivore + Field Herbivore**: 자원 분리
- **Shy + Brave**: 정찰-보호 관계

#### **중립 관계**
- 서로 무시
- 자원 경쟁 없음
- 평화 공존

---

## 📊 **행동 복잡도 계산**

### **기본 조합**
- 4 성격 × 3 식성 × 5 서식지 = **60가지**

### **특수 시너지**
- 레전더리 조합: **10가지**
- 시간대별 변화: **6 시간대**
- 날씨별 변화: **5가지**

### **그룹 행동**
- 같은 식성 그룹: **3가지**
- 다른 식성 관계: **6가지**
- 특수 공생: **5가지**

### **총 가능한 행동 패턴**
- 60 × 10 × 6 × 5 = **18,000가지** 이상의 독특한 상황

---

## 🚀 **구현 로드맵**

### **Phase 1: 기초 시스템 (1주)**
- PetController에 성격 속도 배율 적용
- WanderActivity 기본 확장
- 디버그 표시 추가

### **Phase 2: 식성 시스템 (1주)**
- 식성별 행동 패턴
- 먹이 선호도
- 그룹 형성

### **Phase 3: 서식지 시스템 (1주)**
- 서식지별 특수 행동
- 선호 구역 시스템
- 환경 상호작용

### **Phase 4: 특수 조합 (2주)**
- 레전더리 조합 10종
- 특수 스킬
- 시너지 효과

### **Phase 5: 시간/날씨 (1주)**
- 시간대별 활동
- 날씨 반응
- 계절 변화

### **Phase 6: 그룹 AI (2주)**
- 무리 행동
- 포식자-피식자
- 공생 관계

### **Phase 7: 최적화 (1주)**
- 성능 최적화
- 밸런싱
- 버그 수정

---

## 💡 **핵심 차별화 포인트**

1. **예측 가능한 다양성**: 랜덤이 아닌 규칙 기반
2. **자연스러운 생태계**: 실제 동물 행동 모방
3. **무한한 관찰 재미**: 매번 다른 상황 연출
4. **플레이어 개입 최소화**: AI 자율 행동
5. **스토리텔링**: 각 펫의 개성과 이야기

---

*이 기획서는 기존 코드를 최대한 활용하면서 최소한의 수정으로 최대의 효과를 내도록 설계되었습니다.*

*Last Updated: 2025.01.19*