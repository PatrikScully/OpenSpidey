using OpenSpidey.Core.Actors;
using OpenSpidey.Core.Actors.Procedures;
using OpenSpidey.Core.Actors.Thugs;
using OpenSpidey.Core.Actors.Venom;
using OpenSpidey.Core.Effects;
using OpenSpidey.Core.Mathematics;
using OpenSpidey.Game.Player;

namespace OpenSpidey.Game.Actors;

/// <summary>CVenom combat boundaries: MechList player fields, trap/tug web contacts, world line probe and pose joints.</summary>
public partial class GodotVenomActor : IVenomCombatHost
{
    public IVenomCombatHost? Combat => this;
    private GodotPlayer RequirePlayer() =>
        registry.Player ?? throw new InvalidOperationException("CVenom combat states require MechList.");
    public uint PlayerActionState => RequirePlayer().Combat.State.ActionState;
    public short PlayerHealth => RequirePlayer().Combat.State.Health;
    public int PlayerField36C => RequirePlayer().Combat.State.Field36C;
    public bool PlayerCrawling => RequirePlayer().Combat.State.IsCrawling;
    public byte PlayerField8E8 => RequirePlayer().Combat.State.Field8E8;
    public byte PlayerField8E9 => RequirePlayer().Combat.State.Field8E9;
    public void SetAnimationFrame(int frame) => Model.SetSourceAnimationFrame(frame);

    private IThugTrapWeb? Web(bool tug) => configuration.RecoverWeb(this, tug);
    public bool WebHeld(bool tug) => Web(tug) is not null;
    public void BurstWeb(bool tug) => Web(tug)?.Burst();
    public int TrapWebHookCount() => Web(false) switch
    {
        null => -1,
        TrapWebEffect web => web.HookCount,
        _ => throw new NotSupportedException("Venom trap webs must be CTrapWebEffect contacts.")
    };

    public void SetArmPose(FixedVector3 first, FixedVector3 second)
    {
        if (first == default && second == default && !Model.SourcePoseEnabled) return;
        throw new NotSupportedException("CVenom arm-stretch pose joints 16 and 17 (venom pose links at 0x55A3B4) are not connected.");
    }

    public void SendSelfMessage(int code) => Messages.Enqueue(new ThugMessage(code, this));

    public VenomLineHit? LineToItem(FixedVector3 start, FixedVector3 end)
    {
        int length = VenomLineProbe.Length(start, end);
        if (length <= 0 || World.Trace(start, end) is not { } hit) return null;
        // SLineInfo.Distance is the hit parameter in whole units along the line; derive it from the contact point.
        return new VenomLineHit(FixedVectorMath.OriginalDistance(start, hit.Position), length);
    }

    // CVenomElectrified spawns CSkinGoo particles (0x4E9540, 0x43A690); only the existence flag is tracked here.
    public void SetElectrified(bool present) { }

    public void DeleteWeb() =>
        throw new NotSupportedException("The CWeb at CVenom 0x428 is only created by the unported WebYankAndPunch/ImmediateAttack states.");
}
