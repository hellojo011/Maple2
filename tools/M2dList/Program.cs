// Lists the entries inside a MapleStory2 .m2d archive, so you can check whether a file
// the ingest expects is actually present in the client data.
//
//   dotnet run -- /ClientData/Server.m2d AI/
//   dotnet run -- /ClientData/Server.m2d AI/AI_DefaultNew.xml --dump
using Maple2.File.IO;
using Maple2.File.IO.Crypto.Common;

if (args.Length < 1) {
    Console.Error.WriteLine("usage: M2dList <archive.m2d> [prefix] [--dump]");
    return 1;
}

string archive = args[0];
string prefix = args.Length > 1 && !args[1].StartsWith("--") ? args[1] : string.Empty;
bool dump = args.Contains("--dump");

if (!File.Exists(archive)) {
    Console.Error.WriteLine($"Not found: {archive}");
    return 1;
}

using var reader = new M2dReader(archive);
List<PackFileEntry> matches = reader.Files
    .Where(entry => entry.Name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
    .OrderBy(entry => entry.Name, StringComparer.OrdinalIgnoreCase)
    .ToList();

Console.WriteLine($"{archive}: {reader.Files.Count} entries total, {matches.Count} matching \"{prefix}\"");
foreach (PackFileEntry entry in matches) {
    Console.WriteLine($"  {entry.Name}");
}

if (dump) {
    foreach (PackFileEntry entry in matches) {
        Console.WriteLine();
        Console.WriteLine($"----- {entry.Name} -----");
        Console.WriteLine(reader.GetString(entry));
    }
}

return 0;
