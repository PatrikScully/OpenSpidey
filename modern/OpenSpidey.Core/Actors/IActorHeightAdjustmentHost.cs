namespace OpenSpidey.Core.Actors;

public interface IActorHeightAdjustmentHost
{
    int SetHeight(bool initial, int above, int below);
}
