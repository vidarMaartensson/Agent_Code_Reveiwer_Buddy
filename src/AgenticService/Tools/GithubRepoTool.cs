using System.ComponentModel;
using System.Text;
using LibGit2Sharp;
using Microsoft.Extensions.Logging;

namespace AgenticService.Tools;

public class GitHubTools
{
    private readonly ILogger<GitHubTools> _logger;

    public GitHubTools(ILogger<GitHubTools> logger)
    {
        _logger = logger;
    }

    public string CloneRepo(string repoUrl)
    {
        string tempPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Repository.Clone(repoUrl, tempPath);
        return tempPath;
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

    public string ReadFiles(string localPath, List<string> relativePaths)
    {
        var sb = new StringBuilder();
        foreach (var relPath in relativePaths)
        {
            var fullPath = Path.Combine(localPath, relPath);
            if (!File.Exists(fullPath)) continue;

            sb.AppendLine($"--- Start of {relPath} ---");
            sb.AppendLine(File.ReadAllText(fullPath));
            sb.AppendLine($"--- End of {relPath} ---");
            sb.AppendLine();
        }
        return sb.ToString();
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