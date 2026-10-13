using OpenSpidey.Assets.Models;
using OpenSpidey.Game.Import;

namespace OpenSpidey.Game.Effects;

/// <summary>Original CDome/Piece/Ring models, retaining the actual shared rigid-source renderer and ring-owned deformation.</summary>
internal partial class GodotWebDomeModel : GodotSourceRigidModel
{
    public new static GodotWebDomeModel Create(PsxAssetLibrary bank, ushort modelIndex) => CreateModel<GodotWebDomeModel>(bank, modelIndex);
}
