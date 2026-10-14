using OpenSpidey.Core.Actors;
using OpenSpidey.Core.Actors.Procedures;
using OpenSpidey.Core.Actors.Rhino;
using OpenSpidey.Core.Mathematics;

internal static class RhinoPlayXAPleaseTests
{
    public static void Run(Action<bool, string> assert)
    {
        RhinoSpeechEntry[] table =
        [
            new(0, 10, 0, 5, 0),
            new(0, 11, 0, 6, 1),
            new(0, 12, 1, 7, 2),
        ];
        var state = new RhinoState(1, 0, 0);
        var host = new Host(table) { RandomValue = 0, PlayerCanTalk = true, PlayAccepted = true };
        RhinoPlayXAPlease.Execute(state, table, host, 0, 3, 1);
        assert(host.LastBank == 0 && host.LastSlot == 10 && host.LastPriority == 5 && host.AttachedPlayer,
            "Pick0 with empty field_3DC plays gRhinoData[0] through the player when field_4 is zero.");
        assert(state.Field3DC == 1, "Slot0 sets bit0 of the played-mask when three slots share bank offset0.");

        host = new Host(table) { RandomValue = 3, PlayerCanTalk = true, PlayAccepted = true };
        state.Field3DC = 0;
        RhinoPlayXAPlease.Execute(state, table, host, 0, 3, 1);
        assert(host.LastBank == -1, "Rnd(4)==3 skips playback because pick must stay below slot count.");

        host = new Host(table) { RandomValue = 2, PlayerCanTalk = true, PlayAccepted = true };
        state.Field3DC = 0;
        RhinoPlayXAPlease.Execute(state, table, host, 0, 3, 1);
        assert(host.LastBank == 0 && host.LastSlot == 12 && host.LastPriority == 7 && host.AttachedRhino,
            "Rhino-voice table rows play at the body and attach to CRhino instead of CPlayer.");

        host = new Host(table) { RandomValue = 0, PlayerCanTalk = false, PlayAccepted = true };
        state.Field3DC = 0;
        RhinoPlayXAPlease.Execute(state, table, host, 0, 3, 1);
        assert(host.LastBank == -1 && state.Field3DC == 0,
            "Player lines honor CPlayer::CanITalkRightNow and leave field_3DC untouched on failure.");
    }

    private sealed class Host(RhinoSpeechEntry[] table) : IRhinoHost
    {
        public int RandomValue { get; init; }
        public bool PlayerCanTalk { get; init; }
        public bool PlayAccepted { get; init; }
        public int LastBank { get; private set; } = -1;
        public int LastSlot { get; private set; } = -1;
        public int LastPriority { get; private set; } = -1;
        public bool AttachedRhino { get; private set; }
        public bool AttachedPlayer { get; private set; }
        public ActorBodyMotion Body { get; } = new();
        public ActorProcedureState Actor { get; } = new(new ActorBodyMotion());
        public ActorProcedureList Procedures { get; } = new();
        public int AnimationId => 0;
        public bool AnimationFinished => false;
        public ushort ItemFlags { get; set; }
        public int ElapsedTicks => 1;
        public int AttackClock => 0;
        public FixedVector3 PlayerPosition { get; set; }
        public int PlayerInputSuppression => 0;
        public bool HasMessages => false;
        public bool PlayerIsCrawling => false;
        public int Difficulty => 1;
        public int WebDomeCount => 0;
        public ReadOnlySpan<RhinoSpeechEntry> SpeechEntries => table;
        public int Random(int bound) => RandomValue;
        public bool TryPlayPositionSpeech(FixedVector3 position, int bank, int slot, int priority)
        {
            if (!PlayAccepted) return false;
            LastBank = bank; LastSlot = slot; LastPriority = priority;
            return true;
        }
        public bool PlayerCanTalkNow() => PlayerCanTalk;
        public void AttachRhinoSpeech(int bank, int slot) => AttachedRhino = true;
        public void AttachPlayerSpeech(int bank, int slot) => AttachedPlayer = true;
        public int GroundHeight(FixedVector3 position, int above, int below, int flags) => 0;
        public void SetHeight(bool initial, int above, int below) { }
        public void RunAnimation(int animation) { }
        public void Neutralize() { }
        public void BuildTransform() { }
        public void ProcessMessages() { }
        public void PlaySounds() { }
        public void DoPhysics(int mode) { }
        public void SetGlobalBoss(bool enabled) { }
        public void SetBossCameraMode() { }
        public ActorBodyMotion? RecoverPlayerBody() => null;
        public void AttachPlayerLookAt(uint completionMask, int rate, int tolerance) { }
        public void PlayPositionalSound(int id) { }
        public bool LineOfSightToPlayer(int argument) => true;
        public RhinoWallChunkLists WallChunks { get; } = new();
        public bool TryGetChargeWallContact(FixedVector3 start, FixedVector3 direction, out RhinoChargeWallContact contact)
        {
            contact = default;
            return false;
        }
        public void ChunkItemByChecksum(uint checksum) { }
        public void RewindChargePosition(FixedVector3 position) { }
        public void NotifyChargeBarrierVisible(object? collider) { }
        public void ApplyChargeBarrierCollision(int mode) { }
        public void ApplyChargeScrapeDamage() { }
        public void BeginStuckHorn(RhinoChargeWallContact contact, int shake) { }
        public bool IsPlayerBody(object? collider) => false;
        public bool TryDetermineFightState(RhinoState state, int argument) => false;
        public bool ChargeLineHitsPlayer(FixedVector3 start, FixedVector3 end, int radius) => false;
        public void HitPlayerFromCharge(FixedVector3 delta) { }
    }
}
