using MailKit;
using MimeKit;
using WorkerHost;

public static class EmailHelpers
{
    public static Expense? ParseMessage(IMimeMessage message, UniqueId uid)
    {
        var parsedTotalSuccessfully = Double.TryParse(message.Subject.Replace("$", "").Split(" ")[3], out var totalCost);
        if (!parsedTotalSuccessfully)
        {
            return null;
        }

        var merchant = message.Subject.Split("transaction with")[1].Trim();
        return new Expense
        {
            Id = uid.ToString(),
            Date = message.Date.DateTime,
            RawSubject = message.Subject,
            Total = totalCost,
            Merchant = merchant,
            IngressDate = DateTime.UtcNow
        };
    }

    public static MimeMessage BuildMessage(Summary summary, string[] to, string from)
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

        message.Body = new TextPart("plain")
        {
            Text = $"{dot} You are at {summary.PercentOfLimit}% of budget. The current balance is ${summary.Balance}."
        };

        return message;
    }
}