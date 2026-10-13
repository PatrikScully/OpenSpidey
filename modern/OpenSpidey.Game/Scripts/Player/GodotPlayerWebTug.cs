using OpenSpidey.Core.Effects;
using OpenSpidey.Core.Mathematics;
using OpenSpidey.Core.Player;
using OpenSpidey.Game.Actors;
using OpenSpidey.Game.Effects;

namespace OpenSpidey.Game.Player;

public sealed class GodotPlayerWebTugConfiguration
{
    public required Func<uint> Clock { get; init; }
    public required Func<FixedVector3> CameraPositionWholeUnits { get; init; }
    public required Func<int, int> Random { get; init; }
    public required Func<FixedVector3, int> GroundY { get; init; }
    public required Action<FixedVector3, int, Func<int, int>> EmitFirePulse { get; init; }
    public required Action SetFirstContactDetails { get; init; }
    public required Func<GlowFlashParameters, Func<int, int>, GlowFlashState> EmitSnapGlow { get; init; }
}

/// <summary>Source action0x20000 and CWeb snap using the existing player web, real target actors and shared bit owners.</summary>
public sealed class GodotPlayerWebTug : IPlayerWebTugHost, IWebStrandSnapHost
{
    private readonly GodotPlayer player;
    private readonly GodotPlayerFireWeb fireWeb;
    private readonly GodotWebStrands strands;
    private readonly GodotPlayerWebTugConfiguration configuration;
    public PlayerWebTugState State { get; }
    private GodotPlayerMelee Melee => player.Melee ?? throw new InvalidOperationException("Web tug requires the genuine player melee/pose owner.");
    public bool AnimationFinished => player.Model.Playback.IsFinished;
    public FixedVector3 PlayerPosition => player.Controller.Position;

    public GodotPlayerWebTug(GodotPlayer player, GodotPlayerFireWeb fireWeb, GodotWebStrands strands,
        GodotPlayerWebTugConfiguration configuration)
    {
        this.player = player ?? throw new ArgumentNullException(nameof(player));
        State = new(player.Combat.State);
        this.fireWeb = fireWeb ?? throw new ArgumentNullException(nameof(fireWeb));
        this.strands = strands ?? throw new ArgumentNullException(nameof(strands));
        this.configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        strands.ConfigureSourcePhase(configuration.Clock, configuration.Random, configuration.EmitFirePulse);
    }

    public void UpdateThrow() => PlayerWebTugBehavior.Step(Melee.State, fireWeb.State, State, this);
    public bool CheckJump() => Melee.CheckJump();
    public IReadOnlyList<FixedVector3>? CalculatePath(WebStrandOwner strand)
    {
        PlayerKickHandle handle = strand.TargetHandle;
        // The original dereferences an expired handle. Leave the path absent so the owned web can retire safely.
        IWebStrandTarget? target = fireWeb.RecoverTarget(ref handle);
        return target is null ? null : PlayerTugWebPath.Calculate(PlayerPosition, SourcePosition(target));
    }
    public void Snap(WebStrandOwner strand, FixedVector3 direction, IReadOnlyList<FixedVector3>? path) =>
        strand.SwitchToSnap(direction, path, this, configuration.CameraPositionWholeUnits(), configuration.Random);
    public void OrientToNormal(bool useTarget, FixedVector3 target) => Melee.OrientToNormal(useTarget, target);
    public void PlayAnimation(ushort animation, int firstFrame, int lastFrame) => Melee.PlaySingleAnimation(animation, firstFrame, lastFrame);
    public void SwitchToStandMode() => Melee.SwitchToStandMode();
    public FixedVector3 SourcePosition(IWebStrandTarget target) => fireWeb.ResolveNativeTarget(target).SourcePosition;
    public FixedVector3 TargetAttachment(IWebStrandTarget target) => fireWeb.ResolveNativeTarget(target) is GodotManipulationActor manipulation
        ? manipulation.GetAttachPoint() : SourcePosition(target);
    public bool TugImpulse(IWebStrandTarget target, FixedVector3 contact, FixedVector3 direction, IReadOnlyList<FixedVector3>? path) =>
        fireWeb.ResolveNativeTarget(target) switch
        {
            GodotThugActor thug => new GodotThugWebTug(thug).TugImpulse(direction, path),
            GodotManipulationActor manipulation => manipulation.TugImpulse(contact, direction),
            // CHostage inherits CBaddy::TugImpulse: dispose the optional path and return0.
            GodotHostageActor => false,
            _ => throw new NotSupportedException("Web snap target has no proven original TugImpulse adapter.")
        };
    public void SetFirstContactDetails() => configuration.SetFirstContactDetails();
    public void EmitBrokenStrand(KnottedWebFragmentRequest request, Func<int, int> random) =>
        strands.EmitFragment(request, random, configuration.GroundY, configuration.CameraPositionWholeUnits);
    public GlowFlashState EmitSnapGlow(GlowFlashParameters parameters, Func<int, int> random) => configuration.EmitSnapGlow(parameters, random);
}
