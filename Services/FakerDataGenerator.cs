using Bogus;

namespace DictoriumDemo.Services;

public static class FakerDataGenerator
{
    private static readonly Faker _faker = new Faker("en");

    /// <summary>
    /// Generates up to <paramref name="count"/> unique key-value pairs using Bogus.
    /// Keys  — English words (commerce product names, adjectives, etc.)
    /// Values — Russian-transliterated or English descriptions
    /// Max count is clamped to 500 to protect the browser.
    /// </summary>
    public static List<(string Key, string Value)> Generate(int count)
    {
        count = Math.Clamp(count, 1, 500);

        var seen   = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var result = new List<(string, string)>(count);
        int attempts = 0;

        while (result.Count < count && attempts < count * 5)
        {
            attempts++;
            string key = _faker.Commerce.ProductAdjective().ToLower().Replace(" ", "_")
                         + "_" + _faker.Commerce.Product().ToLower().Replace(" ", "_");
            if (!seen.Add(key)) continue;

            string value = _faker.Commerce.ProductName();
            result.Add((key, value));
        }

        return result;
    }
}
