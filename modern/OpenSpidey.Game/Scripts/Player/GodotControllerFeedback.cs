using Godot;

namespace OpenSpidey.Game.Player;

/// <summary>Maps source actuator requests to connected controllers and the shared 60 Hz clock.</summary>
public partial class GodotControllerFeedback : Node
{
    private sealed record Motor(uint Started, ushort Duration, float Strength);
    private readonly Dictionary<(int Controller, int Actuator), Motor> motors = [];
    private readonly HashSet<int> activeDevices = [];
    public required Func<uint> Clock { get; init; }
    public bool Enabled { get; set; } = true;
    public bool Available => Enabled && Input.GetConnectedJoypads().Count != 0;

    public int RemainingTicks(int controller, int actuator)
    {
        Validate(controller, actuator);
        if (!motors.TryGetValue((controller, actuator), out Motor? motor)) return 0;
        uint elapsed = unchecked(Clock() - motor.Started);
        return elapsed >= motor.Duration ? 0 : motor.Duration - (int)elapsed;
    }

    public void Activate(int controller, ushort duration, int actuator, int strength)
    {
        Validate(controller, actuator);
        float magnitude = actuator == 0 ? (strength == 0 ? 0 : 1) : Math.Clamp(strength / 255f, 0, 1);
        motors[(controller, actuator)] = new(Clock(), duration, magnitude);
        Synchronize(controller);
    }

    public override void _PhysicsProcess(double delta)
    {
        foreach (int controller in motors.Keys.Select(key => key.Controller).Distinct().ToArray()) Synchronize(controller);
        foreach (var key in motors.Keys.Where(key => RemainingTicks(key.Controller, key.Actuator) == 0).ToArray()) motors.Remove(key);
    }

    private void Synchronize(int controller)
    {
        var devices = Input.GetConnectedJoypads();
        if (controller >= devices.Count) return;
        int device = devices[controller];
        int weakTime = RemainingTicks(controller, 0), strongTime = RemainingTicks(controller, 1);
        if (!Enabled || Math.Max(weakTime, strongTime) == 0)
        {
            if (activeDevices.Remove(device)) Input.StopJoyVibration(device);
            return;
        }
        float weak = weakTime == 0 ? 0 : motors[(controller, 0)].Strength;
        float strong = strongTime == 0 ? 0 : motors[(controller, 1)].Strength;
        Input.StartJoyVibration(device, weak, strong, Math.Max(weakTime, strongTime) / 60f);
        activeDevices.Add(device);
    }

    private static void Validate(int controller, int actuator)
    {
        if (controller is < 0 or > 1) throw new ArgumentOutOfRangeException(nameof(controller));
        if (actuator is < 0 or > 1) throw new ArgumentOutOfRangeException(nameof(actuator));
    }

    public override void _ExitTree()
    {
        foreach (int device in activeDevices) Input.StopJoyVibration(device);
        activeDevices.Clear();
        motors.Clear();
    }
}
