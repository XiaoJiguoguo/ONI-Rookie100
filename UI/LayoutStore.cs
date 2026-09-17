using System;
using System.IO;
using Newtonsoft.Json;

namespace Rookie100.UI
{
    /// <summary>窗口位置/尺寸记忆，持久化到模组目录 ui_layout.json。</summary>
    public static class LayoutStore
    {
        private static string contentPath;

        public class WindowLayout
        {
            public float X { get; set; }
            public float Y { get; set; }
            public float Width { get; set; }
            public float Height { get; set; }
        }

        public static void SetContentPath(string path)
        {
            contentPath = path;
        }

        private static string JsonPath => contentPath == null ? null : Path.Combine(contentPath, "ui_layout.json");

        public static WindowLayout Load()
        {
            try
            {
                var path = JsonPath;
                if (path != null && File.Exists(path))
                {
                    return JsonConvert.DeserializeObject<WindowLayout>(File.ReadAllText(path));
                }
            }
            catch (Exception e)
            {
                ModLogger.Warn("读取 ui_layout.json 失败: " + e.Message);
            }

            return null;
        }

        public static void Save(WindowLayout layout)
        {
            try
            {
                var path = JsonPath;
                if (path == null)
                {
                    return;
                }

                File.WriteAllText(path, JsonConvert.SerializeObject(layout, Formatting.Indented));
            }
            catch (Exception e)
            {
                ModLogger.Warn("写入 ui_layout.json 失败: " + e.Message);
            }
        }
    }
}
