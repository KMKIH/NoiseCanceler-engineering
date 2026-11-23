using System;
using System.Linq;
using FMOD.Studio;
using FMODUnity;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

// Test Code
// 트랙에서 시작한 FMOD 인스턴스의 재생 위치를 Timeline 시각과 비교하기 위한 트랙이다.
[TrackColor(0.1f, 0.55f, 0.45f)]
[TrackClipType(typeof(CustomFMODEventClip))]
[TrackBindingType(typeof(PlayManager))]
public class CustomFMODEventTrack : TrackAsset
{
    public override Playable CreateTrackMixer(PlayableGraph graph, GameObject go, int inputCount)
    {
        var playable = ScriptPlayable<CustomFMODEventMixer>.Create(graph, inputCount);
        var mixer = playable.GetBehaviour();
        mixer.Director = go.GetComponent<PlayableDirector>();
        mixer.Clips = GetClips().ToArray();
        return playable;
    }
}

// Test Code
// 클립 진입 시 음원을 재생하고, 위치를 측정할 수 있도록 같은 인스턴스를 PlayManager에 전달한다.
public class CustomFMODEventMixer : PlayableBehaviour
{
    public PlayableDirector Director;
    public TimelineClip[] Clips;
    private EventInstance instance;
    private PlayManager receiver;
    private TimelineClip activeClip;
    private bool failed;

    public override void ProcessFrame(Playable playable, FrameData info, object playerData)
    {
        // 이 테스트 트랙은 런타임 전용이다. 에디터 스크러빙에서는 음원을 생성하지 않는다.
        if (!Application.isPlaying || Director == null) return;
        if (playerData is not PlayManager target) return;
        if (receiver != null && receiver != target) Stop();
        receiver = target;

        TimelineClip current = null;
        foreach (var clip in Clips)
        {
            if (Director.time >= clip.start && Director.time < clip.end)
            {
                if (current != null)
                {
                    Fail("BGM 클립은 동시에 하나만 재생할 수 있습니다.");
                    return;
                }
                current = clip;
            }
        }
        if (current != activeClip)
        {
            Stop();
            activeClip = current;
            failed = false;
        }
        if (current == null || failed) return;
        var asset = (CustomFMODEventClip)current.asset;
        if (!instance.isValid())
        {
            // Evaluate/시킹만으로 새 음원을 재생하지 않는다.
            if (info.evaluationType != FrameData.EvaluationType.Playback && !receiver.IsStartingManualTimeline) return;
            if (!receiver.IsSongPreloaded)
            {
                Fail("BGM 샘플 사전 로드가 끝나기 전에 Timeline이 실행됐습니다.");
                return;
            }
            instance = RuntimeManager.CreateInstance(asset.EventReference);
            if (!Check(instance.setVolume(asset.Volume))) return;
            int startMs = (int)Math.Round((asset.StartPositionSeconds + Director.time - current.start) * 1000.0);
            if (!Check(instance.setTimelinePosition(Math.Max(0, startMs)))) return;
            if (!Check(instance.start())) return;
            receiver.ConnectTimelineSong(instance, current.start, asset.StartPositionSeconds);
        }
        // 보정 때문에 매 프레임 setTimelinePosition을 호출하지 않는다.
        bool paused = Director.state != PlayState.Playing || info.effectiveSpeed == 0;
        instance.setPaused(paused);
        if (!paused) instance.setPitch((float)Math.Abs(info.effectiveSpeed));
    }

    public override void OnGraphStop(Playable playable)
    {
        // Director.Pause는 인스턴스를 보존한다. Stop/그래프 해제는 OnPlayableDestroy에서 정리한다.
        if (instance.isValid()) instance.setPaused(true);
    }

    public override void OnPlayableDestroy(Playable playable) => Stop();

    private bool Check(FMOD.RESULT result)
    {
        if (result == FMOD.RESULT.OK) return true;
        Fail($"FMOD BGM 재생 실패: {result}");
        return false;
    }

    private void Fail(string message)
    {
        failed = true;
        if (receiver != null) receiver.FailTimelineSong(message);
        Stop();
        Debug.LogError(message, Director);
    }

    private void Stop()
    {
        if (!instance.isValid()) return;
        if (receiver != null) receiver.DisconnectTimelineSong(instance);
        instance.stop(FMOD.Studio.STOP_MODE.IMMEDIATE);
        instance.release();
        instance.clearHandle();
    }
}
