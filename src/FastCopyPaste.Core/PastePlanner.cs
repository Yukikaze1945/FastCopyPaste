namespace FastCopyPaste.Core;

public static class PastePlanner
{
    private static readonly StringComparer PathComparer = StringComparer.OrdinalIgnoreCase;

    public static PastePlan Create(
        IEnumerable<string> sources,
        string targetDirectory,
        PasteMode mode)
    {
        if (string.IsNullOrWhiteSpace(targetDirectory))
        {
            return PastePlan.Invalid("目标目录为空。", mode);
        }

        string target;
        try
        {
            target = NormalizePath(targetDirectory);
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return PastePlan.Invalid($"目标目录无效：{ex.Message}", mode);
        }

        if (!Directory.Exists(target))
        {
            return PastePlan.Invalid($"目标目录不存在：{target}", mode);
        }

        var normalizedSources = new List<string>();
        var seen = new HashSet<string>(PathComparer);

        foreach (var sourceValue in sources)
        {
            if (string.IsNullOrWhiteSpace(sourceValue))
            {
                continue;
            }

            string source;
            try
            {
                source = NormalizePath(sourceValue);
            }
            catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
            {
                return PastePlan.Invalid($"源路径无效：{ex.Message}", mode);
            }

            if (!File.Exists(source) && !Directory.Exists(source))
            {
                return PastePlan.Invalid($"源项目不存在：{source}", mode);
            }

            if (IsRootPath(source))
            {
                return PastePlan.Invalid($"不允许把整个盘符根目录作为源：{source}", mode);
            }

            if (PathComparer.Equals(source, target))
            {
                return PastePlan.Invalid("目标目录不能与源目录相同。", mode);
            }

            if (Directory.Exists(source) && IsSameOrDescendant(target, source))
            {
                return PastePlan.Invalid($"不能把目录粘贴到它自身或子目录：{source}", mode);
            }

            if (seen.Add(source))
            {
                normalizedSources.Add(source);
            }
        }

        if (normalizedSources.Count == 0)
        {
            return PastePlan.Invalid("剪贴板中没有可粘贴的文件或目录。", mode);
        }

        if (normalizedSources.Any(source =>
                PathComparer.Equals(Path.GetDirectoryName(source), target)))
        {
            return new PastePlan(
                PastePlanStatus.PassThrough,
                mode,
                normalizedSources,
                target,
                [],
                mode == PasteMode.Copy
                    ? "同目录复制应由资源管理器创建副本。"
                    : "源项目已经位于目标目录中。");
        }

        var conflicts = normalizedSources
            .Select(source => Path.Combine(target, Path.GetFileName(source)))
            .Where(path => File.Exists(path) || Directory.Exists(path))
            .Distinct(PathComparer)
            .ToArray();

        return new PastePlan(
            PastePlanStatus.Ready,
            mode,
            normalizedSources,
            target,
            conflicts,
            null);
    }

    public static string NormalizePath(string path)
    {
        var fullPath = Path.GetFullPath(path.Trim());
        var root = Path.GetPathRoot(fullPath);
        if (root is not null && PathComparer.Equals(fullPath, root))
        {
            return root;
        }

        return fullPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
    }

    public static bool IsSameOrDescendant(string candidate, string parent)
    {
        var normalizedCandidate = NormalizePath(candidate);
        var normalizedParent = NormalizePath(parent);
        if (PathComparer.Equals(normalizedCandidate, normalizedParent))
        {
            return true;
        }

        return normalizedCandidate.StartsWith(
            normalizedParent + Path.DirectorySeparatorChar,
            StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsRootPath(string path)
    {
        var root = Path.GetPathRoot(path);
        return root is not null && PathComparer.Equals(path, root);
    }
}

