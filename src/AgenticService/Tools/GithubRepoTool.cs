using System.ComponentModel;
using System.Text;
using LibGit2Sharp;
using Microsoft.Extensions.Logging;

namespace AgenticService.Tools;

public class GitHubTools
{
    private readonly ILogger<GitHubTools> _logger;
    private readonly string? _token;

    public GitHubTools(ILogger<GitHubTools> logger, IConfiguration config)
    {
        _logger = logger;
        // Optional, only needed to review private repositories
        _token = config["GitHub:Token"] ?? config["GITHUB_TOKEN"];
    }

    public string CloneRepo(string repoUrl)
    {
        var url = NormalizeRepoUrl(repoUrl);
        string tempPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        // Shallow clone: we only read the latest files, so skip downloading the full history
        var options = new CloneOptions { FetchOptions = { Depth = 1 } };
        if (!string.IsNullOrWhiteSpace(_token))
        {
            options.FetchOptions.CredentialsProvider = (_, _, _) =>
                new UsernamePasswordCredentials { Username = "x-access-token", Password = _token };
        }

        try
        {
            Repository.Clone(url, tempPath, options);
        }
        // GitHub asks for credentials both for private and for non-existent repositories
        catch (LibGit2SharpException ex) when (ex.Message.Contains("authentication", StringComparison.OrdinalIgnoreCase))
        {
            Cleanup(tempPath);
            throw new InvalidOperationException(
                $"Could not access {url}. The repository is private or does not exist. Check the URL, or make the repository public.", ex);
        }
        catch
        {
            Cleanup(tempPath);
            throw;
        }
        return tempPath;
    }

    /// <summary>
    /// Turns what people paste from the browser (e.g. "github.com/owner/repo/tree/main") into a cloneable URL.
    /// Only http(s) URLs are allowed, so a request can never clone a path on the server itself.
    /// </summary>
    public static string NormalizeRepoUrl(string repoUrl)
    {
        var url = repoUrl.Trim();
        if (!url.Contains("://")) url = "https://" + url;

        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) ||
            (uri.Scheme != Uri.UriSchemeHttps && uri.Scheme != Uri.UriSchemeHttp))
            throw new InvalidOperationException("Please provide an https URL to the repository, e.g. https://github.com/owner/repo.");

        var isGitHub = uri.Host.Equals("github.com", StringComparison.OrdinalIgnoreCase) ||
                       uri.Host.Equals("www.github.com", StringComparison.OrdinalIgnoreCase);
        var segments = uri.AbsolutePath.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (!isGitHub || segments.Length < 2) return url;

        var repo = segments[1].EndsWith(".git", StringComparison.OrdinalIgnoreCase) ? segments[1][..^4] : segments[1];
        return $"https://github.com/{segments[0]}/{repo}.git";
    }

    public List<string> GetFileList(string localPath)
    {
        return Directory.GetFiles(localPath, "*.*", SearchOption.AllDirectories)
                        .Where(f => !f.Contains($"{Path.DirectorySeparatorChar}.git{Path.DirectorySeparatorChar}") && 
                                    !f.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}") && 
                                    !f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}") &&
                                    !f.Contains($"{Path.DirectorySeparatorChar}node_modules{Path.DirectorySeparatorChar}") &&
                                    IsSourceCodeFile(f))
                        .Select(f => Path.GetRelativePath(localPath, f))
                        .ToList();
    }

    public List<(string Path, string Content)> ReadFiles(string localPath, List<string> relativePaths)
    {
        var files = new List<(string Path, string Content)>();
        foreach (var relPath in relativePaths)
        {
            var fullPath = Path.Combine(localPath, relPath);
            if (!File.Exists(fullPath)) continue;

            var sb = new StringBuilder();
            sb.AppendLine($"--- Start of {relPath} ---");
            sb.AppendLine(File.ReadAllText(fullPath));
            sb.AppendLine($"--- End of {relPath} ---");
            sb.AppendLine();
            files.Add((relPath, sb.ToString()));
        }
        return files;
    }

    public void Cleanup(string localPath)
    {
        if (Directory.Exists(localPath))
        {
            DeleteDirectory(localPath);
        }
    }

    private static bool IsSourceCodeFile(string filePath)
    {
        var validExtensions = new[] { ".cs", ".js", ".ts", ".jsx", ".tsx", ".html", ".css" };
        var extension = Path.GetExtension(filePath).ToLower();
        return validExtensions.Contains(extension);
    }

    private void DeleteDirectory(string targetDir)
    {
        string[] files = Directory.GetFiles(targetDir);
        string[] dirs = Directory.GetDirectories(targetDir);

        foreach (string file in files)
        {
            try {
                File.SetAttributes(file, FileAttributes.Normal);
                File.Delete(file);
            } catch (Exception ex) { 
                _logger.LogWarning(ex, "Failed to delete file {File} during cleanup. Attempting to continue.", file);
            }
        }

        foreach (string dir in dirs)
        {
            DeleteDirectory(dir);
        }

        Directory.Delete(targetDir, false);
    }
}