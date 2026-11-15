using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using MordheimLedgerApp.Core.Exchange;
using MordheimLedgerApp.Core.Models.Library;
using MordheimLedgerApp.Core.Services;
using MordheimLedgerApp.Features.Library.WarbandArchetypes.CreateEdit;
using MordheimLedgerApp.Features.Warbands.CreateEdit;
using MordheimLedgerApp.Services;
using System.Collections.ObjectModel;

namespace MordheimLedgerApp.Features.Warbands;

public partial class WarbandListViewModel : BaseViewModel
{
    private readonly IWarbandArchetypePickerNavigationService _pickerNavigation;
    private readonly IWarbandService _warbandService;
    private readonly IWarbandArchetypePickerService _warbandArchetypePickerService;
    private readonly ILibraryService _libraryService;
    private readonly IDetailDialogService _detailDialogs;
    private readonly IEquipmentPickerService _equipmentPickerService;
    private readonly ISkillPickerService _skillPickerService;
    private readonly ISpellPickerService _spellPickerService;
    private readonly IInjuryPickerService _injuryPickerService;
    private readonly IMutationPickerService _mutationPickerService;
    private readonly IHiredSwordPickerService _hiredSwordPickerService;
    private readonly OfficialContentService _officialContent;
    [ObservableProperty]
    private ObservableCollection<WarbandRow> rows = new();

    [ObservableProperty]
    private bool hasWarbands;

    // Reste false pendant le court instant entre l'affichage de la page et le vrai début du
    // chargement (Loading.IsLoading ne passe à true qu'à l'intérieur de LoadWarbandsAsync) : sans ce
    // garde-fou, HasWarbands vaudrait encore false par défaut durant cette fenêtre et l'état vide
    // (bouton "Créer") s'afficherait brièvement avant le spinner - cf. CampaignViewModel.IsInitialized
    // dans DmTools.
    [ObservableProperty]
    private bool isInitialized;

