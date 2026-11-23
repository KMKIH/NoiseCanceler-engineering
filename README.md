# Noise Canceler 기술 문서

`Noise Canceler` 프로젝트에서 구현한 주요 시스템과 코드, 개발 중 겪은 문제와 해결 과정을 정리했습니다.

## 프로젝트 소개

| 항목 | 내용 |
|---|---|
| 프로젝트 | Noise Canceler |
| 장르 | 리듬 게임 |
| 개발 기간 | 2024.07 ~ 2026.07 |
| 개발 인원 | 총 7명 (PM 1, 기획 2, <u>**개발 1**</u>, 아트 1, 사운드 1, QA 1) |
| 담당 역할 | Unity 클라이언트 개발 전반 |
| 사용 기술 | Unity 6 (`6000.0.67f1`), C#, FMOD, Addressables |

## 주요 개발 사례

| 분류 | 주제 | 요약 | 링크 |
|---|---|---|---|
| 문제 해결 | Timeline 연출과 FMOD 음원 사이의 싱크 개선 | 음원과 연출의 시작 시점 및 재생 중 편차를 FMOD 사전 로드와 Timeline 시간 보정으로 개선 | [**상&#8288;세**](docs/trouble-shooting/timeline-fmod-sync.md) |

## 전체 문서

### 시스템 설계
| 주제 | 요약 | 링크 |
|---|---|---|
| Custom Update 시스템 | 음악 시간 갱신, 입력, 판정을 명시적인 순서로 실행 | [**상&#8288;세**](docs/architecture/custom-update-system.md) |
| MAD 기반 입력 오프셋 이상치 제거 | 소수 입력 표본에서 MAD 기반으로 이상치를 제거하고 사용자별 입력 오프셋 계산 | [**상&#8288;세**](docs/architecture/mad-based-input-offset-filtering.md) |
| Timeline 연출 Facade 시스템 | 연출 기능을 Controller별로 분리하고 Timeline 호출 경로를 Facade로 통합 | [**상&#8288;세**](docs/architecture/timeline-action-facade.md) |
| UI 입력 관리 | 중첩된 UI를 Stack으로 관리해 최상단 UI에만 입력 권한 부여 | [**상&#8288;세**](docs/architecture/ui-focus-stack-system.md) |
| UI Navigation | `F`·`J`·`Space` 기반 메뉴 이동과 선택 로직 공통화 | [**상&#8288;세**](docs/architecture/ui-navigation-system.md) |

### 문제 해결 기록 (Trouble Shooting)
| 주제 | 요약 | 링크 |
|---|---|---|
| Timeline 연출과 FMOD 음원 사이의 싱크 개선 | 음원과 연출의 시작 시점 및 재생 중 편차를 FMOD 사전 로드와 Timeline 시간 보정으로 개선 | [**상&#8288;세**](docs/trouble-shooting/timeline-fmod-sync.md) |
| 곡이 진행될수록 노트 판정 시점이 앞당겨지는 문제 | 정수 절삭으로 누적된 채보 시간 오차를 음원 시간 기반 이동과 인덱스 기반 마디 시작 시각 계산으로 개선 | [**상&#8288;세**](docs/trouble-shooting/note-judgment-timing-drift.md) |
| Fade In 중 새 노트가 보이지 않는 문제 | Signal 처리와 노트 등록이 겹칠 때 누락된 Fade 상태를 종료 재동기화와 리비전 검사로 보완 | [**상&#8288;세**](docs/trouble-shooting/timeline-note-fade-race-condition.md) |
| 씬 전환 후 이전 씬의 FocusManager를 참조하는 문제 | Additive 씬 로드 중 전역 탐색으로 발생한 참조 불일치를 GameManager의 FocusManager 참조 공유로 해결 | [**상&#8288;세**](docs/trouble-shooting/focus-manager-reference-problem.md) |

## 관련 코드

| 파일 | 역할 |
|---|---|
| [PlayManager.cs](src/rhythm-sync/PlayManager.cs) | 음원 프리로드, Signal 기반 별도 재생과 Timeline 주기 보정 |
| [CustomFMODEventTrack.cs](src/rhythm-sync/CustomFMODEventTrack.cs) | 실험용 트랙에서 FMOD 인스턴스를 재생하고 PlayManager에 연결 |
| [CustomFMODEventClip.cs](src/rhythm-sync/CustomFMODEventClip.cs) | 트랙에서 재생할 이벤트, 음원 시작 위치와 음량 설정 |
| [Updateable.cs](src/custom-update/Updateable.cs) | 순서 제어 대상이 구현할 `CustomUpdate()` 정의 |
| [UpdateManager.cs](src/custom-update/UpdateManager.cs) | 등록된 `Updateable`을 인스펙터 순서대로 실행 |
| [Note.cs](src/rhythm-sync/Note.cs) | 음원 시간 기반 절대 위치 계산과 생성·파괴 시 Fade 상태 처리 |
| [DataManager.cs](src/rhythm-sync/DataManager.cs) | FMOD 음악 재생 위치를 공통 시간값으로 갱신 |
| [Sheet.cs](src/rhythm-sync/Sheet.cs) | 정확한 마디 길이와 인덱스로 마디 시작 시각 계산 |
| [BarInfo.cs](src/rhythm-sync/BarInfo.cs) | 마디와 박자별 노트 판정 시각 구성 |
| [TimelineActionFacade.cs](src/timeline-action/TimelineActionFacade.cs) | Timeline 연출 기능의 단일 진입점과 Controller 호출 중계 |
| [NoteEffectController.cs](src/timeline-action/NoteEffectController.cs) | 노트 Fade 상태 관리 |
| [GlowEffectController.cs](src/timeline-action/GlowEffectController.cs) | 글로우 활성화와 색상·강도 변경 처리 |
| [ShakeEffectController.cs](src/timeline-action/ShakeEffectController.cs) | 판정 시 화면 흔들림 상태와 실행 매개변수 관리 |
| [OffsetEstimator.cs](src/input-offset/OffsetEstimator.cs) | MAD 기반 이상치 제거와 대표 오프셋 계산 |
| [FocusManager.cs](src/ui-focus/FocusManager.cs) | Stack 기반 UI 포커스 등록·해제 및 현재 입력 대상 판별 |
| [IFocusable.cs](src/ui-focus/IFocusable.cs) | 포커스 진입·획득·해제 이벤트 인터페이스 |
| [INavigationButton.cs](src/ui-navigation/INavigationButton.cs) | UI 버튼의 Normal·Highlight·Press·Release 상태 전환 표준화 |
| [NavigationButtonController.cs](src/ui-navigation/NavigationButtonController.cs) | 활성 버튼 목록, 선택 인덱스, 순환 이동과 선택 입력 관리 |

## 저장소 구성
```text
├─ README.md
├─ src/
└─ docs/
   ├─ architecture/
   ├─ data/
   └─ trouble-shooting/
```
- `src/`: 일부 공개 코드
- `docs/architecture/`: 구조, 책임 분리, 주요 의사결정
- `docs/data/`: 실험 조건과 측정 결과
- `docs/trouble-shooting/`: 증상, 원인 분석, 수정 내용, 검증, 회고

## 공개 범위와 제한

- 전체 Unity 프로젝트가 아닌 선별 코드와 기술 문서만 포함합니다.
- 게임 에셋, 데이터, 상용 플러그인, 제3자 코드, 민감 정보는 공개하지 않습니다.
- 프로젝트 전용 타입과 외부 의존성 일부가 생략되어 단독 실행과 빌드를 지원하지 않습니다.
- 공동 작업이 포함된 사례는 각 문서에 기여 범위를 명시합니다.

## 라이선스

이 저장소는 오픈소스 프로젝트가 아닙니다. 코드와 문서는 포트폴리오 열람 목적으로만 공개하며, 별도 허가 없는 복제, 수정, 재배포, 상업적 이용을 허용하지 않습니다.
