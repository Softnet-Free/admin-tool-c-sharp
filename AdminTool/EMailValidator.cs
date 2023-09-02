using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Globalization;
using System.Text.RegularExpressions;

namespace AdminTool
{
    class EMailValidator
    {
        public static bool IsValid(string email)
        {
            try
            {
                email = Regex.Replace(email, @"(@)(.+)$", DomainMapper, RegexOptions.None);
            }
            catch (ArgumentException)
            {
                return false;
            }

            try
            {
                return Regex.IsMatch(email, @"^[^@\s]+@[^@\s]+\.[^@\s]+$", RegexOptions.IgnoreCase);
            }
            catch (ArgumentException)
            {
                return false;
            }
        }

        protected static string DomainMapper(Match match)
        {
            var idn = new IdnMapping();
            string domainName = idn.GetAscii(match.Groups[2].Value);
            return match.Groups[1].Value + domainName;
        }
    }
}
