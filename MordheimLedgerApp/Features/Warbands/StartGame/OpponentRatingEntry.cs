using CommunityToolkit.Mvvm.ComponentModel;

namespace MordheimLedgerApp.Features.Warbands.StartGame;

/// <summary>Valeur d'une bande adverse saisie au début de partie (1.6) - facultative, jusqu'à 3 adversaires. Sert à
/// l'XP d'outsider en Fin de partie (Warband.OpponentRatings) et à l'aide des Dramatis Personae.</summary>
public partial class OpponentRatingEntry : ObservableObject
{
    private readonly Action _changed;

    public OpponentRatingEntry(string label, Action changed, string text = "")
    {
        this.label = label;
        _changed = changed;
        this.text = text;
    }

    [ObservableProperty]
    private string label;

    [ObservableProperty]
    private string text;

    [ObservableProperty]
    private bool canRemove;

    partial void OnTextChanged(string value) => _changed();

    public int? Value => int.TryParse(Text, out var rating) && rating > 0 ? rating : null;
}
