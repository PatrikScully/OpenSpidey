namespace OpenSpidey.Core.Missions;

public static class MissionLevelId
{
    public static int FromAssetName(string asset)
    {
        string name = Path.GetFileNameWithoutExtension(asset.Replace('\\', '/'));
        if (name.Length < 4) throw new InvalidDataException("Mission name has no level/area code.");
        int level = name[1];
        if (name[0] is 'd' or 'D') level = 0x99;
        else if (level is >= '0' and <= '9') level -= 48;
        else if (level is >= 'A' and <= 'Z') level -= 49;
        else if (level is >= 'a' and <= 'z') level -= 81;
        return (level << 8) | (name[3] - 48);
    }
}
