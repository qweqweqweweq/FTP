using Server.Models;
using System.Collections.Generic;
using System.Net;

namespace Server
{
    internal class Program
    {
        public static List<User> Users = new List<User>();
        public static IPAddress IpAddress;
        public static int Port;

        static void Main(string[] args)
        {
        }

        public static bool AuthorizationUser(string login, string password)
        {
            User user = null;
            user = Users.Find(x => x.login == login && x.password == password);
            return user != null;
        }
    }
