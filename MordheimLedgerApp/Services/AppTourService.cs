using MordheimLedgerApp.Components.Dialogs;
using Plugin.Maui.Onboarding;
using Plugin.Maui.Onboarding.Internals;
using Plugin.Maui.Onboarding.Views;

namespace MordheimLedgerApp.Services
{
    /// <summary>
    /// Visites guidées courtes (projecteur + bulle, cf. ThirdParty/PluginMauiOnboarding), reprises de
    /// Resonance (DmTools) : une par écran, affichée la première fois qu'on y arrive plutôt qu'en un seul
    /// parcours au démarrage, pour ne pas submerger l'utilisateur. Chacune commence par une bulle centrée
    /// qui présente l'écran dans son ensemble, puis met ses contrôles en lumière un par un. Elles ne font
    /// qu'expliquer, sans demander de créer quoi que ce soit. Chaque écran déclenche la sienne
    /// (WarbandListPage, WarbandDetailPage, LibraryPage, WarbandEditDialog - une visite par étape de
    /// l'assistant de création) ; une visite passée ou terminée ne revient plus, sauf via « Revoir »
    /// dans les Paramètres.
    /// </summary>
    public class AppTourService
    {
        // Clés des contrôles mis en lumière, posées dans le XAML via OnboardingTargetBehavior. Quand une
        // clé est portée par plusieurs éléments (gabarit de ligne ou de carte, vue réutilisée), le
        // registre du plugin retient le plus haut à l'écran sur la page courante.
        public const string WarbandRowKey = "Tour.WarbandRow";
        public const string WarbandPlayKey = "Tour.WarbandPlay";
        public const string WarbandCreateKey = "Tour.WarbandCreate";
        public const string WarbandEditKey = "Tour.WarbandEdit";
        public const string WarbandDeleteKey = "Tour.WarbandDelete";

        public const string DetailSummaryKey = "Tour.DetailSummary";
        public const string DetailGameKey = "Tour.DetailGame";
        public const string DetailInventoryKey = "Tour.DetailInventory";
        public const string DetailTabsKey = "Tour.DetailTabs";
        public const string DetailGroupKey = "Tour.DetailGroup";
        public const string DetailCardKey = "Tour.DetailCard";
        public const string DetailCardEditKey = "Tour.DetailCardEdit";
        public const string DetailCardStatsKey = "Tour.DetailCardStats";
        public const string DetailCardChipsKey = "Tour.DetailCardChips";
        public const string DetailCardXpKey = "Tour.DetailCardXp";

        public const string CodexSectionKey = "Tour.CodexSection";
        public const string CodexFilterKey = "Tour.CodexFilter";
        public const string CodexTileKey = "Tour.CodexTile";
        public const string CodexTileInfoKey = "Tour.CodexTileInfo";
        public const string CodexActionsKey = "Tour.CodexActions";

        public const string WizardStepKey = "Tour.WizardStep";
        public const string WizardNameKey = "Tour.WizardName";
        public const string WizardFreeModeKey = "Tour.WizardFreeMode";
        public const string WizardArchetypeKey = "Tour.WizardArchetype";
        public const string WizardNavKey = "Tour.WizardNav";
        public const string WizardBudgetKey = "Tour.WizardBudget";
        public const string WizardRecruitsKey = "Tour.WizardRecruits";
        public const string WizardEquipmentKey = "Tour.WizardEquipment";

        private const string WarbandsTourKey = "mordheim-warbands";
        private const string DetailTourKey = "mordheim-warband-detail";
        private const string CodexTourKey = "mordheim-codex";
        private const string WizardGeneralTourKey = "mordheim-wizard-general";
        private const string WizardWarriorsTourKey = "mordheim-wizard-warriors";
        private const string WizardEquipmentTourKey = "mordheim-wizard-equipment";

        private static readonly string[] AllTourKeys =
            [WarbandsTourKey, DetailTourKey, CodexTourKey, WizardGeneralTourKey, WizardWarriorsTourKey, WizardEquipmentTourKey];

        // Seules plateformes où le plugin sait lire la position d'un contrôle à l'écran (cf.
        // OnboardingBoundsExtensions) : ailleurs, chaque étape attendrait 10 s pour rien.
#if ANDROID || IOS || WINDOWS
        public static bool IsSupported => true;
#else
        public static bool IsSupported => false;
#endif

