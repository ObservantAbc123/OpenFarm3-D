using OctoprintHelper;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace PrintManagement;

public static class MaintenanceHelpers
{

    #region Globals
    private static readonly Regex ParamRegex = new(@"([XYZE])\s*(-?\d+(\.\d+)?)", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    #endregion

    #region Local Helpers
    /// <summary>
    /// Get the current extruder temp. by extracting it from the JSON
    /// contained in the payload of <param name="response"/>.
    /// </summary>
    /// <param name="response">Contains payload-of-interest</param>
    /// <returns>The temperature</returns>
    private static async Task<double?> ExtractToolTempFromResponseAsync(HttpResponseMessage response)
    {

        // read content directly as a Stream
        await using var stream = await response.Content.ReadAsStreamAsync();
        double? actualValue = null;

        // parse the stream asynchronously
        using var doc = await JsonDocument.ParseAsync(stream);
        foreach (JsonProperty property in doc.RootElement.EnumerateObject())
        {
            if (property.Name.StartsWith("tool0") &&
                property.Value.TryGetProperty("actual", out JsonElement actualElement))
            {
                // handle null value if "actual" is null
                actualValue = actualElement.ValueKind == JsonValueKind.Null
                    ? null
                    : actualElement.GetDouble();
                break;
            }
        }
        return actualValue;
    }

    /// <summary>
    /// Helper for parsing movement commands.
    /// </summary>
    /// <param name="line">Line of gcode</param>
    /// <param name="axis">Axis of translation</param>
    /// <returns></returns>
    private static double? GetValue(string line, char axis)
    {
        var matches = ParamRegex.Matches(line);
        foreach (Match match in matches)
        {
            if (match.Groups[1].Value.Equals(axis.ToString(), StringComparison.OrdinalIgnoreCase))
            {
                if (double.TryParse(match.Groups[2].Value, out double val))
                    return val;
            }
        }
        return null;
    }
    #endregion

    #region Volumetric Calculations
    /// <summary>
    /// Wrapper for the proceeding computation.
    /// </summary>
    /// <param name="_fileClient">File-Processor helper</param>
    /// <param name="printJobId">ID of target job</param> // TODO: i am a fool this should be a printid
    /// <param name="filamentDiameterMm">Diameter of extrusion nozzle; defaults to 1.75mm as this is common</param>
    /// <returns></returns>
    public static async Task<decimal> CalculateVolume(FileServerClient.FileServerClient _fileClient, long printJobId, double filamentDiameterMm = 1.75)
    {
        byte[] data = await _fileClient.GetGcodeBytesAsync(printJobId);
        using var stream = new MemoryStream(data);
        return (decimal)await CalculateVolume(stream, filamentDiameterMm);
    }

    /// <summary>
    /// For a given Print, upon completion, retroactively and statically
    /// analyzes the associated GCode and computes a (reasonably precise)
    /// figure representing, in Cubic Meters, how much material was extruded.
    /// </summary>
    /// <param name="gcodeStream"></param>
    /// <param name="filamentDiameterMm"></param>
    /// <returns></returns>
    private static Task<double> CalculateVolume(Stream gcodeStream, double filamentDiameterMm = 1.75)
    {
        double totalExtrusionMm = 0;
        double lastE = 0;
        bool isRelativeE = false; // default is Absolute (M82)

        using (var reader = new StreamReader(gcodeStream))
        {
            string line;
            while ((line = reader.ReadLine()) != null)
            {
                line = line.Trim();
                if (string.IsNullOrEmpty(line) || line.StartsWith(";")) continue;

                // check extrusion mode
                if (line.StartsWith("M82", StringComparison.OrdinalIgnoreCase))
                {
                    isRelativeE = false;
                    continue;
                }
                if (line.StartsWith("M83", StringComparison.OrdinalIgnoreCase))
                {
                    isRelativeE = true;
                    lastE = 0; // tracking reset for relative
                    continue;
                }

                // only care if Absolute mode
                if (line.StartsWith("G92", StringComparison.OrdinalIgnoreCase))
                {
                    double? newE = GetValue(line, 'E');
                    if (newE.HasValue && !isRelativeE) lastE = newE.Value;
                    continue;
                }

                // calculate extrusion on moves
                if (line.StartsWith("G1", StringComparison.OrdinalIgnoreCase))
                {
                    double? eVal = GetValue(line, 'E');
                    if (eVal.HasValue)
                    {
                        double delta = 0;

                        if (isRelativeE)
                        {
                            // relative mode, the value IS the extrusion amount
                            delta = eVal.Value;
                        }
                        else
                        {
                            // absolute mode, calculate difference
                            delta = eVal.Value - lastE;
                            lastE = eVal.Value;
                        }

                        // filter retractions (negative delta)
                        if (delta > 0) totalExtrusionMm += delta;
                    }
                }
            }
        }

        // V = Length * (pi * r^2)
        double radiusMm = filamentDiameterMm / 2.0;
        double areaMm2 = Math.PI * Math.Pow(radiusMm, 2);
        return Task.FromResult((totalExtrusionMm * areaMm2) * 1e-9); // mm^3 to m^3
    }
    #endregion

    #region Linear Calculations
    public static async Task<decimal> CalculateLinearTravel(FileServerClient.FileServerClient _fileClient, long printJobId)
    {
        byte[]? data = await _fileClient.GetGcodeBytesAsync(printJobId);
        using var stream = new MemoryStream(data);
        return (decimal)await CalculateLinearTravel(stream);
    }

    private static Task<double> CalculateLinearTravel(Stream gcodeStream)
    {
        double totalDistMm = 0;
        double currentX = 0, currentY = 0, currentZ = 0;
        bool isRelativeMove = false; // default is absolute (G90)

        using (var reader = new StreamReader(gcodeStream))
        {
            string line;
            while ((line = reader.ReadLine()) != null)
            {
                line = line.Trim();
                if (string.IsNullOrEmpty(line) || line.StartsWith(";")) continue;

                // check positioning mode
                if (line.StartsWith("G90", StringComparison.OrdinalIgnoreCase))
                {
                    isRelativeMove = false;
                    continue;
                }
                if (line.StartsWith("G91", StringComparison.OrdinalIgnoreCase))
                {
                    isRelativeMove = true;
                    continue;
                }
                // handle set position (G92); resets current coordinates
                if (line.StartsWith("G92", StringComparison.OrdinalIgnoreCase))
                {
                    currentX = GetValue(line, 'X') ?? currentX;
                    currentY = GetValue(line, 'Y') ?? currentY;
                    currentZ = GetValue(line, 'Z') ?? currentZ;
                    continue;
                }

                if (line.StartsWith("G0", StringComparison.OrdinalIgnoreCase) ||
                    line.StartsWith("G1", StringComparison.OrdinalIgnoreCase))
                {
                    double? xVal = GetValue(line, 'X');
                    double? yVal = GetValue(line, 'Y');
                    double? zVal = GetValue(line, 'Z');

                    if (!xVal.HasValue && !yVal.HasValue && !zVal.HasValue) continue;

                    double moveDist = 0;

                    if (isRelativeMove)
                    {
                        // relative: the arguments are the deltas
                        double dX = xVal ?? 0;
                        double dY = yVal ?? 0;
                        double dZ = zVal ?? 0;

                        moveDist = Math.Sqrt(dX * dX + dY * dY + dZ * dZ);

                        // update state
                        currentX += dX;
                        currentY += dY;
                        currentZ += dZ;
                    }
                    else
                    {
                        // absolute: the arguments are the targets
                        double targetX = xVal ?? currentX;
                        double targetY = yVal ?? currentY;
                        double targetZ = zVal ?? currentZ;

                        moveDist = Math.Sqrt(
                            Math.Pow(targetX - currentX, 2) +
                            Math.Pow(targetY - currentY, 2) +
                            Math.Pow(targetZ - currentZ, 2)
                        );

                        // update state
                        currentX = targetX;
                        currentY = targetY;
                        currentZ = targetZ;
                    }

                    totalDistMm += moveDist;
                }
            }
        }
        return Task.FromResult(totalDistMm / 1000.0); // mm to meters
    }
    #endregion


    #region Printer Interaction
    public static async Task<double> GetCurrentPrinterToolTemp(IOctoprintHelper _octoHelper, RegisteredInstance _client,
        ILogger<PMSWorker> _logger)
    {
        double temp = 0.0;
        try
        {
            HttpResponseMessage response = await _octoHelper.GetExtruderTemperature(_client._clientConnection);
            return temp = await ExtractToolTempFromResponseAsync(response) ?? 0.0;
        }
        catch (HttpRequestException ex)
        {
            _logger.LogWarning("Could not retrieve extruder temperature, Printer may not be printing.");
            return temp;
        }
    }
    #endregion
}