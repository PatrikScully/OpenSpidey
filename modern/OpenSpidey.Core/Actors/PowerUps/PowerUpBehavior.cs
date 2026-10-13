using OpenSpidey.Core.Effects;
using OpenSpidey.Core.Mathematics;
using OpenSpidey.Core.Missions;

namespace OpenSpidey.Core.Actors.PowerUps;

public interface IPowerUpHost
{
    int LogicFrame { get; }
    uint Clock { get; }
    int LevelId { get; }
    int PlayerMaxHealth { get; }
    bool SpeechBusy { get; }
    CampaignProgress Progress { get; }
    int Random(int bound);
    int GroundHeight(FixedVector3 position, int above, int below);
    void ShadowOn();
    void KillShadow();
    void InitializeItemsBank();
    void SelectModel(uint checksum);
    void AttachToPowerUpList();
    void SendNodePulses(ushort node);
    void SendSignal(ushort node);
    void MarkDead();
    void EmitGlow(GlowFlashParameters parameters);
    void CreatePickupBit(uint tint);
    void SetPickupBitPosition(FixedVector3 position);
    void DeletePickupBit();
    void DeleteGlow();
    bool IncreaseHealth(int amount);
    bool IncreaseWebbing(int amount);
    bool SetFireWebbing();
    bool SetArmor(bool active);
    void IncreasePickupRevision(int amount);
    void PlaySound(ushort sound, int volume, int pitch);
    void PlaySpeech(int bank, int slot, int priority);
}

/// <summary>Entire source CPowerUp constructor, AI, physics, age, collection effect and owned-effect deletion.</summary>
public static class PowerUpBehavior
{
    public static ushort? RemapType(int type, int difficulty)
    {
        if (difficulty == 3)
        {
            if (type == 14) return null;
            if (type == 15) type = 14;
            else if (type == 16) type = 15;
        }
        else if (difficulty <= 1 && type == 14) type = 15;
        return unchecked((ushort)type);
    }

    public static void Construct(PowerUpState state, ushort type, FixedVector3 position, FixedVector3 velocity,
        uint flags, int lifetime, int signalNode, IPowerUpHost host)
    {
        state.FailedCollectionFlash = 0; state.Type = type;
        state.Lifetime = unchecked((ushort)lifetime); state.SignalNode = unchecked((ushort)signalNode);
        state.GroundClearance = 128; state.Dropping = 1;
        state.WorkingPosition = state.Position = position;
        if ((flags & 8) != 0) state.Velocity = state.Velocity with { Y = -245760 };
        else state.Velocity = velocity;
        state.FrictionX = state.FrictionY = state.FrictionZ = 5;
        state.AngularVelocityY = 24; state.AccelerationY = 32768;
        state.Field122 = unchecked((short)(host.Random(20) + 90));
        state.Field11E = state.Field120 = 16; state.ShadowScale = 32;
        ShadowOn(state, host);
        if ((flags & 16) != 0) { state.Falling = 1; state.GroundCheckFinished = 1; }
        else
        {
            if ((flags & 4) != 0) state.Falling = 1;
            int height = host.GroundHeight(state.WorkingPosition, 512, 8000);
            state.GroundHeight = height;
            if (height == -1) KillShadow(state, host);
            else if ((flags & 4) == 0)
            {
                state.ShadowPosition = state.Position with { Y = height };
                if (type != 18 || GameWord.Absolute(unchecked(state.Position.Y - height) >> 12) < 64)
                    state.WorkingPosition = state.Position = state.Position with { Y = unchecked(height - 262144) };
            }
        }
        state.Radius = 128; state.ShadowThreshold = 300;
        host.InitializeItemsBank();
        uint? checksum = type switch
        {
            8 => 0x17646B0D, 10 => 0xA092D785, 11 => 0x7F648179, 12 => 0x12820A41, 13 => 0xC6739C3B,
            14 or 15 or 16 => 0x7E74F3D4, 18 => null,
            _ => throw new InvalidDataException($"Bad original power-up type {type}.")
        };
        if (type == 8) state.Health = 4096;
        else if (type == 14) state.Health = 20;
        else if (type == 15) state.Health = 50;
        else if (type == 16) state.Health = 100;
        state.IsThreeDimensional = type == 18 ? (byte)0 : (byte)1;
        if (checksum is uint model) { state.ModelChecksum = model; host.SelectModel(model); }
        switch (type)
        {
            case 10:
                state.Health = unchecked((short)state.Lifetime); state.Lifetime = ushort.MaxValue;
                break;
            case 18:
                if (state.Lifetime != ushort.MaxValue)
                { state.Health = unchecked((short)state.Lifetime); state.Lifetime = ushort.MaxValue; }
                break;
        }
        // Actual PC46B352 attaches before hiding the non-model pickup and before already-collected comic death.
        host.AttachToPowerUpList();
        if (state.IsThreeDimensional == 0) state.ItemFlags |= 1;
        if (type == 10 && (host.Progress.CollectedComicMask & (1u << state.Health)) != 0) Die(state, host);
    }

