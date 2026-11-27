using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MordheimLedgerApp.Components.Dialogs;
using MordheimLedgerApp.Core.Data;
using MordheimLedgerApp.Core.Models.Feedback;
using MordheimLedgerApp.Core.Rules;
using MordheimLedgerApp.Core.Services;
using MordheimLedgerApp.Services;

namespace MordheimLedgerApp.Features.Feedback;

/// <summary>Un type de message proposé en haut du formulaire (puce radio).</summary>
public partial class FeedbackKindOption(FeedbackKind kind, string title, string description) : ObservableObject
{
    public FeedbackKind Kind { get; } = kind;
    public string Title { get; } = title;
    public string Description { get; } = description;

    [ObservableProperty]
    private bool _isSelected;
}

/// <summary>Capture d'écran jointe, avec sa miniature.</summary>
public sealed class FeedbackScreenshot(FeedbackAttachment attachment)
{
    public FeedbackAttachment Attachment { get; } = attachment;
    public ImageSource Image { get; } = ImageSource.FromStream(() => new MemoryStream(Convert.FromBase64String(attachment.Base64)));
}

/// <summary>Formulaire « Nous écrire » (maquette mockups/report-issue.html) : sans compte, le message part au relais
/// qui en fait un ticket dans le dépôt privé de support. Résultat : true si le message est parti ou mis en attente.</summary>
public partial class FeedbackFormViewModel : DialogViewModel<bool>
{
    private readonly AppDatabase _db;
    private FeedbackTechnicalInfo? _technical;

    public FeedbackFormViewModel(AppDatabase db)
    {
        _db = db;
        Kinds =
        [
            new(FeedbackKind.Bug, Loc["FeedbackKindBug"], Loc["FeedbackKindBugDesc"]),
            new(FeedbackKind.Content, Loc["FeedbackKindContent"], Loc["FeedbackKindContentDesc"]),
            new(FeedbackKind.Translation, Loc["FeedbackKindTranslation"], Loc["FeedbackKindTranslationDesc"]),
            new(FeedbackKind.Idea, Loc["FeedbackKindIdea"], Loc["FeedbackKindIdeaDesc"]),
            new(FeedbackKind.Other, Loc["FeedbackKindOther"], Loc["FeedbackKindOtherDesc"])
        ];
        Kinds[0].IsSelected = true;
    }

    protected override bool CancelResult => false;

    protected override object? EditableState => new { Kind, Summary, Details, Where, Email, Screenshots = Screenshots.Count };

    public IReadOnlyList<FeedbackKindOption> Kinds { get; }

