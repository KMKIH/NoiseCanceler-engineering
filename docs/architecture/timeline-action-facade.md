# Timeline 연출 Facade 시스템

## 개요

리듬 게임의 노트 페이드, 글로우, 화면 흔들림과 같은 연출은 Unity Timeline으로 구성한다. Timeline만으로 처리하기 어려운 동작은 Signal을 통해 스크립트의 함수를 호출하여 구현한다.

각 연출의 책임을 분리하고 SRP를 지키는 과정에서 필요한 기능이 여러 스크립트에 흩어졌고, 기획자가 연출마다 담당 컴포넌트와 함수를 찾아 Signal에 연결해야 하는 문제가 생겼다. 이를 해결하기 위해 Timeline에서 사용하는 연출 기능을 하나의 진입점으로 제공하는 Facade를 구현하여, 기획자가 내부 구조를 알지 않아도 필요한 연출을 쉽게 구성할 수 있도록 했다.

## 문제 분석

1. Timeline이 개별 연출 시스템을 직접 참조하면 결합도가 높아진다

   - 노트 페이드, 글로우, 화면 흔들림 등의 구현이나 메서드가 변경되면 Timeline Signal 연결도 함께 수정해야 한다
   - 연출 기능이 추가될수록 Timeline이 직접 참조해야 하는 컴포넌트가 늘어난다

2. 기획자가 연출별 호출 대상을 직접 파악해야 한다

   - 원하는 연출을 담당하는 스크립트와 호출할 메서드를 찾아 Signal에 직접 연결해야 한다
   - 기능이 여러 컴포넌트에 분산될수록 설정 과정이 복잡해지고 잘못된 메서드를 연결할 가능성이 커진다


## 설계 목표

1. Timeline에서 사용하는 연출 기능에 단일 진입점을 제공한다
   - Timeline Signal은 개별 연출 클래스를 직접 연결하지 않고 [`TimelineActionFacade`](../../src/timeline-action/TimelineActionFacade.cs)만 연결한다
   - 기획자가 하나의 컴포넌트에서 필요한 연출 함수를 선택할 수 있게 한다

2. 연출 기능별 책임을 분리한다
   - 노트 페이드, 글로우, 화면 흔들림을 각각의 Controller가 담당한다
   - Facade는 요청을 전달하고 각 Controller는 실제 연출을 처리한다

3. 연출 변경과 추가가 기존 Timeline에 미치는 영향을 줄인다
   - 내부 구현이 변경되어도 Facade의 공개 함수가 유지되면 기존 Signal 연결을 그대로 사용할 수 있게 한다

## 문제 접근 방법

### 기능별 Controller 분리

[구현 방식]

- 노트의 투명 상태와 Fade 처리는 [`NoteEffectController`](../../src/timeline-action/NoteEffectController.cs)가 담당한다
- 글로우 활성화, 색상과 강도 변경은 [`GlowEffectController`](../../src/timeline-action/GlowEffectController.cs)가 담당한다
- 판정 시 화면 흔들림의 상태와 매개변수는 [`ShakeEffectController`](../../src/timeline-action/ShakeEffectController.cs)가 담당한다
- 각 Controller는 자신의 연출 상태와 실행 로직만 관리한다

[선택 이유]

- 기능마다 변경 이유가 다르므로 하나의 클래스에 모든 연출 로직을 모으지 않기 위함
- 각 클래스가 하나의 연출 책임만 갖도록 구성해 SRP를 지키고 수정 영향 범위를 제한하기 위함

### Facade를 통한 단일 진입점 제공

[구현 방식]

- [`TimelineActionFacade`](../../src/timeline-action/TimelineActionFacade.cs)가 기능별 Controller를 직렬화된 참조로 보유한다
- Timeline Signal에서 호출할 공개 함수를 Facade에 모은다
- Facade는 요청을 해당 Controller에 전달하며 실제 연출 로직은 직접 처리하지 않는다

```text
Unity Timeline Signal
          │
          ▼
TimelineActionFacade
          │
          ├─ NoteEffectController  : 노트 Fade
          ├─ GlowEffectController  : Glow 활성화·색상·강도
          └─ ShakeEffectController : 판정 시 화면 흔들림
```

[선택 이유]

- Timeline이 내부 연출 클래스의 구성과 구현 방법을 알지 않아도 되게 하기 위함
- 기획자에게 Timeline에서 사용할 수 있는 함수만 한곳에 노출해 Signal 설정 과정을 단순화하기 위함
- 외부 호출 지점은 유지하면서 내부 Controller를 교체하거나 수정할 수 있게 하기 위함

## 관련 코드

- [`TimelineActionFacade`](../../src/timeline-action/TimelineActionFacade.cs)
- [`NoteEffectController`](../../src/timeline-action/NoteEffectController.cs)
- [`GlowEffectController`](../../src/timeline-action/GlowEffectController.cs)
- [`ShakeEffectController`](../../src/timeline-action/ShakeEffectController.cs)
