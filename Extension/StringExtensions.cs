namespace ScrambleWebServer.Extension
{
    public static class StringExtensions
    {
        public static byte[] GetBytes(this string input)
        {
            return System.Text.Encoding.Default.GetBytes(input);
        }

        public static string GetString(this byte[] bytes)
        {
            return System.Text.Encoding.Default.GetString(bytes);
        }
    }
}