    public string Subtitle => $"Mordheim Ledger {AppInfo.Current.VersionString}";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowWhere), nameof(ShowTranslationNote), nameof(DetailsLabel), nameof(DetailsHint), nameof(HasDetailsHint))]
    private FeedbackKind _kind = FeedbackKind.Bug;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SendCommand))]
    private string _summary = string.Empty;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SendCommand))]
    private string _details = string.Empty;

    [ObservableProperty]
    private string _where = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowEmailError))]
    private string _email = string.Empty;

    [ObservableProperty]
    private bool _attachTechnical = true;

    [ObservableProperty]
    private bool _showTechnical;

    [ObservableProperty]
    private string _technicalPreview = string.Empty;

    public ObservableCollection<FeedbackScreenshot> Screenshots { get; } = [];

    public bool CanAddScreenshot => Screenshots.Count < FeedbackRules.MaxScreenshots;

    public bool ShowWhere => FeedbackRules.AsksWhere(Kind);
    public bool ShowTranslationNote => Kind == FeedbackKind.Translation;
    public bool ShowEmailError => !FeedbackRules.IsValidEmail(Email);
    public int MaxSummaryLength => FeedbackRules.MaxSummaryLength;
    public int MaxDetailsLength => FeedbackRules.MaxDetailsLength;
    public int MaxWhereLength => FeedbackRules.MaxWhereLength;

    public string DetailsLabel => Loc[Kind switch
    {
        FeedbackKind.Bug => "FeedbackDetailsBug",
        FeedbackKind.Content => "FeedbackDetailsContent",
        FeedbackKind.Translation => "FeedbackDetailsTranslation",
        FeedbackKind.Idea => "FeedbackDetailsIdea",
        _ => "FeedbackDetailsOther"
    }];

    public string DetailsHint => Kind switch
    {
        FeedbackKind.Bug => Loc["FeedbackDetailsBugHint"],
        FeedbackKind.Content => Loc["FeedbackDetailsContentHint"],
        FeedbackKind.Translation => Loc["FeedbackDetailsTranslationHint"],
        FeedbackKind.Idea => Loc["FeedbackDetailsIdeaHint"],
        _ => string.Empty
    };

    public bool HasDetailsHint => !string.IsNullOrEmpty(DetailsHint);

    [RelayCommand]
    private void SelectKind(FeedbackKindOption option)
    {
        foreach (var k in Kinds) k.IsSelected = ReferenceEquals(k, option);
        Kind = option.Kind;
    }

    [RelayCommand]
    private async Task ToggleTechnical()
    {
        if (!ShowTechnical)
        {
            _technical ??= await FeedbackService.BuildTechnicalInfoAsync(_db);
            TechnicalPreview = FeedbackService.Describe(_technical);
        }
        ShowTechnical = !ShowTechnical;
    }

    partial void OnAttachTechnicalChanged(bool value)
    {
        if (!value) ShowTechnical = false;
    }

    [RelayCommand]
    private async Task AddScreenshot()
    {
        try
        {
            var picked = await FeedbackService.PickScreenshotsAsync(FeedbackRules.MaxScreenshots - Screenshots.Count);
            foreach (var attachment in picked) Screenshots.Add(new FeedbackScreenshot(attachment));
            OnPropertyChanged(nameof(CanAddScreenshot));
        }
        catch (Exception ex)
        {
            await ShowErrorAsync(ex);
        }
    }

    [RelayCommand]
    private void RemoveScreenshot(FeedbackScreenshot screenshot)
    {
        Screenshots.Remove(screenshot);
        OnPropertyChanged(nameof(CanAddScreenshot));
    }

    private bool CanSend() => FeedbackRules.CanSend(Summary, Details);

    [RelayCommand(CanExecute = nameof(CanSend))]
    private async Task Send()
    {
        if (ShowEmailError)
        {
            await ShowInfoAsync(Loc["FeedbackEmailInvalidTitle"], Loc["FeedbackEmailInvalid"]);
            return;
        }

        var result = FeedbackSendResult.Rejected;
        await Loading.RunAsync(async () =>
        {
            var report = FeedbackRules.Normalize(new FeedbackReport
            {
                Kind = Kind,
                Summary = Summary,
                Details = Details,
                Where = Where,
                Email = Email,
                Language = LocalizationService.Instance.Culture.TwoLetterISOLanguageName,
                Technical = AttachTechnical ? _technical ?? await FeedbackService.BuildTechnicalInfoAsync(_db) : null,
                Screenshots = Screenshots.Select(s => s.Attachment).ToList()
            });
            result = await FeedbackService.SendAsync(report);
        });

        switch (result)
        {
            case FeedbackSendResult.Sent:
                await ShowInfoAsync(Loc["FeedbackSentTitle"], Loc["FeedbackSent"]);
                Close(true);
                break;
            case FeedbackSendResult.Retry:
                await ShowInfoAsync(Loc["FeedbackQueuedTitle"], Loc["FeedbackQueued"]);
                Close(true);
                break;
            case FeedbackSendResult.RateLimited:
                await ShowInfoAsync(Loc["FeedbackRateLimitedTitle"], Loc["FeedbackRateLimited"]);
                Close(true);
                break;
            default:
                // Refusé : le texte reste dans le formulaire pour que le joueur le corrige ou le copie.
                await ShowInfoAsync(Loc["FeedbackRejectedTitle"], Loc["FeedbackRejected"]);
                break;
        }
    }
}
