using MordheimLedgerApp.Core.Models;
using MordheimLedgerApp.Core.Services;

namespace MordheimLedgerApp.Services
{
    /// <summary>
    /// Charge la bande d'exemple (Core.Services.SampleWarband) dans la langue courante, depuis la fin de
    /// l'accueil ou les Réglages. Se souvient d'un premier chargement pour que les deux points d'entrée
    /// puissent prévenir avant d'en créer une seconde copie - recharger crée toujours une nouvelle bande.
    /// </summary>
    public class SampleWarbandService
    {
        private const string ImportedKey = "sample_warband_imported";

        private readonly IWarbandService _warbandService;
        private readonly ILibraryService _libraryService;

        public SampleWarbandService(IWarbandService warbandService, ILibraryService libraryService)
        {
            _warbandService = warbandService;
            _libraryService = libraryService;
        }

        public bool HasImportedSample => Preferences.Default.Get(ImportedKey, false);

        /// <exception cref="InvalidOperationException">La bibliothèque n'a plus les Mercenaires Reiklander
        /// officiels (ou l'un de leurs guerriers/objets) : rien n'est créé.</exception>
        public async Task<Warband> ImportAsync()
        {
            var loc = LocalizationService.Instance;
            var warband = await SampleWarband.CreateAsync(_warbandService, _libraryService, loc.Language,
                loc["SampleWarbandName"], [loc["SampleWarbandWarriors"], loc["SampleWarbandMarksmen"]])
                ?? throw new InvalidOperationException(loc["SampleWarbandMissingContent"]);

            Preferences.Default.Set(ImportedKey, true);
            return warband;
        }
    }
}
