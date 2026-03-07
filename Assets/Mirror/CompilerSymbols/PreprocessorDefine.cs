using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Build;

namespace Mirror
{
    static class PreprocessorDefine
    {
        /// <summary>
        /// Add define symbols as soon as Unity gets done compiling.
        /// </summary>
        [InitializeOnLoadMethod]
        public static void AddDefineSymbols()
        {
#if UNITY_2021_2_OR_NEWER
            NamedBuildTarget namedTarget = GetValidNamedBuildTarget();
            string currentDefines = PlayerSettings.GetScriptingDefineSymbols(namedTarget);
#else
            // Deprecated in Unity 2023.1
            string currentDefines = PlayerSettings.GetScriptingDefineSymbolsForGroup(EditorUserBuildSettings.selectedBuildTargetGroup);
#endif
            // Remove oldest when adding next month's symbol.
            // Keep a rolling 12 months of symbols.
            HashSet<string> defines = new HashSet<string>(currentDefines.Split(';'))
            {
                "MIRROR",
                "MIRROR_89_OR_NEWER",
                "MIRROR_90_OR_NEWER",
                "MIRROR_93_OR_NEWER",
                "MIRROR_96_OR_NEWER"
            };

            // only touch PlayerSettings if we actually modified it,
            // otherwise it shows up as changed in git each time.
            string newDefines = string.Join(";", defines);
            if (newDefines != currentDefines)
            {
#if UNITY_2021_2_OR_NEWER
                PlayerSettings.SetScriptingDefineSymbols(namedTarget, newDefines);
#else
                // Deprecated in Unity 2023.1
                PlayerSettings.SetScriptingDefineSymbolsForGroup(EditorUserBuildSettings.selectedBuildTargetGroup, newDefines);
#endif
            }
        }

#if UNITY_2021_2_OR_NEWER
        /// <summary>
        /// Gets a valid NamedBuildTarget. In Unity 6 and some editor states, selectedBuildTargetGroup
        /// can map to an invalid (empty) target and cause ArgumentException. Fall back to Standalone.
        /// </summary>
        static NamedBuildTarget GetValidNamedBuildTarget()
        {
            try
            {
                var group = EditorUserBuildSettings.selectedBuildTargetGroup;
                var named = NamedBuildTarget.FromBuildTargetGroup(group);
                // Validate by reading defines; throws if target name is invalid (e.g. empty)
                PlayerSettings.GetScriptingDefineSymbols(named);
                return named;
            }
            catch (ArgumentException)
            {
                return NamedBuildTarget.Standalone;
            }
        }
#endif
    }
}
