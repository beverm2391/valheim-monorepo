const fs = require("node:fs") as typeof import("node:fs");

type ComparisonResult = {
    model: string;
    reasoningPolicy: string;
    latencyMs: number;
    text?: string;
    error?: string;
};

type ComparisonModel = {
    id: string;
    reasoningPolicy: string;
    reasoning?: { enabled: false } | { effort: "low" };
};

const OpenRouterUrl = "https://openrouter.ai/api/v1/chat/completions";
const ComparisonPanel: ComparisonModel[] = [
    {
        id: "x-ai/grok-4.6",
        reasoningPolicy: "mandatory; effort=low (lowest supported)",
        reasoning: { effort: "low" }
    },
    {
        id: "qwen/qwen3.8-27b",
        reasoningPolicy: "disabled (enabled=false)",
        reasoning: { enabled: false }
    },
    {
        id: "anthropic/claude-sonnet-5",
        reasoningPolicy: "disabled (enabled=false)",
        reasoning: { enabled: false }
    },
    {
        id: "openai/gpt-5.6-luna",
        reasoningPolicy: "disabled (enabled=false)",
        reasoning: { enabled: false }
    },
    {
        id: "thinkingmachines/inkling",
        reasoningPolicy: "disabled (enabled=false)",
        reasoning: { enabled: false }
    },
    {
        id: "aion-labs/aion-3.0",
        reasoningPolicy: "mandatory; provider default (no selectable effort)"
    },
    {
        id: "deepseek/deepseek-v4-flash-0731",
        reasoningPolicy: "disabled (enabled=false)",
        reasoning: { enabled: false }
    }
];

function usage(): never {
    console.error(
        "Usage:\n" +
        "  ts-node --transpile-only tools/crow-lab/compare.ts --prompt <text>\n" +
        "  ts-node --transpile-only tools/crow-lab/compare.ts --prompt-file <path>"
    );
    process.exit(2);
}

function fail(message: string): never {
    throw new Error(message);
}

function parseArguments(args: string[]): { prompt: string } {
    let prompt: string | undefined;
    let promptFile: string | undefined;

    for (let index = 0; index < args.length; index += 1) {
        const argument = args[index];
        if (!["--prompt", "--prompt-file"].includes(argument) || index + 1 >= args.length) {
            usage();
        }
        const value = args[index + 1];
        index += 1;
        if (argument === "--prompt") {
            if (prompt !== undefined) usage();
            prompt = value;
        } else if (argument === "--prompt-file") {
            if (promptFile !== undefined) usage();
            promptFile = value;
        }
    }

    if ((prompt === undefined) === (promptFile === undefined)) {
        return fail("supply exactly one of --prompt or --prompt-file");
    }
    if (promptFile !== undefined) {
        try {
            prompt = fs.readFileSync(promptFile, "utf8");
        } catch (error) {
            return fail(`could not read prompt file: ${error instanceof Error ? error.message : String(error)}`);
        }
    }
    if (prompt === undefined || prompt.length === 0) {
        return fail("prompt must not be empty");
    }

    return { prompt };
}

async function callModel(model: ComparisonModel, prompt: string, apiKey: string): Promise<ComparisonResult> {
    const started = Date.now();
    try {
        const response = await fetch(OpenRouterUrl, {
            method: "POST",
            headers: {
                Authorization: `Bearer ${apiKey}`,
                "Content-Type": "application/json",
                "X-OpenRouter-Title": "Benheim Raw Model Comparison"
            },
            body: JSON.stringify({
                model: model.id,
                messages: [{ role: "user", content: prompt }],
                ...(model.reasoning ? { reasoning: model.reasoning } : {})
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
            return fail("OpenRouter response did not contain text content");
        }
        return {
            model: model.id,
            reasoningPolicy: model.reasoningPolicy,
            latencyMs: Date.now() - started,
            text: content
        };
    } catch (error) {
        return {
            model: model.id,
            reasoningPolicy: model.reasoningPolicy,
            latencyMs: Date.now() - started,
            error: error instanceof Error ? error.message : String(error)
        };
    }
}

function printResults(results: ComparisonResult[]): void {
    for (const result of results) {
        console.log(`\n=== ${result.model} (${result.latencyMs} ms) ===`);
        console.log(`reasoning: ${result.reasoningPolicy}`);
        if (result.error) {
            console.log(`ERROR: ${result.error}`);
        } else {
            console.log(result.text);
        }
    }
}

async function main(): Promise<void> {
    const { prompt } = parseArguments(process.argv.slice(2));
    const apiKey = process.env.OPENROUTER_API_KEY?.trim();
    if (!apiKey) {
        return fail("OPENROUTER_API_KEY is required; use the supported scoped secret wrapper");
    }

    const results = await Promise.all(ComparisonPanel.map((model) => callModel(model, prompt, apiKey)));
    printResults(results);
    if (results.some((result) => result.error)) {
        process.exitCode = 1;
    }
}

main().catch((error) => {
    console.error(error instanceof Error ? error.message : String(error));
    process.exitCode = 1;
});
