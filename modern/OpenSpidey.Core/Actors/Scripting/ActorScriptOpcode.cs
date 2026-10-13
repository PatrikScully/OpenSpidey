namespace OpenSpidey.Core.Actors.Scripting;

/// <summary>Recovered flow instruction IDs; actor-specific effect commands are supplied by the host.</summary>
public enum ActorScriptOpcode : ushort
{
    Stop = 0x4100,
    Jump = 0x4101,
    JumpAndYield = 0x4102,
    SetLabel = 0x4104,
    DiscoverLabels = 0x4105,
    SwitchScript = 0x4106,
    Yield = 0x4107,
    Add = 0x4110,
    Subtract = 0x4111,
    IfGreater = 0x4112,
    IfLess = 0x4113,
    IfEqual = 0x4114,
    IfAllFlags = 0x4115,
    IfNoFlags = 0x4116,
    IfWhatIf = 0x4117,
    IfPlayerCeiling = 0x4118,
    IfSymbiote = 0x4119,
    EndIf = 0x4120,
    StopExecution = 0x4240,
    TimedMotion = 0x4280,
    WaitForSignal = 0x4281,
    WaitTicks = 0x42B6,
    EnableDeathSkip = 0x42B7
}
