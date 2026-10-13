using Godot;
using OpenSpidey.Core.Effects;
using OpenSpidey.Core.Mathematics;
using OpenSpidey.Core.Player;
using OpenSpidey.Game.Player;

namespace OpenSpidey.Game.World;

/// <summary>Real posed106 hands emit through the shared source CEmber owner and canonical Flat head before any bit movement.</summary>
internal static class FlamingFistsImportChecks
{
    public static void Verify(GodotPlayer player, GodotPlayerWebRuntime webs, GodotPlayerCombatRuntime combat)
    {
        GodotPlayerFlamingFists owner = webs.FlamingFists ?? throw new InvalidOperationException("The actual source flaming-fist owner is not configured.");
        if (player.Model.Playback.ClipId != 106)
            throw new InvalidOperationException("Positive flaming-fist proof requires the real fixture's already-selected source106 pose.");
        PlayerHandHooks hands = new(player.Melee!.GetPlayerHookPosition(1), player.Melee.GetPlayerHookPosition(0));
        FixedVector3 left = hands.Left, right = hands.Right;
        byte originalFire = player.Inventory!.State.FireWebbing;
        int originalBonus = player.Combat.State.Field5AC;
        int count = webs.Embers.Count, particles = combat.Particles.Count;
        var registrations = new List<SourceParticleRegistration>();
        void Created(SourceParticleRegistration bit) => registrations.Add(bit);
        webs.Embers.SourceBitCreated += Created;
        try
        {
            player.Inventory.State.FireWebbing = 0; player.Combat.State.Field5AC = 1;
            owner.FinishFrame(hands);
            player.Inventory.State.FireWebbing = 1; player.Combat.State.Field5AC = 0;
            owner.FinishFrame(hands);
            Assert(webs.Embers.Count == count && combat.Particles.Count == particles && registrations.Count == 0,
                "Both genuine fire-byte and bonus-int gates must avoid native emission or source Flat registration.");
            player.Combat.State.Field5AC = 1;
            owner.FinishFrame(hands);
            FireEmber[] born = registrations.Select(bit => (FireEmber)bit.Identity).ToArray();
            Assert(born.Length == 6 && webs.Embers.Count == count + 6 && combat.Particles.Count == particles + 6,
                "Actual source106 must create three left then three right CEmber identities through the existing shared renderer and scheduler.");
            Assert(born.Take(3).All(ember => ember.Position == left) && born.Skip(3).All(ember => ember.Position == right) &&
                hands.Left == left && hands.Right == right,
                "Fire-fist particles must consume borrowed posed hook1/0 endpoints before swing changes them, without mutating the hand owner.");
            Assert(webs.Embers.Embers.Take(6).SequenceEqual(born.Reverse()) &&
                combat.Particles.Identities(SourceParticleList.Flat).Take(6).SequenceEqual(born.Reverse()),
                "Each genuine CEmber constructor must insert the same native/source identity at the Flat head in reverse emission order.");
            Assert(born.All(ember => ember.Alive && ember.Origin == new FixedVector3(ember.Position.X, 0, ember.Position.Z) &&
                ember.FadeDelay is >= 5 and <= 9 && ember.Velocity.Y is >= 24576 and <= 40960 &&
                ember.Scale is >= 350 and <= 549 && ember.Amplitude is >= 10 and <= 19 &&
                ember.OrbitPhase is >= 0 and < 4096 && ember.AmplitudePhase is >= 0 and < 4096 && ember.CodeRgb == 0x2E0080FF),
                "Actual simby5 argument256 must reach the original Ember constructor's delay/velocity/RNG fields; newborn Flat particles must not move during emission.");
            Assert(registrations.All(bit => bit.IsAlive()) && player.Combat.State.Field5AC == 1 && player.Inventory.State.FireWebbing == 1,
                "Source flaming-fist presentation must preserve live effect ownership and the shared inventory/bonus fields until their actual later phases.");
        }
        finally
        {
            webs.Embers.SourceBitCreated -= Created;
            player.Inventory.State.FireWebbing = originalFire;
            player.Combat.State.Field5AC = originalBonus;
        }
        GD.Print("PASS: 6 native flaming-fist checks: genuine posed106 hooks, exact fire/bonus gates, six simby5 constructors, canonical shared Flat head order and retained newborn fields.");
        static void Assert(bool condition, string evidence) { if (!condition) throw new InvalidDataException(evidence); }
    }
}
