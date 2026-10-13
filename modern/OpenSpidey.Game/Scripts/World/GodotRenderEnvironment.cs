using Godot;
using OpenSpidey.Assets.Models;
using OpenSpidey.Assets.Textures;
using OpenSpidey.Core.Actors;
using OpenSpidey.Core.Missions;
using OpenSpidey.Core.Presentation;
using OpenSpidey.Core.Rendering;

namespace OpenSpidey.Game.World;

/// <summary>Scene-owned original sky clear, OBJ backdrops and fog command/render phases.</summary>
public partial class GodotRenderEnvironment : Node3D,IRenderEnvironmentHost,OpenSpidey.Core.Rendering.IPlayerWaterEffectFrameHost
{
    private Func<Camera3D?> camera=null!;
    private Func<uint> sourceClock=null!;
    private Node3D worldRoot=null!;
    private Godot.Environment? previousEnvironment;
    private World3D? sourceWorld;
    private Godot.Environment environment=null!;
    private bool disposed;
    private readonly GodotSourceFogMaterials materials=new();
    private (ushort Duration,ushort Minimum,ushort Range)? pendingFog;
    private uint lastClock;
    public RenderFogState Fog {get;}=new();
    public SourcePolygonDepthState SourceDepth { get; } = new();
    public GodotSourceBackdrops Backdrops {get;private set;}=null!;
    public uint SkyColour {get;private set;}
    public uint SkyColourTarget {get;private set;}
    public int SkyRestorations {get;private set;}
    public int FogSurfaceCount=>materials.SurfaceCount;
    public static GodotRenderEnvironment Create(Node3D worldRoot,Func<PsxAssetLibrary> objectBank,
        Func<IReadOnlyList<DecodedTexture>> objectTextures,Func<Camera3D?> camera,Func<uint> sourceClock)
    {
        var result=new GodotRenderEnvironment {Name="OriginalRenderEnvironment",worldRoot=worldRoot,camera=camera,sourceClock=sourceClock};
        result.Backdrops=GodotSourceBackdrops.Create(objectBank,objectTextures);result.AddChild(result.Backdrops);return result;
    }
    public override void _Ready()
    {
        sourceWorld=GetWorld3D();
        previousEnvironment=sourceWorld.Environment;
        environment=new Godot.Environment {BackgroundMode=Godot.Environment.BGMode.Color,BackgroundColor=Colors.Black};
        sourceWorld.Environment=environment;
        lastClock=sourceClock();
        materials.Attach(worldRoot);materials.Synchronize(Fog);
    }
    public void SetFog(ushort duration,ushort near,ushort range)=>pendingFog=(duration,near,range);
    public void SetSkyColour(uint packed)
    {
        SkyColourTarget=packed;
        ApplySkyColour(packed);
    }
    public void RestoreSkyColourTarget(){ApplySkyColour(SkyColourTarget);SkyRestorations++;}
    private void ApplySkyColour(uint packed)
    {
        // Db_UpdateSky's exact legacy special case and -1/no-clear behavior.
        SkyColour=packed==0x466973?0x154070:packed;
        environment.BackgroundMode=SkyColour==uint.MaxValue?Godot.Environment.BGMode.Keep:Godot.Environment.BGMode.Color;
        environment.BackgroundColor=new Color(((SkyColour>>16)&255)/255f,((SkyColour>>8)&255)/255f,(SkyColour&255)/255f);
    }
    public void SetFadeColour(uint packed)=>Fog.FadeColour=packed;
    public void CreateBackground(uint checksum,ActorAngles angularVelocity)=>Backdrops.Add(checksum,angularVelocity);
    public void SetBackgroundVisible(ushort index,bool visible)=>Backdrops.SetVisible(index,visible);
    public void EndCommandList()
    {
        if(pendingFog is not { } command)return;
        Fog.Set(command.Duration,command.Minimum,command.Range);pendingFog=null;materials.Synchronize(Fog);
    }
    public override void _Process(double delta) => SynchronizeSourceRender();
    public void SynchronizeSourceRender()
    {
        SourceDepth.BeginRender();
        // M3d_RenderSetup advances the fog transition once per render, not once per source physics tick.
        Fog.AdvanceRender();materials.Attach(worldRoot);materials.Synchronize(Fog);
        uint clock=sourceClock();
        if(clock!=lastClock){Backdrops.AdvanceSource();lastClock=clock;}
        if(camera() is { } view)Backdrops.Synchronize(view);
    }
    public override void _Notification(int what)
    {
        if(what!=NotificationPredelete||disposed)return;
        disposed=true;
        materials.Dispose();
        if(sourceWorld is not null && GodotObject.IsInstanceValid(sourceWorld) && sourceWorld.Environment==environment)
            sourceWorld.Environment=previousEnvironment;
        environment?.Dispose();
        sourceWorld=null;
    }
}
