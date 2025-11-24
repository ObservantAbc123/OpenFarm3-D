using Avalonia.Media;
using System;

public enum Colorization
{
    Green,
    Yellow,
    Red
}

public static class MaintenanceColorHelper {
    public static Colorization GetReportStatusColorCode(DateTime lastService, DateTime nextService) =>
        GetZoneAndLocalT(lastService, nextService).zone;

    public static IBrush GetReportStatusBrush(DateTime lastService, DateTime nextService)
    {
        var (zone, localT) = GetZoneAndLocalT(lastService, nextService);
        var color = InterpolateZoneColor(zone, localT);
        return new SolidColorBrush(color);
    }

    // map [lastService, nextService] into:
    //   zone  = Green / Yellow / Red (thirds)
    //   localT in [0,1] inside that third
    private static (Colorization zone, double localT) GetZoneAndLocalT(
        DateTime lastService,
        DateTime nextService)
    {
        var now = DateTime.Now;

        var totalSeconds = (nextService - lastService).TotalSeconds;
        if (totalSeconds <= 0)
            return (Colorization.Red, 1.0);   // degenerate / overdue => full red

        var elapsedSeconds = (now - lastService).TotalSeconds;
        var p = elapsedSeconds / totalSeconds; // 0 = just serviced, 1 = at nextService
        p = Math.Clamp(p, 0.0, 1.0);

        const double third = 1.0 / 3.0;

        if (p <= third)
        {
            // 0–1 within green band
            return (Colorization.Green, p / third);
        }

        if (p <= 2 * third)
        {
            // 0–1 within yellow band
            return (Colorization.Yellow, (p - third) / third);
        }

        // 0–1 within red band
        return (Colorization.Red, (p - 2 * third) / third);
    }

    // leucistic / playful palettes per band.
    // localT in [0,1] maps from very pale -> stronger tint
    private static Color InterpolateZoneColor(Colorization zone, double localT)
    {
        Color start, end;
        switch (zone)
        {
            case Colorization.Green:
                // very pale mint -> richer green
                start = Color.Parse("#DFFFE5");
                end   = Color.Parse("#31C45E");
                break;

            case Colorization.Yellow:
                // creamy yellow -> strong indicator yellow
                start = Color.Parse("#FFF9DC");
                end   = Color.Parse("#F2C94C");
                break;

            case Colorization.Red:
            default:
                // soft rose -> warning red
                start = Color.Parse("#FFE1E1");
                end   = Color.Parse("#EB5757");
                break;
        }
        return Lerp(start, end, localT);
    }

    private static Color Lerp(Color a, Color b, double t)
    {
        t = Math.Clamp(t, 0.0, 1.0);

        byte r = (byte)(a.R + (b.R - a.R) * t);
        byte g = (byte)(a.G + (b.G - a.G) * t);
        byte bl = (byte)(a.B + (b.B - a.B) * t);
        byte al = (byte)(a.A + (b.A - a.A) * t);

        return Color.FromArgb(al, r, g, bl);
    }
}