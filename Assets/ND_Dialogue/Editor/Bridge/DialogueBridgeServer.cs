using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text;
using System.Threading;
using UnityEditor;
using UnityEngine;

namespace ND.Dialogue.Editor.Bridge
{
    /// <summary>
    /// Localhost HTTP endpoint of the dialogue AI bridge (same protocol as ND_BehaviorTree's):
    ///   POST http://127.0.0.1:{port}/   header X-NDBT-Token: {token}   body {"command": "...", "args": {...}}
    ///   → {"ok": true, "result": ...} or {"ok": false, "error": "..."}
    /// Runs only while <see cref="DialogueAiAccess.IsActive"/>. Port and per-session token are written to
    /// Library/NDDialogue/bridge.json (deleted when the server stops). Requests run on the main thread; the server
    /// stops before every domain reload and restarts afterwards, trying the next ports if the old one is still held.
    /// </summary>
    [InitializeOnLoad]
    public static class DialogueBridgeServer
    {
        private const string LogTag = "[ND Dialogue Bridge]";
        private const int RequestTimeoutMs = 30000;
        private const int PortFallbacks = 10;

        private sealed class PendingRequest
        {
            public string Body;
            public string Response;
            public readonly ManualResetEventSlim Done = new ManualResetEventSlim(false);
        }

        private static HttpListener s_Listener;
        private static string s_Token;
        private static int s_RunningPort;
        private static int s_ConfiguredPort;
        private static readonly ConcurrentQueue<PendingRequest> s_Queue = new ConcurrentQueue<PendingRequest>();

        public static bool IsRunning => s_Listener != null && s_Listener.IsListening;
        public static int RunningPort => s_RunningPort;
        public static string LastError { get; private set; }
        public static string InfoFilePath => Path.Combine(Path.GetDirectoryName(Application.dataPath), "Library", "NDDialogue", "bridge.json");

        static DialogueBridgeServer()
        {
            DialogueAiAccess.Changed += Sync;
            AssemblyReloadEvents.beforeAssemblyReload += Stop;
            EditorApplication.quitting += Stop;
            EditorApplication.update += Pump;
            EditorApplication.delayCall += Sync;
        }

        /// <summary>Starts or stops the server to match the access state.</summary>
        public static void Sync()
        {
            bool shouldRun = DialogueAiAccess.IsActive;
            if (shouldRun && (!IsRunning || s_ConfiguredPort != DialogueAiAccess.Port)) { Stop(); Start(); }
            else if (!shouldRun && IsRunning) Stop();
        }

        private static void Start()
        {
            s_ConfiguredPort = DialogueAiAccess.Port;
            string firstError = null;
            for (int offset = 0; offset <= PortFallbacks; offset++)
            {
                int port = s_ConfiguredPort + offset;
                if (port > 65535) break;
                try
                {
                    s_Token = Guid.NewGuid().ToString("N");
                    s_RunningPort = port;
                    s_Listener = new HttpListener();
                    s_Listener.Prefixes.Add($"http://127.0.0.1:{port}/");
                    s_Listener.Start();
                    new Thread(ListenLoop) { IsBackground = true, Name = "ND Dialogue Bridge" }.Start();
                    WriteInfoFile();
                    LastError = offset == 0 ? null : $"Port {s_ConfiguredPort} was busy ({firstError}); using {port}.";
                    Debug.Log($"{LogTag} Listening on 127.0.0.1:{port} ({DialogueAiAccess.Level}). Connection info: {InfoFilePath}");
                    return;
                }
                catch (Exception e)
                {
                    firstError ??= e.Message;
                    try { s_Listener?.Close(); } catch (Exception) { /* never started */ }
                    s_Listener = null;
                }
            }
            LastError = firstError;
            Debug.LogError($"{LogTag} Could not start on ports {s_ConfiguredPort}–{s_ConfiguredPort + PortFallbacks}: {firstError}. Change the port in Project Settings → ND Dialogue → AI Bridge.");
            Stop();
        }

        public static void Stop()
        {
            try { s_Listener?.Stop(); s_Listener?.Close(); } catch (Exception) { /* already closed */ }
            s_Listener = null;
            s_Token = null;
            try { if (File.Exists(InfoFilePath)) File.Delete(InfoFilePath); } catch (Exception) { /* best effort */ }
            while (s_Queue.TryDequeue(out var pending))
            {
                pending.Response = Error("Unity is reloading scripts; retry in a few seconds.");
                pending.Done.Set();
            }
        }

        private static void WriteInfoFile()
        {
            Directory.CreateDirectory(Path.GetDirectoryName(InfoFilePath));
            var info = new Dictionary<string, object>
            {
                ["port"] = s_RunningPort,
                ["token"] = s_Token,
                ["project"] = Path.GetDirectoryName(Application.dataPath),
                ["unityVersion"] = Application.unityVersion,
                ["access"] = DialogueAiAccess.Level.ToString()
            };
            File.WriteAllText(InfoFilePath, DialogueJson.Serialize(info, pretty: true));
        }

        private static void ListenLoop()
        {
            var listener = s_Listener;
            while (listener != null && listener.IsListening)
            {
                HttpListenerContext context;
                try { context = listener.GetContext(); }
                catch (Exception) { break; } // stopped
                ThreadPool.QueueUserWorkItem(_ => Handle(context));
            }
        }

        private static void Handle(HttpListenerContext context)
        {
            string response;
            int status = 200;
            try
            {
                if (!IPAddress.IsLoopback(context.Request.RemoteEndPoint.Address)) { status = 403; response = Error("Only local connections are accepted."); }
                else if (context.Request.HttpMethod != "POST") { status = 405; response = Error("Use POST with a JSON body {\"command\", \"args\"}."); }
                else if (s_Token == null || context.Request.Headers["X-NDBT-Token"] != s_Token) { status = 401; response = Error("Invalid or missing token (read Library/NDDialogue/bridge.json)."); }
                else
                {
                    string body;
                    using (var reader = new StreamReader(context.Request.InputStream, Encoding.UTF8)) body = reader.ReadToEnd();
                    var pending = new PendingRequest { Body = body };
                    s_Queue.Enqueue(pending);
                    response = pending.Done.Wait(RequestTimeoutMs)
                        ? pending.Response
                        : Error("Unity did not answer in time (busy compiling, importing or showing a dialog).");
                }
            }
            catch (Exception e)
            {
                status = 500;
                response = Error(e.Message);
            }

            try
            {
                var bytes = Encoding.UTF8.GetBytes(response);
                context.Response.StatusCode = status;
                context.Response.ContentType = "application/json; charset=utf-8";
                context.Response.ContentLength64 = bytes.Length;
                context.Response.OutputStream.Write(bytes, 0, bytes.Length);
                context.Response.OutputStream.Close();
            }
            catch (Exception) { /* client went away */ }
        }

        /// <summary>Main thread: execute queued requests.</summary>
        private static void Pump()
        {
            int budget = 8;
            while (budget-- > 0 && s_Queue.TryDequeue(out var pending))
            {
                pending.Response = DialogueBridgeCommands.Execute(pending.Body);
                pending.Done.Set();
            }
        }

        internal static string Error(string message) =>
            DialogueJson.Serialize(new Dictionary<string, object> { ["ok"] = false, ["error"] = message });
    }
}
