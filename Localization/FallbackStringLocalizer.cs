using Microsoft.Extensions.Localization;
using System.Globalization;

namespace ForestChurches.Localization
{
    /// <summary>
    /// Fallback localizer using hardcoded translations when ResourceManager fails.
    /// This provides actual translations instead of returning key names.
    /// </summary>
    internal class FallbackStringLocalizer : IStringLocalizer
    {
        private readonly Dictionary<string, Dictionary<string, string>> _translations = new()
        {
            ["en"] = new()
            {
                ["NAV_BRAND"] = "Forest Churches",
                ["NAV_HOME"] = "Home",
                ["NAV_ABOUT"] = "About Us",
                ["NAV_WHATS_NEARBY"] = "What's Nearby",
                ["NAV_CONTACT"] = "Contact",
                ["NAV_LOGIN"] = "Login",
                ["HOMEPAGE_HERO_TITLE"] = "Welcome to Forest Churches",
                ["HOMEPAGE_HERO_SUBTITLE"] = "Connecting Communities in the Forest of Dean",
                ["FOOTER_CONTACT_LINK"] = "Contact Us",
            },
            ["th"] = new()
            {
                ["NAV_BRAND"] = "โบสถ์ป่า",
                ["NAV_HOME"] = "หน้าแรก",
                ["NAV_ABOUT"] = "เกี่ยวกับเรา",
                ["NAV_WHATS_NEARBY"] = "บริเวณใกล้เคียง",
                ["NAV_CONTACT"] = "ติดต่อ",
                ["NAV_LOGIN"] = "เข้าสู่ระบบ",
                ["HOMEPAGE_HERO_TITLE"] = "ยินดีต้อนรับสู่โบสถ์ป่า",
                ["HOMEPAGE_HERO_SUBTITLE"] = "เชื่อมต่อชุมชนในป่าดีน",
                ["FOOTER_CONTACT_LINK"] = "ติดต่อเรา",
            },
            ["fa"] = new()
            {
                ["NAV_BRAND"] = "کلیساهای جنگل",
                ["NAV_HOME"] = "خانه",
                ["NAV_ABOUT"] = "درباره ما",
                ["NAV_WHATS_NEARBY"] = "چه چیزی نزدیک است",
                ["NAV_CONTACT"] = "تماس",
                ["NAV_LOGIN"] = "ورود",
                ["HOMEPAGE_HERO_TITLE"] = "به کلیساهای جنگل خوش آمدید",
                ["HOMEPAGE_HERO_SUBTITLE"] = "اتصال جوامع در جنگل دین",
                ["FOOTER_CONTACT_LINK"] = "تماس با ما",
            },
            ["de"] = new()
            {
                ["NAV_BRAND"] = "Waldkirchen",
                ["NAV_HOME"] = "Startseite",
                ["NAV_ABOUT"] = "Über uns",
                ["NAV_WHATS_NEARBY"] = "Was ist in der Nähe",
                ["NAV_CONTACT"] = "Kontakt",
                ["NAV_LOGIN"] = "Anmelden",
                ["HOMEPAGE_HERO_TITLE"] = "Willkommen bei den Waldkirchen",
                ["HOMEPAGE_HERO_SUBTITLE"] = "Verbindung von Gemeinden im Forest of Dean",
                ["FOOTER_CONTACT_LINK"] = "Kontaktieren Sie uns",
            },
            ["fr"] = new()
            {
                ["NAV_BRAND"] = "Églises de la Forêt",
                ["NAV_HOME"] = "Accueil",
                ["NAV_ABOUT"] = "À propos de nous",
                ["NAV_WHATS_NEARBY"] = "Qu'y a-t-il à proximité",
                ["NAV_CONTACT"] = "Contact",
                ["NAV_LOGIN"] = "Connexion",
                ["HOMEPAGE_HERO_TITLE"] = "Bienvenue aux Églises de la Forêt",
                ["HOMEPAGE_HERO_SUBTITLE"] = "Connecter les communautés dans la Forest of Dean",
                ["FOOTER_CONTACT_LINK"] = "Nous contacter",
            },
            ["uk"] = new()
            {
                ["NAV_BRAND"] = "Лісові церкви",
                ["NAV_HOME"] = "Головна",
                ["NAV_ABOUT"] = "Про нас",
                ["NAV_WHATS_NEARBY"] = "Що поблизу",
                ["NAV_CONTACT"] = "Контакт",
                ["NAV_LOGIN"] = "Вхід",
                ["HOMEPAGE_HERO_TITLE"] = "Ласкаво просимо до Лісових церков",
                ["HOMEPAGE_HERO_SUBTITLE"] = "Поєднання громад у Лісі Дин",
                ["FOOTER_CONTACT_LINK"] = "Зв'яжіться з нами",
            },
        };

        public LocalizedString this[string name]
        {
            get
            {
                var culture = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName;
                if (_translations.TryGetValue(culture, out var langDict) && langDict.TryGetValue(name, out var value))
                {
                    return new LocalizedString(name, value, resourceNotFound: false);
                }
                // Fallback to English
                if (_translations["en"].TryGetValue(name, out var enValue))
                {
                    return new LocalizedString(name, enValue, resourceNotFound: false);
                }
                return new LocalizedString(name, name, resourceNotFound: true);
            }
        }

        public LocalizedString this[string name, params object[] arguments]
        {
            get
            {
                var result = this[name];
                if (!result.ResourceNotFound && arguments.Length > 0)
                {
                    return new LocalizedString(name, string.Format(result.Value, arguments), resourceNotFound: false);
                }
                return result;
            }
        }

        public IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures)
        {
            var culture = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName;
            if (_translations.TryGetValue(culture, out var langDict))
            {
                return langDict.Select(kv => new LocalizedString(kv.Key, kv.Value, resourceNotFound: false));
            }
            return _translations["en"].Select(kv => new LocalizedString(kv.Key, kv.Value, resourceNotFound: false));
        }

        public IStringLocalizer WithCulture(CultureInfo culture)
        {
            return this;
        }
    }
}
