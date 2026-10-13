using OpenSpidey.Core.Effects;
using OpenSpidey.Core.Mathematics;

namespace OpenSpidey.Core.Player;

public interface IWebStrandSnapHost
{
    FixedVector3 PlayerPosition { get; }
    FixedVector3 SourcePosition(IWebStrandTarget target);
    FixedVector3 TargetAttachment(IWebStrandTarget target);
    bool TugImpulse(IWebStrandTarget target, FixedVector3 contact, FixedVector3 direction,
        IReadOnlyList<FixedVector3>? path);
    void SetFirstContactDetails();
    void EmitBrokenStrand(KnottedWebFragmentRequest request, Func<int, int> random);
    GlowFlashState EmitSnapGlow(GlowFlashParameters parameters, Func<int, int> random);
}
