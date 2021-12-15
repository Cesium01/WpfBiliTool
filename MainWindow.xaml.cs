using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Input;

namespace WpfBiliTool
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private FileStream fs;
        private MyClient myClient;
        public MainWindow()
        {
            InitializeComponent();
            myClient = new MyClient();
            filePathBox.Text = "（上传文件需要登录，尚未完成）";
            uploadBtn.IsEnabled = false;
        }

        protected override void OnClosed(EventArgs e)
        {
            base.OnClosed(e);
            if (fs != null)
            {
                fs.Flush();
                fs.Close();
                fs = null;
            }
        }

        private void OpenFileBtn_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new Microsoft.Win32.OpenFileDialog()
            {
                Filter = "(*.jpg;*.jpeg;*.png;*.bmp)|*.jpg;*.jpeg;*.png;*.bmp"
            };
            bool? result = dialog.ShowDialog();
            if (result == true)
            {
                filePathBox.Text = dialog.FileName;
                fs = (FileStream)dialog.OpenFile();
            }
        }

        private async void UploadBtn_Click(object sender, RoutedEventArgs e)
        {
            uploadBtn.Content = "上传中...";
            uploadBtn.IsEnabled = false;
            if (fs!=null && fs.CanRead)
            {
                try
                {
                    longUrlBox.Text = await myClient.UploadImage(fs);
                }
                catch (Exception exception)
                {
                    MessageBox.Show(exception.Message);
                }
            }
            else
            {
                MessageBox.Show("未选择文件或文件无法读取");
            }
            uploadBtn.Content = "上传图片";
            uploadBtn.IsEnabled = true;
        }

        private async void GetShortUrlBtn_Click(object sender, RoutedEventArgs e)
        {
            getShortUrlBtn.Content = "获取中...";
            getShortUrlBtn.IsEnabled = false;
            if (!string.IsNullOrEmpty(longUrlBox.Text))
            {
                try
                {
                    shortUrlBox.Text = await myClient.GetShortUrl(longUrlBox.Text);
                }
                catch (Exception exception)
                {
                    MessageBox.Show(exception.Message);
                }
            }
            else
            {
                MessageBox.Show("长链为空");
            }
            getShortUrlBtn.Content = "获取短链";
            getShortUrlBtn.IsEnabled = true;
        }

        private void ClearBtn_Click(object sender, RoutedEventArgs e)
        {
            filePathBox.Text = "";
            longUrlBox.Text = "";
            shortUrlBox.Text = "";
            topicNameBox.Text = "";
            if (fs != null)
            {
                fs.Flush();
                fs.Close();
                fs = null;
            }
        }

        private void TopicBtn_Click(object sender, RoutedEventArgs e)
        {
            string encodedTopicName = System.Web.HttpUtility.UrlEncode(topicNameBox.Text, Encoding.UTF8).ToUpper();
            System.Diagnostics.Process.Start("explorer.exe", "https://t.bilibili.com/topic/" + encodedTopicName);
        }

        private async void RollBtn_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(rollNumberBox.Text) || string.IsNullOrEmpty(dynamicUrlBox.Text))
            {
                MessageBox.Show("抽奖人数或动态链接未填写");
            }
            else
            {
                Dictionary<string, string> users;
                rollBtn.IsEnabled = false;
                selectedBox.Text = "正在获取数据...";
                try
                {
                    users = await myClient.DynamicRoll(dynamicUrlBox.Text, rollMethodBox.SelectedIndex);
                }
                catch (Exception ex)
                {
                    MessageBox.Show(ex.Message);
                    rollBtn.IsEnabled = true;
                    selectedBox.Clear();
                    return;
                }
                if (users.Count < int.Parse(rollNumberBox.Text))
                {
                    selectedBox.Clear();
                    MessageBox.Show("回复/转发人数不足");
                }
                else
                {
                    Dictionary<string, string> selectedUsers = RollUsers(users, int.Parse(rollNumberBox.Text));
                    selectedBox.Clear();
                    foreach (string uid in selectedUsers.Keys)
                    {
                        selectedBox.Text += selectedUsers[uid] + ": https://space.bilibili.com/" + uid + "\n";
                    }
                }
                rollBtn.IsEnabled = true;
            }
        }

        private void RollNumber_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if((e.Key >= Key.D0 && e.Key <= Key.D9) || (e.Key >= Key.NumPad0 && e.Key <= Key.NumPad9)
                || e.Key == Key.Delete || e.Key == Key.Left || e.Key == Key.Right || e.Key == Key.Back)
            {
            }
            else
            {
                e.Handled = true;
            }
        }

        private static Dictionary<string, string> RollUsers(Dictionary<string, string> users, int number)
        {
            List<string> uidList = new List<string>(users.Keys);
            HashSet<string> selectedSet = new HashSet<string>();
            Random r = new Random();
            while (selectedSet.Count < number)
            {
                selectedSet.Add(uidList[r.Next(users.Count)]);
            }
            Dictionary<string, string> selectedUsers = new Dictionary<string, string>();
            foreach (string uid in selectedSet)
            {
                selectedUsers[uid] = users[uid];
            }
            return selectedUsers;
        }
    }
}
