using Common;
using Microsoft.Win32;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
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

namespace ClientWPF.Pages
{
    /// <summary>
    /// Логика взаимодействия для Main.xaml
    /// </summary>
    public partial class Main : Page
    {
        MainWindow mw;
        private IPAddress ipAddress;
        private int port;
        private int userId = -1;
        private Stack<string> directoryStack = new Stack<string>();
        public Main(MainWindow _mw, IPAddress ip, int _port, int _idUser)
        {
            InitializeComponent();
            this.mw = _mw;
            this.ipAddress = ip;
            this.port = _port;
            this.userId = _idUser;

            LoadDirectories();
        }

        private void LoadDirectories()
        {
            try
            {
                var response = SendCommand("cd");
                if (response?.Command == "cd")
                {
                    if (string.IsNullOrEmpty(response.Data))
                    {
                        MessageBox.Show("Список директорий пуст.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                        return;
                    }

                    List<string> directories;
                    try
                    {
                        // Пробуем десериализовать список директорий
                        directories = JsonConvert.DeserializeObject<List<string>>(response.Data);
                    }
                    catch (JsonException)
                    {
                        MessageBox.Show("Ошибка при получении списка директорий: Неверный формат данных", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                        return;
                    }

                    list.Items.Clear();

                    if (directoryStack.Count > 0)
                    {
                        list.Items.Add("Назад");
                    }

                    foreach (var dir in directories)
                    {
                        list.Items.Add(dir);
                    }
                }
                else
                {
                    MessageBox.Show($"Не удалось загрузить список директорий: {response?.Data ?? "Неизвестная ошибка"}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка загрузки: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        private ViewModelMessage SendCommand(string message)
        {
            try
            {
                IPEndPoint endPoint = new IPEndPoint(ipAddress, port);
                using (Socket socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp))
                {
                    socket.Connect(endPoint);
                    if (socket.Connected)
                    {
                        var request = new ViewModelSend(message, userId);
                        byte[] requestBytes = Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(request));
                        socket.Send(requestBytes);

                        byte[] responseBytes = new byte[10485760];
                        int receivedBytes = socket.Receive(responseBytes);
                        string responseData = Encoding.UTF8.GetString(responseBytes, 0, receivedBytes);

                        return JsonConvert.DeserializeObject<ViewModelMessage>(responseData);
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка соединения: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            return null;
        }

        private void Back(object sender, RoutedEventArgs e)
        {
            mw.frame.Navigate(new Pages.Connect(mw));
        }

        private void Load(object sender, RoutedEventArgs e)
        {
            OpenFileDialog openFileDialog = new OpenFileDialog();
            openFileDialog.Filter = "All files (*.*)|*.*";
            if (openFileDialog.ShowDialog() == true)
            {
                string filePath = openFileDialog.FileName;
                SendFileToServer(filePath);
            }
        }

        public void SendFileToServer(string filePath)
        {
            try
            {
                var socket = Connect.Connecting(ipAddress, port);
                if (socket == null)
                {
                    MessageBox.Show("Не удалось подключиться к серверу.");
                    return;
                }
                if (!File.Exists(filePath))
                {
                    MessageBox.Show("Указанный файл не существует.");
                    return;
                }
                FileInfo fileInfo = new FileInfo(filePath);
                FileInfoFTP fileInfoFTP = new FileInfoFTP(File.ReadAllBytes(filePath), fileInfo.Name);
                ViewModelSend viewModelSend = new ViewModelSend(JsonConvert.SerializeObject(fileInfoFTP), userId);
                byte[] messageByte = Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(viewModelSend));
                socket.Send(messageByte);
                byte[] buffer = new byte[10485760];
                int bytesReceived = socket.Receive(buffer);
                string serverResponse = Encoding.UTF8.GetString(buffer, 0, bytesReceived);
                ViewModelMessage responseMessage = JsonConvert.DeserializeObject<ViewModelMessage>(serverResponse);
                socket.Close();
                LoadDirectories();
                if (responseMessage.Command == "message")
                {
                    MessageBox.Show(responseMessage.Data);
                }
                else
                {
                    MessageBox.Show("Неизвестный ответ от сервера.");
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
        }

        private void LoadFile(string fileName)
        {
            string localSavePath = GetUniqueFilePath(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), System.IO.Path.GetFileName(fileName));
            Console.WriteLine($"Trying to download file from server: {fileName}");
            var socket = Connect.Connecting(ipAddress, port);
            if (socket == null)
            {
                MessageBox.Show("Не удалось подключиться к серверу.");
                return;
            }
            string command = $"get {fileName}";
            ViewModelSend viewModelSend = new ViewModelSend(command, userId);
            byte[] messageBytes = Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(viewModelSend));
            socket.Send(messageBytes);
            byte[] buffer = new byte[10485760];
            int bytesReceived = socket.Receive(buffer);
            string serverResponse = Encoding.UTF8.GetString(buffer, 0, bytesReceived);
            ViewModelMessage responseMessage = JsonConvert.DeserializeObject<ViewModelMessage>(serverResponse);
            socket.Close();
            if (responseMessage.Command == "file")
            {
                byte[] fileData = JsonConvert.DeserializeObject<byte[]>(responseMessage.Data);
                File.WriteAllBytes(localSavePath, fileData);
                MessageBox.Show($"Файл скачан и сохранён в: {localSavePath}");
            }
            else MessageBox.Show("Не удалось получить файл. Проверьте путь на сервере.");
        }
        private string GetUniqueFilePath(string directory, string fileName)
        {
            string uniqueFilePath = System.IO.Path.Combine(directory, fileName);
            return uniqueFilePath;
        }

        private void ListOpenFold(object sender, MouseButtonEventArgs e)
        {
            if (list.SelectedItem == null)
                return;

            string selectedItem = list.SelectedItem.ToString();

            if (selectedItem == "Назад")
            {
                if (directoryStack.Count > 0)
                {
                    directoryStack.Pop();
                    LoadDirectories();
                }
            }
            if (selectedItem.EndsWith("\\"))
            {
                directoryStack.Push(selectedItem);
                var response = SendCommand($"cd {selectedItem.TrimEnd('/')}");

                if (response?.Command == "cd")
                {
                    var items = JsonConvert.DeserializeObject<List<string>>(response.Data);
                    list.Items.Clear();
                    list.Items.Add("Назад");
                    foreach (var item in items)
                    {
                        list.Items.Add(item);
                    }
                }
                else
                {
                    MessageBox.Show($"Ошибка открытия директории: {response?.Data}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            else
            {
                DownloadFile(selectedItem);
            }
        }

        private void DownloadFile(string fileName)
        {
            try
            {
                string localSavePath = GetUniqueFilePath(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), System.IO.Path.GetFileName(fileName));
                Console.WriteLine($"Trying to download file from server: {fileName}");
                var socket = Connect.Connecting(ipAddress, port);
                if (socket == null)
                {
                    MessageBox.Show("Не удалось подключиться к серверу.");
                    return;
                }
                string command = $"get {fileName}";
                ViewModelSend viewModelSend = new ViewModelSend(command, userId);
                byte[] messageBytes = Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(viewModelSend));
                socket.Send(messageBytes);
                byte[] buffer = new byte[10485760];
                int bytesReceived = socket.Receive(buffer);
                string serverResponse = Encoding.UTF8.GetString(buffer, 0, bytesReceived);
                ViewModelMessage responseMessage = JsonConvert.DeserializeObject<ViewModelMessage>(serverResponse);
                socket.Close();
                if (responseMessage.Command == "file")
                {
                    byte[] fileData = JsonConvert.DeserializeObject<byte[]>(responseMessage.Data);
                    File.WriteAllBytes(localSavePath, fileData);
                    MessageBox.Show($"Файл скачан и сохранён в: {localSavePath}");
                }
                else MessageBox.Show("Не удалось получить файл. Проверьте путь на сервере.");
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
        }
    }
}
