using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.CommandLine;

var repoArgument = new Argument<string>(
    name: "repo",
    description: "The repository"
);

var userArgument = new Argument<string>(
    name: "user",
    description: "The user whose belong the repository"
);

var folderArgument = new Argument<string>(
    name: "folder",
    description: "The folder to download in the repository"
);

var branchOption = new Option<string>(
    name: "--branch",
    description: "The branch of the repository to fetch from"
);

var tokenOption = new Option<string>(
    name: "--token",
    description: "An personal access token needed for private repositories"
);

var parser = new RootCommand(description: "Download a given directory in a Github repository")
{
    repoArgument,
    userArgument,
    folderArgument,
    branchOption,
    tokenOption
};

parser.SetHandler(async (string repo, string user, string folder, string? branch, string? token) =>
{
    var client = new GithubHttpClient(token)
    {
        User = user,
        Repo = repo,
        Branch = branch
    };
    await client.DownloadDirectory(folder);
}, repoArgument, userArgument, folderArgument, branchOption, tokenOption);

await parser.InvokeAsync(args);

class GithubHttpClient
{
    private readonly HttpClient _client;

    public string? Repo { get; set; }
    public string? User { get; set; }
    public string? Branch { get; set; }

    public GithubHttpClient(string? token)
    {
        _client = new HttpClient();
        _client.DefaultRequestHeaders.Accept.Clear();
        _client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github.v3+json"));
        if (!string.IsNullOrEmpty(token))
            _client.DefaultRequestHeaders.Add("Authorization", $"token {token}");
        _client.DefaultRequestHeaders.Add("User-Agent", ".NET GithubHttpClient");
    }

    public async Task<List<RepositoryElement>?> GetRepositoryElements(string path, string branch)
    {
        var stream = await _client.GetStreamAsync($"https://api.github.com/repos/{User}/{Repo}/contents/{path}?ref={branch}");
        return await JsonSerializer.DeserializeAsync<List<RepositoryElement>>(stream);
    }

    public async Task DownloadFile(RepositoryElement repoFile)
    {
        Console.WriteLine($"\tDownloading {repoFile.Path}...");
        var content = await _client.GetByteArrayAsync(repoFile.DownloadURL);
        await File.WriteAllBytesAsync($"{Repo}/{repoFile.Path}", content);
    }

    public async Task DownloadDirectory(string path = "")
    {
        var directory = $"{Repo}/{path}";

        Console.WriteLine($"Directory {directory}");

        if (directory != "")
            Directory.CreateDirectory(directory);

        var elementList = await GetRepositoryElements(path, Branch ?? "master");

        if (elementList == null)
            return;

        foreach (var element in elementList)
        {
            switch (element.Type)
            {
                case "dir":
                    await DownloadDirectory(element.Path ?? "");
                    break;
                case "file":
                    await DownloadFile(element);
                    break;
                default:
                    break;
            }
        }    
    }
}

class RepositoryElement
{
    [JsonPropertyName("type")]
    public string? Type { get; set; }
    [JsonPropertyName("path")]
    public string? Path { get; set; }
    [JsonPropertyName("download_url")]
    public string? DownloadURL { get; set; }
}