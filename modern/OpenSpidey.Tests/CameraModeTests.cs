using System.Text.Json;
using OpenSpidey.Core.Cameras;
using OpenSpidey.Core.Mathematics;

internal static class CameraModeTests
{
    public static void Run(Action<bool, string> assert)
    {
        using Stream stream = typeof(CameraModeTests).Assembly.GetManifestResourceStream("OpenSpidey.Tests.Fixtures.camera-mode.json")!;
        using JsonDocument document = JsonDocument.Parse(stream);
        foreach (JsonElement test in document.RootElement.EnumerateArray())
        {
            JsonElement input = test.GetProperty("input");
            var state = new CameraModeState { Mode = input[0].GetUInt32(), Position = new(1000, 1111, 1222),
                CurrentRotation = new(10, 21, 32, 43), CollisionFlag = 255, AngleInterpolationTicks = 77 };
            state.PushMode(); uint previous = state.SetMode(7); state.Position = new(-1000, -1111, -1222);
            state.DesiredRotation = new(-10, -21, -32, -43); state.MoveFrontToDesiredPosition(input[1].GetInt32() != 0);
            List<long> actual = [previous, state.SavedMode, state.CollisionFlag, state.AngleInterpolationTicks];
            void Vec(FixedVector3 value) => actual.AddRange([value.X, value.Y, value.Z]);
            void Quat(FixedQuaternion value) => actual.AddRange([value.X, value.Y, value.Z, value.W]);
            Vec(state.Position); Quat(state.CurrentRotation); Quat(state.PreviousRotation); Quat(state.DisplayRotation); Vec(state.SavedPosition); Quat(state.SavedRotation);
            state.PopMode(); actual.Add(state.Mode); Vec(state.Position); Quat(state.CurrentRotation);
            foreach (uint next in new uint[] { 16, 17, 3 })
            { state.AngleInterpolationTicks = 77; previous = state.SetMode(next); actual.AddRange([previous, state.Mode, state.AngleInterpolationTicks]); }
            assert(actual.SequenceEqual(test.GetProperty("expected").EnumerateArray().Select(value => value.GetInt64())),
                $"Actual PC camera mode{input[0]} water{input[1]}: single saved mode, pose snapshot only4/5/6, front7 rotation copy/position retention, post-water early return and mode16/17 reset.");
        }
    }
}
