const fs = require("node:fs") as typeof import("node:fs");
const path = require("node:path") as typeof import("node:path");

type CrowResponse = {
    speak: boolean;
    text: string;
};

type ModelResult = {
    model: string;
    latencyMs: number;
    response?: CrowResponse;
    error?: string;
};

const OpenRouterUrl = "https://openrouter.ai/api/v1/chat/completions";
const PromptFiles = [
    { name: "base.md", required: true },
    { name: "player_lore.md", required: false },
    { name: "examples.md", required: false }
];
const LabRoot = __dirname;

function usage(): never {
    console.error(
        "Usage:\n" +
        "  ts-node --transpile-only tools/crow-lab/crow.ts <scenario.json> --model <model-id> [--dry-run]\n" +
        "  ts-node --transpile-only tools/crow-lab/crow.ts --validate-response '<json>'"
    );
    process.exit(2);
}

function fail(message: string): never {
    throw new Error(message);
}

function parseCrowResponse(content: string): CrowResponse {
    let value: unknown;
    try {
        value = JSON.parse(content);
    } catch {
        return fail("model response is not JSON");
    }

    if (value === null || typeof value !== "object" || Array.isArray(value)) {
        return fail("model response must be one JSON object");
    }

    const record = value as Record<string, unknown>;
    const keys = Object.keys(record).sort();
    if (keys.length !== 2 || keys[0] !== "speak" || keys[1] !== "text") {
        return fail("model response must contain exactly speak and text");
    }
    if (typeof record.speak !== "boolean" || typeof record.text !== "string") {
        return fail("speak must be boolean and text must be string");
    }
    if (!record.speak && record.text !== "") {
        return fail("text must be empty when speak is false");
    }
    if (record.speak && record.text.trim().length === 0) {
        return fail("text must be non-empty when speak is true");
    }

    return { speak: record.speak, text: record.text };
}

function readScenario(fileArgument: string): { path: string; value: Record<string, unknown> } {
    const scenarioPath = path.resolve(process.cwd(), fileArgument);
    let value: unknown;
    try {
        value = JSON.parse(fs.readFileSync(scenarioPath, "utf8"));
    } catch (error) {
        return fail(`could not read scenario: ${error instanceof Error ? error.message : String(error)}`);
    }

    if (value === null || typeof value !== "object" || Array.isArray(value)) {
        return fail("scenario must be one JSON object");
    }
    const scenario = value as Record<string, unknown>;
    if (!Array.isArray(scenario.timeline) || scenario.timeline.length === 0) {
        return fail("scenario.timeline must be a non-empty array");
    }

    let triggers = 0;
    for (const rawEntry of scenario.timeline) {
        if (rawEntry === null || typeof rawEntry !== "object" || Array.isArray(rawEntry)) {
            return fail("each timeline entry must be one object");
        }
        const entry = rawEntry as Record<string, unknown>;
        if (typeof entry.timestamp !== "string" || Number.isNaN(Date.parse(entry.timestamp))) {
            return fail("each timeline entry needs a valid timestamp");
        }
        if (!["global_chat", "crow_message", "gameplay_event"].includes(String(entry.kind))) {
            return fail("timeline kind must be global_chat, crow_message, or gameplay_event");
        }
        if (entry.trigger === true) {
            if (entry.kind !== "gameplay_event") {
                return fail("the triggering entry must be a gameplay_event");
            }
            triggers += 1;
        }
    }
    if (triggers !== 1) {
        return fail("scenario must contain exactly one triggering gameplay event");
    }

    return { path: scenarioPath, value: scenario };
}

function assemblePrompt(): string {
    const sections: string[] = [];
    for (const promptFile of PromptFiles) {
        const promptPath = path.join(LabRoot, "prompts", promptFile.name);
        if (!fs.existsSync(promptPath)) {
            if (promptFile.required) {
                return fail(`required prompt file is missing: ${promptFile.name}`);
            }
            continue;
        }

        let content: string;
        try {
            content = fs.readFileSync(promptPath, "utf8").trim();
        } catch (error) {
            return fail(`could not read prompt file ${promptFile.name}: ${error instanceof Error ? error.message : String(error)}`);
        }
        if (!content) {
            if (promptFile.required) {
                return fail(`required prompt file is empty: ${promptFile.name}`);
            }
            continue;
        }
        sections.push(content);
    }
    return sections.join("\n\n");
}

