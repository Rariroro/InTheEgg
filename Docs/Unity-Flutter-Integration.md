# Unity-Flutter 펫 데이터 연동 설계서

##이 문서는 유니티와 플러터 결합프로젝트를 위해. 유니티측 클로드코드와 플러터측 클로드코드에게 양쪽의 상황을 동일하게 이해하기 위해 작성하는 문서임
상대쪽이 알아야 하는 부분만 기록하면됨. 두 측은 동일한 문서를 가지고 있고 동일한 내용으로 동기화되어야함.

## 1. 개요

Flutter 앱에서 유저가 보유한 펫을 Unity 게임에서 플레이할 수 있도록 연결하는 시스템입니다.

**사용 패키지**: `flutter_embed_unity`

---

## 2. 데이터 저장 위치

| 데이터 종류 | 예시 | 저장 위치 | 비고 |
|-------------|------|-----------|------|
| **영구 데이터** | 펫 보유/친밀도/스폰여부, 아이템 보유/수량 | Flutter (서버) | 앱 삭제/기기 변경해도 유지 |
| **욕구 데이터** | 배고픔, 졸림 | Unity (PlayerPrefs) | 앱 삭제 시 리셋됨 (OK) |
| **게임 데이터** | 펫 위치, 현재 행동 | Unity만 | 휘발성 (게임 종료 시 리셋) |

### Flutter에서 관리하는 상세 데이터

| 종류 | 속성 | 설명 |
|------|------|------|
| **펫** | petCardId, petName, petIntimacy, isSpawned | 스폰 안 된 펫은 Egg 상태 |
| **레전드 펫** | petCardId, petName, isSpawned | 친밀도/욕구 없음, 먹이 불가 |
| **환경 아이템** | id, isSpawned | 스폰 안 됨 = 선물상자, 위치는 고정 |
| **음식 아이템** | id, quantity | 게임에서 사용 시 수량 감소 |

### 수치 범위

| 속성 | 범위 | 기본값 | 비고 |
|------|------|--------|------|
| **친밀도 (petIntimacy)** | 0 ~ 100 | 0 | 일반 펫만 해당 |
| **배고픔 (hunger)** | 0 ~ 100 | 50 | Unity PlayerPrefs 저장 |
| **졸림 (sleepy)** | 0 ~ 100 | 50 | Unity PlayerPrefs 저장 |

---

## 3. ID 매칭 테이블

> **Unity 담당**: 아래 Flutter ID에 맞춰 프리팹을 매칭해주세요.

### 일반 펫 (60종)

| Flutter ID | 한글 이름 | Unity 프리팹 |
|------------|----------|-------------|
| pet_001 | 거북이 | Turtle |
| pet_002 | 플라밍고 | Flamingo |
| pet_003 | 병아리 | Chick |
| pet_004 | 닭 | Chicken |
| pet_005 | 돼지 | Pig |
| pet_006 | 소 | Cow |
| pet_007 | 고양이 | Cat |
| pet_008 | 강아지 | Dog |
| pet_009 | 오리 | Duck |
| pet_010 | 엘크 | Elk |
| pet_011 | 멧돼지 | Boar |
| pet_012 | 늑대 | Wolf |
| pet_013 | 토끼 | Rabbit |
| pet_014 | 스컹크 | Skunk |
| pet_015 | 사슴 | Deer |
| pet_016 | 너구리 | Raccoon |
| pet_017 | 올빼미 | Owl |
| pet_018 | 여우 | Fox |
| pet_019 | 다람쥐 | Squirrel |
| pet_020 | 두더지 | Mole |
| pet_021 | 고슴도치 | Porcupine |
| pet_022 | 낙타 | Camel |
| pet_023 | 염소 | Goat |
| pet_024 | 개미핥기 | Anteater |
| pet_025 | 이구아나 | Iguana |
| pet_026 | 천산갑 | Pangolin |
| pet_027 | 알파카 | Alpaca |
| pet_028 | 캥거루 | Kangaroo |
| pet_029 | 미어캣 | Meerkat |
| pet_030 | 노새 | Mule |
| pet_031 | 들소 | Bison |
| pet_032 | 타조 | Ostrich |
| pet_033 | 말 | Horse |
| pet_034 | 얼룩말 | Zebra |
| pet_035 | 황소 | Bull |
| pet_036 | 암사자 | Lioness |
| pet_037 | 기린 | Giraffe |
| pet_038 | 사자 | Lion |
| pet_039 | 코뿔소 | Rhino |
| pet_040 | 코끼리 | Elephant |
| pet_041 | 양 | Sheep |
| pet_042 | 고릴라 | Gorilla |
| pet_043 | 주머니쥐 | Possum |
| pet_044 | 표범 | Leopard |
| pet_045 | 곰 | Bear |
| pet_046 | 공작새 | Peacock |
| pet_047 | 호랑이 | Tiger |
| pet_048 | 판다 | Panda |
| pet_049 | 원숭이 | Monkey |
| pet_050 | 나무늘보 | Sloth |
| pet_051 | 레서판다 | RedPanda |
| pet_052 | 코알라 | Koala |
| pet_053 | 말레이곰 | Malayan |
| pet_054 | 카멜레온 | Chameleon |
| pet_055 | 버팔로 | Buffalo |
| pet_056 | 하마 | Hippo |
| pet_057 | 아르마딜로 | Armadillo |
| pet_058 | 악어 | Crocodile |
| pet_059 | 오리너구리 | Platypus |
| pet_060 | 수달 | Otter |

