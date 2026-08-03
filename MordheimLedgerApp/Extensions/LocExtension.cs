using CommunityToolkit.Mvvm.Messaging;
using MordheimLedgerApp.Services;
using System.ComponentModel;

namespace MordheimLedgerApp.Extensions
{
    [ContentProperty(nameof(Key))]
    public class LocExtension : IMarkupExtension<BindingBase>
    {
        public string Key { get; set; } = string.Empty;

        public BindingBase ProvideValue(IServiceProvider serviceProvider)
            => new Binding
            {
                Mode = BindingMode.OneWay,
                Path = nameof(LocalizedString.Value),
                Source = new LocalizedString(Key)
            };

        object IMarkupExtension.ProvideValue(IServiceProvider serviceProvider)
            => ProvideValue(serviceProvider);
    }

    public class LocalizedString : INotifyPropertyChanged
    {
        private readonly string _key;
        public event PropertyChangedEventHandler? PropertyChanged;

        public LocalizedString(string key)
        {
            _key = key;

            // A LocalizedString is created on every {loc:Loc ...} binding — potentially dozens per
            // page. A plain subscription on this singleton would leak forever; WeakReferenceMessenger
            // only holds a weak reference, so orphaned instances (page closed) remain GC-able.
            WeakReferenceMessenger.Default.Register<LanguageChangedMessage>(this,
                (r, m) => ((LocalizedString)r).OnLanguageChanged());
        }

        private void OnLanguageChanged()
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Value)));

        public string Value => LocalizationService.Instance[_key];
    }
}
