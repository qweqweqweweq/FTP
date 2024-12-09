using Common;
using Newtonsoft.Json;
using Server.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace Server
{
    internal class Program
    {
        public static List<User> Users = new List<User>();
        public static IPAddress IpAddress;
        public static int Port;

        static void Main(string[] args)
        {
            Console.Write("Введите IP адрес сервера: ");
            string sIpAddress = Console.ReadLine();
            Console.Write("Введите порт: ");
            string sPort = Console.ReadLine();
            if (int.TryParse(sPort, out Port) && IPAddress.TryParse(sIpAddress, out IpAddress))
            {
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine("Данные успешно введены. Запускаю сервер.");
                StartServer();
            }
            Console.Read();
        }

        public static bool AuthUser(string login, string password, out User authUser)
        {
            authUser = Users.FirstOrDefault(x => x.login == login && x.password == password);
            return authUser != null;
        }

        public static bool AuthorizationUser(string login, string password, out int IdUser)
        {
            IdUser = -1;
            User user = Users.Find(x => x.login == login && x.password == password); ;
            if (user != null)
            {
                IdUser = user.Id;
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

        private static void HandleClient(Socket Handler)
        {
            try
            {
                string Data = null;
                byte[] Bytes = new byte[10485760];
                int BytesRec = Handler.Receive(Bytes);
                Data += Encoding.UTF8.GetString(Bytes, 0, BytesRec);
                Console.WriteLine("Сообщение от пользователя: " + Data + "\n");
                string Reply = "";
                ViewModelSend viewModelSend = JsonConvert.DeserializeObject<ViewModelSend>(Data);
                if (viewModelSend != null)
                {
                    ViewModelMessage viewModelMessage;
                    string[] DataCommand = viewModelSend.Message.Split(new string[1] { " " }, StringSplitOptions.None);
                    if (DataCommand[0] == "connect")
                    {
                        string[] DataMessage = viewModelSend.Message.Split(new string[1] { " " }, StringSplitOptions.None);
                        if (AuthorizationUser(DataMessage[1], DataMessage[2], out int IdUser))
                        {
                            IdUser = Users.Find(x => x.login == DataMessage[1] && x.password == DataMessage[2]).Id;
                            viewModelMessage = new ViewModelMessage("authorization", IdUser.ToString());
                            string login = Users.Find(x => x.login == DataMessage[1] && x.password == DataMessage[2]).login;
                            string password = Users.Find(x => x.login == DataMessage[1] && x.password == DataMessage[2]).password;
                            LogCommands(IdUser, viewModelSend.Message.Split(' ')[0]);
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
                        if (viewModelSend.Id != -1)
                        {
                            string[] DataMessage = viewModelSend.Message.Split(new string[1] { " " }, StringSplitOptions.None);
                            List<string> FoldersFiles = new List<string>();
                            if (DataMessage.Length == 1)
                            {
                                Users[viewModelSend.Id].temp_src = Users[viewModelSend.Id].src;
                                FoldersFiles = GetDirectory(Users[viewModelSend.Id].src);
                            }
                            else
                            {
                                string cdFolder = string.Join(" ", DataMessage.Skip(1));
                                Users[viewModelSend.Id].temp_src = Path.Combine(Users[viewModelSend.Id - 1].temp_src, cdFolder);
                                FoldersFiles = GetDirectory(Users[viewModelSend.Id - 1].temp_src);
                            }
                            if (FoldersFiles.Count == 0)
                                viewModelMessage = new ViewModelMessage("message", "Директория пуста или не существует.");
                            else
                                viewModelMessage = new ViewModelMessage("cd", JsonConvert.SerializeObject(FoldersFiles));
                        }
                        else
                            viewModelMessage = new ViewModelMessage("message", "Необходимо авторизоваться!");
                        Reply = JsonConvert.SerializeObject(viewModelMessage);
                        byte[] message = Encoding.UTF8.GetBytes(Reply);
                        Handler.Send(message);
                    }
                    else if (DataCommand[0] == "get")
                    {
                        if (viewModelSend.Id != -1)
                        {
                            string[] DataMessage = viewModelSend.Message.Split(new string[1] { " " }, StringSplitOptions.None);
                            string getFile = string.Join(" ", DataMessage.Skip(1));
                            string fullFilePath = Path.Combine(Users[viewModelSend.Id - 1].temp_src, getFile);
                            Console.WriteLine($"Получение доступа к файлу: {fullFilePath}");

                            if (File.Exists(fullFilePath))
                            {
                                byte[] byteFile = File.ReadAllBytes(fullFilePath);
                                viewModelMessage = new ViewModelMessage("file", JsonConvert.SerializeObject(byteFile));
                                string login = Users[viewModelSend.Id - 1].login;
                                string password = Users[viewModelSend.Id - 1].password;
                                var IdUser = Users.Find(x => x.login == login && x.password == password).id;
                                LogCommands(IdUser, "get");
                            }
                            else
                                viewModelMessage = new ViewModelMessage("message", "Файл не найден.");
                        }
                        else
                            viewModelMessage = new ViewModelMessage("message", "Необходимо авторизоваться!");

                        Reply = JsonConvert.SerializeObject(viewModelMessage);
                        byte[] message = Encoding.UTF8.GetBytes(Reply);
                        Handler.Send(message);
                    }
                    else
                    {
                        if (viewModelSend.Id != -1)
                        {
                            FileInfoFTP SendFileInfo = JsonConvert.DeserializeObject<FileInfoFTP>(viewModelSend.Message);
                            string savePath = Path.Combine(Users[viewModelSend.Id - 1].temp_src, SendFileInfo.Name);
                            File.WriteAllBytes(savePath, SendFileInfo.Data);
                            viewModelMessage = new ViewModelMessage("message", "Файл загружен");
                        }
                        else
                            viewModelMessage = new ViewModelMessage("message", "Необходимо авторизоваться");

                        Reply = JsonConvert.SerializeObject(viewModelMessage);
                        byte[] message = Encoding.UTF8.GetBytes(Reply);
                        Handler.Send(message);
                    }
                }
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"Ошибка: {ex.Message}");
            }
            finally
            {
                Handler.Shutdown(SocketShutdown.Both);
                Handler.Close();
            }
        }

        private static void LogCommands(int IdUser, string Command)
        {

        }

        public static void StartServer()
        {
            IPEndPoint endPoint = new IPEndPoint(IpAddress, Port);
            Socket sListener = new Socket(
                AddressFamily.InterNetwork,
                SocketType.Stream,
                ProtocolType.Tcp);
            sListener.Bind(endPoint);
            sListener.Listen(10);
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine("Сервер запущен.");
            while (true)
            {
                try
                {
                    Socket Handler = sListener.Accept();
                    
                }
                catch (Exception ex)
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine("Что-то случилось: " + ex.Message);
                }
            }
        }
    }
}
