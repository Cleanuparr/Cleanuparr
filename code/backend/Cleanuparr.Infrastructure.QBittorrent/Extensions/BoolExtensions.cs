using System;
using System.Collections.Generic;
using System.Text;

namespace QBittorrent.Client.Extensions
{
    internal static class BoolExtensions
    {
        private const string TrueString = "true";
        private const string FalseString = "false";

        public static string ToLowerString(this bool value) => value ? TrueString : FalseString;
    }
}
