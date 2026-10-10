using System;
using System.Linq;
using BenheimQoL.GreydwarfResident;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

ResidentSpeechContext context = ResidentSpeechContract.CreateContext(
    "evening", "Rain", "Meadows", wet: true, cold: false,
    tubBurning: true, playerSeated: false, new[] { "The coals are kind." });
const string prompt = "George prompt bytes, including its final newline.\n";
JObject request = ResidentSpeechContract.BuildRequest(context, prompt);

Expect(request["model"]!.Value<string>() == "google/gemini-3.5-flash-lite", "fixed model");
Expect(request["max_tokens"]!.Value<int>() == 120, "token limit");
Expect(request["temperature"]!.Value<double>() == .8, "temperature");
JObject schema = (JObject)request["response_format"]!["json_schema"]!["schema"]!;
Expect(schema["additionalProperties"]!.Value<bool>() == false, "schema rejects extra properties");
Expect(schema["required"]!.Values<string>().SequenceEqual(new[] { "speak", "text" }), "schema requires exact fields");
Expect(schema["properties"]!["speak"]!["type"]!.Value<string>() == "boolean", "speak schema type");
Expect(schema["properties"]!["text"]!["type"]!.Value<string>() == "string", "text schema type");
JArray messages = (JArray)request["messages"]!;
Expect(messages.Count == 2, "system and context messages");
Expect(messages[0]!["role"]!.Value<string>() == "system" && messages[0]!["content"]!.Value<string>() == prompt,
    "system prompt is embedded unchanged");
Expect(messages[1]!["role"]!.Value<string>() == "user", "context is user content");
JObject sentContext = JObject.Parse(messages[1]!["content"]!.Value<string>()!);
Expect(sentContext.Properties().Select(property => property.Name).SequenceEqual(new[]
{
    "event", "dayPart", "weather", "biome", "wet", "cold", "tubBurning", "playerSeated", "recentRemarks"
}), "context has the exact ordered nine fields");
Expect(ResidentSpeechContract.ParseContext(sentContext).TubBurning, "native tub activation maps to tubBurning");

ResidentSpeechProviderResponse valid = ParseResponse("{\"speak\":true,\"text\":\"  Warm water suits you.  \"}");
Expect(valid.FailureReason is null && valid.Remark?.Speak == true, "valid speech parsed");
Expect(valid.Remark!.Text == "Warm water suits you.", "spoken line is trimmed");
Expect(valid.Model == "google/gemini-3.5-flash-lite", "safe actual model retained");
Expect(valid.Usage["prompt_tokens"] == 17 && valid.Usage["cost"] == .0002,
    "only whitelisted numeric usage is retained");
Expect(!valid.Usage.ContainsKey("provider_extra") && !valid.Usage.ContainsKey("completion_tokens"),
    "arbitrary and nonnumeric usage is omitted");

ResidentSpeechProviderResponse silence = ParseResponse("{\"speak\":false,\"text\":\"\"}");
Expect(silence.FailureReason is null && silence.Remark is { Speak: false, Text: "" }, "strict silence shape accepted");

ExpectFailure("{\"speak\":true,\"text\":\"hello\",\"extra\":1}", "invalid_remark", "extra reply property");
ExpectFailure("{\"speak\":true}", "invalid_remark", "missing reply property");
ExpectFailure("{\"speak\":\"true\",\"text\":\"hello\"}", "invalid_remark", "wrong reply type");
ExpectFailure("{\"speak\":false,\"text\":\"not silent\"}", "invalid_remark", "silence must have empty raw text");
ExpectFailure("{\"speak\":true,\"text\":\"   \"}", "invalid_remark", "empty speech");
ExpectFailure("{\"speak\":true,\"text\":\"<color=red>hello</color>\"}", "invalid_remark", "markup rejected");
ExpectFailure("{\"speak\":true,\"text\":\"hello\\nthere\"}", "invalid_remark", "control characters rejected");
ExpectFailure("{\"speak\":true,\"text\":\"" + new string('x', 141) + "\"}", "invalid_remark", "speech length bound");

Expect(ResidentSpeechContract.ParseProviderResponse("not-json", ResidentSpeechContract.Model).FailureReason ==
    "provider_response_json", "malformed provider body");
Expect(ResidentSpeechContract.ParseProviderResponse("{}", ResidentSpeechContract.Model).FailureReason ==
    "model_content_missing", "provider body without content");
Expect(ParseResponse("not-json").FailureReason == "model_reply_json", "malformed model content");
ResidentSpeechProviderResponse oversizedContent = ParseResponse("not-json-" + new string('x', 5000));
Expect(oversizedContent.ContentExcerpt?.Length == 4096 && oversizedContent.ContentTruncated,
    "trace content excerpt is bounded");

ExpectThrows(() => ResidentSpeechContract.ParseContext(JObject.Parse("""
    {"event":"approach","dayPart":"dusk","weather":"Rain","biome":"Meadows","wet":true,
     "cold":false,"tubBurning":true,"playerSeated":false,"recentRemarks":[]}
    """)), "invalid_context", "unknown day part rejected");
ExpectThrows(() => ResidentSpeechContract.ParseContext(JObject.Parse("""
    {"event":"approach","dayPart":"day","weather":"Rain","biome":"Meadows","wet":true,
     "cold":false,"tubBurning":true,"playerSeated":false,"recentRemarks":[],"extra":1}
    """)), "invalid_context", "extra context field rejected");
ExpectThrows(() => ResidentSpeechContract.ParseContext(JObject.Parse("""
    {"event":"approach","dayPart":"day","weather":"Rain","biome":"Meadows","wet":true,
     "cold":false,"tubBurning":true,"playerSeated":false,"recentRemarks":["<b>repeat</b>"]}
    """)), "invalid_context", "markup in recent remarks rejected");
ExpectThrows(() => ResidentSpeechContract.CreateContext("day", new string('w', 65), "Meadows",
    false, false, false, false, Array.Empty<string>()), "invalid_context", "context string length bound");

Console.WriteLine("Resident speech contract and parser checks passed");

static ResidentSpeechProviderResponse ParseResponse(string content)
{
    string body = JsonConvert.SerializeObject(new
    {
        model = "google/gemini-3.5-flash-lite",
        usage = new { prompt_tokens = 17, completion_tokens = "unknown", total_tokens = 24, cost = .0002, provider_extra = 9 },
        choices = new[] { new { message = new { content } } }
    });
    return ResidentSpeechContract.ParseProviderResponse(body, ResidentSpeechContract.Model);
}

static void ExpectFailure(string content, string reason, string scenario)
{
    ResidentSpeechProviderResponse result = ParseResponse(content);
    Expect(result.FailureReason == reason, scenario + ": expected " + reason + ", got " + result.FailureReason);
}

static void ExpectThrows(Action action, string reason, string scenario)
{
    try { action(); }
    catch (ResidentSpeechContractException exception) when (exception.Reason == reason) { return; }
    throw new InvalidOperationException(scenario + ": expected " + reason);
}

static void Expect(bool condition, string scenario)
{
    if (!condition) throw new InvalidOperationException(scenario);
}
