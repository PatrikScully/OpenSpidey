using System.Text.Json;
using OpenSpidey.Core.Gameplay;
using OpenSpidey.Core.Presentation;

internal static class SubwayProgressTests
{
    public static void Run(Action<bool, string> assert)
    {
        var countdown = new MissionCountdown();
        var host = new Host();
        SubwayProgress.Display(countdown, host);
        assert(countdown is { DisplayEnabled: 1, Running: 1, RemainingTicks: SubwayProgress.DurationTicks, Rate: 4096 } &&
            host.Pulses.Count == 0 && host.Draws.Count == 0,
            "First l5a5 display arms the shared countdown and skips draw while widescreen is active.");

        host.WideScreenValue = 0;
        SubwayProgress.Display(countdown, host);
        assert(host.Draws.Count > 0 && host.TextureLookups == 4 &&
            host.Draws.Any(draw => draw.Texture == 3 && draw.Depth == 1),
            "Armed l5a5 display with cleared widescreen loads four subway textures and draws the car piece.");

        countdown.RemainingTicks = 0;
        host.Draws.Clear();
        SubwayProgress.Display(countdown, host);
        assert(host.Pulses.SequenceEqual(new ushort[] { 77 }) && host.Draws.Count == 0,
            "Expired subway countdown pulses EndLevelNode and skips the bar.");

        SubwayProgress.Clear(countdown, 1234, index => host.Release(index));
        assert(countdown is { DisplayEnabled: 0, Running: 0, LastUpdateTick: 1234 } && host.LiveTextures == 0,
            "l5a5 clear restores the shared countdown and releases subway textures.");
    }

    private sealed class Host : ISubwayProgressHost
    {
        private readonly bool[] present = new bool[4];
        public int ScreenOffsetY => 0;
        public int WideScreenValue { get; set; } = 40;
        public uint Clock => 100;
        public ushort EndLevelNode => 77;
        public List<ushort> Pulses { get; } = [];
        public List<(int Texture, float Depth)> Draws { get; } = [];
        public int TextureLookups { get; private set; }
        public int LiveTextures => present.Count(value => value);
        public bool TexturePresent(int index) => present[index];
        public void FindTexture(int index, string name)
        {
            if (present[index]) throw new InvalidOperationException("Texture already cached.");
            present[index] = true; TextureLookups++;
        }
        public void SendPulse(ushort node) => Pulses.Add(node);
        public SubwayProgressPiece CreatePiece(int texture, int x, int y, int width, int height) =>
            SubwayProgress.CreatePanelPiece(x, y, width, height);
        public void DrawPiece(int texture, SubwayProgressPiece piece, float depth) => Draws.Add((texture, depth));
        public void Release(int index) => present[index] = false;
    }
}
