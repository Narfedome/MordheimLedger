using System.Globalization;
using MordheimLedgerApp.Features.Warbands.EndOfGame;
using MordheimLedgerApp.Features.Warbands.EndOfGame.Steps;

namespace MordheimLedgerApp.Converters
{
    /// <summary>Choisit la vue de l'étape courante du wizard Fin de Partie (2026-09-22, voir
    /// EndOfGamePageViewModel.CurrentStepKind's own doc) - une instance PAR StepKind, créée une seule
    /// fois puis réutilisée (_cache), pas reconstruite à chaque conversion. Revenu sur le choix initial
    /// "jamais de cache" (retour utilisateur 2026-09-22 - le Picker Résultat "apparaît une demi seconde
    /// puis disparaît") : EndOfGamePageViewModel.InitializeAsync termine par un
    /// OnPropertyChanged(string.Empty) (rafraîchissement générique, nécessaire pour d'autres propriétés -
    /// voir sa doc), qui refait relire CurrentStepKind par ce convertisseur MÊME QUAND l'étape n'a pas
    /// changé (Résultat, toujours l'étape 0, encore active à ce moment-là) - sans cache, ce second appel
    /// construisait une DEUXIÈME instance de ResultStepView, dont le Picker n'a alors plus le temps de se
    /// stabiliser avant d'être affiché (masquant/perdant la sélection déjà correcte de la première
    /// instance). Avec le cache, ce second appel renvoie la MÊME instance (déjà correctement affichée),
    /// donc ContentView.Content ne change même pas de référence - rien à reconstruire. Chaque étape ne
    /// garde son état que dans EndOfGamePageViewModel (source unique partagée par toutes les étapes) -
    /// réutiliser l'instance de vue ne change rien à ça, juste une économie de reconstruction.</summary>
    public sealed class StepViewConverter : IValueConverter
    {
        private readonly Dictionary<EndOfGamePageViewModel.StepKind, ContentView> _cache = new();

        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is not EndOfGamePageViewModel.StepKind kind) return null;
            if (_cache.TryGetValue(kind, out var cached)) return cached;

            ContentView? view = kind switch
            {
                EndOfGamePageViewModel.StepKind.Result => new ResultStepView(),
                EndOfGamePageViewModel.StepKind.OutOfAction => new OutOfActionStepView(),
                EndOfGamePageViewModel.StepKind.Injury => new InjuryStepView(),
                EndOfGamePageViewModel.StepKind.PitFight => new PitFightStepView(),
                EndOfGamePageViewModel.StepKind.Captives => new CaptivesStepView(),
                EndOfGamePageViewModel.StepKind.Experience => new ExperienceStepView(),
                EndOfGamePageViewModel.StepKind.Advance => new AdvanceStepView(),
                EndOfGamePageViewModel.StepKind.ExplorationRoll => new ExplorationRollStepView(),
                EndOfGamePageViewModel.StepKind.ExplorationResult => new ExplorationResultStepView(),
                EndOfGamePageViewModel.StepKind.WyrdstoneSale => new WyrdstoneSaleStepView(),
                EndOfGamePageViewModel.StepKind.AvailableVeterans => new AvailableVeteransStepView(),
                EndOfGamePageViewModel.StepKind.RareItems => new RareItemsStepView(),
                EndOfGamePageViewModel.StepKind.RareItemPurchase => new RareItemPurchaseStepView(),
                EndOfGamePageViewModel.StepKind.HiredSwords => new HiredSwordsStepView(),
                EndOfGamePageViewModel.StepKind.DramatisPersonae => new DramatisPersonaeStepView(),
                EndOfGamePageViewModel.StepKind.PairEngagement => new PairEngagementStepView(),
                EndOfGamePageViewModel.StepKind.PairDuel => new PairDuelStepView(),
                EndOfGamePageViewModel.StepKind.DismissWarriors => new DismissWarriorsStepView(),
                EndOfGamePageViewModel.StepKind.EquipmentTrading => new EquipmentTradingStepView(),
                EndOfGamePageViewModel.StepKind.RecruitHeroesCount => new RecruitHeroesCountStepView(),
                EndOfGamePageViewModel.StepKind.RecruitHeroDetail => new RecruitHeroDetailStepView(),
                EndOfGamePageViewModel.StepKind.RecruitVeteranTopUp => new RecruitVeteranTopUpStepView(),
                EndOfGamePageViewModel.StepKind.RecruitHenchmenCount => new RecruitHenchmenCountStepView(),
                EndOfGamePageViewModel.StepKind.RecruitHenchmenEquipment => new RecruitHenchmenEquipmentStepView(),
                EndOfGamePageViewModel.StepKind.RecruitHenchmenNames => new RecruitHenchmenNamesStepView(),
                EndOfGamePageViewModel.StepKind.EquipmentReallocation => new EquipmentReallocationStepView(),
                EndOfGamePageViewModel.StepKind.Recap => new RecapStepView(),
                _ => null
            };

            if (view is not null) _cache[kind] = view;
            return view;
        }

        public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
            throw new NotSupportedException();
    }
}
