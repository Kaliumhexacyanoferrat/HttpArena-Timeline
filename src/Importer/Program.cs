using Importer.Services;

var repoPath = args.Length > 0
    ? args[0]
    : Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), "../HttpArena"));

var outputPath = args.Length > 1
    ? args[1]
    : Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), "data"));

var startingCommit = args.Length > 2
    ? args[2]
    : "756e9ddf7e4bbf357f0504bca31cce398d089042";

Console.WriteLine($"Repository: {repoPath}");
Console.WriteLine($"Output:     {outputPath}");
Console.WriteLine($"Starting:   {startingCommit}");
Console.WriteLine();

var importer = new TimelineImporter(repoPath, outputPath, startingCommit);
await importer.RunAsync();
