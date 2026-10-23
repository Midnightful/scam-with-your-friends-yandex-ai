using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using Cysharp.Threading.Tasks;
using Kolkata.Speech;
using Mirror;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UIElements;

public static class FriendsIntegration
{
    private static DropdownField backendControl;

    public static void Attach(VisualElement root)
    {
        var section = root.Q("host-ai-section");
        if (section == null || section.Q("friends-yandex") != null) return;
        var panel = new VisualElement { name = "friends-yandex" };
        panel.Add(new Label("Yandex AI Studio"));
        var key = new TextField("API key / API-ключ") { isPasswordField = true, value = YandexGptSettings.ApiKey };
        var folder = new TextField("Folder ID / Каталог") { value = YandexGptSettings.FolderId };
        var model = new TextField("Model / Модель") { value = YandexGptSettings.Model };
        var status = new Label();
        var save = new Button(() => { try { YandexGptSettings.Save(key.value, folder.value, model.value); status.text = "Saved / Сохранено"; } catch(Exception ex) { status.text = ex.Message; } }) { text = "Save / Сохранить" };
        panel.Add(key); panel.Add(folder); panel.Add(model); panel.Add(save); panel.Add(status); section.Add(panel);
        var backend = root.Q<DropdownField>("host-ai-backend");
        backendControl = backend;
        foreach (var field in new[] { key, folder, model }) {
            field.style.color = Color.black;
            field.labelElement.style.color = new Color(0.96f, 0.87f, 0.69f);
            var input = field.Q("unity-text-input");
            if (input != null) { input.style.color = Color.black; input.style.backgroundColor = new Color(0.92f, 0.92f, 0.92f); }
        }
        if (backend != null) {
            var choices = new List<string>(backend.choices);
            if (!choices.Contains("Yandex AI Studio")) choices.Add("Yandex AI Studio");
            backend.choices = choices;
            panel.RemoveFromHierarchy();
            var row = backend.parent;
            row.parent.Insert(row.parent.IndexOf(row) + 1, panel);
            panel.style.flexDirection = FlexDirection.Column; panel.style.alignSelf = Align.Stretch;
            panel.style.marginTop = 8; panel.style.marginBottom = 12; panel.style.paddingLeft = 16; panel.style.paddingRight = 16;
            SyncBackend(backend);
        }
    }

    public static bool SelectBackend(string value) {
        if (!NetworkServer.active) return value == "Yandex AI Studio";
        bool active = value == "Yandex AI Studio";
        PlayerPrefs.SetInt("FriendsAI.YandexEnabled", active ? 1 : 0); PlayerPrefs.Save();
        if (active && ScammerGameManager.Instance != null) ScammerGameManager.Instance.SetOfficialAiBackend(false);
        SyncBackend(backendControl);
        return active;
    }