function parseModel(args: string[]): { model: string; dryRun: boolean } {
    let dryRun = false;
    let model = "";

    for (let index = 0; index < args.length; index += 1) {
        const argument = args[index];
        if (argument === "--dry-run") {
            dryRun = true;
            continue;
        }
        if (argument !== "--model") {
            usage();
        }
        if (model || index + 1 >= args.length) {
            usage();
        }
        model = args[index + 1].trim();
        index += 1;
    }

    if (!model) {
        return fail("supply one OpenRouter model ID");
    }
    return { model, dryRun };
}

async function callModel(
    model: string,
    prompt: string,
    scenario: Record<string, unknown>,
    apiKey: string
): Promise<ModelResult> {
    const started = Date.now();
    try {
        const response = await fetch(OpenRouterUrl, {
            method: "POST",
            headers: {
                Authorization: `Bearer ${apiKey}`,
                "Content-Type": "application/json",
                "X-OpenRouter-Title": "Benheim Crow Lab"
            },
            body: JSON.stringify({
                model,
                messages: [
                    { role: "system", content: prompt },
                    {
                        role: "user",
                        content: "Respond to this unified Valheim timeline. The entry with trigger=true is the current beat.\n\n" +
                            JSON.stringify(scenario, null, 2)
                    }
                ],
                response_format: {
                    type: "json_schema",
                    json_schema: {
                        name: "crow_response",
                        strict: true,
                        schema: {
                            type: "object",
                            properties: {
                                speak: { type: "boolean" },
                                text: { type: "string" }
                            },
                            required: ["speak", "text"],
                            additionalProperties: false
                        }
                    }
                },
                provider: { require_parameters: true },
                temperature: 1,
                max_tokens: 100
            })
        });

        const body = await response.json() as Record<string, any>;
        if (!response.ok) {
            const message = typeof body?.error?.message === "string"
                ? body.error.message
                : `HTTP ${response.status}`;
            return fail(`OpenRouter ${response.status}: ${message}`);
        }
        const content = body?.choices?.[0]?.message?.content;
        if (typeof content !== "string") {
            return fail("OpenRouter response did not contain message content");
        }

        return {
            model,
            latencyMs: Date.now() - started,
            response: parseCrowResponse(content)
        };
    } catch (error) {
        return {
            model,
            latencyMs: Date.now() - started,
            error: error instanceof Error ? error.message : String(error)
        };
    }
}

function printResults(results: ModelResult[]): void {
    for (const result of results) {
        console.log(`\n=== ${result.model} (${result.latencyMs} ms) ===`);
        if (result.response) {
            console.log(JSON.stringify(result.response));
        } else {
            console.log(`ERROR: ${result.error}`);
        }
    }
}

async function main(): Promise<void> {
    const args = process.argv.slice(2);
    if (args[0] === "--validate-response") {
        if (args.length !== 2) {
            usage();
        }
        console.log(JSON.stringify(parseCrowResponse(args[1])));
        return;
    }
    if (args.length < 3 || args[0].startsWith("--")) {
        usage();
    }

    const scenario = readScenario(args[0]);
    const prompt = assemblePrompt();
    const { model, dryRun } = parseModel(args.slice(1));
    if (dryRun) {
        parseCrowResponse('{"speak":false,"text":""}');
        console.log(`scenario=${path.relative(process.cwd(), scenario.path)}`);
        console.log(`timeline_entries=${(scenario.value.timeline as unknown[]).length}`);
        console.log(`prompt_chars=${prompt.length}`);
        console.log(`model=${model}`);
        console.log("response_validation=ok");
        return;
    }

    const apiKey = process.env.OPENROUTER_API_KEY?.trim();
    if (!apiKey) {
        return fail("OPENROUTER_API_KEY is required; use the supported scoped secret wrapper");
    }

    const result = await callModel(model, prompt, scenario.value, apiKey);
    printResults([result]);
    if (result.error) {
        process.exitCode = 1;
    }
}

main().catch((error) => {
    console.error(error instanceof Error ? error.message : String(error));
    process.exitCode = 1;
});
