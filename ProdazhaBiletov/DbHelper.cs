using System;
using System.Data.SqlClient;
using System.Security.Cryptography;
using System.Text;

namespace ProdazhaBiletov
{
    public static class DbHelper
    {
        public const string ConnectionString =
            "Data Source=stud-mssql.sttec.yar.ru,38325;Initial Catalog=user86_db;User ID=user86_db;Password=********";

        public static SqlConnection OpenConnection()
        {
            SqlConnection con = new SqlConnection(ConnectionString);
            con.Open();
            return con;
        }

        public static string HashPassword(string password)
        {
            using (SHA256 sha = SHA256.Create())
            {
                byte[] bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(password));
                StringBuilder sb = new StringBuilder();
                foreach (byte b in bytes)
                    sb.Append(b.ToString("x2"));
                return sb.ToString();
            }
        }
    }
}