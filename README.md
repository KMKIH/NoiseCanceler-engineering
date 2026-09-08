# NoiseCanceler Engineering

`Noise Canceler` 프로젝트에서 구현한 주요 시스템과 코드, 개발 중 겪은 문제와 해결 과정을 정리했습니다.

## 프로젝트 소개

| 항목 | 내용 |
|---|---|
| 프로젝트 | Noise Canceler |
| 장르 | 리듬 게임 |
| 개발 기간 | 2024.07 ~ 2026.08 |
| 개발 인원 | 총 6명 (PM 1, 기획 2, <u>**개발 1**</u>, 아트 1, 사운드 1) |
| 담당 역할 | Unity 클라이언트 개발 전반 |
| 사용 기술 | Unity 6 (`6000.0.67f1`), C#, FMOD, Addressables |

## 저장소 구성
```text
├─ README.md
├─ src/
└─ docs/
   ├─ architecture/
   └─ trouble-shooting/
```
- `src/`: 일부 공개 코드
- `docs/architecture/`: 구조, 책임 분리, 주요 의사결정
- `docs/trouble-shooting/`: 증상, 원인 분석, 수정 내용, 검증, 회고

## 주요 내용

### 시스템 설계
| 시스템 | 핵심 설계 | 상세 문서 |
|---|---|---|
| Custom Update 시스템 | 음악 시간 갱신, 입력, 판정을 명시적인 순서로 실행 | [Custom Update 시스템](docs/architecture/custom-update-system.md) |
| MAD 기반 입력 오프셋 이상치 제거 | 소수 입력 표본에서 MAD 기반으로 이상치를 제거하고 사용자별 입력 오프셋 계산 | [MAD 기반 입력 오프셋 이상치 제거](docs/architecture/mad-based-input-offset-filtering.md) |
| Timeline 연출 Facade 시스템 | 연출 기능을 Controller별로 분리하고 Timeline 호출 경로를 Facade로 통합 | [Timeline 연출 Facade 시스템](docs/architecture/timeline-action-facade.md) |
| UI 입력 관리 | 중첩된 UI를 Stack으로 관리해 최상단 UI에만 입력 권한 부여 | [UI Focus Stack 시스템](docs/architecture/ui-focus-stack-system.md) |
| UI Navigation | `F`·`J`·`Space` 기반 메뉴 이동과 선택 로직 공통화 | [UI Navigation 시스템](docs/architecture/ui-navigation-system.md) |

### 문제 해결 기록 (Trouble Shooting)
| 문제 | 원인 | 해결 방향 | 상세 문서 |
|---|---|---|---|
| 곡 후반으로 갈수록 노트가 음원보다 앞당겨지는 문제 | 정수로 잘린 마디 길이를 누적 계산해 채보 시간이 점차 앞당겨짐 | 음원 재생 시간으로 이동 기준을 통일하고 마디 시작 시각을 인덱스로 계산 | [곡 후반으로 갈수록 노트가 음원보다 앞당겨지는 문제](docs/trouble-shooting/note-sync-drift-time-precision.md) |
| Timeline 연출과 FMOD 음원의 시작 시점이 어긋나는 문제 | 음악과 연출의 초기화 과정 결합, Unity와 FMOD의 독립된 시간 기준 | 음원을 사전 로드하고 Timeline Signal에서 재생하며 FMOD 음악 위치를 기준으로 Timeline 보정 | [Timeline 연출과 FMOD 음원 사이의 싱크 개선](docs/trouble-shooting/timeline-fmod-sync.md) |
| Timeline FadeIn 시 일부 신규 노트가 보이지 않는 문제 | Signal 처리와 노트 등록이 같은 프레임에 겹쳐 발생한 상태 동기화 누락 | Fade 종료 재동기화와 리비전 검사로 최종 상태 보장 | [Timeline Signal과 노트 생성이 겹칠 때 Fade 상태가 누락되는 문제](docs/trouble-shooting/timeline-note-fade-race-condition.md) |
| 씬 전환 후 GameManager가 이전 FocusManager를 참조하는 문제 | Additive 로드 중 두 씬이 공존할 때 범위 없는 전역 탐색 수행 | GameManager가 얻은 FocusManager 참조를 관련 UI가 공유 | [FocusManager 참조 불일치 문제](docs/trouble-shooting/focus-manager-reference-problem.md) |

## 관련 코드

| 파일 | 역할 |
|---|---|
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

## 공개 범위와 제한

- 전체 Unity 프로젝트가 아닌 선별 코드와 기술 문서만 포함합니다.
- 게임 에셋, 데이터, 상용 플러그인, 제3자 코드, 민감 정보는 공개하지 않습니다.
- 프로젝트 전용 타입과 외부 의존성 일부가 생략되어 단독 실행과 빌드를 지원하지 않습니다.
- 공동 작업이 포함된 사례는 각 문서에 기여 범위를 명시합니다.

## 라이선스

이 저장소는 오픈소스 프로젝트가 아닙니다. 코드와 문서는 포트폴리오 열람 목적으로만 공개하며, 별도 허가 없는 복제, 수정, 재배포, 상업적 이용을 허용하지 않습니다.
