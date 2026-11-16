using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.Models.Spt.Tables;

namespace SPTStalker.Common;

/// <summary>Adds text to every game language (locales are lazy loaded, so this goes through transformers).</summary>
[Injectable]
public class LocaleWriter(LocaleTable localeTable)
{
    /// <param name="perLanguage">language code to (locale key to text)</param>
    /// <param name="fallback">used for languages missing from <paramref name="perLanguage"/></param>
    public void Add(Dictionary<string, Dictionary<string, string>> perLanguage, Dictionary<string, string> fallback)
    {
        foreach (var (language, lazyLocale) in localeTable.Global)
        {
            var entries = perLanguage.GetValueOrDefault(language) ?? fallback;
            lazyLocale.AddTransformer(locale =>
            {
                if (locale is null)
                {
                    return locale;
                }

                foreach (var (key, text) in entries)
                {
                    locale[key] = text;
                }

                return locale;
            });
        }
    }

    public void AddForAllLanguages(Dictionary<string, string> entries) => Add([], entries);
}
