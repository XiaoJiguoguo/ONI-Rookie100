using System;
using System.IO;
using Rookie100.Content;
using UnityEngine;
using UnityEngine.UI;

namespace Rookie100.UI
{
    /// <summary>A small transparent guide: idle while visible, cheer once on quest events.</summary>
    [RequireComponent(typeof(RawImage))]
    public sealed class GuideCompanion : MonoBehaviour
    {
        private const int Columns = 16;
        private const int Rows = 8;
        private const int IdleFrames = 41;
        private const int CheerFrames = 83;
        private const float FrameRate = 30f;
        private RawImage image;
        private Texture2D atlas;
        private float startedAt;
        private bool cheering;
        private bool loadAttempted;
        private int displayedFrame = -1;

        private void OnEnable()
        {
            image = GetComponent<RawImage>();
            image.raycastTarget = false;
            image.color = Color.white;
            if (!loadAttempted)
            {
                loadAttempted = true;
                LoadAtlas();
            }
            cheering = false;
            startedAt = Time.unscaledTime;
            displayedFrame = -1;
            if (atlas == null) return;
            QuestTracker.QuestCompleted += Celebrate;
            QuestTracker.QuestClaimed += Celebrate;
            DrawFrame(0);
        }

        private void LoadAtlas()
        {
            try
            {
                using (var stream = typeof(GuideCompanion).Assembly.GetManifestResourceStream(
                    "Rookie100.Resources.guide_dupe.png"))
                {
                    if (stream == null) throw new FileNotFoundException("Guide animation resource missing");
                    using (var bytes = new MemoryStream())
                    {
                        stream.CopyTo(bytes);
                        atlas = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                        if (!ImageConversion.LoadImage(atlas, bytes.ToArray(), true))
                            throw new InvalidDataException("Guide animation PNG could not be decoded");
                    }
                }
                if (atlas.width != Columns * 128 || atlas.height != Rows * 192)
                    throw new InvalidDataException("Unexpected guide atlas dimensions");
                atlas.name = "Rookie100GuideAtlas";
                atlas.filterMode = FilterMode.Bilinear;
                atlas.wrapMode = TextureWrapMode.Clamp;
                image.texture = atlas;
                image.enabled = true;
            }
            catch (Exception e)
            {
                if (atlas != null) Destroy(atlas);
                atlas = null;
                image.enabled = false;
                ModLogger.Warn("Guide animation unavailable: " + e.Message);
            }
        }

        private void Celebrate(QuestDef quest)
        {
            // Multiple quest events in one scan share one celebration; avoid restarting it.
            if (quest == null || cheering || atlas == null) return;
            cheering = true;
            startedAt = Time.unscaledTime;
            DrawFrame(IdleFrames);
        }

        private void Update()
        {
            if (atlas == null) return;
            int elapsedFrames = Mathf.FloorToInt((Time.unscaledTime - startedAt) * FrameRate);
            if (cheering && elapsedFrames >= CheerFrames)
            {
                cheering = false;
                startedAt = Time.unscaledTime;
                elapsedFrames = 0;
            }
            DrawFrame(cheering ? IdleFrames + elapsedFrames : elapsedFrames % IdleFrames);
        }

        private void DrawFrame(int frame)
        {
            if (frame == displayedFrame) return;
            displayedFrame = frame;
            // PNG rows start at the top; Unity UV coordinates start at the bottom.
            image.uvRect = new Rect((frame % Columns) / (float)Columns,
                1f - (frame / Columns + 1) / (float)Rows, 1f / Columns, 1f / Rows);
        }

        private void OnDisable()
        {
            QuestTracker.QuestCompleted -= Celebrate;
            QuestTracker.QuestClaimed -= Celebrate;
            cheering = false;
        }

        private void OnDestroy()
        {
            QuestTracker.QuestCompleted -= Celebrate;
            QuestTracker.QuestClaimed -= Celebrate;
            if (image != null) image.texture = null;
            if (atlas != null) Destroy(atlas);
            atlas = null;
        }
    }
}
