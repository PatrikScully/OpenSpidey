namespace OpenSpidey.Core.Actors.Scorpion;

/// <summary>Jonah's source constructor configuration; encounter behavior owns subsequent state transitions.</summary>
public sealed class JonahState(int constructionOrdinal) : ScorpionEncounterBodyState(constructionOrdinal)
{
    public const short ActorType = 316;
    public static uint WhatShouldScorpionDo(int state) => state == 5 ? 2u : state == 8 ? 0u : 1u;
    public uint SignalFlags { get; set; }
    public void ShouldPlead() => SignalFlags |= 0xC;
}
