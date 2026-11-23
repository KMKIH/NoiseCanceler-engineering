# Editor Preview와 Play Mode에서의 FMOD 이벤트 실행 차이

FMOD Event Track으로 음원과 연출을 배치했을 때, Timeline 창의 Editor Preview에서는 박자가 맞았지만 Game View의 Play Mode에서는 어긋남이 느껴졌다. 같은 클립을 두 모드에서 재생할 때 음원이 어떻게 시작되는지 확인했다.

## FMOD Event Track의 재생 경로

두 모드 모두 Timeline의 재생 헤드가 클립에 들어오면 클립 시작점에서 얼마나 지났는지 계산한다. 이 값을 음원의 시작 위치로 사용한다. 재생 경로는 그다음 단계인 `OnEnter()`에서 `Application.isPlaying` 값에 따라 나뉜다.

| 실행 모드 | 클립 진입 후 음원을 재생하는 과정 |
|---|---|
| Editor Preview | 트랙이 `Enter` 이벤트를 보내면 FMOD 에디터의 미리듣기 시스템이 `LoadPreviewBanks()`를 호출한다. 이어서 `PreviewEvent()`가 에디터용 이벤트 인스턴스를 생성하고 지정한 위치에서 음원을 재생한다. |
| Play Mode | 트랙이 `PlayEvent()`를 직접 호출한다. 이 함수는 `RuntimeManager.CreateInstance()`로 런타임 이벤트 인스턴스를 만들고 지정한 위치에서 음원을 재생한다. |

Editor Preview에서는 미리듣기 시스템이 트랙의 이벤트를 받아 음원을 준비하고, Play Mode에서는 트랙이 런타임 인스턴스를 직접 생성해 재생한다. 두 모드에서 클립이 지정한 음원 시작 위치는 같아도, 클립 진입부터 재생이 시작되기까지 걸리는 시간은 달라질 수 있다.

## 참고 자료

프로젝트에 설치된 Unity 6000.0.67f1, Timeline 1.8.10과 FMOD Unity Integration을 기준으로 확인했다.

- **Unity Timeline 문서** — Timeline 창의 프리뷰와 Game View의 Play Mode가 어떻게 다른지 설명한다. [Timeline 재생 컨트롤](https://docs.unity3d.com/Packages/com.unity.timeline@1.8/manual/tl-play-ctrls.html)
- **FMOD 트랙 구현** — `FMODEventTrack.cs`와 `FMODEventPlayable.cs`에서 클립 진입 판정, `OnEnter()`의 분기, 런타임 인스턴스 재생을 확인했다.
- **FMOD 에디터 구현** — `Editor/EditorUtils.cs`에서 `Enter` 이벤트 수신, 프리뷰 Bank 로드, `PreviewEvent()`의 인스턴스 생성을 확인했다.
