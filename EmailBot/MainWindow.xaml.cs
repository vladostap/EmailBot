using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Net.Mail;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using MailKit;
using MailKit.Net.Imap;
using MailKit.Search;
using MimeKit;

namespace EmailBot
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private const int _port = 993;
        private const string _gImap = "imap.gmail.com";

        public MainWindow()
        {
            InitializeComponent();

            //Login.Text = "";
            //Password.Text = "";
            //CodeWord.Text = "";
        }

        /// <summary>
        /// Обновляем ListBox
        /// каждые 5 сек
        /// </summary>
        /// <param name="login"></param>
        /// <param name="password"></param>
        /// <param name="codeWord"></param>
        private async void UpdateListBox(string login, string password, string codeWord)
        {
            while (true)
            {
                await Task.Delay(10000);

                var messages = await GetMailMessagesAsync(Login.Text,
                    Password.Text, CodeWord.Text);

                ListView.Items.Clear();

                if (messages != null || messages.Count() > 0)
                {
                    foreach (var message in messages)
                        ListView.Items.Add(message);
                }

                SendResponseEmail(login, password, messages);
            }
        }

        /// <summary>
        /// Авторизация в почтовом ящике
        /// + валидация полей
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private async void Auth_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(Login.Text)
                    || string.IsNullOrWhiteSpace(Password.Text)
                    || string.IsNullOrWhiteSpace(CodeWord.Text))
                {
                    MessageBox.Show("Пустое поле!");
                }
                else
                {
                    EnableControls();

                    var messages = await GetMailMessagesAsync(Login.Text,
                        Password.Text, CodeWord.Text);

                    if (messages != null || messages.Count() > 0)
                    {
                        foreach (var message in messages)
                            ListView.Items.Add(message);
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"{ex}");
            }

            UpdateListBox(Login.Text, Password.Text, CodeWord.Text);
        }

        /// <summary>
        /// Делаем неактивными 
        /// элементы управления
        /// </summary>
        private void EnableControls()
        {
            Login.IsEnabled = false;
            Password.IsEnabled = false;
            CodeWord.IsEnabled = false;
            Auth.IsEnabled = false;
        }

        /// <summary>
        /// Получаем сообщения по 
        /// заданной теме письма
        /// </summary>
        /// <param name="login"></param>
        /// <param name="password"></param>
        /// <param name="codeWord"></param>
        /// <returns></returns>
        private Task<List<MessageModel>> GetMailMessagesAsync(string login, string password, string codeWord)
        {
            return Task.Run(() =>
            {
                using (var client = new ImapClient())
                {
                    client.ServerCertificateValidationCallback = (s, c, h, e) => true;
                    client.Connect(_gImap, _port, true);
                    client.AuthenticationMechanisms.Remove("XOAUTH2");
                    client.Authenticate(login, password);

                    var inbox = client.Inbox;
                    inbox.Open(FolderAccess.ReadOnly);

                    var messages = inbox.Search(SearchQuery
                        .NotSeen
                        .And(SearchQuery
                        .SubjectContains(codeWord))).ToList();

                    var model = new List<MessageModel>();

                    string fileName = "";

                    foreach (var message in messages)
                    {
                        var msg = client.Inbox.GetMessage(message);

                        foreach (var attachment in msg.Attachments.Where(p => p.ContentType.MediaType == "image"))
                        {
                            fileName = $"./{Guid.NewGuid()}_{attachment.ContentDisposition.FileName}";

                            using (var file = new FileStream(fileName, FileMode.Create))
                            {
                                attachment.WriteTo(file);
                            }
                        }

                        model.Add(new MessageModel
                        {
                            From = client.Inbox.GetMessage(message).From.ToString(),
                            Text = client.Inbox.GetMessage(message).TextBody,
                            Attachment = fileName
                        });

                        inbox.Open(FolderAccess.ReadWrite);
                        inbox.AddFlags(message, MessageFlags.Seen, true);
                    }

                    client.Disconnect(true);

                    return model;
                }
            });
        }

        /// <summary>
        /// Отправляем ответ:
        /// На изображение накладываем
        /// текст тела сообщения
        /// </summary>
        /// <param name="login"></param>
        /// <param name="password"></param>
        /// <param name="model"></param>
        private void SendResponseEmail(string login, string password, List<MessageModel> model)
        {
            try
            {
                SmtpClient mailServer = new SmtpClient("smtp.gmail.com", 587)
                {
                    EnableSsl = true,
                    Credentials = new System.Net.NetworkCredential(login, password)
                };

                foreach (var item in model)
                {

                    MailMessage msg = new MailMessage(login, item.From)
                    {
                        Subject = "Response From Email Bot",
                        Body = "Hi!"
                    };

                    string newFileName = $"{Guid.NewGuid()}{item.Attachment}";

                    using (var image = System.Drawing.Image.FromFile(item.Attachment))
                    {
                        using (var graphics = Graphics.FromImage(image))
                        {
                            var textBounds = graphics.VisibleClipBounds;
                            textBounds.Inflate(-5, -5);
                            graphics.DrawString(
                                item.Text,
                                System.Drawing.SystemFonts.CaptionFont,
                                Brushes.Green,
                                textBounds
                            );
                        }

                        image.Save(newFileName);
                    }

                    msg.Attachments.Add(new Attachment(newFileName));

                    mailServer.Send(msg);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Unable to send email. Error : {ex.Message}");
            }
        }
    }
}
