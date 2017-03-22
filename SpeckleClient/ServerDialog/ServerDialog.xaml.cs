using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace SpeckleClient.ServerDialog
{
    /// <summary>
    /// Interaction logic for ServerDialog.xaml
    /// </summary>
    public partial class ServerDialog : Window
    {
        private static readonly HttpClient client = new HttpClient();

        public string f_apitoken { get; set; }
        public string f_apiurl { get; set; }

        List<string> extServers = new List<string>();
        List<string> proper = new List<string>();


        public ServerDialog()
        {
            InitializeComponent();

            string strPath = System.Environment.GetFolderPath(System.Environment.SpecialFolder.LocalApplicationData);
            strPath = strPath + @"\SpeckleSettings";

            foreach (string file in Directory.EnumerateFiles(strPath, "*.txt"))
            {
                string content = File.ReadAllText(file);
                proper.Add(content.TrimEnd('\r', '\n'));
                extServers.Add(content.TrimEnd('\r', '\n').Split(',')[2] + " @ " + content.TrimEnd('\r', '\n').Split(',')[0]);
            }

            existingServers.ItemsSource = extServers;
        }

        private void button_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            this.Close();
        }

        private void Rectangle_MouseDown(object sender, MouseButtonEventArgs e)
        {
            this.DragMove();
            //this.drag
        }

        private void button_register_Click(object sender, RoutedEventArgs e)
        {
            string validationErrors = "";


            var password = this.password.Text;

            if ((password.Length < 7))
            {
                validationErrors += "Password too short (need to be longer than 8 chars please!).\n";
            }

            System.Net.Mail.MailAddress addr = null;

            try
            {
                addr = new System.Net.Mail.MailAddress(email.Text);
            }
            catch
            {
                validationErrors += "Invalid email address.\n";
            }

            if (addr != null) Debug.WriteLine(addr.Address);

            Uri uriResult;
            bool urlok = Uri.TryCreate(serverURL.Text, UriKind.Absolute, out uriResult) &&
                (uriResult.Scheme == Uri.UriSchemeHttp || uriResult.Scheme == Uri.UriSchemeHttps);

            if (!urlok)
                validationErrors += "Invalid server url. Check again.\n";

            if (this.name.Text == "")
            {
                validationErrors += "Please fill in your name. It's more friendly like this.";
            }

            if (validationErrors != "")
            {
                MessageBox.Show(validationErrors);
                return;
            }


            Dictionary<string, string> dictss = new Dictionary<string, string>();

            dictss.Add("email", addr.Address);
            dictss.Add("password", password);
            dictss.Add("name", this.name.Text);

            var response = CreateUserAsync(uriResult, dictss);

            if (response["success"] == "true")
            {
                MessageBox.Show("Congrats! You've made an account with " + uriResult.Host + ". You api token is: " + response["apitoken"] + ". \n " +
                    "From now, you should be able to select this server from the right hand panel witout inputting your password. Back to grasshopper now, have fun!");

                this.f_apitoken = response["apitoken"];
                this.f_apiurl = uriResult.ToString();

                writeToSettings(uriResult, response["apitoken"], addr.ToString());
                DialogResult = true;

                this.Close();

            }
            else
            {
                MessageBox.Show(response["message"]);
            }
        }


        void writeToSettings(Uri uri, string apitoken, string email)
        {
            string strPath = System.Environment.GetFolderPath(System.Environment.SpecialFolder.LocalApplicationData);

            System.IO.Directory.CreateDirectory(strPath + @"\SpeckleSettings");

            strPath = strPath + @"\SpeckleSettings\";

            string fileName = uri.Host;
            string content = uri.ToString() + "," + apitoken + "," + email;

            Debug.WriteLine(content);

            System.IO.StreamWriter file = new System.IO.StreamWriter(strPath + fileName + "." + email + ".txt");
            file.WriteLine(content);
            file.Close();
        }

        static Dictionary<string, string> CreateUserAsync(Uri url, Dictionary<string, string> user)
        {
            string result = "";
            using (var client = new WebClient())
            {
                client.Headers[HttpRequestHeader.ContentType] = "application/json";
                try
                {
                    result = client.UploadString(new Uri(url.OriginalString + "/accounts/register"), "POST", user.FromDictionaryToJson());
                }
                catch (WebException e)
                {
                    var ret = new Dictionary<string, string>();
                    ret.Add("success", "false");
                    ret.Add("message", e.Message);
                    return ret;
                }
            }

            Dictionary<string, string> parsedResponse = result.FromJsonToDictionary();

            return parsedResponse;
        }

        private void button_selectServer_Click(object sender, RoutedEventArgs e)
        {
            if (existingServers.SelectedIndex == -1)
            {
                MessageBox.Show("Please select a server or register with one on the left hand side.");
                return;
            }

            var selectedServer = proper[existingServers.SelectedIndex].Split(',');

            this.f_apitoken = selectedServer[1];
            this.f_apiurl = selectedServer[0];

            DialogResult = true;

            this.Close();
        }


    }

    public static class Extensions
    {
        public static string FromDictionaryToJson(this Dictionary<string, string> dictionary)
        {
            var kvs = dictionary.Select(kvp => string.Format("\"{0}\":\"{1}\"", kvp.Key, kvp.Value));
            return string.Concat("{", string.Join(",", kvs), "}");
        }

        public static Dictionary<string, string> FromJsonToDictionary(this string json)
        {
            if (json == null || json == "")
            {
                var ret = new Dictionary<string, string>();
                ret.Add("success", "false");
                ret.Add("message", "Null response. Give a shout to d.stefanescu@ucl.ac.uk.");
                return ret;
            }

            string[] keyValueArray = json.Replace("{", string.Empty).Replace("}", string.Empty).Replace("\"", string.Empty).Split(',');
            var thedict = new Dictionary<string, string>();

            try
            {
                thedict = keyValueArray.ToDictionary(item => item.Split(':')[0], item => item.Split(':')[1]);
            }
            catch
            {
                thedict.Add("success", "false");
                thedict.Add("message", json);
                return thedict;
            }
            return thedict;
        }
    }
}
