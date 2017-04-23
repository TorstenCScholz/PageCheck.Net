using MailKit.Net.Smtp;
using MimeKit;
using Newtonsoft.Json;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using System;
using System.Text;

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

class Receive
{
    const string QueueName = "sendmail";

    private static void SendEmail(SimpleSmtpEmail mail)
    {
        try
        {
            using (var client = new SmtpClient())
            {
                // TODO: What does this do specifically?
                // For demo-purposes, accept all SSL certificates
                client.ServerCertificateValidationCallback = (s, c, h, e) => true;
                
                client.Connect(mail.SmtpAddress, mail.SmtpPort, false);
                
                client.AuthenticationMechanisms.Remove("XOAUTH2");

                client.Authenticate(mail.SmtpUser, mail.SmtpPassword);

                var message = new MimeMessage();
                message.From.Add(new MailboxAddress(mail.NameFrom, mail.EmailFrom));
                message.To.Add(new MailboxAddress(mail.NameTo, mail.EmailTo));
                message.Subject = mail.Subject;

                message.Body = new TextPart("plain")
                {
                    Text = mail.Content
                };

                client.Send(message);

                client.Disconnect(true);

                Console.WriteLine("Email sent.");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex.Message);
        }
    }

    public static void Main()
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

            var consumer = new EventingBasicConsumer(channel);
            consumer.Received += (model, ea) =>
            {
                var body = ea.Body;
                var message = Encoding.UTF8.GetString(body);
                var simpleMail = JsonConvert.DeserializeObject<SimpleSmtpEmail>(message);

                SendEmail(simpleMail);
            };
            channel.BasicConsume(queue: QueueName,
                                 noAck: true,
                                 consumer: consumer);

            Console.WriteLine(" Press [enter] to exit.");
            Console.ReadLine();
        }
    }
}
