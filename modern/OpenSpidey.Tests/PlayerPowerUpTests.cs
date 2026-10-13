using System.Text.Json;
using OpenSpidey.Core.Mathematics;
using OpenSpidey.Core.Player;

internal static class PlayerPowerUpTests
{
    public static void Run(Action<bool, string> assert)
    {
        using Stream resource = typeof(PlayerPowerUpTests).Assembly.GetManifestResourceStream("OpenSpidey.Tests.Fixtures.player-powerup.json")!;
        using JsonDocument document = JsonDocument.Parse(resource);
        foreach (JsonElement test in document.RootElement.EnumerateArray())
        {
            int[] input = test.GetProperty("input").EnumerateArray().Select(value => unchecked((int)value.GetInt64())).ToArray();
            JsonElement expected = test.GetProperty("expected");
            string context = $"Original power-up {test.GetProperty("kind").GetString()} [{string.Join(',', input)}]";
            switch (test.GetProperty("kind").GetString())
            {
                case "collection":
                    var pickups = new List<IPlayerCollectiblePowerUp>(); var events = new List<int>();
                    for (int index = 0; index < input[9]; index++)
                        pickups.Add(new Pickup((ushort)input[2], index == 0 ? (ushort)input[3] : (ushort)0, (ushort)input[4],
                            index == 0 ? new(input[5], input[6], input[7]) : default, (byte)input[8], index, events));
                    var host = new CollectionHost(pickups, events); var state = new PlayerPowerUpCollectionState { PickupJingleReady = (byte)input[1] };
                    PlayerPowerUpCollection.Collect(state, default, (uint)input[0], host);
                    assert(state.PickupJingleReady == expected.GetProperty("jingle").GetInt32(), context + " preserves the source first-eligible/rearming latch.");
                    byte[] flash = new byte[2]; for (int index = 0; index < pickups.Count; index++) flash[index] = pickups[index].FailedCollectionFlash;
                    assert(flash.Select(value => (int)value).SequenceEqual(Read(expected.GetProperty("flash"))) && events.SequenceEqual(Read(expected.GetProperty("events"))),
                        context + " preserves exact type11 action gates, rejection flashes, source list order and jingle events.");
                    break;
                case "health":
                    var combat = new PlayerCombatState((short)input[0], input[1]); var inventory = new PlayerWebInventory { PickupRevision = input[3] };
                    var collection = new PlayerPowerUpCollectionState { LastHealthPickupClock = (uint)input[5] };
                    bool result = PlayerPowerUpResources.IncreaseHealth(combat, inventory, collection, input[2], (uint)input[4]);
                    long[] actual = [result ? 1 : 0, combat.Health, inventory.PickupRevision, collection.LastHealthPickupClock];
                    assert(actual.SequenceEqual(expected.EnumerateArray().Select(value => value.GetInt64())), context + " preserves signed-short wrap, max clamp and successful revision/clock writes.");
                    break;
                case "armor":
                    var armorCombat = new PlayerCombatState(100, 100) { ArmorTexturesActive = input[1] != 0, ArmorActive = input[2] != 0, ArmorAmount = input[3] };
                    var armorHost = new ArmorHost(input[4], input[5] != 0, input[6] != 0, input[7]);
                    bool armorResult = PlayerPowerUpResources.SetArmor(armorCombat, input[0] != 0, armorHost);
                    int[] armorState = [armorResult ? 1 : 0, armorCombat.ArmorActive ? 1 : 0, armorCombat.ArmorTexturesActive ? 1 : 0,
                        armorCombat.ArmorAmount, armorHost.Textures.Processing ? 1 : 0, armorCombat.ArmorAnimationVariant];
                    assert(armorState.SequenceEqual(Read(expected.GetProperty("state"))) && armorHost.Events.SequenceEqual(Read(expected.GetProperty("events"))),
                        context + " preserves original icon reload, difficulty amount, global/field asymmetry and guarded suit swaps.");
                    break;
            }
        }
    }
    private static IEnumerable<int> Read(JsonElement values) => values.EnumerateArray().Select(value => value.GetInt32());
    private sealed class Pickup(ushort type, ushort bodyFlags, ushort radius, FixedVector3 position, byte result, int index, List<int> events) : IPlayerCollectiblePowerUp
    {
        public ushort Type => type; public ushort BodyFlags => bodyFlags; public ushort Radius => radius;
        public FixedVector3 Position => position; public byte FailedCollectionFlash { get; set; }
        public byte TakeEffect() { events.AddRange([1, index]); return result; }
    }
    private sealed class CollectionHost(IReadOnlyList<IPlayerCollectiblePowerUp> pickups, List<int> events) : IPlayerPowerUpCollectionHost
    {
        public IReadOnlyList<IPlayerCollectiblePowerUp> SourcePowerUpOrder => pickups;
        public void PlaySound(ushort sound, int volume, int pitch) => events.AddRange([2, sound, volume, pitch]);
    }
    private sealed class ArmorHost(int difficulty, bool low, bool processing, int suit) : IPlayerArmorResourceHost, IPlayerArmorTextureHost
    {
        public int Difficulty => difficulty; public List<int> Events { get; } = [];
        public PlayerArmorTextureState Textures { get; } = new() { LowGraphics = low, Processing = processing, Suit = suit };
        public void ClearArmorHealthIcon() { }
        public void LoadArmorHealthIcon() => Events.Add(3);
        public void ProcessArmorTextures(bool active) => PlayerPowerUpResources.ProcessArmorTextures(Textures, active, this);
        public void SwapSuitTextures(int source, int target) => Events.AddRange([4, source, target]);
    }
}
