using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace XFramework.Editor
{
    public class AudioSplitterWindow : EditorWindow
    {
        private const string MenuPath = "XFramework/Audio/SplitAudio";
        private static readonly List<string> OutputFormatLabels = new List<string>
        {
            "源格式优先",
            "WAV PCM"
        };

        [SerializeField] private float m_SilenceThresholdDb = -38f;
        [SerializeField] private float m_MinSilenceSeconds = 0.08f;
        [SerializeField] private float m_MinSegmentSeconds = 0.05f;
        [SerializeField] private float m_PaddingSeconds = 0.015f;
        [SerializeField] private OutputFormat m_OutputFormat = OutputFormat.SourceWhenSupported;
        [SerializeField] private bool m_RestoreSourceImporter = true;

        private readonly List<string> m_AudioClipPaths = new List<string>();
        private VisualElement m_ListContainer;
        private Button m_SplitButton;

        [MenuItem(MenuPath)]
        private static void Open()
        {
            GetWindow<AudioSplitterWindow>("音频切片");
        }

        public void CreateGUI()
        {
            BuildUI();
            AddSelectionAudioClips();
        }

        private void BuildUI()
        {
            VisualElement root = rootVisualElement;
            root.Clear();
            root.style.paddingLeft = 10;
            root.style.paddingRight = 10;
            root.style.paddingTop = 10;
            root.style.paddingBottom = 10;

            root.Add(new Label("音频切片")
            {
                style =
                {
                    unityFontStyleAndWeight = FontStyle.Bold,
                    fontSize = 16,
                    marginBottom = 8
                }
            });

            root.Add(CreateSlider("静音阈值 dB", m_SilenceThresholdDb, -60f, -10f, value => m_SilenceThresholdDb = value));
            root.Add(CreateSlider("最短静音秒数", m_MinSilenceSeconds, 0.02f, 0.5f, value => m_MinSilenceSeconds = value));
            root.Add(CreateSlider("最短片段秒数", m_MinSegmentSeconds, 0.02f, 1f, value => m_MinSegmentSeconds = value));
            root.Add(CreateSlider("片段前后留白秒数", m_PaddingSeconds, 0f, 0.1f, value => m_PaddingSeconds = value));

            PopupField<string> outputFormatField = new PopupField<string>("输出格式", OutputFormatLabels, (int)m_OutputFormat)
            {
                tooltip = "源文件为 WAV 时保持 WAV；MP3/OGG 等压缩格式会输出 WAV。"
            };
            outputFormatField.RegisterValueChangedCallback(evt => m_OutputFormat = (OutputFormat)OutputFormatLabels.IndexOf(evt.newValue));
            root.Add(outputFormatField);

            Toggle restoreToggle = new Toggle("完成后恢复源导入设置")
            {
                value = m_RestoreSourceImporter,
                tooltip = "切片时会临时把源音频设置为可读取；开启后会在完成时恢复源音频导入设置。"
            };
            restoreToggle.RegisterValueChangedCallback(evt => m_RestoreSourceImporter = evt.newValue);
            root.Add(restoreToggle);

            root.Add(new HelpBox(
                "输出位置：每个源音频旁边生成一个同名 _Slices 文件夹。Unity 当前只能从采样数据写出 WAV；源文件是 MP3/OGG 时会输出 WAV。",
                HelpBoxMessageType.Info));

            root.Add(CreateDropArea());

            Button addSelectionButton = new Button(AddSelectionAudioClips)
            {
                text = "添加选中的音频",
                tooltip = "把 Project 当前选中的音频加入列表。"
            };
            addSelectionButton.style.marginTop = 6;
            root.Add(addSelectionButton);

            m_ListContainer = new VisualElement();
            m_ListContainer.style.marginTop = 8;
            root.Add(m_ListContainer);

            VisualElement buttonRow = new VisualElement
            {
                style =
                {
                    flexDirection = FlexDirection.Row,
                    marginTop = 10
                }
            };

            m_SplitButton = new Button(SplitAudioClips)
            {
                text = "开始切片",
                tooltip = "按静音段切分列表中的音频。"
            };
            m_SplitButton.style.flexGrow = 1;
            buttonRow.Add(m_SplitButton);

            Button clearButton = new Button(ClearAudioClips)
            {
                text = "清空",
                tooltip = "清空音频列表。"
            };
            clearButton.style.marginLeft = 6;
            buttonRow.Add(clearButton);
            root.Add(buttonRow);

            RebuildList();
        }

        private static Slider CreateSlider(string label, float value, float lowValue, float highValue, Action<float> onChange)
        {
            Slider slider = new Slider(label, lowValue, highValue)
            {
                value = value,
                showInputField = true
            };
            slider.RegisterValueChangedCallback(evt => onChange(evt.newValue));
            return slider;
        }

        private VisualElement CreateDropArea()
        {
            VisualElement dropArea = new VisualElement
            {
                tooltip = "把 Project 里的 AudioClip 或音频文件拖到这里。"
            };
            dropArea.style.height = 68;
            dropArea.style.marginTop = 10;
            dropArea.style.borderBottomColor = Color.gray;
            dropArea.style.borderTopColor = Color.gray;
            dropArea.style.borderLeftColor = Color.gray;
            dropArea.style.borderRightColor = Color.gray;
            dropArea.style.borderBottomWidth = 1;
            dropArea.style.borderTopWidth = 1;
            dropArea.style.borderLeftWidth = 1;
            dropArea.style.borderRightWidth = 1;
            dropArea.style.justifyContent = Justify.Center;
            dropArea.style.alignItems = Align.Center;

            dropArea.Add(new Label("拖拽音频到这里"));
            dropArea.RegisterCallback<DragUpdatedEvent>(_ =>
            {
                DragAndDrop.visualMode = HasAudioClipInDrag() ? DragAndDropVisualMode.Copy : DragAndDropVisualMode.Rejected;
            });
            dropArea.RegisterCallback<DragPerformEvent>(_ =>
            {
                if (!HasAudioClipInDrag())
                {
                    return;
                }

                DragAndDrop.AcceptDrag();
                AddDraggedAudioClips();
            });

            return dropArea;
        }

        private void AddSelectionAudioClips()
        {
            bool changed = false;
            foreach (UnityEngine.Object selectedObject in Selection.objects)
            {
                changed |= TryAddAudioClipPath(AssetDatabase.GetAssetPath(selectedObject));
            }

            if (changed)
            {
                RebuildList();
            }
        }

        private void AddDraggedAudioClips()
        {
            bool changed = false;
            foreach (UnityEngine.Object draggedObject in DragAndDrop.objectReferences)
            {
                changed |= TryAddAudioClipPath(AssetDatabase.GetAssetPath(draggedObject));
            }

            foreach (string draggedPath in DragAndDrop.paths)
            {
                changed |= TryAddAudioClipPath(draggedPath);
            }

            if (changed)
            {
                RebuildList();
            }
        }

        private bool TryAddAudioClipPath(string assetPath)
        {
            if (string.IsNullOrEmpty(assetPath))
            {
                return false;
            }

            assetPath = assetPath.Replace('\\', '/');
            if (m_AudioClipPaths.Contains(assetPath))
            {
                return false;
            }

            if (AssetDatabase.LoadAssetAtPath<AudioClip>(assetPath) == null)
            {
                return false;
            }

            m_AudioClipPaths.Add(assetPath);
            return true;
        }

        private bool HasAudioClipInDrag()
        {
            foreach (UnityEngine.Object draggedObject in DragAndDrop.objectReferences)
            {
                if (AssetDatabase.LoadAssetAtPath<AudioClip>(AssetDatabase.GetAssetPath(draggedObject)) != null)
                {
                    return true;
                }
            }

            foreach (string draggedPath in DragAndDrop.paths)
            {
                if (AssetDatabase.LoadAssetAtPath<AudioClip>(draggedPath.Replace('\\', '/')) != null)
                {
                    return true;
                }
            }

            return false;
        }

        private void ClearAudioClips()
        {
            m_AudioClipPaths.Clear();
            RebuildList();
        }

        private void RebuildList()
        {
            if (m_ListContainer == null)
            {
                return;
            }

            m_ListContainer.Clear();
            if (m_AudioClipPaths.Count == 0)
            {
                m_ListContainer.Add(new Label("还没有添加音频。"));
            }
            else
            {
                for (int i = 0; i < m_AudioClipPaths.Count; i++)
                {
                    int index = i;
                    VisualElement row = new VisualElement
                    {
                        style =
                        {
                            flexDirection = FlexDirection.Row,
                            alignItems = Align.Center,
                            marginBottom = 3
                        }
                    };

                    row.Add(new Label(m_AudioClipPaths[i])
                    {
                        style =
                        {
                            flexGrow = 1,
                            unityTextAlign = TextAnchor.MiddleLeft
                        }
                    });

                    row.Add(new Button(() =>
                    {
                        m_AudioClipPaths.RemoveAt(index);
                        RebuildList();
                    })
                    {
                        text = "移除",
                        tooltip = "从列表移除该音频。"
                    });

                    m_ListContainer.Add(row);
                }
            }

            m_SplitButton?.SetEnabled(m_AudioClipPaths.Count > 0);
        }

        private void SplitAudioClips()
        {
            if (m_AudioClipPaths.Count == 0)
            {
                EditorUtility.DisplayDialog("音频切片", "请先添加一个或多个 AudioClip 资源。", "OK");
                return;
            }

            int totalWritten = 0;
            try
            {
                for (int i = 0; i < m_AudioClipPaths.Count; i++)
                {
                    string path = m_AudioClipPaths[i];
                    EditorUtility.DisplayProgressBar("正在切分音频", path, (float)i / m_AudioClipPaths.Count);
                    totalWritten += SplitClip(path);
                }
            }
            finally
            {
                EditorUtility.ClearProgressBar();
                AssetDatabase.Refresh();
            }

            EditorUtility.DisplayDialog("音频切片", $"已生成 {totalWritten} 个片段。", "OK");
        }

        private int SplitClip(string assetPath)
        {
            AudioImporter importer = AssetImporter.GetAtPath(assetPath) as AudioImporter;
            if (importer == null)
            {
                return 0;
            }

            AudioImporterSampleSettings originalSettings = importer.defaultSampleSettings;
            bool originalForceToMono = importer.forceToMono;

            try
            {
                AudioImporterSampleSettings readableSettings = originalSettings;
                readableSettings.loadType = AudioClipLoadType.DecompressOnLoad;
                readableSettings.compressionFormat = AudioCompressionFormat.PCM;
                importer.defaultSampleSettings = readableSettings;
                importer.forceToMono = false;
                importer.SaveAndReimport();

                AudioClip clip = AssetDatabase.LoadAssetAtPath<AudioClip>(assetPath);
                if (clip == null || clip.samples <= 0 || clip.channels <= 0)
                {
                    return 0;
                }

                float[] samples = new float[clip.samples * clip.channels];
                if (!clip.GetData(samples, 0))
                {
                    Debug.LogWarning($"[AudioSplitter] Failed to read samples: {assetPath}");
                    return 0;
                }

                List<Segment> segments = DetectSegments(samples, clip.samples, clip.channels, clip.frequency);
                if (segments.Count == 0)
                {
                    Debug.LogWarning($"[AudioSplitter] No voiced segment detected: {assetPath}");
                    return 0;
                }

                string outputFolder = CreateOutputFolder(assetPath, clip.name);
                string outputExtension = GetOutputExtension(assetPath);
                int written = 0;
                for (int i = 0; i < segments.Count; i++)
                {
                    Segment segment = segments[i];
                    string outputPath = $"{outputFolder}/{clip.name}_{i + 1:00}{outputExtension}";
                    WriteWav(outputPath, samples, clip.channels, clip.frequency, segment.StartFrame, segment.EndFrame);
                    written++;
                }

                Debug.Log($"[AudioSplitter] Split {assetPath} into {written} clip(s): {outputFolder}");
                return written;
            }
            finally
            {
                if (m_RestoreSourceImporter)
                {
                    importer.defaultSampleSettings = originalSettings;
                    importer.forceToMono = originalForceToMono;
                    importer.SaveAndReimport();
                }
            }
        }

        private List<Segment> DetectSegments(float[] samples, int frameCount, int channels, int sampleRate)
        {
            List<Segment> segments = new List<Segment>();
            float threshold = Mathf.Pow(10f, m_SilenceThresholdDb / 20f);
            int minSilenceFrames = Mathf.Max(1, Mathf.RoundToInt(m_MinSilenceSeconds * sampleRate));
            int minSegmentFrames = Mathf.Max(1, Mathf.RoundToInt(m_MinSegmentSeconds * sampleRate));
            int paddingFrames = Mathf.Max(0, Mathf.RoundToInt(m_PaddingSeconds * sampleRate));

            bool inSegment = false;
            int segmentStart = 0;
            int lastActiveFrame = 0;
            int silentFrames = 0;

            for (int frame = 0; frame < frameCount; frame++)
            {
                float amplitude = GetFrameAmplitude(samples, frame, channels);
                if (amplitude >= threshold)
                {
                    if (!inSegment)
                    {
                        inSegment = true;
                        segmentStart = Mathf.Max(0, frame - paddingFrames);
                    }

                    lastActiveFrame = frame;
                    silentFrames = 0;
                }
                else if (inSegment)
                {
                    silentFrames++;
                    if (silentFrames >= minSilenceFrames)
                    {
                        AddSegment(segments, segmentStart, Mathf.Min(frameCount, lastActiveFrame + paddingFrames + 1), minSegmentFrames);
                        inSegment = false;
                        silentFrames = 0;
                    }
                }
            }

            if (inSegment)
            {
                AddSegment(segments, segmentStart, Mathf.Min(frameCount, lastActiveFrame + paddingFrames + 1), minSegmentFrames);
            }

            return segments;
        }

        private static void AddSegment(List<Segment> segments, int startFrame, int endFrame, int minSegmentFrames)
        {
            if (endFrame - startFrame >= minSegmentFrames)
            {
                segments.Add(new Segment(startFrame, endFrame));
            }
        }

        private static float GetFrameAmplitude(float[] samples, int frame, int channels)
        {
            float amplitude = 0f;
            int sampleOffset = frame * channels;
            for (int channel = 0; channel < channels; channel++)
            {
                amplitude = Mathf.Max(amplitude, Mathf.Abs(samples[sampleOffset + channel]));
            }

            return amplitude;
        }

        private static string CreateOutputFolder(string assetPath, string clipName)
        {
            string parentFolder = Path.GetDirectoryName(assetPath)?.Replace('\\', '/');
            string outputFolder = $"{parentFolder}/{clipName}_Slices";

            if (!AssetDatabase.IsValidFolder(outputFolder))
            {
                AssetDatabase.CreateFolder(parentFolder, $"{clipName}_Slices");
            }

            return outputFolder;
        }

        private string GetOutputExtension(string assetPath)
        {
            if (m_OutputFormat == OutputFormat.SourceWhenSupported)
            {
                string extension = Path.GetExtension(assetPath);
                if (string.Equals(extension, ".wav", StringComparison.OrdinalIgnoreCase))
                {
                    return extension;
                }

                Debug.LogWarning($"[AudioSplitter] {assetPath} is {extension}. Unity does not provide an encoder for that format here, so slices are written as WAV.");
            }

            return ".wav";
        }

        private static void WriteWav(string assetPath, float[] source, int channels, int sampleRate, int startFrame, int endFrame)
        {
            string fullPath = Path.GetFullPath(assetPath);
            int frameCount = Mathf.Max(0, endFrame - startFrame);
            int sampleCount = frameCount * channels;
            const int bytesPerSample = 2;
            int byteCount = sampleCount * bytesPerSample;

            using FileStream stream = new FileStream(fullPath, FileMode.Create, FileAccess.Write);
            using BinaryWriter writer = new BinaryWriter(stream);

            writer.Write(new byte[] { (byte)'R', (byte)'I', (byte)'F', (byte)'F' });
            writer.Write(36 + byteCount);
            writer.Write(new byte[] { (byte)'W', (byte)'A', (byte)'V', (byte)'E' });
            writer.Write(new byte[] { (byte)'f', (byte)'m', (byte)'t', (byte)' ' });
            writer.Write(16);
            writer.Write((short)1);
            writer.Write((short)channels);
            writer.Write(sampleRate);
            writer.Write(sampleRate * channels * bytesPerSample);
            writer.Write((short)(channels * bytesPerSample));
            writer.Write((short)16);
            writer.Write(new byte[] { (byte)'d', (byte)'a', (byte)'t', (byte)'a' });
            writer.Write(byteCount);

            int sourceOffset = startFrame * channels;
            for (int i = 0; i < sampleCount; i++)
            {
                float value = Mathf.Clamp(source[sourceOffset + i], -1f, 1f);
                writer.Write((short)Mathf.RoundToInt(value * short.MaxValue));
            }
        }

        private readonly struct Segment
        {
            public Segment(int startFrame, int endFrame)
            {
                StartFrame = startFrame;
                EndFrame = endFrame;
            }

            public int StartFrame { get; }
            public int EndFrame { get; }
        }

        private enum OutputFormat
        {
            SourceWhenSupported,
            WavPcm
        }
    }
}
