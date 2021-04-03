namespace ScrambleWebServer.Extension
{
    using System;

    public static class StringExtensions
    {
        public static byte[] GetBytes(this string input)
        {
            byte[] bytes = new byte[input.Length * sizeof(char)];
            Buffer.BlockCopy(input.ToCharArray(), 0, bytes, 0, bytes.Length);
            return bytes;
        }

        public static string GetString(this byte[] bytes)
        {
            char[] characters = new char[(int)System.Math.Ceiling((double)bytes.Length / sizeof(char))];
            Buffer.BlockCopy(bytes, 0, characters, 0, bytes.Length);
            return new string(characters);
        }
    }
}
