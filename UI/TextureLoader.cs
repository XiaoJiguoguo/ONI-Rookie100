using System;
using System.Reflection;
using UnityEngine;

namespace Rookie100.UI
{
    // Bind byte[] explicitly: newer Unity also exposes ReadOnlySpan<byte>,
    // which cannot be resolved by the mod's net48 reference assemblies.
    internal static class TextureLoader
    {
        private static readonly Func<Texture2D, byte[], bool, bool> loadImage = Resolve();

        private static Func<Texture2D, byte[], bool, bool> Resolve()
        {
            var type = Type.GetType("UnityEngine.ImageConversion, UnityEngine.ImageConversionModule", true);
            var method = type.GetMethod("LoadImage", BindingFlags.Public | BindingFlags.Static,
                null, new[] { typeof(Texture2D), typeof(byte[]), typeof(bool) }, null);
            if (method == null) throw new MissingMethodException(type.FullName, "LoadImage(Texture2D, byte[], bool)");
            return (Func<Texture2D, byte[], bool, bool>)Delegate.CreateDelegate(
                typeof(Func<Texture2D, byte[], bool, bool>), method);
        }

        public static bool LoadImage(Texture2D texture, byte[] bytes, bool markNonReadable)
            => loadImage(texture, bytes, markNonReadable);
    }
}
