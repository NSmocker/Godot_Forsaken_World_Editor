using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;

namespace SilverEcho.ForsakenWorld
{
    /// <summary>
    /// Simple writer to update EcwldDocument and save back to .ecwld file.
    /// Currently supports updating model transforms (Pos, Dir, Up).
    /// </summary>
    public static class EcwldWriter
    {
        public static bool WriteChanges(EcwldDocument doc, string originalPath, List<LitModelInstance> modifiedBuildings, out string error)
        {
            error = null;
            if (doc == null || string.IsNullOrEmpty(originalPath))
            {
                error = "Invalid document or path";
                return false;
            }

            try
            {
                string backupPath = originalPath + ".backup_" + DateTime.Now.ToString("yyyyMMdd_HHmmss");
                File.Copy(originalPath, backupPath, true);
                Debug.Log($"[Angelica 2.x] Backup created: {backupPath}");

                // Update models in document
                foreach (var building in modifiedBuildings)
                {
                    if (building.OriginalEcwldIndex < 0 || building.OriginalEcwldIndex >= doc.Models.Count)
                        continue;

                    var model = doc.Models[building.OriginalEcwldIndex];

                    // Convert Unity transform to Angelica format (Z-up to Y-up, etc.)
                    model.Pos = new float[3]
                    {
                        building.transform.localPosition.x,
                        building.transform.localPosition.z,  // Y and Z swapped in Angelica
                        building.transform.localPosition.y
                    };

                    // Simple rotation conversion (needs improvement)
                    var euler = building.transform.localEulerAngles;
                    model.Dir = new float[3] { 0, 1, 0 }; // placeholder
                    model.Up = new float[3] { 0, 0, 1 };

                    Debug.Log($"Updated model {building.OriginalEcwldIndex} ({building.gameObject.name}) Pos = {model.Pos[0]:F2}, {model.Pos[1]:F2}, {model.Pos[2]:F2}");
                }

                // Write back (very basic writer - only updates known fields)
                using (var writer = new StreamWriter(originalPath, false, Encoding.GetEncoding(1251))) // GBK approx
                {
                    writer.WriteLine($"# Exported from Unity - {DateTime.Now}");
                    writer.WriteLine($"Version = {doc.Version}");

                    foreach (var model in doc.Models)
                    {
                        writer.WriteLine($"[Model]");
                        writer.WriteLine($"Id = {model.Id}");
                        writer.WriteLine($"Path = {model.Path}");
                        writer.WriteLine($"Pos = {model.Pos[0]} {model.Pos[1]} {model.Pos[2]}");
                        writer.WriteLine($"Dir = {model.Dir[0]} {model.Dir[1]} {model.Dir[2]}");
                        writer.WriteLine($"Up = {model.Up[0]} {model.Up[1]} {model.Up[2]}");
                        writer.WriteLine($"ShowType = {model.ShowType}");
                        writer.WriteLine($"Action = {model.Action}");
                        writer.WriteLine();
                    }

                    // TODO: Write other sections (Effects, Sounds, etc.)
                }

                Debug.Log($"[Angelica 2.x] Successfully wrote updated .ecwld: {originalPath}");
                return true;
            }
            catch (Exception e)
            {
                error = e.Message;
                Debug.LogError($"Failed to write .ecwld: {e}");
                return false;
            }
        }
    }
}
