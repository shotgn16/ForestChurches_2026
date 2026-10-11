JSON localization: place files in the Resources folder named e.g.

SharedResources.json
SharedResources.en.json
SharedResources.fr.json

Keys are simple string keys with string values. The JsonStringLocalizer falls back to parent cultures and then to the invariant file.