    public static void SetNode(PowerUpState state, int node) { state.HasNode = 1; state.NodeId = unchecked((ushort)node); }
    public static void SetGravity(PowerUpState state, int gravity, int friction)
    { state.AccelerationY = gravity; state.FrictionY = unchecked((byte)friction); }

    public static void Update(PowerUpState state, IPowerUpHost host)
    {
        state.ShadowScale = (state.ItemFlags & 1) != 0 && state.IsThreeDimensional != 0 ? (ushort)0 : (ushort)32;
        if (state.FailedCollectionFlash != 0)
        { state.Tint = 0x101010; state.ItemFlags |= 0xC00; state.FailedCollectionFlash = 0; }
        else state.ItemFlags &= 0xF3FF;
        if (state.FadeCounter != 0)
        {
            if (state.FadeCounter <= 2) { Die(state, host); return; }
            state.ItemFlags |= 0x400; state.FadeCounter = unchecked(state.FadeCounter - 2);
            uint tint = unchecked((uint)(unchecked(255 * state.FadeCounter) / 64));
            state.Tint = tint | ((tint << 8) | tint) << 8;
        }
        CheckAge(state, host);
        if (state.PlayerDistance > 8000) { DeleteStuff(state, host); return; }
        if (state.GroundHeight != -1) ShadowOn(state, host);
        DoPhysics(state, host);
        if (state.IsThreeDimensional == 0)
        {
            if (!state.PickupBitPresent) CreateBit(state, host);
            if (!state.PickupBitPresent) throw new InvalidOperationException("Non-3d source pickup has no pickup bit.");
            host.SetPickupBitPosition(state.Position);
        }
    }

    public static void DoPhysics(PowerUpState state, IPowerUpHost host)
    {
        state.Yaw = unchecked((short)((state.Yaw + state.AngularVelocityY * (short)state.Timing.ElapsedTicks) & 4095));
        if (state.Falling != 0)
        {
            if (state.GroundCheckFinished == 0 && state.GroundHeight is < 0 and > -5)
            {
                if ((host.LogicFrame & 1) != 0)
                {
                    int height = host.GroundHeight(state.WorkingPosition, 0, 8000);
                    if (height == -1)
                    {
                        state.GroundHeight--;
                        if (state.GroundHeight <= -5) state.GroundCheckFinished = 1;
                    }
                    else state.GroundHeight = height;
                }
                return;
            }
            state.WorkingPosition += state.Velocity;
            int vy = unchecked(state.Velocity.Y + state.AccelerationY);
            state.Velocity = state.Velocity with { Y = unchecked(vy - (vy >> state.FrictionY)) };
            if (state.GroundCheckFinished == 0)
            {
                int threshold = unchecked(state.GroundHeight - (state.GroundClearance << 12));
                if (state.WorkingPosition.Y > threshold && state.Velocity.Y > 0)
                { state.WorkingPosition = state.WorkingPosition with { Y = threshold }; state.Velocity = default; state.Falling = 0; }
            }
        }
        state.Position = state.WorkingPosition;
    }

    public static void CheckAge(PowerUpState state, IPowerUpHost host)
    {
        if (state.Lifetime == ushort.MaxValue) return;
        if (state.Lifetime != 0) state.Lifetime--;
        if (state.Lifetime < 60) state.Field120 = unchecked((ushort)(state.Lifetime * state.Field11E / 60));
        if (state.Lifetime is < 60 and > 30) state.Dropping = (host.LogicFrame & 2) != 0 ? (byte)1 : (byte)0;
        if (state.Lifetime <= 30) state.Dropping = (host.LogicFrame & 1) != 0 ? (byte)1 : (byte)0;
        if (state.IsThreeDimensional != 0)
        {
            if (state.Dropping != 0) state.ItemFlags &= 0xFFFE;
            else state.ItemFlags |= 1;
        }
        if (state.Lifetime != 0) return;
        if (state.PlayerDistance <= 8000) host.EmitGlow(Glow(state.Position, 20, 255, 255, 255, 20));
        Die(state, host);
    }

