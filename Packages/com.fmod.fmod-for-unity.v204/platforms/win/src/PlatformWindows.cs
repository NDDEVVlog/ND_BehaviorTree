using System.Collections.Generic;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

#if !UNITY_EDITOR
namespace FMOD
{
    public partial class VERSION
    {
#if UNITY_STANDALONE_WIN
        public const string dll = "fmodstudio" + suffix;
#endif
    }
}

namespace FMOD.Studio
{
    public partial class STUDIO_VERSION
    {
#if UNITY_STANDALONE_WIN
        public const string dll = "fmodstudio" + VERSION.suffix;
#endif
    }
}
#endif

namespace FMODUnity
{
#if UNITY_EDITOR
    [InitializeOnLoad]
#endif
    public class PlatformWindows : Platform
    {
        static PlatformWindows()
        {
            Settings.AddPlatformTemplate<PlatformWindows>("2c5177b11d81d824dbb064f9ac8527da");
        }

        internal override string DisplayName { get { return "Windows"; } }
        internal override string FolderName { get { return "win"; } }
        internal override void DeclareRuntimePlatforms(Settings settings)
        {
            settings.DeclareRuntimePlatform(RuntimePlatform.WindowsPlayer, this);
        }

#if UNITY_EDITOR
        internal override IEnumerable<BuildTarget> GetBuildTargets()
        {
            yield return BuildTarget.StandaloneWindows;
            yield return BuildTarget.StandaloneWindows64;
        }

        internal override Legacy.Platform LegacyIdentifier { get { return Legacy.Platform.Windows; } }

        protected override IEnumerable<FileRecord> GetBinaryFiles(BuildTarget buildTarget, bool allVariants, string suffix)
        {
            string dllSuffix = suffix + ".dll";

            switch (buildTarget)
            {
                case BuildTarget.StandaloneWindows:
                    yield return new FileRecord("x86/fmodstudio" + dllSuffix);
                    break;
                case BuildTarget.StandaloneWindows64:
#if UNITY_2023_1_OR_NEWER
                    yield return new FileRecord("arm64/fmodstudio" + dllSuffix);
#endif
                    yield return new FileRecord("x86_64/fmodstudio" + dllSuffix);
                    break;
                default:
                    throw new System.NotSupportedException("Unrecognised Build Target");
            }
        }

        protected override IEnumerable<FileRecord> GetOptionalBinaryFiles(BuildTarget buildTarget, bool allVariants)
        {
            switch (buildTarget)
            {
                case BuildTarget.StandaloneWindows:
                    yield return new FileRecord("x86/gvraudio.dll");
                    yield return new FileRecord("x86/resonanceaudio.dll");
                    break;
                case BuildTarget.StandaloneWindows64:
                    yield return new FileRecord("x86_64/gvraudio.dll");
                    yield return new FileRecord("x86_64/resonanceaudio.dll");
                    break;
                default:
                    throw new System.NotSupportedException("Unrecognised Build Target");
            }
        }

        internal override bool SupportsAdditionalCPP(BuildTarget target)
        {
            return true;
        }
#endif

        internal override string GetPluginPath(string pluginName)
        {
            return string.Format("{0}/{1}/{2}.dll", GetPluginBasePath(), RuntimeUtils.GetPluginArchitectureFolder(), pluginName);
        }
#if UNITY_EDITOR
        internal override OutputType[] ValidOutputTypes
        {
            get
            {
                return sValidOutputTypes;
            }
        }

        private static OutputType[] sValidOutputTypes = {
           new OutputType() { displayName = "Windows Audio Session API", outputType = FMOD.OUTPUTTYPE.WASAPI },
           new OutputType() { displayName = "Windows Sonic", outputType = FMOD.OUTPUTTYPE.WINSONIC },
        };

        internal override int CoreCount { get { return MaximumCoreCount; } }
#endif

        internal override List<CodecChannelCount> DefaultCodecChannels { get { return staticCodecChannels; } }

        private static List<CodecChannelCount> staticCodecChannels = new List<CodecChannelCount>()
        {
            new CodecChannelCount { format = CodecType.FADPCM, channels = 0 },
            new CodecChannelCount { format = CodecType.Vorbis, channels = 32 },
        };
    }
}
