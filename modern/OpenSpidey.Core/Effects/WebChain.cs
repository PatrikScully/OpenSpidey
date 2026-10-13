using OpenSpidey.Core.Mathematics;

namespace OpenSpidey.Core.Effects;

public sealed class WebChainPoint
{
    public FixedVector3 Position { get; internal set; }
    public FixedVector3 PreviousPosition { get; internal set; }
    public FixedVector3 Velocity { get; internal set; }
    public bool Anchored { get; }
    internal WebChainPoint(FixedVector3 position, bool anchored)
    { Position = PreviousPosition = position; Anchored = anchored; }
}

/// <summary>Original CChain fixed-point loose-web integration and one-pass source constraint order.</summary>
public sealed class WebChain
{
    private readonly WebChainPoint[] points;
    public IReadOnlyList<WebChainPoint> Points { get; }
    public int RestLength { get; }
    public int RestLengthSquared { get; }
    public int Gravity { get; }
    public int Friction { get; }
    public int Recoil { get; }
    public bool GroundEnabled { get; private set; }
    public int GroundY { get; private set; }

    public WebChain(IReadOnlyList<FixedVector3> positions, int restLength, int gravity, int friction, int recoil)
    {
        ArgumentNullException.ThrowIfNull(positions);
        if (positions.Count == 0) throw new ArgumentException("A source chain requires its first anchored point.", nameof(positions));
        RestLength = restLength; RestLengthSquared = unchecked(restLength * restLength);
        Gravity = gravity; Friction = friction; Recoil = recoil;
        points = positions.Select((position, index) => new WebChainPoint(position, index == 0)).ToArray();
        Points = Array.AsReadOnly(points);
    }

    public void SetGroundY(int groundY) { GroundEnabled = true; GroundY = groundY; }

    public void Move(FixedVector3 anchor)
    {
        WebChainPoint first = points[0];
        first.PreviousPosition = first.Position;
        first.Position = anchor;
        first.Velocity = anchor - first.PreviousPosition;
        foreach (WebChainPoint point in points.Skip(1))
        {
            point.PreviousPosition = point.Position;
            point.Velocity = ((point.Velocity * Friction) >> 12) + new FixedVector3(0, Gravity, 0);
            point.Position += point.Velocity;
            if (GroundEnabled && point.Position.Y > GroundY)
            { point.Position = point.Position with { Y = GroundY }; point.Velocity = point.Velocity with { Y = 0 }; }
        }
        int pair = 0;
        for (int pass = 1; pass < points.Length; pass++)
        {
            WebChainPoint previous = points[pair], current = points[pair + 1];
            FixedVector3 delta = current.Position - previous.Position;
            FixedVector3 whole = delta >> 12;
            int squared = unchecked(whole.X * whole.X + whole.Y * whole.Y + whole.Z * whole.Z);
            if (squared <= unchecked(RestLengthSquared + 2)) continue;
            int length = FixedVectorMath.OriginalSquareRoot(squared);
            FixedVector3 correction = previous.Position + delta * RestLength / length - current.Position;
            current.Position += correction;
            current.Velocity += correction;
            if (!previous.Anchored) previous.Velocity -= (correction * Recoil) >> 12;
            // The PC cursor advances only on an extended segment; preserve that source loop behavior.
            pair++;
        }
    }
}
