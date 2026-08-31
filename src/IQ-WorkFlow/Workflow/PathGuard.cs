using System.IO;

namespace IQWorkflow;

/// <summary>
/// Keeps a path inside the directory it is supposed to be inside.
///
/// <b>A case folder is user input, and so is everything derived from it.</b> This workflow runs
/// unattended over client financial files, reads whatever a folder contains and writes artefacts
/// beside them; a <c>..</c> segment, an absolute path where a relative one was expected, or a
/// symlink pointing elsewhere all turn "write the report into the run folder" into "write it
/// anywhere". The check is one line at each boundary and its absence is not visible until it
/// matters.
///
/// <b>Compared after full resolution, not before.</b> <c>Path.GetFullPath</c> is what collapses
/// <c>..</c>, resolves a relative path against the working directory and normalises separators, so
/// comparing the strings as given would be comparing two spellings rather than two locations.
/// </summary>
internal static class PathGuard
{
    /// <summary>
    /// The full path of <paramref name="candidate"/>, or null where it does not resolve inside
    /// <paramref name="root"/>.
    ///
    /// Null rather than an exception: a caller validating configuration wants to report every
    /// problem it finds, not stop at the first.
    /// </summary>
    internal static string? Contain(string root, string candidate)
    {
        if (string.IsNullOrWhiteSpace(root) || string.IsNullOrWhiteSpace(candidate))
        {
            return null;
        }

        string fullRoot;
        string fullCandidate;

        try
        {
            fullRoot = Path.GetFullPath(root);
            fullCandidate = Path.GetFullPath(candidate, fullRoot);
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
        {
            // A path the platform will not even parse is not a path we should try to interpret.
            return null;
        }

        // Compared with the separator appended, so "/case" cannot be read as containing
        // "/case-archive" — the same boundary mistake as a prefix match on a canonical path.
        var prefix = fullRoot.EndsWith(Path.DirectorySeparatorChar)
            ? fullRoot
            : fullRoot + Path.DirectorySeparatorChar;

        if (string.Equals(fullCandidate, fullRoot, PathComparison))
        {
            return fullCandidate;
        }

        return fullCandidate.StartsWith(prefix, PathComparison) ? fullCandidate : null;
    }

    /// <summary>
    /// As <see cref="Contain"/>, but for a caller that cannot continue without the path.
    ///
    /// The message names the setting and the value it rejected, and nothing else: an operator needs
    /// to know which input was wrong, and a log of an unattended run should not carry more of the
    /// filesystem than that.
    /// </summary>
    internal static string Require(string root, string candidate, string what) =>
        Contain(root, candidate)
        ?? throw new InvalidOperationException(
            $"The {what} \"{candidate}\" resolves outside \"{root}\". Paths are held inside their "
            + "root so an unattended run cannot read or write outside the case it was given.");

    /// <summary>
    /// Windows paths are case-insensitive and this workflow targets Windows — Core's conversion
    /// stage drives Word and Excel over COM. Comparing case-sensitively here would let
    /// <c>C:\Case</c> and <c>c:\case</c> read as different roots and reject a legitimate path.
    /// </summary>
    private const StringComparison PathComparison = StringComparison.OrdinalIgnoreCase;
}