---

### 레전드 펫 - 유니콘 시리즈 (10종)

| Flutter ID | 한글 이름 | Unity 프리팹 |
|------------|----------|-------------|
| pet_legend_001 | Terra (테라) | Terra |
| pet_legend_002 | Mint (민트) | Mint |
| pet_legend_003 | Rose (로즈) | Rose |
| pet_legend_004 | Shadow (섀도우) | Shadow |
| pet_legend_005 | Twin (트윈) | Twin |
| pet_legend_006 | Dream (드림) | Dream |
| pet_legend_007 | Prism (프리즘) | Prism |
| pet_legend_008 | Night (나이트) | Night |
| pet_legend_009 | Sky (스카이) | Sky |
| pet_legend_010 | Pure (퓨어) | Pure |

---

### 레전드 펫 - 드래곤 시리즈 (11종)

| Flutter ID | 한글 이름 | Unity 프리팹 |
|------------|----------|-------------|
| pet_legend_011 | Ocean (오션) | Ocean |
| pet_legend_012 | Spring (스프링) | Spring |
| pet_legend_013 | Peach (피치) | Peach |
| pet_legend_014 | Cloud (클라우드) | Cloud |
| pet_legend_015 | Volcano (볼케이노) | Volcano |
| pet_legend_016 | Amber (앰버) | Amber |
| pet_legend_017 | Blossom (블로썸) | Blossom |
| pet_legend_018 | Star (스타) | Star |
| pet_legend_019 | Storm (스톰) | Storm |
| pet_legend_020 | Sunset (선셋) | Sunset |
| pet_legend_021 | Snow (스노우) | Snow |

---

### 환경 아이템 (13종)

| Flutter ID | 한글 이름 | Unity 프리팹 |
|------------|----------|-------------|
| env_foodstore | 음식가게 | FoodstoreEnvironment |
| env_orchard | 과수원 | OrchardEnvironment |
| env_berryfield | 산딸기밭 | BerryfieldEnvironment |
| env_honeypot | 꿀통 | HoneypotEnvironment |
| env_sunflower | 해바라기 | SunflowerEnvironment |
| env_cucumber | 오이밭 | CucumberEnvironment |
| env_ricefield | 논 | RicefieldEnvironment |
| env_watermelon | 수박밭 | WatermelonEnvironment |
| env_cornfield | 옥수수밭 | CornfieldEnvironment |
| env_forest | 숲 | ForestEnvironment |
| env_pond | 물웅덩이 | PondEnvironment |
| env_flowers | 꽃밭 | FlowersEnvironment |
| env_fence | 초식동물용 울타리 | FenceEnvironment |

---

### 음식 아이템 (7종)

| Flutter ID | 한글 이름 | Unity 프리팹 |
|------------|----------|-------------|
| food_001 | 고기 | Meat |
| food_002 | 생선 | Fish |
| food_003 | 바나나 | Banana |
| food_004 | 배추 | Salad |
| food_005 | 옥수수 | Corn |
| food_006 | 건초 | Hay_bale |
| food_007 | 생초 | Grass_clump |

---

## 4. 메시지 포맷 (JSON)

### Flutter → Unity: SCREEN_ENTERED

