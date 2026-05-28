using System.Threading.Tasks;

namespace MindKeeper.Services
{
    public interface IAiService
    {
        Task<string> GenerateTagsAsync(string text);
        Task<string> GenerateSummaryAsync(string text);
    }
}