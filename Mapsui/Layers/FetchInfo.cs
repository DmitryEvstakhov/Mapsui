namespace Mapsui.Layers;

public class FetchInfo
{
    public FetchInfo(MSection section, string? crs = null, ChangeType changeType = ChangeType.Discrete, bool forceUpdate = false)
    {
        Section = section;
        CRS = crs;
        ChangeType = changeType;
        ForceUpdate = forceUpdate;
    }

    public FetchInfo(FetchInfo fetchInfo)
    {
        Section = fetchInfo.Section;
        CRS = fetchInfo.CRS;
        ChangeType = fetchInfo.ChangeType;
        ForceUpdate = fetchInfo.ForceUpdate;
    }

    public MSection Section { get; }

    public MRect Extent => Section.Extent;
    public double Resolution => Section.Resolution;
    public string? CRS { get; }
    public ChangeType ChangeType { get; }
    public bool ForceUpdate { get; }

    public FetchInfo Grow(double amountInScreenUnits)
    {
        var amount = amountInScreenUnits * 2 * Resolution;
        return new FetchInfo(new MSection(Section.Extent.Grow(amount), Resolution), CRS, ChangeType, ForceUpdate);
    }
}
