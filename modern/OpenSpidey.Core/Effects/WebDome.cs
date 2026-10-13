using OpenSpidey.Core.Mathematics;

namespace OpenSpidey.Core.Effects;

public readonly record struct WebDomePlayer(FixedVector3 Position, ushort SurfaceClearance, short AnimationFrame);

/// <summary>Actual CDomePiece constructor and4FA5E0 one-visit delay/tint changes.</summary>
public sealed class WebDomePiece(FixedVector3 position, ushort modelIndex, int delay, int fireVariant)
{
    public FixedVector3 Position { get; set; } = position;
    public string ModelName => FireVariant != 0 ? "firedome" : "webdome3";
    public ushort ModelIndex { get; } = modelIndex;
    public int FireVariant { get; } = fireVariant;
    public int Delay { get; private set; } = delay;
    public int TintStep { get; set; } = 4;
    public uint Tint { get; private set; }
    public ushort ItemFlags { get; set; } = 0x400;
    public ushort BodyFlags { get; set; } = 0x16;
    public void AdvanceActor()
    {
        if (Delay != 0) { Delay = unchecked(Delay - 1); return; }
        int channel = Math.Clamp(unchecked((byte)Tint + TintStep), 0, 32);
        Tint = unchecked((uint)(channel | channel << 8 | channel << 16));
    }
}

public interface IWebDomeHost
{
    WebDomePlayer? RecoverPlayer();
    void AttachDome(WebDome dome);
    void DetachDome(WebDome dome);
    void AttachPiece(WebDomePiece piece);
    void DetachPiece(WebDomePiece piece);
    FixedVector3 FireModelVertexWorldPosition(WebDome dome, int index);
    int FireModelVertexCount { get; }
    void EmitFirePulse(FixedVector3 position, int scale, Func<int, int> random);
    void ShatterTrappedPiece(WebDomePiece piece, int first, int second);
    void EmitShockWave(int fireVariant, Func<int, int> random);
    void EmitRing(FixedVector3 position, int fireVariant);
}

/// <summary>Actual CDome4FA640/4FA830/4FAD50: player-following source models, staged pieces and burst ownership.</summary>
public sealed class WebDome : IDisposable
{
    private readonly IWebDomeHost host;
    private bool disposed;
    private readonly WebDomePiece?[] pieces = new WebDomePiece?[5];
    public FixedVector3 Position { get; private set; }
    public FixedVector3 Scale { get; private set; }
    public string ModelName => FireVariant != 0 ? "firedome" : "webdome2";
    public ushort ModelIndex => FireVariant != 0 ? (ushort)1 : (ushort)0;
    public int FireVariant { get; }
    public int Mode { get; private set; }
    public ushort ItemFlags { get; private set; }
    public ushort BodyFlags { get; set; } = 0x16;
    public bool IsDisposed => disposed;
    public IReadOnlyList<WebDomePiece?> Pieces => pieces;

    public WebDome(WebDomePlayer player, int fireVariant, IWebDomeHost host)
    {
        this.host = host ?? throw new ArgumentNullException(nameof(host));
        Position = player.Position with { Y = unchecked(player.Position.Y + (player.SurfaceClearance << 12)) };
        FireVariant = fireVariant;
        Scale = new(4096, fireVariant != 0 ? 0 : 4096, 4096);
        ItemFlags = fireVariant != 0 ? (ushort)0x201 : (ushort)1;
        host.AttachDome(this);
    }

    public void AdvanceActor(Func<int, int> random)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        WebDomePlayer? player = host.RecoverPlayer();
        if (player is { } live) Position = live.Position with { Y = unchecked(live.Position.Y + (live.SurfaceClearance << 12)) };
        else Burst(random);
        if (FireVariant != 0)
        {
            if (Mode == 0 && player is { AnimationFrame: >= 4 })
            {
                CreatePiece(0, 0, 0); CreatePiece(1, 2, 2); CreatePiece(2, 3, 4); Mode = 1;
            }
            else if (Mode == 1)
            {
                short vertical = unchecked((short)(Scale.Y + 600));
                Scale = Scale with { Y = vertical > 4096 ? 4096 : vertical };
                ItemFlags &= 0xFFFE;
                host.EmitFirePulse(host.FireModelVertexWorldPosition(this, random(host.FireModelVertexCount)), 256, random);
            }
        }
        else if (Mode == 0 && player is { AnimationFrame: >= 4 })
        {
            CreatePiece(0, 3, 0); CreatePiece(1, 0, 2); CreatePiece(2, 4, 4);
            CreatePiece(3, 1, 6); CreatePiece(4, 2, 8); Mode = 1;
        }
        else if (Mode == 1 && pieces[3]!.Tint == 0x202020)
        {
            foreach (WebDomePiece? piece in pieces) piece!.TintStep = -10;
            ItemFlags &= 0xFFFE; Mode = 2;
        }
        foreach (WebDomePiece? piece in pieces) if (piece is not null) piece.Position = Position;
    }

    public void Burst(Func<int, int> random)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        if (FireVariant == 0 && pieces[2] is { } trapped)
        { trapped.ItemFlags &= 0xFBFF; host.ShatterTrappedPiece(trapped, 30, 30); }
        Mode = 3;
        host.EmitShockWave(FireVariant, random);
        host.EmitRing(Position, FireVariant);
        BodyFlags |= 0x40;
    }
    public void Dispose()
    {
        if (disposed) return;
        disposed = true; host.DetachDome(this);
        foreach (WebDomePiece? piece in pieces) if (piece is not null) host.DetachPiece(piece);
    }
    private void CreatePiece(int slot, ushort model, int delay)
    {
        var piece = new WebDomePiece(Position, model, delay, FireVariant != 0 ? 1 : 0) { BodyFlags = 0x36 };
        pieces[slot] = piece; host.AttachPiece(piece);
    }
}