Flutter 화면이 (재)진입했을 때 전송합니다. **Unity는 이 메시지를 받으면 반드시 READY를 전송해야 합니다.**

> ⚠️ **중요**: Unity 인스턴스는 앱 생명주기 동안 메모리에 유지되므로, 최초 로드 시에만 READY를 보내면 화면 재진입 시 데이터 동기화가 안 됩니다. `SCREEN_ENTERED`를 받을 때마다 READY를 재전송해야 합니다.

```json
{
  "type": "SCREEN_ENTERED",
  "data": {}
}
```

---

### Flutter → Unity: INIT_GAME

게임 시작 시 Flutter가 Unity로 보내는 초기화 데이터입니다.

```json
{
  "type": "INIT_GAME",
  "data": {
    "pets": [
      {
        "petCardId": "pet_001",
        "petName": "거북이",
        "petIntimacy": 50,
        "isSpawned": true
      },
      {
        "petCardId": "pet_002",
        "petName": "플라밍고",
        "petIntimacy": 0,
        "isSpawned": false
      }
    ],
    "legendaryPets": [
      {
        "petCardId": "pet_legend_011",
        "petName": "Ocean (오션)",
        "isSpawned": false
      }
    ],
    "environmentItems": [
      {
        "id": "env_sunflower",
        "name": "해바라기",
        "isSpawned": true
      }
    ],
    "foodItems": [
      {
        "id": "food_001",
        "name": "고기",
        "quantity": 5
      }
    ]
  }
}
```

---

### Unity → Flutter: PET_SPAWNED

일반 펫 Egg 터치 시 전송합니다.

```json
{
  "type": "PET_SPAWNED",
  "data": {
    "petCardId": "pet_002",
    "isSpawned": true
  }
}
```

---

### Unity → Flutter: LEGEND_PET_SPAWNED

레전드 펫 Egg 터치 시 전송합니다.

```json
{
  "type": "LEGEND_PET_SPAWNED",
  "data": {
    "petCardId": "pet_legend_011",
    "isSpawned": true
  }
}
```

---

### Unity → Flutter: ENV_ITEM_SPAWNED

선물상자 터치 시 전송합니다.

```json
{
  "type": "ENV_ITEM_SPAWNED",
  "data": {
    "id": "env_sunflower",
    "isSpawned": true
  }
}
```

---

### Unity → Flutter: FOOD_USED

음식을 맵에 놓을 때 전송합니다. (펫이 먹을 때가 아님)

```json
{
  "type": "FOOD_USED",
  "data": {
    "id": "food_001",
    "usedQuantity": 1
  }
}
```

---

### Unity → Flutter: SYNC_INTIMACY

30초마다 또는 게임 종료 시 전송합니다.

> **v1.9 최적화**: 변경된 친밀도만 전송합니다. 60마리 펫 중 5마리만 친밀도가 변경되었다면 5마리 데이터만 전송됩니다. 변경된 펫이 없으면 전송하지 않습니다.

```json
{
  "type": "SYNC_INTIMACY",
  "data": {
    "pets": [
      { "petCardId": "pet_001", "petIntimacy": 75 }
    ]
  }
}
```

> **Flutter 측 처리**: 수신된 펫 데이터만 업데이트하면 됩니다. 전체 펫 목록이 아니라 변경분만 전송되므로, 기존 데이터에 덮어쓰기 방식으로 처리하세요.

---

### Unity → Flutter: GAME_EXIT

게임 종료 시 전송합니다. (SYNC_INTIMACY와 동일한 구조)

```json
{
  "type": "GAME_EXIT",
  "data": {
    "pets": [
      { "petCardId": "pet_001", "petIntimacy": 75 },
      { "petCardId": "pet_002", "petIntimacy": 30 }
    ]
  }
}
```

---

### ~~Unity → Flutter: LOADING_COMPLETE~~ (삭제됨)

> **v2.1 삭제**: Unity에서 자체 로딩화면을 구현했으므로 이 메시지는 더 이상 사용하지 않습니다.
>
> ~~**Unity 측 조치 필요**: `LOADING_COMPLETE` 메시지 전송 코드를 삭제해주세요.~~ ✅ 완료

---

## 5. 데이터 흐름

### 게임 시작할 때

