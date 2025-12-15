# Unity-Flutter 펫 데이터 연동 설계서

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

음식 사용 시 전송합니다.

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

```json
{
  "type": "SYNC_INTIMACY",
  "data": {
    "pets": [
      { "petCardId": "pet_001", "petIntimacy": 75 },
      { "petCardId": "pet_002", "petIntimacy": 30 }
    ]
  }
}
```

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
    │     • 음식 사용 → FOOD_USED
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
| 음식 사용 | FOOD_USED | 즉시 |
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
- 음식 사용 → 서버에는 수량 그대로

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

### 게임 재시작 시 동기화

```
게임 시작
    │
    ├─ 로컬 큐에 미전송 이벤트 있음?
    │
    ├─ 있음 → 먼저 전송 시도 → 성공 후 게임 시작
    │
    └─ 없음 → 바로 게임 시작
```

---

## 8. Unity 구현 가이드 (flutter_embed_unity)

> **중요**: Flutter 측 구현이 완료되었습니다. Unity에서 아래 사항을 구현해야 합니다.

### 8.1 필수 설정

**패키지**: Unity Package Manager에서 `flutter_embed_unity` Unity 모듈 설치

### 8.2 FlutterBridge GameObject 생성

Unity 씬에 빈 GameObject를 생성하고 이름을 **`FlutterManager`**로 설정합니다.

```csharp
// FlutterBridge.cs - 이 스크립트를 FlutterManager GameObject에 연결
using UnityEngine;
using FlutterEmbedUnity;

public class FlutterBridge : MonoBehaviour
{
    // Flutter에서 INIT_GAME 메시지를 받을 때 호출됨
    public void OnInitGame(string jsonMessage)
    {
        Debug.Log("Received INIT_GAME: " + jsonMessage);

        // JSON 파싱 후 게임 초기화
        var message = JsonUtility.FromJson<InitGameMessage>(jsonMessage);
        GameManager.Instance.InitializeGame(message.data);
    }
}
```

### 8.3 Flutter로 메시지 전송

```csharp
// Unity → Flutter 메시지 전송 예시
using FlutterEmbedUnity;

public class UnityToFlutterMessenger
{
    // READY 메시지 전송 (Unity 로딩 완료 시)
    public static void SendReady()
    {
        var message = new { type = "READY", data = new { } };
        SendToFlutter.Send(JsonUtility.ToJson(message));
    }

    // PET_SPAWNED 메시지 전송
    public static void SendPetSpawned(string petCardId)
    {
        var message = new {
            type = "PET_SPAWNED",
            data = new { petCardId = petCardId, isSpawned = true }
        };
        SendToFlutter.Send(JsonUtility.ToJson(message));
    }

    // LEGEND_PET_SPAWNED 메시지 전송
    public static void SendLegendPetSpawned(string petCardId)
    {
        var message = new {
            type = "LEGEND_PET_SPAWNED",
            data = new { petCardId = petCardId, isSpawned = true }
        };
        SendToFlutter.Send(JsonUtility.ToJson(message));
    }

    // ENV_ITEM_SPAWNED 메시지 전송
    public static void SendEnvItemSpawned(string id)
    {
        var message = new {
            type = "ENV_ITEM_SPAWNED",
            data = new { id = id, isSpawned = true }
        };
        SendToFlutter.Send(JsonUtility.ToJson(message));
    }

    // FOOD_USED 메시지 전송
    public static void SendFoodUsed(string id, int usedQuantity)
    {
        var message = new {
            type = "FOOD_USED",
            data = new { id = id, usedQuantity = usedQuantity }
        };
        SendToFlutter.Send(JsonUtility.ToJson(message));
    }

    // SYNC_INTIMACY 메시지 전송 (30초마다 + 백그라운드 전환 시)
    public static void SendSyncIntimacy(List<PetIntimacyData> pets)
    {
        var message = new {
            type = "SYNC_INTIMACY",
            data = new { pets = pets }
        };
        SendToFlutter.Send(JsonUtility.ToJson(message));
    }

    // GAME_EXIT 메시지 전송
    public static void SendGameExit(List<PetIntimacyData> pets)
    {
        var message = new {
            type = "GAME_EXIT",
            data = new { pets = pets }
        };
        SendToFlutter.Send(JsonUtility.ToJson(message));
    }
}

[System.Serializable]
public class PetIntimacyData
{
    public string petCardId;
    public int petIntimacy;
}
```

### 8.4 Unity 생명주기 처리

```csharp
// 백그라운드 전환 감지
void OnApplicationPause(bool pauseStatus)
{
    if (pauseStatus)
    {
        // 앱이 백그라운드로 전환될 때 즉시 동기화
        UnityToFlutterMessenger.SendSyncIntimacy(GetAllPetIntimacyData());
    }
}

// 앱 종료 감지
void OnApplicationQuit()
{
    UnityToFlutterMessenger.SendGameExit(GetAllPetIntimacyData());
}
```

### 8.5 게임 초기화 흐름

```
1. Unity 씬 로드 완료
2. SendToFlutter.Send({"type":"READY"}) 전송
3. Flutter가 INIT_GAME 전송
4. FlutterManager.OnInitGame() 호출됨
5. JSON 파싱 후 펫/아이템 생성
6. 게임 시작
```

### 8.6 메시지 타입 상수

| type 문자열 | 방향 | 설명 |
|-------------|------|------|
| `READY` | Unity → Flutter | Unity 로딩 완료 |
| `INIT_GAME` | Flutter → Unity | 게임 초기화 데이터 |
| `PET_SPAWNED` | Unity → Flutter | 일반 펫 스폰 |
| `LEGEND_PET_SPAWNED` | Unity → Flutter | 레전드 펫 스폰 |
| `ENV_ITEM_SPAWNED` | Unity → Flutter | 환경 아이템 스폰 |
| `FOOD_USED` | Unity → Flutter | 음식 사용 |
| `SYNC_INTIMACY` | Unity → Flutter | 친밀도 동기화 |
| `GAME_EXIT` | Unity → Flutter | 게임 종료 |

### 8.7 주의사항

- **GameObject 이름**: 반드시 `FlutterManager`로 설정 (Flutter에서 이 이름으로 호출)
- **메서드 이름**: `OnInitGame` (Flutter에서 이 메서드로 INIT_GAME 전송)
- **JSON 형식**: 모든 메시지는 `{ "type": "...", "data": { ... } }` 형식
- **READY 메시지**: Unity 로딩 완료 후 반드시 전송해야 Flutter가 INIT_GAME을 보냄

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
│     • 음식 아이템 사용                              │
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
