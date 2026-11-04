using System;
using UnityEngine;

public sealed partial class AutonomousRaceController
{
    [Header("Trailer story: White / Blue / Khaki / Red / Yellow / Lime")]
    public bool storyMode;
    [Tooltip("Road-space cue. White reaches this point as Blue begins the successful corner pass.")]
    public float storyCornerCue = 2230;
    [Range(0, 1)] public float storyAggression = 1;
    public string StoryBeat { get; private set; } = "";
    public float StoryBoostAmount { get; private set; }
    // Existing racer slots: Blue=0, Khaki=1, White=2, Red=3, Yellow=4, Lime=5.
    const float StoryStart = 300, StorySpeed = 240f / 3.6f;
    BoosterDeploymentController storyBooster;
    bool boosterWasEnabled;
    float storyBase, storyBaseSpeed;
    float[] storyOffsets = new float[6];
    static readonly string[] StoryRoles = { "파랑: 카키와 동맹 / 흰색 추월", "카키: 동맹 보호 / 빨강 견제", "흰색: 선두 방어", "빨강: 주인공 / 코너 부스터 역전", "노랑: 후방 추격", "라임: 후방 추격" };
    float Cue(float original) => original + storyCornerCue - 2230;
    static float Blend(float a, float b, float x) => Ease(Mathf.InverseLerp(a, b, x));

    // Each attempt has an approach, a brief hold beside the defensive line, and an aborted return.
    // Alternate sides only while the pulse is zero, so changing strategy never snaps the steering.
    static float AttackPulse(float p, float start, float period, out float side)
    {
        float cycle = Mathf.Max(0, (p-start)/period);
        int attempt = Mathf.FloorToInt(cycle);
        float t = cycle-attempt;
        side = (attempt & 1) == 0 ? -1 : 1;
        return Blend(0,.38f,t) * (1-Blend(.55f,1,t));
    }

    float CombatWeight(float p) => Mathf.Clamp01(storyAggression) *
        (1-Blend(Cue(1600),Cue(1950),p));

    void InitializeStory()
    {
        if (!storyMode) return;
        // The exceptionally wide White and Red need 28m including separation, inside the 30m asphalt.
        usableHalfWidth = Mathf.Min(Vector3.Distance(left[0], right[0]) * .5f - .5f - smoothRoadInset, usableHalfWidth + edgeMargin - .5f);
        LaneExtent = usableHalfWidth;
        storyBooster = racers[3].car.GetComponentInChildren<BoosterDeploymentController>(true);
        if (storyBooster != null)
        {
            boosterWasEnabled = storyBooster.enabled;
            storyBooster.SetCinematicOwner(racers[3].car);
            storyBooster.enabled = false;
            // Reserve the complete transformation envelope, not just the narrower stowed body.
            var red = racers[3];
            float width = red.halfWidth, length = red.halfLength, ride = red.rideHeight;
            for (int pose = 0; pose <= 32; pose++)
            {
                storyBooster.ApplyCinematicAmount(pose / 32f);
                Measure(red);
                width = Mathf.Max(width, red.halfWidth); length = Mathf.Max(length, red.halfLength);
            }
            storyBooster.ApplyCinematicAmount(0);
            red.halfWidth = width + .05f; red.halfLength = length + .05f; red.rideHeight = ride;
        }
        else Debug.LogWarning("Story: Red booster controller is missing; racing still runs, deployment visuals unavailable.", this);
        if (2 * usableHalfWidth < 2 * (racers[2].halfWidth + racers[3].halfWidth) + 1)
            throw new InvalidOperationException("Story pass needs wider asphalt for White and deployed Red.");
    }

    void RestartStory()
    {
        storyBase = StoryStart; storyBaseSpeed = StorySpeed;
        SetStoryOffsets(storyBase);
        for (int i=0; i<racers.Length; i++)
        {
            var r = racers[i];
            r.distance = r.previousDistance = r.startDistance = storyBase + storyOffsets[i];
            r.lateral = r.previousLateral = StoryLane(i, storyBase);
            r.targetLane = r.homeLane = r.lateral;
            r.speed = r.previousSpeed = StorySpeed;
            r.lateralVelocity = r.yawVelocity = r.wheelVelocity = r.wheelAngle = r.previousWheelAngle = 0;
            r.opponent = null; r.phase = RacePhase.Cruise; r.phaseTime=0; r.completedPasses=0; r.abortedPasses=0;
            r.up = Vector3.up;
            Vector3 forward = Direction(r.distance);
            r.yaw = Mathf.Atan2(forward.x, forward.z) * Mathf.Rad2Deg;
            r.rotation = r.previousRotation = Quaternion.LookRotation(forward, r.up);
            Place(r,r.distance,r.lateral,r.rotation,0,r.speed,0);
        }
        UpdateStoryBeat(); PresentStoryBoost(0);
    }