```
Flutter 앱
    │
    │ 1. 서버에서 데이터 로드
    │ 2. Unity로 INIT_GAME 전송
    ▼
Unity 게임
    │
    │ 3. 받은 데이터로 생성
    │    - 펫: isSpawned=true → 펫 생성, false → Egg 생성
    │    - 레전드 펫: isSpawned=true → 레전드 펫 생성, false → Egg 생성
    │    - 환경 아이템: isSpawned=true → 배치, false → 선물상자
    │ 4. PlayerPrefs에서 펫 배고픔/졸림 불러오기
    │    (레전드 펫은 욕구 없음, 일반 펫만)
    │    (없으면 기본값 50)
    │ 5. 게임 시작!
    ▼
```

### 게임 플레이 중

```
Unity 게임
    │
    │ 펫이 먹고, 자고, 놀고...
    │ 유저가 펫 쓰다듬기, 먹이 주기 등 → 친밀도 변화
    │
    │ ─── 즉시 전송 (되돌릴 수 없는 이벤트) ─▶ Flutter로 전송
    │     • Egg 터치 → PET_SPAWNED / LEGEND_PET_SPAWNED
    │     • 선물상자 터치 → ENV_ITEM_SPAWNED
    │     • 음식을 맵에 놓음 → FOOD_USED
    │
    │ ─── 30초마다 ───────────────────────────▶ SYNC_INTIMACY
    │
    ▼
Flutter 앱
    │
    │ 받은 데이터 서버에 저장
    ▼
```

### 게임 종료할 때

```
Unity 게임
    │
    │ 1. 배고픔/졸림 → PlayerPrefs에 저장
    │ 2. GAME_EXIT 전송
    ▼
Flutter 앱
    │
    │ 서버에 저장
    ▼
```

---

## 6. 동기화 전략

### 저장 타이밍

| 타이밍 | 메시지 | 방식 |
|--------|--------|------|
| Egg 터치 | PET_SPAWNED / LEGEND_PET_SPAWNED | 즉시 |
| 선물상자 터치 | ENV_ITEM_SPAWNED | 즉시 |
| 음식을 맵에 놓음 | FOOD_USED | 즉시 |
| 30초 주기 | SYNC_INTIMACY | 주기적 |
| 백그라운드 전환 | SYNC_INTIMACY | 즉시 |
| 게임 종료 | GAME_EXIT | 즉시 |

### 데이터 손실 대비

| 상황 | 대응 | 최악의 손실 |
|------|------|-------------|
| 정상 종료 | 마지막 상태 저장 | 없음 |
| 백그라운드 전환 | 즉시 저장 | 없음 |
| 강제 종료 | 30초 전 데이터까지 보존 | 친밀도 30초분 |

---

## 7. 에러 처리

### 즉시 전송 실패 시 (중요 이벤트)

되돌릴 수 없는 이벤트는 전송 실패 시 심각한 문제:
- Egg 깨짐 → 서버에는 아직 Egg 상태
- 음식을 맵에 놓음 → 서버에는 수량 그대로

**해결책: 로컬 큐 + 재시도**

```
Unity에서 이벤트 발생
    │
    ├─ Flutter로 전송 시도
    │
    ├─ 성공 → 완료
    │
    └─ 실패 → 로컬 큐에 저장
              │
              └─ 3초 후 재시도
                 5초 후 재시도
                 10초 후 재시도
                 ...
                 네트워크 복구 시 재시도
```

### 재시도 전략

| 이벤트 | 재시도 | 최대 횟수 | 실패 시 |
|--------|--------|-----------|---------|
| PET_SPAWNED | 3초, 5초, 10초... | 무제한 | 네트워크 복구까지 대기 |
| LEGEND_PET_SPAWNED | 3초, 5초, 10초... | 무제한 | 네트워크 복구까지 대기 |
| ENV_ITEM_SPAWNED | 3초, 5초, 10초... | 무제한 | 네트워크 복구까지 대기 |
| FOOD_USED | 3초, 5초, 10초... | 무제한 | 네트워크 복구까지 대기 |
| SYNC_INTIMACY | 다음 30초 주기 | 자동 | 다음 주기에 최신 값 전송 |

---

## 8. Flutter 구현 가이드

> **Flutter 개발자를 위한 섹션**입니다. Unity 메시지 수신/발신 처리 방법을 설명합니다.

### 8.1 Unity로 메시지 전송

