using Importer.Services;

var repoPath = args.Length > 0
    ? args[0]
    : Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), "../HttpArena"));

var outputPath = args.Length > 1
    ? args[1]
    : Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), "data"));

var startingCommit = args.Length > 2
    ? args[2]
    : "ad48b80cac2904883a71eb644c98cc3d27924d1c";

Console.WriteLine($"Repository: {repoPath}");
Console.WriteLine($"Output:     {outputPath}");
Console.WriteLine($"Starting:   {startingCommit}");
Console.WriteLine();

var importer = new TimelineImporter(repoPath, outputPath, startingCommit);
await importer.RunAsync();
