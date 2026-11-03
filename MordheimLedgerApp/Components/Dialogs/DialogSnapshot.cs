using System.Collections;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using MordheimLedgerApp.Core.Models;
using MordheimLedgerApp.Core.Models.Library;

namespace MordheimLedgerApp.Components.Dialogs;

/// <summary>Sérialisation des instantanés "modifications non enregistrées" (voir
/// DialogViewModel.EditableState), partagée par tous les dialogs - un seul JsonSerializerOptions, donc un
/// seul cache de métadonnées, plutôt qu'un par type de résultat de DialogViewModel&lt;TResult&gt;.</summary>
internal static class DialogSnapshot
{
    internal static readonly JsonSerializerOptions Options = new()
    {
        // DramatisPersona.PairedWithDramatisPersona est auto-référent, et les modèles se croisent
        // (objet -> règles spéciales...) - on veut les valeurs, pas un graphe fidèle.
        ReferenceHandler = ReferenceHandler.IgnoreCycles
    };

    /// <summary>La première sérialisation d'un graphe de modèles coûte ~1,5 s sur Android (métadonnées
    /// System.Text.Json + compilation à la volée), payée jusque-là sur le thread UI à l'ouverture du premier
    /// dialog d'édition (fiche guerrier en tête, 2026-11-03). Sérialise une fois, hors du thread UI, un
    /// exemplaire rempli de chaque modèle édité par un dialog, peu après le démarrage.</summary>
    public static void WarmUpInBackground() => Task.Run(async () =>
    {
        await Task.Delay(TimeSpan.FromSeconds(2));
        var roots = typeof(EquipmentItem).Assembly.GetTypes()
            .Where(t => t.IsClass && t.IsPublic && !t.IsAbstract && t.Namespace == typeof(EquipmentItem).Namespace)
            .Append(typeof(Warrior)).Append(typeof(Warband));
        foreach (var root in roots)
        {
            try
            {
                if (Sample(root, 0) is { } sample)
                    JsonSerializer.Serialize(new object?[] { sample, string.Empty, Enumerable.Empty<int>().Select(i => i) }, Options);
            }
            catch (Exception)
            {
                // Préchauffage seulement : un modèle qui ne se sérialise pas sera traité normalement
                // (voir DialogViewModel.SerializeEditableState).
            }
        }
    });

    /// <summary>Instance du type avec chaque objet/liste imbriqué rempli d'un élément, pour que la
    /// sérialisation parcoure tous les types atteignables.</summary>
    private static object? Sample(Type type, int depth)
    {
        if (type == typeof(string) || type.IsValueType || depth > 4) return null;
        if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(List<>))
        {
            var list = (IList)Activator.CreateInstance(type)!;
            var elementType = type.GetGenericArguments()[0];
            var element = elementType.IsValueType ? Activator.CreateInstance(elementType) : Sample(elementType, depth + 1);
            if (element is not null) list.Add(element);
            return list;
        }
        if (type.IsAbstract || type.GetConstructor(Type.EmptyTypes) is null) return null;

        var instance = Activator.CreateInstance(type)!;
        foreach (var property in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            if (property.SetMethod is not { IsPublic: true } || property.GetIndexParameters().Length > 0) continue;
            if (Sample(property.PropertyType, depth + 1) is { } value) property.SetValue(instance, value);
        }
        return instance;
    }
}