    // Pas de CollectionView.SelectedItem/SelectionMode natif (même raison que CategoryListPage de
    // DmTools : sur Android, le fond de sélection natif reste teinté par colorAccent quel que soit le
    // style posé dessus) - IsSelected est porté par la ligne elle-même, cf. SelectionMarkerStyle.
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanModifySelectedWarband))]
    private WarbandRow? selectedRow;

    public bool CanModifySelectedWarband => SelectedRow != null;

    public WarbandListViewModel(IWarbandArchetypePickerNavigationService pickerNavigation,
        IWarbandService warbandService, IWarbandArchetypePickerService warbandArchetypePickerService,
        ILibraryService libraryService, IDetailDialogService detailDialogs, IEquipmentPickerService equipmentPickerService,
        ISkillPickerService skillPickerService, ISpellPickerService spellPickerService, IInjuryPickerService injuryPickerService,
        IMutationPickerService mutationPickerService, IHiredSwordPickerService hiredSwordPickerService,
        OfficialContentService officialContent, ExchangeService exchange)
    {
        _exchange = exchange;
        _officialContent = officialContent;
        // Fichier .mordheim ouvert depuis une autre appli pendant que l'appli tourne (voir ExchangeFiles.Receive).
        WeakReferenceMessenger.Default.Register<IncomingExchangeFileMessage>(this,
            (r, m) => { _ = Services.UiErrors.RunAsync(((WarbandListViewModel)r).ImportPendingFileAsync, nameof(WarbandListViewModel)); });
        _pickerNavigation = pickerNavigation;
        _warbandService = warbandService;
        _warbandArchetypePickerService = warbandArchetypePickerService;
        _libraryService = libraryService;
        _detailDialogs = detailDialogs;
        _equipmentPickerService = equipmentPickerService;
        _skillPickerService = skillPickerService;
        _spellPickerService = spellPickerService;
        _injuryPickerService = injuryPickerService;
        _mutationPickerService = mutationPickerService;
        _hiredSwordPickerService = hiredSwordPickerService;
    }

    partial void OnSelectedRowChanged(WarbandRow? oldValue, WarbandRow? newValue)
    {
        if (oldValue != null) oldValue.IsSelected = false;
        if (newValue != null) newValue.IsSelected = true;
    }

    [RelayCommand]
    private async Task LoadWarbandsAsync()
    {
        await Loading.RunAsync(async () =>
        {
            await InitializeAsync();
        });

        // Première page du Shell : c'est ici qu'on annonce (une seule fois) une mise à jour du contenu
        // officiel faite pendant le démarrage.
        if (await _officialContent.TakeStartupMessageAsync() is { } message)
            await ShowInfoAsync(Loc["OfficialContentTitle"], message);

        // Puis, une seule fois par version, ses nouveautés (jamais à la première installation : l'accueil
        // marque la version comme vue).
        if (await WhatsNewService.TakePendingItemsAsync() is { Count: > 0 } news)
            await ShowWhatsNewAsync(news);
    }

    private int _loadedDataVersion = -1;

    /// <summary>Rien n'a été écrit en base depuis le dernier chargement : inutile de relire la liste en
    /// revenant dessus (consultation, édition annulée...) - refonte de novembre 2026.</summary>
    public bool IsUpToDate => IsInitialized && _loadedDataVersion == _warbandService.DataVersion;

    public async Task InitializeAsync()
    {
        var version = _warbandService.DataVersion;
        var warbands = await _warbandService.GetWarbandsAsync();
        var archetypeNames = await Task.WhenAll(warbands.Select(b => _warbandService.GetWarbandArchetypeNameAsync(b.WarbandArchetypeId, LocalizationService.Instance.Language)));
        var ratings = await Task.WhenAll(warbands.Select(b => _warbandService.GetWarbandRatingAsync(b.Id)));
        Rows = new ObservableCollection<WarbandRow>(warbands.Select((w, i) => new WarbandRow(w) { ArchetypeName = archetypeNames[i], Rating = ratings[i] }));
        SelectedRow = null;
        HasWarbands = warbands.Count > 0;
        IsInitialized = true;
        _loadedDataVersion = version;
    }


    [RelayCommand]
    private void Select(WarbandRow row) => SelectedRow = row;

    // --- Import d'un fichier .mordheim (v2, lot 2) ------------------------------------------------------------

    private readonly ExchangeService _exchange;

    private static readonly FilePickerFileType ExchangeFileType = new(new Dictionary<DevicePlatform, IEnumerable<string>>
    {
        // Android ne connaît pas l'extension : tout fichier proposé, la lecture refuse ce qui n'est pas un fichier de l'appli.
        [DevicePlatform.Android] = new[] { "*/*" },
        [DevicePlatform.WinUI] = new[] { ExchangeArchive.FileExtension },
    });

    [RelayCommand]
    private async Task ImportFileAsync()
    {
        // QR code (v2, lot 3) : Android seulement - pas de webcam sous Windows (décision 1.3), le fichier .mordheim y tient
        // lieu de QR.
        if (DeviceInfo.Current.Platform == DevicePlatform.Android)
        {
            var choice = await ShowActionSheetAsync(Loc["ImportTitle"], Loc["ImportChooseFile"], Loc["ImportScanQr"]);
            if (choice is null) return;
            if (choice == Loc["ImportScanQr"] && !await ScanQrWantsFileAsync()) return;
        }
        await PickAndImportFileAsync();
    }

    private async Task PickAndImportFileAsync()
    {
        var picked = await FilePicker.Default.PickAsync(new PickOptions { PickerTitle = Loc["ImportPick"], FileTypes = ExchangeFileType });
        if (picked is null) return;
        await ImportAsync(() => ReadArchiveAsync(picked.OpenReadAsync), picked.FileName);
    }

    /// <summary>Scan d'un ou plusieurs QR, puis import. Vrai si le joueur préfère finalement choisir un fichier
    /// (bouton du pied de la page de scan).</summary>
    private async Task<bool> ScanQrWantsFileAsync()
    {
        if (await Permissions.RequestAsync<Permissions.Camera>() != PermissionStatus.Granted)
        {
            await ShowInfoAsync(Loc["QrScanTitle"], Loc["QrScanNoCamera"]);
            return false;
        }
        var result = await ShowFormAsync(new Exchange.QrScanForm(new Exchange.QrScanViewModel()));
        if (result is { PickFile: true }) return true;
        if (result?.Package is { } package) await ImportAsync(() => Task.FromResult(package), Loc["QrCodeName"]);
        return false;
    }

    private static async Task<ExchangePackage> ReadArchiveAsync(Func<Task<Stream>> open)
    {
        await using var stream = await open();
        return await ExchangeArchive.ReadAsync(stream);
    }

    /// <summary>Fichier reçu d'une autre appli (ExchangeFiles.Receive) : appelé au retour sur la liste et à l'arrivée
    /// du fichier si l'appli tournait déjà.</summary>
    public async Task ImportPendingFileAsync()
    {
        if (ExchangeFiles.TakePending() is not { } pending) return;
        try
        {
            await ImportAsync(() => ReadArchiveAsync(() => Task.FromResult<Stream>(File.OpenRead(pending.Path))), pending.Name);
        }
        finally
        {
            if (pending.IsTemporary) File.Delete(pending.Path);
        }
    }

    private async Task ImportAsync(Func<Task<ExchangePackage>> read, string fileName)
    {
        try
        {
            var package = await read();
            var preview = await _exchange.PreviewAsync(package, Loc.Language);
            if (!preview.CanImport)
            {
                // Entrée Personnalisée absente (QR d'une bande, qui voyage sans son Codex) : à partager d'abord ; sinon
                // contenu officiel d'une version plus récente de l'appli.
                var missingCustom = preview.MissingReferences.Any(r => r.Contains("|" + CatalogEntry.SharedPrefix, StringComparison.Ordinal));
                await ShowInfoAsync(Loc["ImportTitle"], missingCustom
                    ? string.Format(Loc["ImportMissingCustom"], preview.MissingReferences.Count)
                    : Loc["ImportMissingContent"]);
                return;
            }
            // Rien de nouveau ni de différent (fichier déjà importé tel quel) : pas d'écran sans choix à faire.
            if (preview.Warbands.Count == 0 && preview.Catalog.All(c => c.Status is CatalogImportStatus.Existing or CatalogImportStatus.Identical))
            {
                await ShowInfoAsync(Loc["ImportTitle"], Loc["ImportNothing"]);
                return;
            }
            if (await ShowFormAsync(new Exchange.ImportPreviewForm(new Exchange.ImportPreviewViewModel(preview, fileName))) is not { } decisions)
                return;

            ImportResult result = null!;
            await Loading.RunAsync(async () => result = await _exchange.ImportAsync(package, decisions));
            if (result.CatalogCreated + result.CatalogReplaced > 0)
                WeakReferenceMessenger.Default.Send(new CatalogImportedMessage());
            await InitializeAsync();
            await ShowInfoAsync(Loc["ImportTitle"], string.Format(Loc["ImportDone"], result.WarbandIds.Count, result.CatalogCreated, result.CatalogReplaced));
        }
        catch (ExchangeFormatException)
        {
            await ShowInfoAsync(Loc["ImportTitle"], Loc["ImportUnreadable"]);
        }
        catch (Exception ex)
        {
            await ShowErrorAsync(ex);
        }
    }

    [RelayCommand]
    private async Task CreateWarbandAsync()
    {
        // Valeurs de départ raisonnables plutôt que 0/null - purement indicatives, l'utilisateur les
        // ajuste ou les efface (MaxWarriors reste nullable, 10 n'est qu'un point de départ arbitraire).
        var newItem = new Core.Models.Warband();
        var dialogViewModel = new WarbandEditDialogViewModel(newItem, Loc["WarbandCreateTitle"],
             _warbandArchetypePickerService, _warbandService, _libraryService, _detailDialogs, _equipmentPickerService, _skillPickerService,
             _spellPickerService, _injuryPickerService, _mutationPickerService, _hiredSwordPickerService);
        if (await ShowFormAsync(new WarbandEditForm(dialogViewModel)) != true) return;

        await LoadWarbandsAsync();
    }

    // Sélection (corps de la ligne) et ouverture (zone dédiée "Jouer" en bout de ligne, cf.
    // WarbandListPage.xaml) restent deux gestes distincts, comme SceneTemplate/SelectCommand+
    // LaunchCommand dans CampaignPage de DmTools - pas de bouton "Ouvrir" dans la barre du bas.
    [RelayCommand]
    private async Task OpenWarbandAsync(WarbandRow row)
    {
        SelectedRow = row;
        await Shell.Current.GoToAsync($"{nameof(WarbandDetailPage)}?warbandId={row.Warband.Id}");
    }

    /// <summary>Rouvre la bande sélectionnée dans le même dialog que la création (WarbandEditDialogViewModel,
    /// Item.Id != 0 donc IsWizardMode false - onglets librement navigables) au lieu d'un simple prompt de
    /// renommage : équipement/compétences/sorts du roster déjà recruté (via "Roster actuel", voir
    /// EditExistingWarrior) et de nouvelles recrues sont désormais modifiables, dans l'un des deux modes
    /// (Coûts appliqués/Libre, voir IsExistingWarband). L'archétype de la bande n'est pas rechargé par le
    /// dialog lui-même (il n'a de sens à choisir qu'à la création) - on le pré-charge ici et on l'assigne
    /// directement à Archetype avant l'ouverture.</summary>
    [RelayCommand]
    private async Task EditSelectedWarbandAsync()
    {
        if (SelectedRow is not { } row) return;

        var language = LocalizationService.Instance.Language;
        WarbandArchetype? archetype = null;
        await Loading.RunAsync(async () =>
        {
            archetype = await Task.Run(() => _libraryService.GetWarbandArchetypeAsync(row.Warband.WarbandArchetypeId, language));
        });

        var dialogViewModel = new WarbandEditDialogViewModel(row.Warband, Loc["WarbandEditTitle"],
            _warbandArchetypePickerService, _warbandService, _libraryService, _detailDialogs, _equipmentPickerService, _skillPickerService,
            _spellPickerService, _injuryPickerService, _mutationPickerService, _hiredSwordPickerService)
        {
            Archetype = archetype
        };
        if (await ShowFormAsync(new WarbandEditForm(dialogViewModel)) != true) return;

        await LoadWarbandsAsync();
    }

    [RelayCommand]
    private async Task DeleteSelectedWarbandAsync()
    {
        if (SelectedRow is not { } row) return;
        if (!await ConfirmDeleteAsync(row.Name)) return;

        await Loading.RunAsync(async () =>
        {
            await _warbandService.DeleteWarbandAsync(row.Warband.Id);
            await LoadWarbandsAsync();
        });
    }
}
