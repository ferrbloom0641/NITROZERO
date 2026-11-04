#if UNITY_EDITOR
using System;
using UnityEngine;

// Drives the editor recorder once per rendered game frame, before the director.
[DefaultExecutionOrder(-2000)]
public sealed class BoostOutsideRecordingPump : MonoBehaviour
{
    [NonSerialized] public Action tick;
    [NonSerialized] public Action stopped;
    void Update()=>tick?.Invoke();
    void OnDestroy()=>stopped?.Invoke();
}
#endif
