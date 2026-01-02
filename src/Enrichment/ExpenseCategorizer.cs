using Microsoft.Extensions.Logging;
using OpenAI.Chat;

namespace Enrichment;

public class ExpenseCategorizer(ChatClient chatClient, ILogger<ExpenseCategorizer> logger)
{
    public async Task<string> CategorizeMerchant(string merchant)
    {
        try
        {
            List<ChatMessage> messages = new List<ChatMessage>()
            {
                new SystemChatMessage("""
                                      You are a assistant that organizes expenses into the following categories:
                                          - Bills,
                                          - Groceries,
                                          - Entertainment,
                                          - Pet Care,
                                          - Vehicle Fuel & Maintenance,
                                          - Restaurants & Bars,
                                          - Shopping

                                      Given a merchant name, return the best fitting category from the list above. Only return the category name, nothing else.
                                      If the merchant does not fit any category, return "Uncategorized", but only if you must.
                                      """),
                new UserChatMessage(merchant),
            };
            var result = await chatClient.CompleteChatAsync(messages, new ChatCompletionOptions
            {
                Temperature = 0f
            });
            return result.Value.Content[0].Text.Trim();
        }
        catch (Exception e)
        {
            logger.LogError(e, $"Error categorizing merchant {merchant}");
            return "Unable to categorize";
        }
    }
}