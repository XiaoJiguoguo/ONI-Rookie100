using System;
using System.Text.RegularExpressions;

namespace Rookie100.UI
{
    public static class LocalizedUiText
    {
        // Strings.Get returns a StringEntry in the game, not a System.String.
        public static string FromEntry(object entry, string key)
        {
            string value = Convert.ToString(entry);
            if (string.IsNullOrWhiteSpace(value) || value.StartsWith("MISSING", StringComparison.OrdinalIgnoreCase) || value == key)
                return null;
            return Regex.Replace(value, "</?link[^>]*>", "");
        }
    }
}
