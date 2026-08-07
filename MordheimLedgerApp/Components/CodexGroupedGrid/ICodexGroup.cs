namespace MordheimLedgerApp.Components;

/// <summary>Contract every Codex "XxxGroup" class (SkillGroup, MutationGroup, ...) must satisfy to be
/// usable as an item in CodexGroupedGridView.ItemsSource - the control only needs a display Name and
/// the IsFirst flag (trims the shared CodexGroupHeaderStyle's top margin), everything else (the row
/// items themselves) comes from the group already being an IEnumerable (ObservableCollection&lt;TRow&gt;).</summary>
public interface ICodexGroup
{
    string Name { get; }

    /// <summary>True for the first group in the list - set by the owning ViewModel after building it
    /// (a group doesn't know its own position).</summary>
    bool IsFirst { get; set; }
}
