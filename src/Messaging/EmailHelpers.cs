using MailKit.Security;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using MailKit.Net.Imap;
using MailKit.Search;
using MailKit;
using MailKit.Net.Smtp;
using MimeKit;
using Models;

namespace Messaging;

public class EmailService(IConfiguration configuration,
    ILogger<EmailService> logger)
{
    public Expense? ParseMessage(IMimeMessage message)
    {
        var parsedTotalSuccessfully = Double.TryParse(message.Subject.Replace("$", "").Split(" ")[3], out var totalCost);
        if (!parsedTotalSuccessfully)
        {
            return null;
        }

        var merchant = message.Subject.Split("transaction with")[1].Trim();
        return new Expense
        {
            Id = message.MessageId,
            Date = message.Date.DateTime,
            RawSubject = message.Subject,
            Total = totalCost,
            Merchant = merchant,
            IngressDate = DateTime.UtcNow
        };
    }

    public MimeMessage BuildMessage(Summary summary, Dictionary<string, Double> categoryTotals, string[] to, string from)
    {
        var message = new MimeMessage();
        message.From.Add(new MailboxAddress("Balance", from));
        var recipients = to;

        foreach (var recipient in recipients)
        {
            message.To.Add(new MailboxAddress(recipient, recipient));
        }
        var dot = summary.PercentOfLimit > 85 ? "🔴" : summary.PercentOfLimit > 60 ? "🟡" : "🟢";
        message.Subject = $"{dot} You are at {summary.PercentOfLimit}% of budget";
        var categories = String.Join("\n", categoryTotals.Select(x => $"{x.Key}: ${x.Value}"));
        message.Body = new TextPart("plain")
        {
            Text = $"""
                    {dot} You are at {summary.PercentOfLimit}% of budget. The current balance is ${summary.Balance}.
                    
                    Categories:
                    {categories}
                    """
        };

        return message;
    }

    public async Task<IEnumerable<MimeMessage>> GetTransactionEmails()
    {
        try
        {
            using var client = new ImapClient();
            var inbox = await GetImapInbox(client);

            // Delivered today (this method ignores time, just looks at day) 
            // and contains the magic subject
            var query = SearchQuery.DeliveredAfter(DateTime.Now)
                .And(SearchQuery.SubjectContains("You made a")
                    .And(SearchQuery.SubjectContains("transaction with")));

            var messages = new List<MimeMessage>();
            foreach (var uid in inbox.Search(query))
            {
                logger.LogInformation($"Searching for email {uid}");
                var message = await inbox.GetMessageAsync(uid);
                if (message == null) continue;
                messages.Add(message);
            }

            await client.DisconnectAsync(true);
            return messages;
        }
        catch (Exception e)
        {
            logger.LogError(e, "An error occured getting transaction emails");
            return Array.Empty<MimeMessage>();
        }
    }
    
    private async Task<IMailFolder> GetImapInbox(ImapClient client)
    {
        if (client.IsConnected) await client.DisconnectAsync(true);

        await client.ConnectAsync("imap.gmail.com", 993, SecureSocketOptions.SslOnConnect);
        await client.AuthenticateAsync(configuration["EmailFrom"], configuration["EmailAppPassword"]);

        var inbox = client.Inbox;
        await inbox.OpenAsync(FolderAccess.ReadOnly);
        return inbox;
    }
    
    /// <summary>
    /// Send a summary email with aggregation results via SMTP.
    /// </summary>
    private async Task SendEmail(SmtpClient client, Summary summary, Dictionary<string, double> categoryTotals)
    {
        string[] emails = configuration
            .GetSection("EmailTo")
            .GetChildren()
            .Select(c => c.Value)
            .Where(v => !string.IsNullOrWhiteSpace(v))
            .ToArray();
        
        var message = BuildMessage(summary,
            categoryTotals,
            emails,
            configuration["EmailFrom"] ?? throw new Exception("from is missing"));

        await client.ConnectAsync("smtp.gmail.com", 587, MailKit.Security.SecureSocketOptions.StartTls);
        await client.AuthenticateAsync(configuration["EmailFrom"],
            configuration["EmailAppPassword"]); // app password here
        await client.SendAsync(message);
        await client.DisconnectAsync(true);

        logger.LogInformation("Sent email to recipients");
    }

}