namespace print_submission_processing_service;

using System.Text;

public static class ThumbnailReader
{
    /// <summary>
    /// reads the stream until a thumbnail is found or a non-comment line is seen. Will consume lines from the stream,
    /// but should only consume comments.
    /// </summary>
    /// <param name="reader">reader with .gcode file stream. Will consume comment lines from the start of the file,
    /// but will not consume any .gcode commands</param>
    /// <param name="bytesRead"> Number of bytes this reader consumes. Used to calculate finishedBytePos</param>
    /// <returns>string representing base64 thumbnail if found, null otherwise.</returns>
    public static string? GetThumbnailAsBase64String(StreamReader reader, out long bytesRead)
    {
        StringBuilder base64 = new StringBuilder();
        bytesRead = 0;
        bool startFound = false;
        while (reader.Peek() >= 0)
        {
            if (!startFound)
            {
                // Peek at next char without reading
                char nextChar = (char)reader.Peek();
                // If it isn't a comment or newline, we are seeing a .gcode command and there wasn't a thumbnail
                if (nextChar is not (';' or '\n'))
                    return null;
                
                string? line = reader.ReadLine();
                if (string.IsNullOrWhiteSpace(line))
                {
                    bytesRead += 1;
                    continue;
                }
                bytesRead += line.Length;
                bytesRead += 1;
                if (!line.StartsWith("; thumbnail begin")) 
                    continue;
                
                startFound = true;
            }
            else
            {
                string? line = reader.ReadLine();
                if (string.IsNullOrWhiteSpace(line))
                {
                    bytesRead += 1;
                    continue;
                }
                bytesRead += line.Length;
                bytesRead += 1;
                if (line.StartsWith("; thumbnail end"))
                    break;
                base64.Append(line.Trim(';').Trim());
                
                // Peek at next char without reading
                char nextChar = (char)reader.Peek();
                // If it isn't a comment or newline, we are seeing a .gcode command and we missed the end of the thumbnail somehow
                if (nextChar is not (';' or '\n'))
                    throw new Exception("End of thumbnail not found");
            }
        }

        return base64.ToString();
    }
}