    public static void SyncBackend(DropdownField backend) {
        if (backend == null) return;
        if (!backend.choices.Contains("Yandex AI Studio")) { var choices = new List<string>(backend.choices); choices.Add("Yandex AI Studio"); backend.choices = choices; }
        bool active = YandexGptSettings.Selected;
        if (active) backend.SetValueWithoutNotify("Yandex AI Studio");
        VisualElement root = backend;
        while (root.parent != null) root = root.parent;
        if (active) { var section = root.Q("host-ai-section"); if (section != null) section.style.display = NetworkServer.active ? DisplayStyle.Flex : DisplayStyle.None; backend.SetEnabled(NetworkServer.active); }
        var panel = root.Q("friends-yandex");
        if (panel != null) panel.style.display = active ? DisplayStyle.Flex : DisplayStyle.None;
        var openRouter = root.Q("openrouter-panel");
        if (active && openRouter != null) openRouter.style.display = DisplayStyle.None;
    }
    public static void ClearSpeechPreview() {
        if (NetworkClient.localPlayer == null) return;
        var voice = NetworkClient.localPlayer.GetComponent<KolkataPlayerVoice>();
        if (voice == null) return;
        var clear = typeof(KolkataPlayerVoice).GetMethod("ClearPartialTranscript", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        if (clear != null) clear.Invoke(voice, null);
    }
    public static void ConfigureWhisper(Whisper.WhisperManager recognizer) {
        if (recognizer == null) return;
        if (!recognizer.IsLoaded && !recognizer.IsLoading) {
            bool cpuOnly = Environment.GetEnvironmentVariable("KOLKATA_WHISPER_CPU_ONLY") == "1" || WindowsSpeechCompatibility.IsWindowsArm64 || WindowsSpeechCompatibility.NeedsCpuRecognition(SystemInfo.graphicsDeviceVendorID, SystemInfo.graphicsDeviceID, Environment.GetCommandLineArgs());
            var gpu = typeof(Whisper.WhisperManager).GetField("useGpu", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (gpu != null) gpu.SetValue(recognizer, !cpuOnly);
            Debug.Log("[FriendsAI] Whisper requested backend=" + (cpuOnly ? "CPU compatibility" : "GPU"));
        }
        recognizer.audioCtx = 0;
        recognizer.noContext = true;
        recognizer.translateToEnglish = false;
        recognizer.enableTokens = true;
        var model = System.IO.Path.Combine(Application.streamingAssetsPath, "Whisper", "ggml-small.bin");
        if (!recognizer.IsLoaded && !recognizer.IsLoading && System.IO.File.Exists(model)) {
            recognizer.ModelPath = "Whisper/ggml-small.bin";
            recognizer.IsModelPathInStreamingAssets = true;
            Debug.Log("[FriendsAI] Whisper multilingual small selected; language=" + recognizer.language);
        }
    }
    public static string NormalizeReplyText(string text) {
        if (string.IsNullOrEmpty(text)) return text;
        int first = text.IndexOf('{'), last = text.LastIndexOf('}');
        if (first < 0 || last <= first) return text;
        try {
            var body = JObject.Parse(text.Substring(first, last-first+1));
            if (body["dialogue"] == null && body["reply"]?.Type == JTokenType.String) {
                body["dialogue"] = body["reply"].DeepClone(); body.Remove("reply");
                return body.ToString(Formatting.None) + (text.TrimEnd().EndsWith("[[HANGUP]]", StringComparison.Ordinal) ? " [[HANGUP]]" : "");
            }
        } catch (JsonException) { }
        return text;
    }
    public static JObject NormalizeCompletion(JObject response) {
        var choices = response["choices"] as JArray;
        if (choices == null) return response;
        foreach (var choice in choices) {
            var message = choice["message"] as JObject;
            var text = (string)message?["content"];
            if (string.IsNullOrEmpty(text)) continue;
            int first = text.IndexOf('{'), last = text.LastIndexOf('}');
            if (first < 0 || last <= first) continue;
            JObject content;
            try { content = JObject.Parse(text.Substring(first, last-first+1)); } catch { continue; }
            if (content["dialogue"] == null && content["reply"]?.Type == JTokenType.String) {
                content["dialogue"] = content["reply"].DeepClone(); content.Remove("reply");
                // Preserve state fields: inventing missing trust/objectives could alter gameplay.
                message["content"] = content.ToString(Formatting.None);
                Debug.Log("[FriendsAI] Normalized reply to dialogue.");
            }
        }
        return response;
    }

    public static UniTask<JObject> Complete(JObject source, CancellationToken cancellation)
    {
        if (!NetworkServer.active || !NetworkServer.listen) throw new InvalidOperationException("YandexGPT requests require the lobby host.");
        var body = YandexGptSettings.PrepareBody(source, CallLanguage.Current.Code);
        var completion = new UniTaskCompletionSource<JObject>();
        var request = new UnityWebRequest("https://ai.api.cloud.yandex.net/v1/chat/completions", "POST");
        request.timeout = 60; request.redirectLimit = 0;
        request.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(body.ToString(Formatting.None)));
        request.downloadHandler = new DownloadHandlerBuffer();
        request.SetRequestHeader("Content-Type", "application/json");
        request.SetRequestHeader("Authorization", "Bearer " + YandexGptSettings.ApiKey);
        request.SetRequestHeader("OpenAI-Project", YandexGptSettings.FolderId);
        var registration = cancellation.Register(() => completion.TrySetCanceled(cancellation));
        request.SendWebRequest().completed += operation => {
            try {
                if (cancellation.IsCancellationRequested) completion.TrySetCanceled(cancellation);
                else if (request.result != UnityWebRequest.Result.Success) completion.TrySetException(new InvalidOperationException("YandexGPT HTTP " + request.responseCode + ". Check key, folder, model and balance."));
                else completion.TrySetResult(NormalizeCompletion(JObject.Parse(request.downloadHandler.text)));
            } catch (Exception ex) { completion.TrySetException(ex); }
            finally { registration.Dispose(); request.Dispose(); }
        };
        return completion.Task;
    }
}