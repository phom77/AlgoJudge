namespace AlgoJudge.ContentTool.Workspace;

internal sealed partial class WorkspacePathResolver
{
    private readonly string _root;
    private readonly string _physicalRoot;
    private readonly StringComparison _pathComparison =
        OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;

    public WorkspacePathResolver(string root)
    {
        _root = Path.GetFullPath(root);
        _physicalRoot = ResolveLink(new DirectoryInfo(_root));
    }

    public string ResolveDirectory(string relativePath, string description)
    {
        var fullPath = ResolveRelative(relativePath, description);
        if (!Directory.Exists(fullPath))
            throw WorkspaceJson.Error($"{description} does not exist.");
        EnsurePhysicalContainment(new DirectoryInfo(fullPath), description);
        return fullPath;
    }

    public string ResolveRequiredFile(string directory, string fileName, string description)
    {
        var path = Path.GetFullPath(Path.Combine(directory, fileName));
        EnsureLexicalContainment(path, description);
        if (!File.Exists(path))
            throw WorkspaceJson.Error($"{description} is missing.");
        EnsurePhysicalContainment(new FileInfo(path), description);
        return path;
    }

    public string? ResolveOptionalFile(string directory, string fileName, string description)
    {
        var path = Path.GetFullPath(Path.Combine(directory, fileName));
        EnsureLexicalContainment(path, description);
        if (!File.Exists(path))
            return null;
        EnsurePhysicalContainment(new FileInfo(path), description);
        return path;
    }

    public void EnsureContained(FileSystemInfo info, string description) =>
        EnsurePhysicalContainment(info, description);

    private string ResolveRelative(string relativePath, string description)
    {
        if (string.IsNullOrWhiteSpace(relativePath) ||
            Path.IsPathRooted(relativePath) ||
            relativePath.StartsWith("/", StringComparison.Ordinal) ||
            WindowsDrivePath().IsMatch(relativePath) ||
            relativePath.Contains('\\'))
        {
            throw WorkspaceJson.Error(
                $"{description} must be a forward-slash relative path.");
        }

        var segments = relativePath.Split('/');
        if (segments.Any(segment => segment.Length == 0 || segment is "." or ".."))
            throw WorkspaceJson.Error($"{description} contains an unsafe path segment.");

        var fullPath = Path.GetFullPath(Path.Combine(_root, Path.Combine(segments)));
        EnsureLexicalContainment(fullPath, description);
        return fullPath;
    }

    private void EnsureLexicalContainment(string path, string description)
    {
        if (!IsWithin(path, _root))
            throw WorkspaceJson.Error($"{description} escapes the content workspace.");
    }

    private void EnsurePhysicalContainment(FileSystemInfo info, string description)
    {
        var currentPath = info.FullName;
        var relative = Path.GetRelativePath(_root, currentPath);
        var current = new DirectoryInfo(_physicalRoot);
        foreach (var segment in relative.Split(
                     Path.DirectorySeparatorChar,
                     StringSplitOptions.RemoveEmptyEntries))
        {
            FileSystemInfo next = Directory.Exists(Path.Combine(current.FullName, segment))
                ? new DirectoryInfo(Path.Combine(current.FullName, segment))
                : new FileInfo(Path.Combine(current.FullName, segment));
            var resolved = ResolveLink(next);
            if (!IsWithin(resolved, _physicalRoot))
                throw WorkspaceJson.Error($"{description} escapes the workspace through a symbolic link.");
            current = Directory.Exists(resolved)
                ? new DirectoryInfo(resolved)
                : new DirectoryInfo(Path.GetDirectoryName(resolved)!);
        }
    }

    private string ResolveLink(FileSystemInfo info)
    {
        try
        {
            return Path.GetFullPath(info.ResolveLinkTarget(returnFinalTarget: true)?.FullName ?? info.FullName);
        }
        catch (Exception exception) when (
            exception is IOException or
            UnauthorizedAccessException or
            NotSupportedException)
        {
            throw WorkspaceJson.Error($"Workspace path cannot be resolved safely: {info.FullName}.");
        }
    }

    private bool IsWithin(string path, string root)
    {
        var normalizedPath = Path.GetFullPath(path);
        var normalizedRoot = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar);
        return normalizedPath.Equals(normalizedRoot, _pathComparison) ||
               normalizedPath.StartsWith(
                   normalizedRoot + Path.DirectorySeparatorChar,
                   _pathComparison);
    }

    [System.Text.RegularExpressions.GeneratedRegex(
        "^[A-Za-z]:",
        System.Text.RegularExpressions.RegexOptions.NonBacktracking)]
    private static partial System.Text.RegularExpressions.Regex WindowsDrivePath();
}
