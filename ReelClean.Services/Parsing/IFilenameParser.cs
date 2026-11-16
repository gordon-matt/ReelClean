namespace ReelClean.Services.Parsing;

public interface IFilenameParser
{
    ParsedFilename Parse(string filename);
}
