using System;
using System.IO;
using UnityEditor;
using UnityEditor.Recorder;
using UnityEditor.Recorder.Input;
using UnityEditor.Recorder.Encoder;
using UnityEngine;

public sealed class BoostOutsideRecordingRunner
{
    BoostOutsideDirector director;
    BoostOutsideRecordingPump pump;
    RecorderController controller;
    RecorderControllerSettings settings;
    MovieRecorderSettings movie;
    string folder,stem;
    int width,fps,shot,frame,warmup=12,totalFrames,oldShot;
    float oldTime,oldScale,oldCapture;
    bool exitPlay,finished,destroying,oldPaused,oldControls,oldBackground;
    public void Initialize(BoostOutsideDirector d,string path,int size,int rate,bool exit)
    {
        director=d;folder=path;width=size;fps=rate;exitPlay=exit;
        oldTime=d.time;oldShot=d.selectedShot;oldPaused=d.paused;oldControls=d.showControls;
        oldScale=Time.timeScale;oldCapture=Time.captureDeltaTime;oldBackground=Application.runInBackground;
        Time.timeScale=1;Time.captureDeltaTime=1f/fps;Application.runInBackground=true;
        director.batchRecording=true;director.showControls=false;
        totalFrames=Mathf.CeilToInt(d.takeDuration*fps);
        File.WriteAllText(Path.Combine(folder,"takes.tsv"),"File\tShot\tFrames\tFPS\tDuration\n");
        ResetTake();
        pump=new GameObject("Boost Outside Recording Pump"){hideFlags=HideFlags.DontSave}.AddComponent<BoostOutsideRecordingPump>();
        pump.tick=Update;pump.stopped=OnDestroy;
    }
    void ResetTake()
    {
        director.SelectShot(shot);director.Replay();director.paused=true;frame=0;warmup=12;
    }
    void Update()
    {
        if(finished || !director)return;
        try
        {
            if(warmup-->0)return;
            if(controller==null)
            {
                // Warm up render history, then clear effects so every take starts cleanly.
                director.Replay();director.paused=true;
                settings=ScriptableObject.CreateInstance<RecorderControllerSettings>();
                settings.SetRecordModeToManual();settings.FrameRatePlayback=FrameRatePlayback.Constant;
                settings.FrameRate=fps;settings.CapFrameRate=false;settings.ExitPlayMode=false;
                movie=ScriptableObject.CreateInstance<MovieRecorderSettings>();
                movie.name="Boost Outside Take";movie.Enabled=true;movie.CaptureAudio=true;
                movie.ImageInputSettings=new GameViewInputSettings{OutputWidth=width,OutputHeight=width*9/16};
                movie.EncoderSettings=new CoreEncoderSettings{Codec=CoreEncoderSettings.OutputCodec.MP4,
                    EncodingQuality=CoreEncoderSettings.VideoEncodingQuality.Custom,TargetBitRate=width==1920?60:140,
                    GopSize=(uint)fps,EncodingProfile=CoreEncoderSettings.H264EncodingProfile.High,NumConsecutiveBFrames=2};
                string name=director.shots[shot].name;
                foreach(char c in Path.GetInvalidFileNameChars())name=name.Replace(c,'_');
                stem=$"{shot+1:00}_{name}";
                movie.OutputFile=Path.Combine(folder,stem).Replace('\\','/');
                settings.AddRecorderSettings(movie);controller=new RecorderController(settings);
                controller.PrepareRecording();
                if(!controller.StartRecording())throw new InvalidOperationException("Recorder가 시작되지 않았습니다. Console을 확인해 주세요.");
                // Recorder's newly created frame requester becomes active next frame.
                // Hold t=0 during setup so the first captured frame starts every take at zero.
                return;
            }
            if(!controller.IsRecording())throw new InvalidOperationException("Recorder가 예기치 않게 중단되었습니다.");
            if(frame>=totalFrames)
            {
                StopRecorder();
                string output=Path.Combine(folder,stem+".mp4");
                if(!File.Exists(output) || new FileInfo(output).Length==0)throw new IOException("출력 파일이 생성되지 않았습니다: "+output);
                File.AppendAllText(Path.Combine(folder,"takes.tsv"),$"{stem}.mp4\t{shot+1}\t{totalFrames}\t{fps}\t{totalFrames/(float)fps:F3}\n");
                shot++;
                if(shot>=director.shots.Length){Finish($"완료: {shot}개 영상 저장 — {folder}");return;}
                ResetTake();return;
            }
            director.time=frame/(float)fps;director.paused=true;director.showControls=false;
            BoostOutsideBatchRecorder.Status=$"녹화 {shot+1}/{director.shots.Length} · {stem} · {frame/(float)fps:F1}/{director.takeDuration:F1}초";
            frame++;
        }
        catch(Exception e){Debug.LogException(e);Finish("녹화 실패: "+e.Message);}
    }
    void StopRecorder()
    {
        controller?.StopRecording();controller=null;
        if(settings)UnityEngine.Object.DestroyImmediate(settings);if(movie)UnityEngine.Object.DestroyImmediate(movie);
    }
    public void Finish(string message)
    {
        if(finished)return;finished=true;
        try{StopRecorder();}
        finally
        {
            Time.timeScale=oldScale;Time.captureDeltaTime=oldCapture;Application.runInBackground=oldBackground;
            if(director){director.batchRecording=false;director.showControls=oldControls;director.SelectShot(oldShot);director.Sample(oldTime);director.paused=oldPaused;}
            BoostOutsideBatchRecorder.Status=message;BoostOutsideBatchRecorder.Runner=null;
            Debug.Log("[Boost Outside] "+message);
            if(exitPlay)EditorApplication.isPlaying=false;
            if(!destroying && pump)UnityEngine.Object.Destroy(pump.gameObject);
        }
    }
    void OnDestroy(){destroying=true;if(!finished && director)Finish("Play 종료로 녹화를 중단했습니다. 완료 파일은 유지됩니다.");}
}
