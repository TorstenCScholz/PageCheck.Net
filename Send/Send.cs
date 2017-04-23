using System;
using RabbitMQ.Client;
using System.Text;
using CommandLine;
using Newtonsoft.Json;
using System.Net.Http;
using System.Threading.Tasks;
using AngleSharp.Parser.Html;

class Options
{
    [Value(0, HelpText = "Url", Required = true)]
    public string Url { get; set; }

    [Value(1, HelpText = "Class", Required = true)]
    public string Class { get; set; }

    [Option('t', "to", Required = true)]
    public string NameTo { get; set; }

    [Option('f', "from", Required = true)]
    public string NameFrom { get; set; }

    [Option('T', "email-to", Required = true)]
    public string EmailTo { get; set; }

    [Option('F', "email-from", Required = true)]
    public string EmailFrom { get; set; }

    [Option('u', "user", Required = true)]
    public string SmtpUser { get; set; }

    [Option('p', "password", Required = true)]
    public string SmtpPassword { get; set; }

    [Option('a', "smtp-address", Required = true)]
    public string SmtpAddress { get; set; }

    [Option('P', "smtp-port", Required = true)]
    public int SmtpPort { get; set; }

    [Option('s', "subject-positive", Required = false, Default = "Class is present. You know what to do.")]
    public string EmailSubjectPositive { get; set; }

    [Option('S', "subject-negative", Required = false, Default = "Class is *not* present")]
    public string EmailSubjectNegative { get; set; }

    [Option('c', "content-negative", Required = false, Default = "")]
    public string EmailContentPositive { get; set; }

    [Option('C', "subject-negative", Required = false, Default = "")]
    public string EmailContentNegative { get; set; }


}

class SimpleSmtpEmail
{
    public string SmtpUser { set; get; }

    public string SmtpPassword { set; get; }

    public string NameFrom { set; get; }

    public string EmailFrom { set; get; }

    public string NameTo { set; get; }

    public string EmailTo { set; get; }

    public string Subject { set; get; }

    public string Content { set; get; }

    public int SmtpPort { set; get; }

    public string SmtpAddress { set; get; }

    public override string ToString()
    {
        var sb = new StringBuilder();

        sb.AppendLine($"To: {NameTo} ({EmailTo})");
        sb.AppendLine($"From: {NameFrom} ({EmailFrom})");
        sb.AppendLine("====================================");
        sb.AppendLine($"Subject: {Subject}");
        sb.AppendLine("====================================");
        sb.AppendLine($"{Content}");

        return sb.ToString();
    }
}

class Send
{
    const string QueueName = "sendmail";

    private static async Task<string> DownloadWebpage(string url)
    {
        var client = new HttpClient();

        var responseMessage = await client.GetAsync(url);

        responseMessage.EnsureSuccessStatusCode();

        await responseMessage.Content.LoadIntoBufferAsync();

        return await responseMessage.Content.ReadAsStringAsync();
    }

    private static bool IsClassPresent(string content, string cssClass)
    {
        try
        {
            var parser = new HtmlParser();
            var document = parser.Parse(content);

            var cellSelector = $".{cssClass}";
            var cells = document.QuerySelectorAll(cellSelector);

            return (cells.Length > 0);
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex.Message);
            return false;
        }
    }

    private static void QueueSendingEmail(IModel channel, SimpleSmtpEmail simpleEmail)
    {
        var message = JsonConvert.SerializeObject(simpleEmail);
        var body = Encoding.UTF8.GetBytes(message);

        channel.BasicPublish(exchange: "",
                             routingKey: QueueName,
                             basicProperties: null,
                             body: body);

        Console.WriteLine(" [x] Sent");
    }

    public static void Main(string[] args)
    {
        var result = CommandLine.Parser.Default.ParseArguments<Options>(args);

        result.WithParsed(options =>
        {
            DownloadWebpage(options.Url).ContinueWith(
                (messageTask) =>
                {
                    var factory = new ConnectionFactory() { HostName = "localhost" };
                    using (var connection = factory.CreateConnection())
                    using (var channel = connection.CreateModel())
                    {
                        channel.QueueDeclare(queue: QueueName,
                                                durable: false,
                                                exclusive: false,
                                                autoDelete: false,
                                                arguments: null);

                        var messageContent = messageTask.Result;

                        // TODO: Should be sent encoded? SSL?
                        var simpleEmail = new SimpleSmtpEmail
                        {
                            SmtpUser = options.SmtpUser,
                            SmtpPassword = options.SmtpPassword,
                            SmtpAddress = options.SmtpAddress,
                            SmtpPort = options.SmtpPort,
                            NameTo = options.NameTo,
                            EmailTo = options.EmailTo,
                            NameFrom = options.NameFrom,
                            EmailFrom = options.EmailFrom
                        };

                        if (IsClassPresent(messageContent, options.Class))
                        {
                            simpleEmail.Subject = options.EmailSubjectPositive;
                            simpleEmail.Content = options.EmailContentPositive;
                        }
                        else
                        {
                            simpleEmail.Subject = options.EmailSubjectNegative;
                            simpleEmail.Content = options.EmailContentNegative;
                        }

                        QueueSendingEmail(channel, simpleEmail);
                    }
                }
            );
        });

        Console.ReadKey();
    }
}
