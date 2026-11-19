using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace SilverEcho.ForsakenWorld.Editor
{
    public static class MapPatcherFiles
    {
        public sealed class Report
        {
            public string MapName, WorldPath, SharedDataPath, BackupFolder;
            public int Added, Skipped, ChangedBlocks;
        }

        public static Report Patch(string folder, IReadOnlyList<string> gameRelativePaths,
            string gameRoot, MapPatcherGbk encoding)
        {
            if (gameRelativePaths == null || gameRelativePaths.Count == 0)
                throw new InvalidDataException("Додайте хоча б один Litmodel кнопкою +.");
            folder = Path.GetFullPath(folder);
            if (!Directory.Exists(folder)) throw new DirectoryNotFoundException(folder);
            string[] files = Directory.GetFiles(folder);
            string[] worlds = files.Where(p => string.Equals(Path.GetExtension(p), ".ecwld", StringComparison.OrdinalIgnoreCase)).ToArray();
            if (worlds.Length != 1)
                throw new InvalidDataException(worlds.Length == 0
                    ? "У вибраній папці немає .ecwld. Виберіть папку конкретної карти."
                    : "У папці декілька .ecwld. Залиште одну пару файлів карти в окремій папці.");
            string worldPath = worlds[0], map = Path.GetFileNameWithoutExtension(worldPath);
            string bsdPath = files.SingleOrDefault(p => string.Equals(Path.GetFileName(p), map + ".ecbsd", StringComparison.OrdinalIgnoreCase));
            if (bsdPath == null) throw new FileNotFoundException("Не знайдено парний файл " + map + ".ecbsd");
            if (string.IsNullOrWhiteSpace(gameRoot))
            {
                var parent = Directory.GetParent(folder);
                if (parent != null && string.Equals(parent.Name, "maps", StringComparison.OrdinalIgnoreCase) && parent.Parent != null)
                    gameRoot = parent.Parent.FullName;
                else throw new InvalidDataException("Вкажіть «Папка гри»: корінь з папкою LitModels. Автовизначення працює для <гра>/maps/<карта>.");
            }
            gameRoot = Path.GetFullPath(gameRoot);
            if (!Directory.Exists(gameRoot)) throw new DirectoryNotFoundException("Папка гри не існує: " + gameRoot);
            var inputs = new List<PwMapPatcher.Input>();
            var unique = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            int repeatedInputs = 0;
            foreach (string entry in gameRelativePaths)
            {
                string path = PwMapPatcher.NormalizePath(entry);
                string[] parts = path.Split('\\');
                if (parts.Length < 3 || !string.Equals(parts[0], "LitModels", StringComparison.OrdinalIgnoreCase))
                    throw new InvalidDataException("Шлях має починатися з LitModels\\" + map + "\\: " + path);
                if (!string.Equals(parts[1], map, StringComparison.OrdinalIgnoreCase))
                    throw new InvalidDataException("Litmodel належить карті «" + parts[1] + "», а вибрана карта — «" + map + "». Розмістіть BMD у LitModels\\" + map + "\\ і вкажіть цей шлях.");
                if (encoding.Encode(path).Length >= 260)
                    throw new InvalidDataException("Шлях перевищує обмеження клієнта MAX_PATH: " + path);
                if (!unique.Add(path)) { repeatedInputs++; continue; }
                string fullPath = Path.Combine(gameRoot, path.Replace('\\', Path.DirectorySeparatorChar));
                if (!File.Exists(fullPath)) throw new FileNotFoundException("Не знайдено Litmodel: " + fullPath, fullPath);
                inputs.Add(new PwMapPatcher.Input
                {
                    RelativePath = string.Join("\\", parts.Skip(2)),
                    Bmd = File.ReadAllBytes(fullPath)
                });
            }
            byte[] world = File.ReadAllBytes(worldPath), bsd = File.ReadAllBytes(bsdPath);
            string terrainPath = files.SingleOrDefault(p => string.Equals(Path.GetFileName(p), map + ".trn2", StringComparison.OrdinalIgnoreCase));
            if (terrainPath != null) ValidateTerrain(File.ReadAllBytes(terrainPath), world);
            var patch = PwMapPatcher.Build(world, bsd, inputs, encoding.Decoder, encoding.Encode);
            var report = new Report
            {
                MapName = map, WorldPath = worldPath, SharedDataPath = bsdPath,
                Added = patch.Added, Skipped = patch.Skipped + repeatedInputs, ChangedBlocks = patch.ChangedBlocks
            };
            if (patch.Added == 0) return report;

            // Validate the generated files with the same independent read pass before touching disk.
            PwMapPatcher.Build(patch.World, patch.SharedData, Array.Empty<PwMapPatcher.Input>(), encoding.Decoder, encoding.Encode);
            string suffix = Guid.NewGuid().ToString("N");
            string worldTemp = worldPath + "." + suffix + ".tmp", bsdTemp = bsdPath + "." + suffix + ".tmp";
            string backup = Path.Combine(folder, "MapPatcher-backup-" + DateTime.Now.ToString("yyyyMMdd-HHmmss") + "-" + suffix.Substring(0, 8));
            bool bsdReplaced = false;
            try
            {
                File.WriteAllBytes(worldTemp, patch.World);
                File.WriteAllBytes(bsdTemp, patch.SharedData);
                EnsureUnchanged(worldPath, world);
                EnsureUnchanged(bsdPath, bsd);
                Directory.CreateDirectory(backup);
                File.Copy(worldPath, Path.Combine(backup, Path.GetFileName(worldPath)));
                File.Copy(bsdPath, Path.Combine(backup, Path.GetFileName(bsdPath)));
                // New BSD is a strict extension; the old world stays readable until its replacement.
                File.Replace(bsdTemp, bsdPath, null);
                bsdReplaced = true;
                File.Replace(worldTemp, worldPath, null);
                report.BackupFolder = backup;
            }
            catch (Exception error)
            {
                if (bsdReplaced)
                {
                    try { File.Copy(Path.Combine(backup, Path.GetFileName(bsdPath)), bsdPath, true); }
                    catch (Exception rollback)
                    {
                        throw new IOException("Не вдалося завершити патч і відновити ECBSD. Резервна копія: " + backup,
                            new AggregateException(error, rollback));
                    }
                }
                throw new IOException("Карту не пропатчено. " + error.Message +
                    (Directory.Exists(backup) ? " Резервна копія: " + backup : ""), error);
            }
            finally
            {
                TryDeleteTemporary(worldTemp);
                TryDeleteTemporary(bsdTemp);
            }
            return report;
        }

        private static void ValidateTerrain(byte[] terrain, byte[] world)
        {
            var r = new PwMapCursor(terrain, "TRN2");
            if (r.U32() != 0x54524e32 || r.U32() != 1) throw new InvalidDataException("Невідомий формат TRN2.");
            float width = r.Float(), length = r.Float();
            var w = new PwMapCursor(world, "ECWLD");
            w.Seek(8);
            if (Math.Abs(width - w.Float()) > 0.01f || Math.Abs(length - w.Float()) > 0.01f)
                throw new InvalidDataException("Розміри TRN2 та ECWLD не збігаються.");
        }

        private static void EnsureUnchanged(string path, byte[] original)
        {
            if (!File.ReadAllBytes(path).SequenceEqual(original))
                throw new IOException("Файл змінився під час підготовки патча: " + path);
        }

        private static void TryDeleteTemporary(string path)
        {
            try { if (File.Exists(path)) File.Delete(path); }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }
    }
}
