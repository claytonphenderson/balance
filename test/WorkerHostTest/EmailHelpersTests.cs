// using System.Reactive.Subjects;
// using MimeKit;
// using MimeKit.Text;
// using WorkerHost;
//
// namespace WorkerHostTest;
//
// public class EmailHelpersTests
// {
//     [Fact]
//     public void ParsesSubjectCorrectly()
//     {
//         var date = DateTimeOffset.Now;
//         var uid = new MailKit.UniqueId(1, 2);
//         var testMessage = new TestMessage()
//         {
//             Subject = "You made a $10.12 transaction with BIG DOG",
//             Date = date,
//         };
//         var result = EmailHelpers.ParseMessage(testMessage, uid);
//
//         Assert.NotNull(result);
//         Assert.Equal(uid.ToString(), result.Id);
//         Assert.Equal(10.12, result.Total);
//         Assert.Equal("BIG DOG", result.Merchant);
//     }
//
//     [Fact]
//     public void HandlesBadTotal()
//     {
//         var date = DateTimeOffset.Now;
//         var uid = new MailKit.UniqueId(1, 2);
//         var testMessage = new TestMessage()
//         {
//             Subject = "You made a 10.12b transaction with BIG DOG",
//             Date = date,
//             MessageId = "testId"
//         };
//         var result = EmailHelpers.ParseMessage(testMessage, uid);
//
//         // parsing the total will fail and return null
//         Assert.Null(result);
//     }
//
//     [Fact]
//     public void BuildsMessage()
//     {
//         var summary = new Summary()
//         {
//             PercentOfLimit = 63.3,
//             Balance = 123.45
//         };
//
//         var result = EmailHelpers.BuildMessage(summary, ["a", "b"], "c");
//
//         Assert.NotNull(result);
//         Assert.Contains("🟡", result.Subject);
//     }
// }
//
// class TestMessage : IMimeMessage
// {
//     public HeaderList Headers => throw new NotImplementedException();
//
//     public MessageImportance Importance { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }
//     public MessagePriority Priority { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }
//     public XMessagePriority XPriority { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }
//     public MailboxAddress Sender { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }
//     public MailboxAddress ResentSender { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }
//
//     public InternetAddressList From => throw new NotImplementedException();
//
//     public InternetAddressList ResentFrom => throw new NotImplementedException();
//
//     public InternetAddressList ReplyTo => throw new NotImplementedException();
//
//     public InternetAddressList ResentReplyTo => throw new NotImplementedException();
//
//     public InternetAddressList To => throw new NotImplementedException();
//
//     public InternetAddressList ResentTo => throw new NotImplementedException();
//
//     public InternetAddressList Cc => throw new NotImplementedException();
//
//     public InternetAddressList ResentCc => throw new NotImplementedException();
//
//     public InternetAddressList Bcc => throw new NotImplementedException();
//
//     public InternetAddressList ResentBcc => throw new NotImplementedException();
//
//     public string Subject { get; set; }
//     public DateTimeOffset Date { get; set; }
//     public DateTimeOffset ResentDate { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }
//
//     public MessageIdList References => throw new NotImplementedException();
//
//     public string InReplyTo { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }
//     public string MessageId { get; set; }
//     public string ResentMessageId { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }
//     public Version MimeVersion { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }
//     public MimeEntity Body { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }
//
//     public string TextBody => throw new NotImplementedException();
//
//     public string HtmlBody => throw new NotImplementedException();
//
//     public IEnumerable<MimeEntity> BodyParts => throw new NotImplementedException();
//
//     public IEnumerable<MimeEntity> Attachments => throw new NotImplementedException();
//
//     public void Accept(MimeVisitor visitor)
//     {
//         throw new NotImplementedException();
//     }
//
//     public void Dispose()
//     {
//         throw new NotImplementedException();
//     }
//
//     public string GetTextBody(TextFormat format)
//     {
//         throw new NotImplementedException();
//     }
//
//     public void Prepare(EncodingConstraint constraint, int maxLineLength = 78)
//     {
//         throw new NotImplementedException();
//     }
//
//     public void WriteTo(FormatOptions options, Stream stream, bool headersOnly, CancellationToken cancellationToken = default)
//     {
//         throw new NotImplementedException();
//     }
//
//     public void WriteTo(FormatOptions options, Stream stream, CancellationToken cancellationToken = default)
//     {
//         throw new NotImplementedException();
//     }
//
//     public void WriteTo(Stream stream, bool headersOnly, CancellationToken cancellationToken = default)
//     {
//         throw new NotImplementedException();
//     }
//
//     public void WriteTo(Stream stream, CancellationToken cancellationToken = default)
//     {
//         throw new NotImplementedException();
//     }
//
//     public void WriteTo(FormatOptions options, string fileName, CancellationToken cancellationToken = default)
//     {
//         throw new NotImplementedException();
//     }
//
//     public void WriteTo(string fileName, CancellationToken cancellationToken = default)
//     {
//         throw new NotImplementedException();
//     }
//
//     public Task WriteToAsync(FormatOptions options, Stream stream, bool headersOnly, CancellationToken cancellationToken = default)
//     {
//         throw new NotImplementedException();
//     }
//
//     public Task WriteToAsync(FormatOptions options, Stream stream, CancellationToken cancellationToken = default)
//     {
//         throw new NotImplementedException();
//     }
//
//     public Task WriteToAsync(Stream stream, bool headersOnly, CancellationToken cancellationToken = default)
//     {
//         throw new NotImplementedException();
//     }
//
//     public Task WriteToAsync(Stream stream, CancellationToken cancellationToken = default)
//     {
//         throw new NotImplementedException();
//     }
//
//     public Task WriteToAsync(FormatOptions options, string fileName, CancellationToken cancellationToken = default)
//     {
//         throw new NotImplementedException();
//     }
//
//     public Task WriteToAsync(string fileName, CancellationToken cancellationToken = default)
//     {
//         throw new NotImplementedException();
//     }
// }
