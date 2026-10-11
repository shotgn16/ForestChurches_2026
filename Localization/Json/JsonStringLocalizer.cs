using Microsoft.Extensions.Localization;
using System.Globalization;
using System.Text.Json;

namespace ForestChurches.Localization.Json
{
    internal class JsonStringLocalizer : IStringLocalizer
    {
        private readonly string _resourceName;
        private readonly string _contentRoot;

        // cache: culture -> dictionary
        private readonly Dictionary<string, Dictionary<string, string>> _cache = new();

        public JsonStringLocalizer(string resourceName, string contentRoot)
        {
            _resourceName = resourceName;
            _contentRoot = contentRoot;
        }

        private Dictionary<string, string> LoadCulture(CultureInfo culture)
        {
            var cultureKey = culture.Name;
            if (_cache.TryGetValue(cultureKey, out var dict)) return dict;

            dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            // Try file name patterns: Resources/{resource}.{culture}.json, Resources/{resource}.{twoLetter}.json, Resources/{resource}.json
            var resourcesFolder = Path.Combine(_contentRoot, "Resources");
            var candidates = new[] {
                Path.Combine(resourcesFolder, $"{_resourceName}.{culture.Name}.json"),
                Path.Combine(resourcesFolder, $"{_resourceName}.{culture.TwoLetterISOLanguageName}.json"),
                Path.Combine(resourcesFolder, $"{_resourceName}.json")
            };

            foreach (var file in candidates)
            {
                if (!File.Exists(file)) continue;
                try
                {
                    var json = File.ReadAllText(file);
                    var parsed = JsonSerializer.Deserialize<Dictionary<string, string>>(json);
                    if (parsed == null) continue;
                    foreach (var kv in parsed)
                    {
                        if (!dict.ContainsKey(kv.Key)) dict[kv.Key] = kv.Value;
                    }
                }
                catch
                {
                    // ignore malformed files
                }
            }

            _cache[cultureKey] = dict;
            return dict;
        }

        private string? Lookup(string name, CultureInfo culture)
        {
            // try full culture then neutral then invariant
            var culturesToTry = new List<CultureInfo>();
            var cur = culture;
            while (cur != CultureInfo.InvariantCulture)
            {
                culturesToTry.Add(cur);
                if (string.IsNullOrEmpty(cur.Name)) break;
                cur = cur.Parent;
            }
            culturesToTry.Add(CultureInfo.InvariantCulture);

            foreach (var c in culturesToTry)
            {
                var dict = LoadCulture(c);
                if (dict != null && dict.TryGetValue(name, out var val))
                {
                    return val;
                }
            }

            return null;
        }

        public LocalizedString this[string name]
        {
            get
            {
                var val = Lookup(name, CultureInfo.CurrentUICulture) ?? name;
                var notFound = val == name;
                return new LocalizedString(name, val, resourceNotFound: notFound);
            }
        }

        public LocalizedString this[string name, params object[] arguments]
        {
            get
            {
                var format = Lookup(name, CultureInfo.CurrentUICulture) ?? name;
                var value = string.Format(format, arguments);
                var notFound = format == name;
                return new LocalizedString(name, value, resourceNotFound: notFound);
            }
        }

        public IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures)
        {
            var dict = LoadCulture(CultureInfo.CurrentUICulture);
            foreach (var kv in dict)
            {
                yield return new LocalizedString(kv.Key, kv.Value, resourceNotFound: false);
            }
        }

        public IStringLocalizer WithCulture(CultureInfo culture)
        {
            // this implementation ignores culture scoping
            return new JsonStringLocalizer(_resourceName, _contentRoot);
        }
    }
}
