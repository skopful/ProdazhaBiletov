using System;
using System.Linq;

namespace ProdazhaBiletov
{
    public static class UserSession
    {
        public static int Id { get; set; }
        public static string Familiya { get; set; }
        public static string Imya { get; set; }
        public static string Otchestvo { get; set; }
        public static int RoleId { get; set; }
        public static string Role { get; set; }

        public static bool IsCashier
        {
            get
            {
                return RoleId == 2 ||
                       (Role != null && Role.IndexOf("касс", StringComparison.CurrentCultureIgnoreCase) >= 0);
            }
        }

        public static bool IsAdmin
        {
            get
            {
                return !IsCashier && Role != null &&
                       Role.Trim().StartsWith("админ", StringComparison.CurrentCultureIgnoreCase);
            }
        }

        public static string ShortName
        {
            get
            {
                if (string.IsNullOrEmpty(Imya))
                    return Familiya;
                return Imya.Substring(0, 1) + ". " + Familiya;
            }
        }

        public static string FullName
        {
            get { return string.Join(" ", new[] { Familiya, Imya, Otchestvo }.Where(p => !string.IsNullOrWhiteSpace(p))); }
        }

        public static void Clear()
        {
            Id = 0;
            Familiya = null;
            Imya = null;
            Otchestvo = null;
            RoleId = 0;
            Role = null;
        }
    }
}