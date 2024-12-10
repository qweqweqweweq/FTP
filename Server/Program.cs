using Common;
using MySqlConnector;
using Newtonsoft.Json;
using Server.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace Server
{
    public class Program
    {
        public static List<User> Users = new List<User>();
        public static IPAddress IpAddress;
        public static int Port;
        private static string connectionString = "server=127.0.0.1;port=3306;Database=FTP;uid=root;";

        static void Main(string[] args)
        {
            LoadUsersFromDatabase();
            Console.Write("Введите IP адрес сервера: ");
            string sIdAddress = Console.ReadLine();
            Console.Write("Введите порт: ");
            string sPort = Console.ReadLine();
            if (int.TryParse(sPort, out Port) && IPAddress.TryParse(sIdAddress, out IpAddress))
            {
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine("Данные успешно введены. Запускаю сервер.");
                StartServer();
            }
            Console.Read();
        }

        public static bool AuthenticateUser(string login, string password)
        {
            using (MySqlConnection conn = new MySqlConnection(connectionString))
            {
                conn.Open();
                string query = "SELECT COUNT(*) FROM Users WHERE Login = @Login AND Password = @Password";
                using (MySqlCommand cmd = new MySqlCommand(query, conn))
                {
                    cmd.Parameters.AddWithValue("@Login", login);
                    cmd.Parameters.AddWithValue("@Password", password);
                    int count = Convert.ToInt32(cmd.ExecuteScalar());
                    return count > 0;
                }
            }
        }

        public static bool AuthorizationUser(string login, string password, out int userId)
        {
            userId = -1;
            User user = Users.Find(x => x.login == login && x.password == password);
            if (user != null)
            {
                userId = user.Id;
                return true;
            }
            return false;
        }

        public static List<string> GetDirectory(string src)
        {
            List<string> FoldersFiles = new List<string>();
            if (Directory.Exists(src))
            {
                string[] dirs = Directory.GetDirectories(src);
                foreach (string dir in dirs)
                {
                    FoldersFiles.Add(dir + "\\");
                }
                string[] files = Directory.GetFiles(src);
                foreach (string file in files)
                {
                    FoldersFiles.Add(file);
                }
            }
            return FoldersFiles;
        }

        public static void StartServer()
        {
            IPEndPoint endPoint = new IPEndPoint(IpAddress, Port);
            Socket sListener = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            sListener.Bind(endPoint);
            sListener.Listen(10);
            Console.WriteLine("Сервер запущен");
            while (true)
            {
                try
                {
                    Socket Handler = sListener.Accept();
                    Task.Run(() => HandleClient(Handler));
                }
                catch (Exception ex)
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine("Ошибка при принятии соединения: " + ex.Message);
                }
            }
        }

        private static void HandleClient(Socket Handler)
        {
            try
            {
                string Data = null;
                byte[] Bytes = new byte[10485760];
                int BytesRec = Handler.Receive(Bytes);
                Data += Encoding.UTF8.GetString(Bytes, 0, BytesRec);
                Console.Write("Сообщение от пользователя: " + Data + "\n");
                string Reply = "";
                ViewModelSend ViewModelSend = JsonConvert.DeserializeObject<ViewModelSend>(Data);
                if (ViewModelSend != null)
                {
                    ViewModelMessage viewModelMessage;
                    string[] DataCommand = ViewModelSend.Message.Split(new string[1] { " " }, StringSplitOptions.None);
                    if (DataCommand[0] == "connect")
                    {
                        string[] DataMessage = ViewModelSend.Message.Split(new string[1] { " " }, StringSplitOptions.None);
                        if (AuthorizationUser(DataMessage[1], DataMessage[2], out int userId))
                        {
                            userId = Users.Find(x => x.login == DataMessage[1] && x.password == DataMessage[2]).Id;
                            viewModelMessage = new ViewModelMessage("authorization", userId.ToString());
                            string username = Users.Find(x => x.login == DataMessage[1] && x.password == DataMessage[2]).login;
                            string password = Users.Find(x => x.login == DataMessage[1] && x.password == DataMessage[2]).password;
                            LogCommandToDatabase(userId, ViewModelSend.Message.Split(' ')[0]);
                        }
                        else
                        {
                            viewModelMessage = new ViewModelMessage("message", "Неправильный логин и пароль пользователя.");
                        }
                        Reply = JsonConvert.SerializeObject(viewModelMessage);
                        byte[] message = Encoding.UTF8.GetBytes(Reply);
                        Handler.Send(message);
                    }
                    else if (DataCommand[0] == "cd")
                    {
                        if (ViewModelSend.Id != -1)
                        {
                            string[] DataMessage = ViewModelSend.Message.Split(new string[1] { " " }, StringSplitOptions.None);
                            List<string> FoldersFiles = new List<string>();
                            if (DataMessage.Length == 1)
                            {
                                Users[ViewModelSend.Id - 1].temp_src = Users[ViewModelSend.Id - 1].src;
                                FoldersFiles = GetDirectory(Users[ViewModelSend.Id - 1].src);
                            }
                            else
                            {
                                string cdFolder = string.Join(" ", DataMessage.Skip(1));
                                if (cdFolder == Users[ViewModelSend.Id - 1].src)
                                {
                                    Users[ViewModelSend.Id - 1].temp_src = Users[ViewModelSend.Id - 1].src;
                                }
                                else if (cdFolder.Contains(Users[ViewModelSend.Id - 1].temp_src))
                                {
                                    Users[ViewModelSend.Id - 1].temp_src = Path.Combine(Users[ViewModelSend.Id - 1].temp_src, cdFolder);
                                }
                                else
                                {
                                    viewModelMessage = new ViewModelMessage("message", "Директория пуста или не существует.");
                                }
                                FoldersFiles = GetDirectory(Users[ViewModelSend.Id - 1].temp_src);
                            }
                            if (FoldersFiles.Count == 0)
                            {
                                viewModelMessage = new ViewModelMessage("message", "Директория пуста или не существует.");
                            }
                            else
                            {
                                viewModelMessage = new ViewModelMessage("cd", JsonConvert.SerializeObject(FoldersFiles));
                            }
                        }
                        else
                        {
                            viewModelMessage = new ViewModelMessage("message", "Необходимо авторизоваться");
                        }
                        Reply = JsonConvert.SerializeObject(viewModelMessage);
                        byte[] message = Encoding.UTF8.GetBytes(Reply);
                        Handler.Send(message);
                    }
                    else if (DataCommand[0] == "get")
                    {
                        if (ViewModelSend.Id != -1)
                        {
                            string[] DataMessage = ViewModelSend.Message.Split(new string[1] { " " }, StringSplitOptions.None);
                            string getFile = string.Join(" ", DataMessage.Skip(1));
                            string fullFilePath = Path.Combine(Users[ViewModelSend.Id - 1].temp_src, getFile);
                            Console.WriteLine($"Trying to access file: {fullFilePath}");
                            if (File.Exists(fullFilePath))
                            {
                                byte[] byteFile = File.ReadAllBytes(fullFilePath);
                                viewModelMessage = new ViewModelMessage("file", JsonConvert.SerializeObject(byteFile));
                                string username = Users[ViewModelSend.Id - 1].login;
                                string password = Users[ViewModelSend.Id - 1].password;
                                var userId = Users.Find(x => x.login == username && x.password == password).Id;
                                LogCommandToDatabase(userId, "get");
                            }
                            else
                            {
                                viewModelMessage = new ViewModelMessage("message", "Файл не найден на сервере.");
                            }
                        }
                        else
                        {
                            viewModelMessage = new ViewModelMessage("message", "Необходимо авторизоваться");
                        }
                        Reply = JsonConvert.SerializeObject(viewModelMessage);
                        byte[] message = Encoding.UTF8.GetBytes(Reply);
                        Handler.Send(message);
                    }
                    else
                    {
                        if (ViewModelSend.Id != -1)
                        {
                            FileInfoFTP SendFileInfo = JsonConvert.DeserializeObject<FileInfoFTP>(ViewModelSend.Message);
                            string savePath = Path.Combine(Users[ViewModelSend.Id - 1].temp_src, SendFileInfo.Name);
                            File.WriteAllBytes(savePath, SendFileInfo.Data);
                            viewModelMessage = new ViewModelMessage("message", "Файл загружен");
                        }
                        else
                        {
                            viewModelMessage = new ViewModelMessage("message", "Необходимо авторизоваться");
                        }
                        Reply = JsonConvert.SerializeObject(viewModelMessage);
                        byte[] message = Encoding.UTF8.GetBytes(Reply);
                        Handler.Send(message);
                    }
                }
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("Ошибка при обработке клиента: " + ex.Message);
            }
            finally
            {
                Handler.Shutdown(SocketShutdown.Both);
                Handler.Close();
            }
        }

        private static void LogCommandToDatabase(int userId, string command)
        {
            string connectionString = "server=127.0.0.1;port=3306;database=FTP;uid=root;";

            using (MySqlConnection conn = new MySqlConnection(connectionString))
            {
                conn.Open();
                string query = "INSERT INTO Commands (IdUser, Command, Date) VALUES (@IdUser, @Command, @Date)";
                using (MySqlCommand cmd = new MySqlCommand(query, conn))
                {
                    cmd.Parameters.AddWithValue("IdUser", userId);
                    cmd.Parameters.AddWithValue("@Command", command);
                    cmd.Parameters.AddWithValue("@Date", DateTime.Now);
                    cmd.ExecuteNonQuery();
                }
            }
        }

        private static void LoadUsersFromDatabase()
        {
            using (MySqlConnection conn = new MySqlConnection(connectionString))
            {
                conn.Open();
                string query = "SELECT Id, Login, Password, Src FROM Users";
                using (MySqlCommand cmd = new MySqlCommand(query, conn))
                {
                    using (MySqlDataReader reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            int id = reader.GetInt32("Id");
                            string login = reader["Login"].ToString();
                            string password = reader["Password"].ToString();
                            string src = reader["Src"].ToString();
                            Users.Add(new User(id, login, password, src));
                        }
                    }
                }
            }
        }
    }
}