        private readonly OnboardingCoordinator _coordinator = new();

        private static LocalizationService Loc => LocalizationService.Instance;

        public bool IsWarbandsTourPending => IsPending(WarbandsTourKey);
        public bool IsDetailTourPending => IsPending(DetailTourKey);
        public bool IsCodexTourPending => IsPending(CodexTourKey);
        public bool IsWizardGeneralTourPending => IsPending(WizardGeneralTourKey);
        public bool IsWizardWarriorsTourPending => IsPending(WizardWarriorsTourKey);
        public bool IsWizardEquipmentTourPending => IsPending(WizardEquipmentTourKey);

        /// <summary>Fait revenir toutes les visites, chacune à la prochaine arrivée sur son écran.</summary>
        public void ResetAll()
        {
            foreach (var key in AllTourKeys)
                OnboardingCompletionStore.SetCompleted(key, false);
        }

        // Lancée seulement avec au moins une bande (cf. WarbandListPage) : sur une liste vide, l'état vide
        // dit déjà quoi faire, et la visite attend la première bande.
        public Task StartWarbandsTourAsync() => StartAsync(OnboardingTourBuilder.Create(WarbandsTourKey)
            .AddStep(Intro("TourWarbandsIntroTitle", "TourWarbandsIntroBody"))
            .AddStep(Step(WarbandRowKey, "TourWarbandRowTitle", "TourWarbandRowBody"))
            .AddStep(Step(WarbandPlayKey, "TourWarbandPlayTitle", "TourWarbandPlayBody"))
            .AddStep(Step(WarbandCreateKey, "TourWarbandCreateTitle", "TourWarbandCreateBody"))
            .AddStep(Step(WarbandEditKey, "TourWarbandEditTitle", "TourWarbandEditBody"))
            .AddStep(Step(WarbandDeleteKey, "TourWarbandDeleteTitle", "TourWarbandDeleteBody"))
            .Build());

        /// <param name="hasHeroes">Faux sur une bande sans héros vivant : les étapes sur la carte d'un
        /// guerrier sont alors sautées (le plugin attendrait sinon 10 s une cible absente).</param>
        /// <param name="hasInventory">Le bouton Réserve n'apparaît qu'une fois un objet mis de côté.</param>
        public Task StartDetailTourAsync(bool hasHeroes, bool hasInventory)
        {
            var builder = OnboardingTourBuilder.Create(DetailTourKey)
                .AddStep(Intro("TourDetailIntroTitle", "TourDetailIntroBody"))
                .AddStep(Step(DetailSummaryKey, "TourDetailSummaryTitle", "TourDetailSummaryBody"))
                .AddStep(Step(DetailGameKey, "TourDetailGameTitle", "TourDetailGameBody"));
            if (hasInventory)
                builder.AddStep(Step(DetailInventoryKey, "TourDetailInventoryTitle", "TourDetailInventoryBody"));
            builder.AddStep(Step(DetailTabsKey, "TourDetailTabsTitle", "TourDetailTabsBody"))
                .AddStep(Step(DetailGroupKey, "TourDetailGroupTitle", "TourDetailGroupBody"));
            if (hasHeroes)
            {
                builder.AddStep(Step(DetailCardKey, "TourDetailCardTitle", "TourDetailCardBody"))
                    .AddStep(Step(DetailCardEditKey, "TourDetailCardEditTitle", "TourDetailCardEditBody"))
                    .AddStep(Step(DetailCardStatsKey, "TourDetailCardStatsTitle", "TourDetailCardStatsBody"))
                    .AddStep(Step(DetailCardChipsKey, "TourDetailCardChipsTitle", "TourDetailCardChipsBody"))
                    .AddStep(Step(DetailCardXpKey, "TourDetailCardXpTitle", "TourDetailCardXpBody"));
            }

            return StartAsync(builder.Build());
        }

