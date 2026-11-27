using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MordheimLedgerApp.Features.Library.RacialProfiles;

namespace MordheimLedgerApp.Features.Library.Races;

/// <summary>Section « Races » du Codex (juste après Bandes) : deux onglets, Races et Profils raciaux, chacun avec sa liste à
/// plat existante. Remplace les deux icônes « gérer » de la section Bandes et leurs pages à part : la race sert désormais
/// aux bandes, aux types de guerrier, aux Francs-Tireurs, aux Dramatis Personae et aux objets.</summary>
public partial class RaceSectionViewModel : ObservableObject
{
    public RaceViewModel Races { get; }
    public RacialProfileViewModel RacialProfiles { get; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsRacesTab), nameof(IsRacialProfilesTab))]
    private int selectedTab;

    public bool IsRacesTab => SelectedTab == 0;
    public bool IsRacialProfilesTab => SelectedTab == 1;

    private bool _profilesLoaded;

    public RaceSectionViewModel(RaceViewModel races, RacialProfileViewModel racialProfiles)
    {
        Races = races;
        RacialProfiles = racialProfiles;
    }

    public Task InitializeAsync() => Races.InitializeAsync();

    [RelayCommand]
    private void ShowRaces() => SelectedTab = 0;

    [RelayCommand]
    private async Task ShowRacialProfiles()
    {
        SelectedTab = 1;
        if (_profilesLoaded) return;
        _profilesLoaded = true;
        await RacialProfiles.InitializeAsync();
    }
}