    void SetStoryOffsets(float p)
    {
        storyOffsets[2] = 0;
        storyOffsets[0] = Mathf.Lerp(-26, 26, Blend(Cue(2230), Cue(2700), p));
        storyOffsets[1] = -52;
        storyOffsets[3] = Mathf.Lerp(-78, 52, Blend(Cue(2460), Cue(3490), p));
        storyOffsets[4] = -112 + 8 * Blend(600, Cue(2100), p);
        storyOffsets[5] = -146 + 12 * Blend(600, Cue(2100), p);
        float weight = CombatWeight(p);
        storyOffsets[0] += weight * 7 * AttackPulse(p,350,650,out _);
        storyOffsets[1] += weight * 2 * AttackPulse(p,570,700,out _);
        storyOffsets[3] += weight * 6 * AttackPulse(p,540,700,out _);
        storyOffsets[4] += weight * 10 * AttackPulse(p,440,740,out _);
        storyOffsets[5] += weight * 8 * AttackPulse(p,650,780,out _);
        // After the hero pass, the trailing pack closes again instead of settling into a procession.
        float pursuitPack = Mathf.Clamp01(storyAggression) * Blend(Cue(3800),Cue(4450),p);
        storyOffsets[1] += 24*pursuitPack;
        storyOffsets[4] += 50*pursuitPack;
        storyOffsets[5] += 54*pursuitPack;
        float latePressure = Mathf.Clamp01(storyAggression) * Blend(Cue(3900),Cue(4250),p) * (1-Blend(Cue(4800),Cue(5100),p));
        foreach(int slot in new[]{0,2,1,4,5})
            storyOffsets[slot] += 5*latePressure*AttackPulse(p,Cue(3900)+slot*65,680+slot*20,out _);
    }

    float StoryLane(int i, float p)
    {
        float fade = Blend(300,700,p);
        float weave = fade * 2.2f * Mathf.Sin((p - 300) / 130f);
        float defense = fade * 2.2f * Mathf.Sin((p - 325) / 130f);
        float heroProbe = fade * 2f * Mathf.Sin((p - 340) / 155f);
        float khakiBlock = fade * 2f * Mathf.Sin((p - 365) / 155f);
        float combat = CombatWeight(p);
        float blueAttack = AttackPulse(p,350,650,out float blueSide);
        float whiteDefense = AttackPulse(p,385,650,out float whiteSide);
        float redAttack = AttackPulse(p,540,700,out float redSide);
        float khakiDefense = AttackPulse(p,570,700,out float khakiSide);
        weave = Mathf.Lerp(weave,4.2f*blueSide*blueAttack,combat);
        defense = Mathf.Lerp(defense,4.2f*whiteSide*whiteDefense,combat);
        heroProbe = Mathf.Lerp(heroProbe,4.6f*redSide*redAttack,combat);
        khakiBlock = Mathf.Lerp(khakiBlock,4.6f*khakiSide*khakiDefense,combat);
        float inner = Extent(racers[i]) - .15f;
        float outer = -inner;
        float value;
        switch(i)
        {
            case 2: value = Mathf.Lerp(defense, inner, Blend(Cue(1960),Cue(2400),p)); break;
            case 0:
                value = Mathf.Lerp(weave, outer, Blend(Cue(1960),Cue(2400),p));
                value = Mathf.Lerp(value, inner, Blend(Cue(2740),Cue(3180),p)); break;
            case 1: value = Mathf.Lerp(khakiBlock,inner,Blend(Cue(1960),Cue(2390),p)); break;
            case 3:
                value = Mathf.Lerp(heroProbe,outer,Blend(Cue(2050),Cue(2440),p));
                value = Mathf.Lerp(value,0,Blend(Cue(3550),Cue(3900),p)); break;
            default:
                float pursuit = AttackPulse(p,i==4?440:650,i==4?740:780,out float pursuitSide);
                float initial = i==4 ? -2f : 2f;
                float attackLane = initial + (i==4?3.8f:4f)*pursuitSide*pursuit;
                value = Mathf.Lerp(Mathf.Lerp(initial,attackLane,combat),inner,Blend(Cue(1960),Cue(2410),p)); break;
        }
        float lateBlend = Mathf.Clamp01(storyAggression)*Blend(Cue(3800),Cue(4300),p);
        float latePulse = AttackPulse(p,Cue(3900)+(i==3?35:i*65),680+(i==3?0:i*20),out float lateSide);
        float lateLane = 4.2f*lateSide*latePulse*(1-Blend(Cue(4800),Cue(5100),p));
        value = Mathf.Lerp(value,lateLane,lateBlend);
        return Mathf.Clamp(value,-Extent(racers[i]),Extent(racers[i]));
    }

