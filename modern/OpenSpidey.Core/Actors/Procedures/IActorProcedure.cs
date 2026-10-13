namespace OpenSpidey.Core.Actors.Procedures;

public interface IActorProcedure
{
    int SourceType { get; }
    int Flags { get; set; }
    Action? ExecuteNext { get; set; }
    void Execute(int elapsedTicks);
}
