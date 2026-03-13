namespace TianyiVision.Acis.Services.Devices;

public sealed record PointStagePlacementModel(
    double X,
    double Y,
    bool UsesGeographicCoordinate);

public sealed record PointStageLayoutPreset(
    double Left,
    double Top,
    double Right,
    double Bottom,
    double FallbackStartX,
    double FallbackStartY,
    int FallbackColumns,
    double FallbackHorizontalGap,
    double FallbackVerticalGap);

public static class PointStageProjection
{
    public static IReadOnlyDictionary<string, PointStagePlacementModel> Project(
        IReadOnlyList<PointWorkspaceItemModel> points,
        PointStageLayoutPreset preset)
    {
        var placements = new Dictionary<string, PointStagePlacementModel>(StringComparer.Ordinal);
        if (points.Count == 0)
        {
            return placements;
        }

        var mappablePoints = points
            .Where(point => point.Coordinate.CanRenderOnMap)
            .ToList();
        var unmappablePoints = points
            .Where(point => !point.Coordinate.CanRenderOnMap)
            .ToList();

        if (mappablePoints.Count == 1)
        {
            var point = mappablePoints[0];
            placements[point.PointId] = new PointStagePlacementModel(
                (preset.Left + preset.Right) / 2d,
                (preset.Top + preset.Bottom) / 2d,
                true);
        }
        else if (mappablePoints.Count > 1)
        {
            var minLongitude = mappablePoints.Min(point => point.Coordinate.Longitude);
            var maxLongitude = mappablePoints.Max(point => point.Coordinate.Longitude);
            var minLatitude = mappablePoints.Min(point => point.Coordinate.Latitude);
            var maxLatitude = mappablePoints.Max(point => point.Coordinate.Latitude);
            var longitudeRange = Math.Max(0.000001d, maxLongitude - minLongitude);
            var latitudeRange = Math.Max(0.000001d, maxLatitude - minLatitude);
            var usableWidth = Math.Max(1d, preset.Right - preset.Left);
            var usableHeight = Math.Max(1d, preset.Bottom - preset.Top);

            foreach (var point in mappablePoints)
            {
                var normalizedLongitude = (point.Coordinate.Longitude - minLongitude) / longitudeRange;
                var normalizedLatitude = (point.Coordinate.Latitude - minLatitude) / latitudeRange;

                placements[point.PointId] = new PointStagePlacementModel(
                    preset.Left + normalizedLongitude * usableWidth,
                    preset.Top + (1d - normalizedLatitude) * usableHeight,
                    true);
            }
        }

        for (var index = 0; index < unmappablePoints.Count; index++)
        {
            var point = unmappablePoints[index];
            var column = index % Math.Max(1, preset.FallbackColumns);
            var row = index / Math.Max(1, preset.FallbackColumns);

            placements[point.PointId] = new PointStagePlacementModel(
                preset.FallbackStartX + column * preset.FallbackHorizontalGap,
                preset.FallbackStartY + row * preset.FallbackVerticalGap,
                false);
        }

        return placements;
    }
}
