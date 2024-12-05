﻿namespace Server.Models
{
    public class User
    {
        public int Id { get; set; }
        public string login { get; set; }
        public string password { get; set; }
        public string src { get; set; }
        public string temp_src { get; set; }
        public User(int id, string login, string password, string src)
        {
            this.Id = id;
            this.login = login;
            this.password = password;
            this.src = src;
            temp_src = src;
        }
    }
}
