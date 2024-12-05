using Server.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using MySql.Data.MySqlClient;

namespace Server
{
    public class DB
    {
        public static readonly string connection = "server=localhost;port=3303;database=FTP;uid=root;";

        public static List<User> AllUsers()
        {
            List<User> users = new List<User>();
            MySqlConnection mySqlConnection = new MySqlConnection(connection);
            mySqlConnection.Open();
            MySqlCommand mySqlCommand = new MySqlCommand("SELECT * FROM `Users`", mySqlConnection);
            MySqlDataReader mySqlDataReader = mySqlCommand.ExecuteReader();
            while (mySqlDataReader.Read())
            {
                users.Add(new User(
                    mySqlDataReader.GetInt32("Id"),
                    mySqlDataReader.GetString("Login"),
                    mySqlDataReader.GetString("Password"),
                    mySqlDataReader.GetString("Src")
                    ));
            }
            mySqlConnection.Close();
            return users;
        }
    }
}