        /// <param name="hasTiles">Faux tant que les types de bande ne sont pas chargés : les étapes sur
        /// une tuile sont alors sautées.</param>
        public Task StartCodexTourAsync(bool hasTiles)
        {
            var builder = OnboardingTourBuilder.Create(CodexTourKey)
                .AddStep(Intro("TourCodexIntroTitle", "TourCodexIntroBody"))
                .AddStep(Step(CodexSectionKey, "TourCodexSectionTitle", "TourCodexSectionBody"))
                .AddStep(Step(CodexFilterKey, "TourCodexFilterTitle", "TourCodexFilterBody"));
            if (hasTiles)
            {
                builder.AddStep(Step(CodexTileKey, "TourCodexTileTitle", "TourCodexTileBody"))
                    .AddStep(Step(CodexTileInfoKey, "TourCodexTileInfoTitle", "TourCodexTileInfoBody"));
            }

            return StartAsync(builder
                .AddStep(Step(CodexActionsKey, "TourCodexActionsTitle", "TourCodexActionsBody"))
                .Build());
        }

        public Task StartWizardGeneralTourAsync() => StartAsync(OnboardingTourBuilder.Create(WizardGeneralTourKey)
            .AddStep(Intro("TourWizardIntroTitle", "TourWizardIntroBody"))
            .AddStep(Step(WizardStepKey, "TourWizardStepTitle", "TourWizardStepBody"))
            .AddStep(Step(WizardNameKey, "TourWizardNameTitle", "TourWizardNameBody"))
            .AddStep(Step(WizardArchetypeKey, "TourWizardArchetypeTitle", "TourWizardArchetypeBody"))
            .AddStep(Step(WizardFreeModeKey, "TourWizardFreeModeTitle", "TourWizardFreeModeBody"))
            .AddStep(Step(WizardNavKey, "TourWizardNavTitle", "TourWizardNavBody"))
            .Build());

        public Task StartWizardWarriorsTourAsync() => StartAsync(OnboardingTourBuilder.Create(WizardWarriorsTourKey)
            .AddStep(Step(WizardBudgetKey, "TourWizardBudgetTitle", "TourWizardBudgetBody"))
            .AddStep(Step(WizardRecruitsKey, "TourWizardRecruitsTitle", "TourWizardRecruitsBody"))
            .Build());

        public Task StartWizardEquipmentTourAsync() => StartAsync(OnboardingTourBuilder.Create(WizardEquipmentTourKey)
            .AddStep(Step(WizardEquipmentKey, "TourWizardEquipmentTitle", "TourWizardEquipmentBody"))
            .Build());

        private static bool IsPending(string tourKey) => IsSupported && !OnboardingCompletionStore.IsCompleted(tourKey);

        private static OnboardingStep Step(string targetKey, string titleKey, string bodyKey) => new()
        {
            TargetKey = targetKey,
            Title = Loc[titleKey],
            Description = Loc[bodyKey]
        };

        // Bulle centrée, sans projecteur : présente l'écran avant d'en détailler les contrôles.
        private static OnboardingStep Intro(string titleKey, string bodyKey) => Step(string.Empty, titleKey, bodyKey);

        // Sans effet si une autre visite est déjà en cours : celle-ci reste en attente et reviendra à la
        // prochaine arrivée sur son écran.
        private async Task StartAsync(OnboardingTour tour)
        {
            if (!IsSupported) return;

            // L'overlay est lui-même une page modale : attendre qu'aucun dialog ne soit en train d'être
            // empilé/dépilé (cf. DialogNavigationGate) avant de le pousser par-dessus.
            await DialogNavigationGate.RunAsync(() => Task.CompletedTask, "AppTourService.Start");

            OnboardingTooltipCard.NextLabel = Loc["TourNext"];
            OnboardingTooltipCard.DoneLabel = Loc["TourDone"];
            OnboardingTooltipCard.SkipLabel = Loc["TourSkip"];
            ApplyThemeColors();

            await _coordinator.StartTourIfNotCompletedAsync(tour);
        }

        // Relu à chaque lancement : la palette a pu changer entre deux visites. La bulle et la lueur
        // prennent la surface et l'accent de la palette ; le voile, lui, reste noir quel que soit le thème.
        private void ApplyThemeColors()
        {
            var resources = Application.Current!.Resources;
            var accent = (Color)resources["AppAccent"];

            _coordinator.TooltipBackgroundColor = (Color)resources["AppSurface"];
            _coordinator.TooltipBorderColor = accent;
            _coordinator.GlowColor = accent.WithAlpha(0.8f);
            _coordinator.ScrimColor = Colors.Black.WithAlpha(0.7f);
        }
    }
}
