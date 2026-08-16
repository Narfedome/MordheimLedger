namespace MordheimLedgerApp.Components;

public partial class WarbandRestrictionEditorView : ContentView
{
    public static readonly BindableProperty EditorProperty =
        BindableProperty.Create(nameof(Editor), typeof(WarbandRestrictionEditor), typeof(WarbandRestrictionEditorView));

    public WarbandRestrictionEditor? Editor
    {
        get => (WarbandRestrictionEditor?)GetValue(EditorProperty);
        set => SetValue(EditorProperty, value);
    }

    public WarbandRestrictionEditorView()
    {
        InitializeComponent();
    }
}
