using Godot;
using OpenSpidey.Assets.Models;
using OpenSpidey.Assets.Textures;
using OpenSpidey.Core.Actors.Choppers;
using OpenSpidey.Game.Effects;
using OpenSpidey.Game.Import;

namespace OpenSpidey.Game.Actors;

public sealed class GodotChopperMissileResources
{
    public MeshInstance3D CreateMesh(PsxAssetLibrary objects)
    {
        ArgumentNullException.ThrowIfNull(objects);
        int index = objects.FindModel(ChopperState.MissileModelChecksum);
        ArrayMesh temporary = PsxGeometryBuilder.BuildModel(objects.Scene.Models[index], objects.Scene.ColorTable,
            new PsxMaterialLibrary(objects.Textures)) ?? throw new InvalidDataException("Chopper missile model has no geometry.");
        try
        {
            var mesh = new ArrayMesh();
            byte[] brightness = GodotGlowFlash.SourceBrightness();
            foreach (int surface in Enumerable.Range(0, temporary.GetSurfaceCount()))
            {
                Godot.Collections.Array arrays = temporary.SurfaceGetArrays(surface);
                Color[] colours = arrays[(int)Mesh.ArrayType.Color].AsColorArray();
                for (int vertex = 0; vertex < colours.Length; vertex++)
                {
                    Color value = colours[vertex];
                    uint packed = (uint)Math.Clamp((int)Math.Round(value.R * 255), 0, 255) |
                        ((uint)Math.Clamp((int)Math.Round(value.G * 255), 0, 255) << 8) |
                        ((uint)Math.Clamp((int)Math.Round(value.B * 255), 0, 255) << 16);
                    colours[vertex] = GodotGlowFlash.SourceColour(packed, 0, brightness);
                }
                arrays[(int)Mesh.ArrayType.Color] = colours;
                mesh.AddSurfaceFromArrays(Mesh.PrimitiveType.Triangles, arrays);
                if (temporary.SurfaceGetMaterial(surface) is StandardMaterial3D material)
                {
                    material.VertexColorIsSrgb = true;
                    mesh.SurfaceSetMaterial(surface, material);
                }
            }
            return new MeshInstance3D { Name = "ChopperMissileMesh", Mesh = mesh, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off };
        }
        finally { temporary.Dispose(); }
    }
}
