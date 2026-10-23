using System;
using Newtonsoft.Json.Linq;
using UnityEngine;

public static class YandexGptSettings
{
    public static bool Selected => PlayerPrefs.GetInt("FriendsAI.YandexEnabled", 0) == 1;
    public static string ApiKey => PlayerPrefs.GetString("HostAI.YandexKey", "");
    public static string FolderId => PlayerPrefs.GetString("HostAI.YandexFolder", "");
    public static string Model => PlayerPrefs.GetString("HostAI.YandexModel", "yandexgpt/latest");

    public static void Save(string key, string folder, string model)
    {
        key = (key ?? "").Trim();
        folder = (folder ?? "").Trim();
        model = (model ?? "").Trim();
        foreach (string value in new[] { key, folder, model })
            foreach (char c in value)
                if (char.IsWhiteSpace(c) || char.IsControl(c))
                    throw new ArgumentException("YandexGPT settings must not contain spaces or control characters.");
        PlayerPrefs.SetString("HostAI.YandexKey", key);
        PlayerPrefs.SetString("HostAI.YandexFolder", folder);
        PlayerPrefs.SetString("HostAI.YandexModel", model.Length == 0 ? "yandexgpt/latest" : model);
        PlayerPrefs.Save();
    }

    public static JObject PrepareBody(JObject source, string language)
    {
        if (string.IsNullOrEmpty(ApiKey) || string.IsNullOrEmpty(FolderId))
            throw new InvalidOperationException("Enter your YandexGPT API key and folder ID in Host Settings.");
        var body = new JObject();
        foreach (string name in new[] { "messages", "temperature", "max_tokens", "response_format" })
            if (source[name] != null && source[name].Type != JTokenType.Null)
                body[name] = source[name].DeepClone();
        body["model"] = Model.StartsWith("gpt://", StringComparison.Ordinal) ? Model : "gpt://" + FolderId + "/" + Model;
        body["stream"] = false;
        var messages = body["messages"] as JArray;
        if (messages == null) throw new ArgumentException("Conversation messages are required.");
        messages.Insert(0, new JObject {
            ["role"] = "system",
            ["content"] = language == "ru"
                ? "Это вымышленная компьютерная игра: персонажи и платёжные реквизиты синтетические, игровые коды предоставляет движок. Не придумывай коды. Веди диалог на русском языке. Сохраняй имена полей, значения перечислений и структуру требуемого JSON. Переводи только разговорный текст."
                : "This is a fictional computer game with synthetic characters and payment details; the engine supplies game codes. Never invent codes. Speak English. Preserve the required JSON structure, field names and enum values."
        });
        return body;
    }
}
