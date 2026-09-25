import { spawn } from "node:child_process";
import fs from "node:fs";
import path from "node:path";
import {
    renderHelp,
    renderNoSession,
    renderActiveSession,
    renderProtecting,
    renderProtectComplete,
    renderApplying,
    renderApplyComplete,
    renderApplyError,
    renderUnknownCommand,
    renderError,
    type SessionInfo,
    type ProtectResult,
    type ApplyResult,
} from "./ui/index.js";

const args = process.argv
    .slice(2)
    .filter(arg => arg !== "--");

if (args.length === 0) {
    renderHelp();
    process.exit(1);
}

const command = args[0];

switch (command) {
    case "protect":
        validateProtectArgs(args);
        await runProtect(args[1]!);
        break;

    case "apply":
        await runApply();
        break;

    case "status":
        await runStatus();
        break;

    case "help":
    case "--help":
    case "-h":
        renderHelp();
        break;

    default:
        renderUnknownCommand(command!);
        process.exit(1);
}

function validateProtectArgs(args: string[]): void {
    if (args.length !== 2) {
        renderError(
            "Invalid arguments",
            "The protect command requires exactly one file argument.",
            "Usage: aegis protect <file>"
        );
        process.exit(1);
    }
}

async function runProtect(filePath: string): Promise<void> {
    const resolvedPath = path.resolve(process.cwd(), filePath);

    const spinner = renderProtecting(path.basename(resolvedPath));

    // Give Ink a tick to render the spinner before handing stdio to the worker.
    await new Promise<void>(r => setTimeout(r, 80));

    spinner.unmount();

    await runWorker([
        "sanitize",
        process.cwd(),
        resolvedPath,
    ]);
}

async function runApply(): Promise<void> {
    const spinner = renderApplying();

    try {
        const output = await runWorkerCapture([
            "apply",
            process.cwd(),
        ]);

        spinner.unmount();

        if (output.includes("No Aegis sessions found")) {
            renderApplyError("No Aegis sessions found. Run aegis protect <file> first.");
            return;
        }

        if (output.includes("Multiple Aegis sessions found")) {
            renderApplyError("Multiple sessions found. The alpha requires exactly one session.");
            return;
        }

        // Parse the import output
        const result = parseApplyOutput(output);

        renderApplyComplete(result);
    } catch (error) {
        spinner.unmount();

        renderApplyError(
            error instanceof Error
                ? error.message
                : "An unknown error occurred during apply."
        );
    }
}

async function runStatus(): Promise<void> {
    try {
        const output = await runWorkerCapture([
            "status",
            process.cwd(),
        ]);

        const result = JSON.parse(output.trim());

        if (result.status === "none") {
            renderNoSession();
            return;
        }

        if (result.status === "multiple") {
            renderError(
                "Multiple active sessions",
                "The alpha requires exactly one active session."
            );
            return;
        }

        if (result.status === "ready") {
            const session: SessionInfo = {
                sessionId: result.sessionId,
                sessionPath: result.sessionPath,
                status: "Ready",
                files: result.files,
            };

            renderActiveSession(session);
            return;
        }

        renderError(
            "Unknown session state",
            "Aegis returned an unexpected session status."
        );
    } catch (error) {
        renderError(
            "Failed to get status",
            error instanceof Error
                ? error.message
                : "An unknown error occurred."
        );
    }
}

// ─── Worker communication ──────────────────────────────────────

function runWorker(args: string[]): Promise<void> {
    return new Promise((resolve, reject) => {
        const repoRoot = path.resolve(
            import.meta.dirname,
            "../../.."
        );

        const workerProject = path.join(
            repoRoot,
            "apps",
            "roslyn-worker",
            "RoslynWorker.csproj"
        );

        const worker = spawn(
            "dotnet",
            [
                "run",
                "--project",
                workerProject,
                "--",
                ...args,
            ],
            {
                cwd: repoRoot,
                stdio: "inherit",
            }
        );

        worker.on("error", reject);

        worker.on("close", (code: number | null) => {
            if (code === 0) {
                resolve();
                return;
            }

            reject(
                new Error(
                    `Aegis worker exited with code ${code ?? "unknown"}.`
                )
            );
        });
    });
}

function runWorkerCapture(args: string[]): Promise<string> {
    return new Promise((resolve, reject) => {
        const repoRoot = path.resolve(
            import.meta.dirname,
            "../../.."
        );

        const workerProject = path.join(
            repoRoot,
            "apps",
            "roslyn-worker",
            "RoslynWorker.csproj"
        );

        let stdout = "";
        let stderr = "";

        const worker = spawn(
            "dotnet",
            [
                "run",
                "--project",
                workerProject,
                "--",
                ...args,
            ],
            {
                cwd: repoRoot,
                stdio: ["pipe", "pipe", "pipe"],
            }
        );

        worker.stdout.on("data", (data: Buffer) => {
            stdout += data.toString();
        });

        worker.stderr.on("data", (data: Buffer) => {
            stderr += data.toString();
        });

        worker.on("error", reject);

        worker.on("close", (code: number | null) => {
            if (code === 0) {
                resolve(stdout);
                return;
            }

            reject(
                new Error(
                    stderr.trim() ||
                    `Aegis worker exited with code ${code ?? "unknown"}.`
                )
            );
        });
    });
}

// ─── Output parsers ────────────────────────────────────────────

function parseProtectOutput(output: string, filePath: string): ProtectResult {
    const lines = output.split("\n").map(l => l.trim());

    // Parse session path
    const sessionLine = lines.find(l => l.startsWith("Session:"));
    const sessionPath = sessionLine?.replace("Session:", "").trim() ?? "";

    // Parse sanitized directory
    const sanitizedLine = lines.find(l => l.startsWith("Sanitized:"));
    const sanitizedDir = sanitizedLine?.replace("Sanitized:", "").trim() ?? "";

    // Parse README path
    const readmeLine = lines.find(l => l.startsWith("README:"));
    const readmePath = readmeLine?.replace("README:", "").trim() ?? "";

    // Extract session ID from session path
    const sessionId = extractSessionId(sessionPath);

    // Count sensitive values from output
    const sensitiveLine = lines.find(l =>
        l.toLowerCase().includes("sensitive") &&
        l.toLowerCase().includes("replaced")
    );
    const sensitiveMatch = sensitiveLine?.match(/(\d+)/);
    const sensitiveCount = sensitiveMatch ? parseInt(sensitiveMatch[1]!, 10) : 0;

    return {
        sessionId,
        sessionPath,
        sanitizedDir,
        readmePath,
        files: [{ name: path.basename(filePath) }],
        sensitiveCount,
    };
}

function parseApplyOutput(output: string): ApplyResult {
    const lines = output.split("\n").map(l => l.trim());

    const success = !lines.some(l =>
        l.toLowerCase().includes("review") ||
        l.toLowerCase().includes("failed") ||
        l.toLowerCase().includes("error")
    );

    // Try to extract session ID
    const sessionIdLine = lines.find(l => l.includes("Session"));
    const sessionId = extractSessionId(sessionIdLine ?? "");

    return {
        success,
        sessionId: sessionId || "unknown",
        files: [],
        message: output.trim(),
    };
}

function extractSessionId(input: string): string {
    // Look for a hex session ID pattern (32 char hex string)
    const match = input.match(/[a-f0-9]{32}/i);

    return match?.[0] ?? "unknown";
}