```dart
import 'package:flutter_embed_unity/flutter_embed_unity.dart';

// SCREEN_ENTERED 전송 (화면 진입/재진입 시)
void sendScreenEntered() {
  final message = jsonEncode({
    "type": "SCREEN_ENTERED",
    "data": {}
  });
  sendToUnity("FlutterManager", "OnScreenEntered", message);
}

// INIT_GAME 전송 (READY 수신 후)
void sendInitGame(GameData data) {
  final message = jsonEncode({
    "type": "INIT_GAME",
    "data": {
      "pets": data.pets.map((p) => {
        "petCardId": p.id,
        "petName": p.name,
        "petIntimacy": p.intimacy,
        "isSpawned": p.isSpawned
      }).toList(),
      "legendaryPets": data.legendaryPets.map((p) => {
        "petCardId": p.id,
        "petName": p.name,
        "isSpawned": p.isSpawned
      }).toList(),
      "environmentItems": data.envItems.map((e) => {
        "id": e.id,
        "name": e.name,
        "isSpawned": e.isSpawned
      }).toList(),
      "foodItems": data.foodItems.map((f) => {
        "id": f.id,
        "name": f.name,
        "quantity": f.quantity
      }).toList()
    }
  });
  sendToUnity("FlutterManager", "OnInitGame", message);
}
```

### 8.2 Unity 메시지 수신

```dart
// Unity 메시지 리스너 등록
void setupUnityListener() {
  onUnityMessage.listen((message) {
    final data = jsonDecode(message);
    final type = data['type'];

    switch (type) {
      case 'READY':
        // Unity 준비 완료 → INIT_GAME 전송
        sendInitGame(currentGameData);
        break;

      case 'PET_SPAWNED':
        // 펫 스폰됨 → 서버에 isSpawned=true 저장
        final petCardId = data['data']['petCardId'];
        updatePetSpawnStatus(petCardId, true);
        break;

      case 'LEGEND_PET_SPAWNED':
        // 레전드 펫 스폰됨
        final petCardId = data['data']['petCardId'];
        updateLegendPetSpawnStatus(petCardId, true);
        break;

      case 'ENV_ITEM_SPAWNED':
        // 환경 아이템 스폰됨
        final envId = data['data']['id'];
        updateEnvItemSpawnStatus(envId, true);
        break;

      case 'FOOD_USED':
        // 음식을 맵에 놓음 → 수량 감소
        final foodId = data['data']['id'];
        final usedQty = data['data']['usedQuantity'];
        decreaseFoodQuantity(foodId, usedQty);
        break;

      case 'SYNC_INTIMACY':
      case 'GAME_EXIT':
        // 친밀도 동기화
        final pets = data['data']['pets'] as List;
        for (var pet in pets) {
          updatePetIntimacy(pet['petCardId'], pet['petIntimacy']);
        }
        break;
    }
  });
}
```

### 8.3 화면 생명주기 처리

```dart
class UnityGameScreen extends StatefulWidget {
  @override
  _UnityGameScreenState createState() => _UnityGameScreenState();
}

class _UnityGameScreenState extends State<UnityGameScreen> {
  @override
  void initState() {
    super.initState();
    setupUnityListener();

    // 화면 진입 시 SCREEN_ENTERED 전송
    // Unity가 READY를 응답하면 INIT_GAME 전송
    sendScreenEntered();
  }

  @override
  void didChangeDependencies() {
    super.didChangeDependencies();
    // 화면 재진입 시에도 SCREEN_ENTERED 전송
    // (Unity가 메모리에 유지되므로 재동기화 필요)
  }
}
```

### 8.4 isSpawned 필드 설명

| isSpawned 값 | Unity 동작 | Flutter 처리 |
|--------------|------------|--------------|
| `true` | 펫/아이템이 바로 스폰됨 | 이미 스폰된 상태 유지 |
| `false` | Egg(선물상자)로 생성됨 | 유저가 Egg 터치 후 `PET_SPAWNED` 수신 시 `true`로 업데이트 |

> **중요**: `isSpawned=false`인 펫은 Unity에서 Egg로 자동 생성됩니다. 유저가 Egg를 터치하면 펫이 스폰되고, Unity가 `PET_SPAWNED` 메시지를 전송합니다. Flutter는 이 메시지를 받아 서버에 `isSpawned=true`로 저장해야 합니다.

