﻿namespace Server.Models
{
    public class User
    {
        public string login { get; set; }
        public string password { get; set; }
        public string src { get; set; }
        public string temp_src { get; set; }
        public User(string login, string password, string src)
        {
            this.login = login;
            this.password = password;
            this.src = src;
            temp_src = src;
        }
    }
}
