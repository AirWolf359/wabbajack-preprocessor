using WabbajackPreprocessor.Core.Settings;

namespace WabbajackPreprocessor.Core.Analysis;

/// <summary>The user's decisions from the three finding lists, ready to apply.</summary>
public sealed class SettingsUpdatePlan
{
    /// <summary>Stale entries to delete (as reported by the analyzer, indices intact).</summary>
    public List<StaleEntry> RemoveEntries { get; } = [];

    /// <summary>Mod names to add to AlwaysEnabled (as <c>mods\&lt;name&gt;</c>).</summary>
    public List<string> MarkAlwaysEnabled { get; } = [];

    /// <summary>Mod names to add to Include (as <c>mods\&lt;name&gt;</c>).</summary>
    public List<string> MarkInclude { get; } = [];

    /// <summary>Mod names to add to NoMatchInclude (as <c>mods\&lt;name&gt;</c>).</summary>
    public List<string> MarkNoMatchInclude { get; } = [];

    public bool IsEmpty =>
        RemoveEntries.Count == 0 && MarkAlwaysEnabled.Count == 0
        && MarkInclude.Count == 0 && MarkNoMatchInclude.Count == 0;
}

public static class SettingsUpdater
{
    /// <summary>
    /// Applies the plan to the settings in memory. Returns a human-readable change log
    /// (one line per change).
    /// </summary>
    public static List<string> Apply(CompilerSettings settings, SettingsUpdatePlan plan)
    {
        var changes = new List<string>();

        // Remove by index, highest first within each list, so earlier indices stay valid.
        foreach (var group in plan.RemoveEntries.GroupBy(e => e.List))
        {
            var list = settings.GetTagList(group.Key);
            foreach (var entry in group.OrderByDescending(e => e.Index))
            {
                if (entry.Index < list.Count && RelPaths.AreEqual(list[entry.Index], entry.Entry))
                {
                    list.RemoveAt(entry.Index);
                    changes.Add($"Removed '{entry.Entry}' from {group.Key}.");
                }
                else if (list.RemoveAll(e => RelPaths.AreEqual(e, entry.Entry)) > 0)
                {
                    // The list changed since analysis; fall back to matching by value.
                    changes.Add($"Removed '{entry.Entry}' from {group.Key}.");
                }
            }
        }

        AddMods(settings, TagList.AlwaysEnabled, plan.MarkAlwaysEnabled, changes);
        AddMods(settings, TagList.Include, plan.MarkInclude, changes);
        AddMods(settings, TagList.NoMatchInclude, plan.MarkNoMatchInclude, changes);
        return changes;
    }

    private static void AddMods(CompilerSettings settings, TagList list, List<string> modNames, List<string> changes)
    {
        var entries = settings.GetTagList(list);
        foreach (var modName in modNames)
        {
            var entry = $"mods\\{modName}";
            if (entries.Any(e => RelPaths.AreEqual(e, entry)))
                continue;
            entries.Add(entry);
            changes.Add($"Added '{entry}' to {list}.");
        }
    }
}
