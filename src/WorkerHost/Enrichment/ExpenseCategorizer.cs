using OpenAI.Chat;

namespace WorkerHost;

public class ExpenseCategorizer
{
    private readonly ChatClient _chatClient;
    public ExpenseCategorizer(ChatClient chatClient)
    {
        _chatClient = chatClient;
    }

    public async Task<string> CategorizeMerchant(string merchant)
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
        var result = await _chatClient.CompleteChatAsync(messages, new ChatCompletionOptions
        {
            Temperature = 0f
        });
        return result.Value.Content[0].Text.Trim();
    }
}