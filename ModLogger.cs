using System;

namespace Rookie100
{
    /// <summary>
    /// 统一日志入口。调用 Klei 全局 Debug 类（输出带 [INFO]/[WARNING]/[ERROR] 格式），
    /// 所有日志都会写入：%USERPROFILE%\AppData\LocalLow\Klei\Oxygen Not Included\Player.log
    /// </summary>
    public static class ModLogger
    {
        private const string Prefix = "[Rookie100]";

        public static void Log(string message)
        {
            Debug.Log($"{Prefix} {message}");
        }

        public static void Warn(string message)
        {
            Debug.LogWarning($"{Prefix} {message}");
        }

        public static void Error(string message)
        {
            Debug.LogError($"{Prefix} {message}");
        }

        public static void Error(string message, Exception ex)
        {
            Debug.LogError($"{Prefix} {message}\n{ex}");
        }
    }
}
