#if WINDOWS
using Microsoft.UI.Xaml.Controls;
#endif

namespace MordheimLedgerApp.Components;

/// <summary>
/// Strips any non-digit character from an Entry's Text as the player types, rather than just hinting
/// a numeric soft keyboard (Keyboard="Numeric" alone doesn't block a physical keyboard, e.g. on
/// Windows). Kept separate from switching these fields to a numeric-typed property (int/int?): the End
/// of Game wizard's dice-roll and PX fields are deliberately bound as plain strings so an empty field
/// stays empty instead of showing a forced "0" (see EndOfGameDialogViewModel's ManualRoll/
/// ExperienceGainedText/etc.) - this behavior gets the "can't type letters" benefit without giving that
/// up.
///
/// On Windows, filtering only via TextChanged (below) let a rejected character render for one frame
/// before being stripped back out - visible as the typed letter flashing then disappearing (reported
/// 2026-08-17). TextChanged fires after WinUI has already committed the edit to the native TextBox, so
/// there's nothing left to prevent by the time it runs. TextBox.BeforeTextChanging fires before that
/// commit and can cancel it outright, so the letter never appears at all - added as a Windows-only
/// pre-commit filter on top of the cross-platform TextChanged fallback (which still runs everywhere,
/// Windows included, as a safety net for any edit that reaches Text through another path, e.g.
/// programmatic assignment or paste).
/// </summary>
public class DigitsOnlyBehavior : Behavior<Entry>
{
    protected override void OnAttachedTo(Entry bindable)
    {
        base.OnAttachedTo(bindable);
        bindable.TextChanged += OnTextChanged;
#if WINDOWS
        bindable.HandlerChanged += OnHandlerChanged;
        AttachPlatformFilter(bindable);
#endif
    }

    protected override void OnDetachingFrom(Entry bindable)
    {
        bindable.TextChanged -= OnTextChanged;
#if WINDOWS
        bindable.HandlerChanged -= OnHandlerChanged;
        DetachPlatformFilter(bindable);
#endif
        base.OnDetachingFrom(bindable);
    }

    private static void OnTextChanged(object? sender, Microsoft.Maui.Controls.TextChangedEventArgs e)
    {
        if (sender is not Entry entry || e.NewTextValue is null) return;

        var digitsOnly = new string(e.NewTextValue.Where(char.IsDigit).ToArray());
        if (digitsOnly != e.NewTextValue)
            entry.Text = digitsOnly;
    }

#if WINDOWS
    // La vue native (TextBox) n'existe qu'une fois le Handler créé - pas forcément déjà le cas dans
    // OnAttachedTo (ex. Entry pas encore réalisée dans un BindableLayout), d'où le double
    // essai/HandlerChanged plutôt qu'un seul hook à l'attache.
    private void OnHandlerChanged(object? sender, EventArgs e)
    {
        if (sender is Entry entry) AttachPlatformFilter(entry);
    }

    private static void AttachPlatformFilter(Entry entry)
    {
        if (entry.Handler?.PlatformView is TextBox textBox)
            textBox.BeforeTextChanging += OnBeforeTextChanging;
    }

    private static void DetachPlatformFilter(Entry entry)
    {
        if (entry.Handler?.PlatformView is TextBox textBox)
            textBox.BeforeTextChanging -= OnBeforeTextChanging;
    }

    private static void OnBeforeTextChanging(TextBox sender, TextBoxBeforeTextChangingEventArgs args) =>
        args.Cancel = args.NewText.Any(c => !char.IsDigit(c));
#endif
}