    void SimulateStory(float dt)
    {
        Elapsed += dt;
        float limit = RoadLength - 15 - 52 - racers[3].halfLength;
        float remaining = Mathf.Max(0, limit-storyBase);
        float desired = Mathf.Min(StorySpeed, Mathf.Sqrt(2 * braking * remaining));
        storyBaseSpeed = Mathf.MoveTowards(storyBaseSpeed, desired, braking*dt);
        float nextBase = Mathf.Min(limit, storyBase + storyBaseSpeed*dt);
        storyBaseSpeed = Mathf.Max(0,(nextBase-storyBase)/dt); storyBase=nextBase;
        SetStoryOffsets(storyBase);
        for(int i=0;i<racers.Length;i++)
        {
            var r=racers[i];
            r.previousDistance=r.distance; r.previousLateral=r.lateral;
            r.previousRotation=r.rotation; r.previousSpeed=r.speed; r.previousWheelAngle=r.wheelAngle;
            r.distance=storyBase+storyOffsets[i];
            r.speed=(r.distance-r.previousDistance)/dt;
            r.lateral=StoryLane(i,storyBase); r.targetLane=r.lateral;
            r.lateralVelocity=(r.lateral-r.previousLateral)/dt;
            r.phase = (i==0 && storyBase>=Cue(2230) && storyBase<Cue(2700)) ||
                (i==3 && storyBase>=Cue(2460) && storyBase<Cue(3490)) ? RacePhase.Pass : RacePhase.Cruise;
            if (CombatWeight(storyBase)>.1f)
            {
                float pulse=AttackPulse(storyBase,i==0||i==2?350:i==4?440:i==5?650:540,
                    i==0||i==2?650:i==4?740:i==5?780:700,out _);
                if (pulse>.15f) r.phase = RacePhase.Prepare;
            }
        }
        foreach(var r in racers) UpdateSteering(r,dt);
        CompletedOvertakes=(storyBase>=Cue(2700)?1:0)+(storyBase>=Cue(3490)?3:0);
        UpdateStoryBeat();
    }

    void UpdateStoryBeat()
    {
        StoryBeat = storyBase<Cue(2120) ? "White blocks Blue / Khaki protects Blue from Red" :
            storyBase<Cue(2460) ? "Corner entry: Blue attacks / Red opens wide + deploys" :
            storyBase<Cue(2700) ? "Blue passes White / Red boost accelerates" :
            storyBase<Cue(3490) ? "Red sweeps past Khaki, White, Blue" :
            storyBase<Cue(3760) ? "Red leads / booster retracts" : "Red > Blue > White > Khaki > Yellow > Lime";
    }

    void PresentStoryBoost(float time)
    {
        // Time-based cues match 240km/h progression; seek/restart/pause evaluate the same pose.
        float p = StoryStart + Mathf.Max(0,time)*StorySpeed;
        StoryBoostAmount = Mathf.Clamp01((p-Cue(2200))/(3.2f*StorySpeed)) *
            (1-Mathf.Clamp01((p-Cue(3500))/(3.2f*StorySpeed)));
        if (storyBooster != null) storyBooster.ApplyCinematicAmount(StoryBoostAmount);
    }
}
