namespace Semblance.Core
{
    /// <summary>
    /// A string hash that never changes between runs, platforms or runtimes (string.GetHashCode can).
    /// Used wherever a line or a choice must be the same every time for the same person and seed.
    /// </summary>
    public static class StableHash
    {
        /// <summary>FNV-1a, 32 bit, as a non-negative int.</summary>
        public static int Of(string text)
        {
            unchecked
            {
                uint h = 2166136261;
                if (text != null)
                    foreach (char c in text)
                    {
                        h ^= c;
                        h *= 16777619;
                    }
                return (int)(h & 0x7fffffff);
            }
        }

        public static int Of(string text, int salt) => Of(text + "|" + salt);

        /// <summary>A stable pick from a list of options.</summary>
        public static T Pick<T>(T[] options, string text, int salt = 0) => options[Of(text, salt) % options.Length];
    }
}
