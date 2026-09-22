using System.Globalization;
using MordheimLedgerApp.Features.Warbands.EndOfGame;
using MordheimLedgerApp.Features.Warbands.EndOfGame.Steps;

namespace MordheimLedgerApp.Converters
{
    /// <summary>Choisit la vue de l'étape courante du wizard Fin de Partie (2026-09-22, voir
    /// EndOfGamePageViewModel.CurrentStepKind's own doc) - une instance fraîche à chaque changement
    /// d'étape, jamais mise en cache : chaque étape ne garde son état que dans EndOfGamePageViewModel
    /// (source unique partagée par toutes les étapes), rien à préserver côté vue entre deux passages sur
    /// la même étape. Toutes les StepKind sont couvertes - EndOfGamePage.xaml ne garde plus aucun bloc
    /// IsXxxStep historique.</summary>
    public sealed class StepViewConverter : IValueConverter
    {
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
            value is EndOfGamePageViewModel.StepKind kind ? kind switch
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
            } : null;

        public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
            throw new NotSupportedException();
    }
}
