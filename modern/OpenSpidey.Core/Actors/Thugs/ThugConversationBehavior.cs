using OpenSpidey.Core.Actors.Procedures;
using OpenSpidey.Core.Mathematics;

namespace OpenSpidey.Core.Actors.Thugs;

public sealed record ThugConversationActor(ThugState State, ActorProcedureState Actor, ThugStateFlags Flags);

public interface IThugConversationHost
{
    ThugPlayerState Player { get; }
    int AttackClock { get; }
    IReadOnlyList<ThugConversationActor> SourceBaddyOrder { get; }
    bool LineOfSight(FixedVector3 from, FixedVector3 to, uint flags);
    void StoreConversationPartner(ThugState owner, ThugState partner);
}

/// <summary>Source StrikeUpConversation, CanAck and its ordered GetClosest partner selection.</summary>
public static class ThugConversationBehavior
{
    public static void StrikeUpConversation(ThugConversationActor self, IThugConversationHost host, Func<int, int> random)
    {
        ThugState state = self.State;
        if (ThugPerception.DistanceToPlayer(state, self.Actor.Body, host.Player, host.AttackClock, 2) > 1792) return;
        ThugConversationActor? partner = FindClosest(self, host, 304) ?? FindClosest(self, host, 312);
        if (partner is null || ApproximateDistance(self.Actor.Body.Position, partner.Actor.Body.Position) >= 0x800) return;
        int choice = random(7);
        state.DialogueClip = choice;
        state.DialogueBank = 4;
        int response = 4, delay = 160, responseBank = 4;
        switch (choice)
        {
            case 0: state.DialogueClip = -3; break;
            case 1: state.DialogueClip = -5; response = 6; break;
            case 2: state.DialogueClip = -7; response = 8; break;
            case 3: state.DialogueClip = -9; response = 10; state.DialogueBank = responseBank = 3; break;
            case 4: state.DialogueClip = -11; response = 12; state.DialogueBank = responseBank = 3; break;
            case 5: state.DialogueClip = -13; response = 14; state.DialogueBank = responseBank = 3; break;
            case 6: state.DialogueClip = -15; response = 14; state.DialogueBank = 3; break;
            default: throw new InvalidOperationException($"Unknown source conversation choice {choice}.");
        }
        if (!CanAcknowledge(partner, self, delay, response, responseBank, host)) return;
        state.ConversationTicks = delay;
        host.StoreConversationPartner(state, partner.State);
        state.State = 21;
        state.Substate = 0;
    }

    public static bool CanAcknowledge(ThugConversationActor partner, ThugConversationActor initiator,
        int delay, int response, int bank, IThugConversationHost host)
    {
        ThugState state = partner.State;
        if (state.AlertTicks != 0 || state.ConversationTicks != 0 || (partner.Flags.Check(state.State) & 0x20) == 0) return false;
        host.StoreConversationPartner(state, initiator.State);
        state.DialogueClip = response;
        state.DialogueBank = bank;
        state.ConversationTicks = delay;
        state.State = 21;
        state.Substate = 0;
        return true;
    }

    public static ThugConversationActor? FindClosest(ThugConversationActor self, IThugConversationHost host, int type)
    {
        int nearestDistance = 10656;
        ThugConversationActor? nearest = null;
        foreach (ThugConversationActor candidate in host.SourceBaddyOrder)
        {
            if ((type != 0 && candidate.State.Type != type) || ReferenceEquals(candidate.State, self.State)) continue;
            int distance = ThugPerception.ApproximateHorizontalDistance(self.Actor.Body.Position, candidate.Actor.Body.Position);
            if (nearestDistance <= distance || !host.LineOfSight(candidate.Actor.Body.Position, self.Actor.Body.Position, 0)) continue;
            nearestDistance = distance;
            nearest = candidate;
        }
        return nearest;
    }

    private static uint ApproximateDistance(FixedVector3 first, FixedVector3 second)
    {
        int largest = Abs(unchecked(first.X - second.X));
        int middle = Abs(unchecked(first.Y - second.Y));
        int smallest = Abs(unchecked(first.Z - second.Z));
        if (largest < middle) (largest, middle) = (middle, largest);
        if (middle < smallest) (middle, smallest) = (smallest, middle);
        if (largest < middle) (largest, middle) = (middle, largest);
        return unchecked((uint)(unchecked(largest + (middle >> 1) + (smallest >> 2)) >> 12));
    }

    private static int Abs(int value) => value < 0 ? unchecked(-value) : value;
}
