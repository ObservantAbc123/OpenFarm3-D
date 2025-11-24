using DatabaseAccess.Models;

namespace print_submission_processing_service;

internal partial class GCodeParser
{
    public PrintMetaData GcodeMetaData = new PrintMetaData();
    public PrinterModel? PrinterModel;
    public MaterialType? MaterialType;

    private readonly List<GCodeCommand> _commands = [];

    /// <summary>
    /// Populates this instance of GCodeParser with commands using the provided stream.
    /// </summary>
    /// <param name="reader">reader with the .gcode stream open already</param>
    /// <param name="byteCount">Number of bytes already read from the stream of the reader before being passed to this method.</param>
    /// <returns>true if all metadata was found, false otherwise.</returns>
    public bool ParseGcodeFile(StreamReader reader, long byteCount)
    {
        while (reader.Peek() >= 0)
        {
            string? line = reader.ReadLine();
            if (string.IsNullOrEmpty(line))
            {
                byteCount += 1;
                continue;
            }
            byteCount += line.Length;
            byteCount += 1;
            GCodeCommand? command = ParseCommand(line);
            if (command is not null)
            {
                _commands.Add(command.Value);
            
                if (command.Value.CommandType == "M0")
                    GcodeMetaData.FinishedBytePos = byteCount;
            }
        }//161691

        return IsAllMetaDataFound();
    }

    /// <summary>
    /// Turn a single line of the .gcode file into a GCodeCommand and add it to Commands.
    /// If this line is a comment, it will be checked for meta data, but no command will be added.
    /// </summary>
    /// <param name="commandString">Single line of the .gcode file to be parsed.</param>
    /// <returns>GCodeCommand representing the commandString. Null if the string is only a comment.</returns>
    private GCodeCommand? ParseCommand(string commandString)
    {

        if (commandString.StartsWith(COMMENT))
        { 
            CheckForMetaData(commandString);
            return null;
        }

        string[] split = commandString.Split(" ");
        string commandType = split.First();

        if (IgnoredCommands.Contains(commandType) || !ValidCommands.Contains(commandType))
            return null;
        
        Dictionary<string, float?> commandParameters = new Dictionary<string, float?>();
        string? paramString = null;
        foreach (string parameter in split.Take(new Range(1, split.Length)))
        {
            string commandValue = parameter[..1];
            if (commandValue == COMMENT)
                break;
            
            if (float.TryParse(parameter[1..], out float value))
                commandParameters[commandValue] = value;
            else if (parameter[1..].Length == 0)
                commandParameters[commandValue] = null;
            else
            {
                paramString = string.Join(" ", split[1..]);
                break;
            }
        }

        return new GCodeCommand(commandType, commandParameters, paramString);
    }

    /// <summary>
    /// Checks a commandString to see if it contains metadata we are looking for.
    /// </summary>
    /// <param name="commandString">string to check for metadata. </param>
    private void CheckForMetaData(string commandString)
    {
        foreach (string searchString in MetaDataSearchStrings)
        {
            if (!commandString.StartsWith(searchString)) continue;
            int eqLoc = commandString.IndexOf('=') + 1;
            switch (searchString)
            {
                case "; printer_model":
                    GcodeMetaData.PrinterModel = commandString[eqLoc..].Trim();
                    break;
                case "; estimated printing time (normal mode)":
                    GcodeMetaData.Time = commandString[eqLoc..].Trim();
                    break;
                case "; total filament used [g]":
                    GcodeMetaData.Weight = commandString[eqLoc..].Trim();
                    break;
                case "; filament_type":
                    GcodeMetaData.Material = commandString[eqLoc..].Trim();
                    break;
            }
        }
    }

    /// <summary>
    /// How many commands to ignore for temperature checks at the start of the gcode
    /// </summary>
    private const int HEADER = 20;
    /// <summary>
    /// How many commands to ignore for temperature at the end of the gcode
    /// </summary>
    private const int FOOTER = 20;
    
    public ValidationResultTypes ValidateParameters()
    {
        if (PrinterModel is null || MaterialType is null)
            throw new Exception("PrinterModel and MaterialType cannot be null");
        int index = 0;
        foreach (GCodeCommand command in _commands)
        {
            switch (command.CommandType)
            {
                // Linear Movement
                case("G0"):
                case("G1"):
                    foreach (string param in command.CommandParameters.Keys)
                    {
                        float? value = command.CommandParameters[param];
                        switch (param)
                        {
                            case("X"):
                                if (value > PrinterModel.BedXMax || value < PrinterModel.BedXMin)
                                    return ValidationResultTypes.X_BOUNDS;
                                break;
                            case("Y"):
                                if (value > PrinterModel.BedYMax || value < PrinterModel.BedYMin)
                                    return ValidationResultTypes.Y_BOUNDS;
                                break;
                            case("Z"):
                                if (value > PrinterModel.BedZMax || value < PrinterModel.BedZMin)
                                    return ValidationResultTypes.Z_BOUNDS;
                                break;
                        }
                    }
                    break;
                // Bed Temperature
                case("M140"):
                case("M190"):
                    foreach (string param in command.CommandParameters.Keys)
                    {
                        float? value = command.CommandParameters[param];
                        switch (param)
                        {
                            case("R"):
                            case("S"):
                                if (value > MaterialType.BedTempCeiling || value < MaterialType.BedTempFloor)
                                    if (index > HEADER && index < _commands.Count - FOOTER)
                                        return ValidationResultTypes.BED_TEMP;                                
                                break;
                        }
                    }
                    break;
                // Nozzle Temperature
                case("M104"):
                case("M109"):
                    foreach (string param in command.CommandParameters.Keys)
                    {
                        float? value = command.CommandParameters[param];
                        switch (param)
                        {
                            case("R"):
                            case("S"):
                                if (value > MaterialType.PrintTempCeiling || value < MaterialType.PrintTempFloor)
                                    if (index > HEADER && index < _commands.Count - FOOTER)
                                        return ValidationResultTypes.NOZZLE_TEMP;
                                break;
                        }
                    }
                    break;
            }

            index++;
        }
        return ValidationResultTypes.PASSED;
    }

    /// <summary>
    /// Returns true if all relevant metadata was found, false otherwise.
    /// Only valid once ParseGcodeFile has been called and completed successfully.
    /// </summary>
    /// <returns>True is all metadata found, false otherwise.</returns>
    private bool IsAllMetaDataFound()
    {
        return !string.IsNullOrEmpty(GcodeMetaData.PrinterModel) &&
               !string.IsNullOrEmpty(GcodeMetaData.Material) &&
               !string.IsNullOrEmpty(GcodeMetaData.Time) &&
               !string.IsNullOrEmpty(GcodeMetaData.Weight) &&
               GcodeMetaData.FinishedBytePos > 0;
    }
}