### 8.5 주의사항

1. **READY 메시지 대기**: `SCREEN_ENTERED` 전송 후 Unity의 `READY`를 기다린 뒤 `INIT_GAME` 전송
2. **SCREEN_ENTERED 드롭 가능**: Unity 로드 전에 전송되면 드롭될 수 있음 → Unity가 `OnApplicationPause(false)`에서 `READY` 재전송하므로 문제없음
3. **메시지 순서**: `SCREEN_ENTERED` → `READY` → `INIT_GAME` 순서 준수
4. **재진입 처리**: 화면 재진입마다 `SCREEN_ENTERED` 전송 필수 (Unity 상태 리셋용)

---

## 9. 요약

```
┌─────────────────────────────────────────────────────┐
│                    핵심 포인트                       │
├─────────────────────────────────────────────────────┤
│                                                     │
│  1. 저장 위치                                       │
│     • Flutter (서버): 친밀도, 스폰 여부, 아이템 수량│
│     • Unity (PlayerPrefs): 배고픔, 졸림             │
│                                                     │
│  2. 즉시 동기화 (Flutter로)                         │
│     • Egg → 펫/레전드 펫 스폰                       │
│     • 선물상자 → 환경 아이템 스폰                   │
│     • 음식을 맵에 배치                              │
│                                                     │
│  3. 주기적 동기화                                   │
│     • 친밀도: 30초마다 + 게임 종료 시               │
│     • 배고픔/졸림: 게임 종료 시 PlayerPrefs 저장    │
│                                                     │
│  4. 레전드 펫 특수 규칙                             │
│     • 친밀도 없음                                   │
│     • 욕구(배고픔/졸림) 없음                        │
│     • 먹이 주기 불가                               │
│     • 스폰 여부만 관리                              │
│                                                     │
└─────────────────────────────────────────────────────┘
```


---

## 변경 이력

| 날짜 | 버전 | 변경 내용 |
|------|------|-----------|
| 2025-12-13 | 1.0 | 초안 작성 |
| 2025-12-13 | 1.1 | ID 매칭 테이블 간소화, JSON 메시지 포맷 추가 |
| 2025-12-13 | 1.2 | 수치 범위 추가 (친밀도, 배고픔, 졸림: 0~100) |
| 2025-12-14 | 1.3 | Unity 구현 가이드 추가 (FlutterBridge, SendToFlutter, 생명주기 처리) |
| 2025-12-15 | 1.4 | **SCREEN_ENTERED 메시지 추가** - 화면 재진입 시 데이터 동기화 문제 해결. Unity는 OnScreenEntered에서 READY 재전송 필수 |
| 2025-12-15 | 1.5 | **Unity 측 구현 완료** - FlutterBridge.OnScreenEntered(), ResetForNewSession() 구현. FlutterModeManager.ResetForNewSession() 추가 |
| 2025-12-15 | 1.6 | **재진입 문제 수정** - OnApplicationPause(false)에서 READY 재전송 (SCREEN_ENTERED 드롭 문제 해결) |
| 2025-12-15 | 1.7 | **펫 스폰 방식 명확화** - Flutter 모드에서 isSpawned=false 펫은 Egg 자동 생성 (버튼 없음). Flutter 구현 가이드 추가 |
| 2025-12-15 | 1.8 | **재진입 시 새 펫 적용 버그 수정** - PetManager.ResetForNewSession(), LegendaryPetManager.ResetForNewSession() 추가. FlutterModeManager에서 호출하여 hasSpawnedPets 플래그 리셋 및 기존 펫/Egg 제거 |
| 2025-12-15 | 1.9 | **성능 최적화** - SYNC_INTIMACY 전송 시 변경된 친밀도만 전송하도록 개선 (60마리 중 5마리만 변경 시 5마리만 전송). LoadingManager 추가로 로딩 화면 지원 |
| 2025-12-15 | 2.0 | **LOADING_COMPLETE 메시지 추가** - 펫 스폰 완료 후 Flutter에 알림. Flutter에서 로딩 오버레이 제거 타이밍으로 활용 가능 |
| 2025-12-15 | 2.1 | **LOADING_COMPLETE 메시지 삭제** - Unity 자체 로딩화면 구현으로 불필요. Flutter 로딩 오버레이도 제거. **Unity 측: LOADING_COMPLETE 전송 코드 삭제 필요** |
