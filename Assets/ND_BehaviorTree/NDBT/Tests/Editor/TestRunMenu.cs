using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.Compilation;
using UnityEditor.TestTools.TestRunner.Api;
using UnityEngine;

namespace ND_BehaviorTree.Editor.Tests
{
    /// <summary>
    /// Runs this package's EditMode tests in the open editor and writes a summary to Library/NDBT/test-results.txt
    /// (also logged). Lets tools that cannot drive the Test Runner window (e.g. the ndbt bridge via menu_run) run tests.
    /// </summary>
    public static class TestRunMenu
    {
        public const string ResultPath = "Library/NDBT/test-results.txt";
        /// <summary>
        /// Core and plugin test assemblies: every editor assembly named "ND_BehaviorTree*.Tests", plus the standalone
        /// "ND.*.Tests" modules (Localization, Dialogue…) — naming convention, no registration.
        /// </summary>
        public static string[] AssemblyNames() => CompilationPipeline.GetAssemblies(AssembliesType.Editor)
            .Select(a => a.name)
            .Where(n => (n.StartsWith("ND_BehaviorTree") || n.StartsWith("ND.")) && n.EndsWith(".Tests"))
            .OrderBy(n => n)
            .ToArray();

        private static TestRunnerApi s_Api;
        private static Callbacks s_Callbacks;

        [MenuItem("ND_BehaviorTree/Diagnostics/Run Editor Tests")]
        public static void Run()
        {
            Directory.CreateDirectory(Path.GetDirectoryName(ResultPath));
            File.WriteAllText(ResultPath, "status: running\n");

            if (s_Api == null)
            {
                s_Api = ScriptableObject.CreateInstance<TestRunnerApi>();
                s_Callbacks = new Callbacks();
                s_Api.RegisterCallbacks(s_Callbacks);
            }
            s_Api.Execute(new ExecutionSettings(new Filter { testMode = TestMode.EditMode, assemblyNames = AssemblyNames() }));
        }

        private sealed class Callbacks : ICallbacks
        {
            public void RunStarted(ITestAdaptor testsToRun) { }
            public void TestStarted(ITestAdaptor test) { }
            public void TestFinished(ITestResultAdaptor result) { }

            public void RunFinished(ITestResultAdaptor result)
            {
                var sb = new StringBuilder();
                sb.AppendLine("status: finished");
                sb.AppendLine($"passed: {result.PassCount}  failed: {result.FailCount}  skipped: {result.SkipCount}  inconclusive: {result.InconclusiveCount}");
                sb.AppendLine($"duration: {result.Duration:0.00}s");
                AppendFailures(result, sb);
                File.WriteAllText(ResultPath, sb.ToString());

                string summary = $"[ND_BehaviorTree Tests] passed {result.PassCount}, failed {result.FailCount}, skipped {result.SkipCount}.";
                if (result.FailCount > 0) Debug.LogError(summary + $" Details: {ResultPath}");
                else Debug.Log(summary);
            }

            private static void AppendFailures(ITestResultAdaptor result, StringBuilder sb)
            {
                if (!result.HasChildren)
                {
                    if (result.TestStatus == TestStatus.Failed)
                        sb.AppendLine($"FAIL {result.FullName}\n  {result.Message?.Trim().Replace("\n", "\n  ")}");
                    return;
                }
                foreach (var child in result.Children) AppendFailures(child, sb);
            }
        }
    }
}
