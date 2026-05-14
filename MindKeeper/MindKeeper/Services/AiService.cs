using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace MindKeeper.Services
{
    public static class AiService
    {
        private static readonly HashSet<string> StopWords = new HashSet<string>
        {
            "и", "в", "на", "с", "к", "у", "за", "по", "из", "о", "для", "это", "то",
            "как", "так", "все", "было", "но", "а", "да", "нет", "или", "же", "вот",
            "только", "еще", "уже", "очень", "можно", "что", "кто", "этот", "тот",
            "такой", "там", "тут", "здесь", "тогда", "теперь", "всегда", "никогда",
            "быть", "стать", "являться", "иметь", "делать", "сказать", "мочь", "знать"
        };

        public static List<string> ExtractKeywords(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return new List<string>();

            var words = text.ToLower()
                .Split(new[] { ' ', '.', ',', ';', ':', '\n', '\r', '\t', '!', '?' }, StringSplitOptions.RemoveEmptyEntries)
                .Where(w => w.Length > 3 && !StopWords.Contains(w))
                .ToList();

            var keywordGroups = words.GroupBy(w => w)
                                     .OrderByDescending(g => g.Count())
                                     .Take(5)
                                     .Select(g => g.Key)
                                     .ToList();
            return keywordGroups;
        }

        public static (List<string> dates, List<string> emails, List<string> phones, List<string> urls) ExtractEntities(string text)
        {
            var dates = new List<string>();
            var emails = new List<string>();
            var phones = new List<string>();
            var urls = new List<string>();

            if (string.IsNullOrWhiteSpace(text)) return (dates, emails, phones, urls);

            var dateRegex = new Regex(@"\b\d{2}\.\d{2}\.\d{4}\b|\b\d{4}-\d{2}-\d{2}\b");
            dates.AddRange(dateRegex.Matches(text).Cast<Match>().Select(m => m.Value));

            var emailRegex = new Regex(@"[\w\.-]+@[\w\.-]+\.\w+");
            emails.AddRange(emailRegex.Matches(text).Cast<Match>().Select(m => m.Value));

            var phoneRegex = new Regex(@"\+?\d[\d\s\-\(\)]{7,}\d");
            phones.AddRange(phoneRegex.Matches(text).Cast<Match>().Select(m => m.Value));

            var urlRegex = new Regex(@"(https?://)?([\w\.-]+)\.([a-z\.]{2,6})(/\S*)?");
            urls.AddRange(urlRegex.Matches(text).Cast<Match>().Select(m => m.Value));

            return (dates.Distinct().ToList(), emails.Distinct().ToList(), phones.Distinct().ToList(), urls.Distinct().ToList());
        }

        public static string GenerateSimpleSummary(string content, int maxSentences = 2)
        {
            if (string.IsNullOrWhiteSpace(content)) return "";
            var sentences = content.Split(new[] { '.', '!', '?' }, StringSplitOptions.RemoveEmptyEntries);
            if (sentences.Length == 0) return "";
            var taken = sentences.Take(maxSentences);
            return string.Join(". ", taken.Select(s => s.Trim())) + ".";
        }
    }
}