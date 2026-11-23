using FMODUnity;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

// Test Code
// 트랙 재생 실험에 사용할 FMOD 이벤트와 음원 시작 위치, 음량을 지정하는 클립이다.
public class CustomFMODEventClip : PlayableAsset, ITimelineClipAsset
{
    public EventReference EventReference;
    [Min(0)] public double StartPositionSeconds;
    [Range(0, 1)] public float Volume = 1;
    public ClipCaps clipCaps => ClipCaps.None;

    public override Playable CreatePlayable(PlayableGraph graph, GameObject owner)
    {
        return Playable.Create(graph);
    }
}