    public static void Die(PowerUpState state, IPowerUpHost host)
    {
        if ((state.BodyFlags & 0x40) != 0) return;
        if (state.Type != 11 && state.HasNode != 0) host.SendNodePulses(state.NodeId);
        if (state.SignalNode != ushort.MaxValue) host.SendSignal(state.SignalNode);
        state.BodyFlags |= 0x40; state.ItemFlags |= 1; host.MarkDead();
    }

    public static void DeleteStuff(PowerUpState state, IPowerUpHost host)
    {
        KillShadow(state, host);
        if (state.PickupBitPresent) host.DeletePickupBit(); state.PickupBitPresent = false;
        if (state.GlowPresent) host.DeleteGlow(); state.GlowPresent = false;
    }

    public static void CreateBit(PowerUpState state, IPowerUpHost host)
    {
        if (state.Type != 18) return;
        uint tint = state.Health switch { 10 => 0x69D46A, 20 => 0xFF8585, 40 => 0x4D4DFF, 80 => 0xD8D8D8, _ => 0x208080 };
        host.CreatePickupBit(tint); state.PickupBitPresent = true;
    }

    public static byte TakeEffect(PowerUpState state, IPowerUpHost host)
    {
        bool taken = false; ushort sound = 0;
        switch (state.Type)
        {
            case 14: case 15: case 16:
                taken = host.IncreaseHealth(unchecked(state.Health * host.PlayerMaxHealth) / 100); sound = 0x1D;
                if (!taken && !host.SpeechBusy && unchecked(host.Clock - state.LastSpeechClock) > 300)
                { host.PlaySpeech(32, 4, 0); state.LastSpeechClock = host.Clock; }
                break;
            case 8:
                taken = host.IncreaseWebbing(state.Health); sound = 0x1E;
                if (!taken && !host.SpeechBusy && unchecked(host.Clock - state.LastSpeechClock) > 300)
                { host.PlaySpeech(32, host.Random(3) + 12, 0); state.LastSpeechClock = host.Clock; }
                break;
            case 12: taken = host.SetFireWebbing(); sound = 0x1E; break;
            case 13:
                if (host.LevelId == 0x1601) { host.Progress.CostumeUnlockFlags |= 0x200; taken = true; }
                else
                {
                    taken = host.SetArmor(true);
                    if (taken && !host.SpeechBusy && unchecked(host.Clock - state.LastSpeechClock) > 300)
                    { host.PlaySpeech(32, host.Random(2), 0); state.LastSpeechClock = host.Clock; }
                }
                sound = 0x1E; break;
            case 18: host.IncreasePickupRevision(state.Health); taken = true; sound = 0x1E; break;
            case 10:
                bool busy = host.SpeechBusy; // Actual PC reads this flag before writing the comic mask.
                host.Progress.CollectedComicMask |= 1u << unchecked((byte)state.Health); taken = true; sound = 0x1F;
                if (!busy && unchecked(host.Clock - state.LastSpeechClock) > 300) host.PlaySpeech(32, 6, 0);
                break;
            case 11:
                if (state.FadeCounter == 0 && state.HasNode != 0)
                { host.SendNodePulses(state.NodeId); Die(state, host); state.HasNode = 0; host.PlaySound(0x1F, 0x2000, 0); }
                host.EmitGlow(Glow(state.Position, 192, 64, 64, 160, 100));
                break;
        }
        if (taken && state.Type != 11) { Die(state, host); host.PlaySound(sound, 0x2000, 0); }
        return taken ? (byte)1 : (byte)0;
    }

    private static void ShadowOn(PowerUpState state, IPowerUpHost host) { state.BodyFlags |= 8; host.ShadowOn(); }
    private static void KillShadow(PowerUpState state, IPowerUpHost host) { state.BodyFlags &= 0xFFF7; host.KillShadow(); }
    // Exact Exp_GlowFlash arguments for its random-radii branch (a7=1), not a replacement effect.
    private static GlowFlashParameters Glow(FixedVector3 position, int radius, byte red, byte green, byte blue, int width) =>
        new(position, 4, red, green, blue, 2, red, green, blue, 20, width, 0, 0,
            radius, radius, radius / 2, radius / 2, 0, 0);
}
