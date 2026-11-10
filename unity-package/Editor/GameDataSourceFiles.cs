using System.IO;

namespace SilverEcho.ForsakenWorld.Editor
{
    internal static class GameDataSourceFiles
    {
        // Compare content, not timestamps: replacement files can retain both size and date.
        internal static bool SameContent(string source, string destination)
        {
            if (!File.Exists(destination)) return false;
            using (var a = File.OpenRead(source))
            using (var b = File.OpenRead(destination))
            {
                if (a.Length != b.Length) return false;
                var left = new byte[65536];
                var right = new byte[left.Length];
                int count;
                while ((count = a.Read(left, 0, left.Length)) > 0)
                {
                    int read = 0;
                    while (read < count)
                    {
                        int next = b.Read(right, read, count - read);
                        if (next == 0) return false;
                        read += next;
                    }
                    for (int i = 0; i < count; i++) if (left[i] != right[i]) return false;
                }
                return true;
            }
        }
    }
}
