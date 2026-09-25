import { spawn } from "node:child_process";
import path from "node:path";

const args = process.argv
    .slice(2)
    .filter(arg => arg !== "--");

if (args.length === 0) {
    printUsage();
    process.exit(1);
}

const command = args[0];

switch (command) {
    case "protect":
        validateProtectArgs(args);
        await runWorker([
            "sanitize",
            process.cwd(),
            path.resolve(process.cwd(), args[1]!),
        ]);
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
        printUsage();
        break;

    default:
        console.error(`Unknown command: ${command}`);
        console.error("");
        printUsage();
        process.exit(1);
}

function validateProtectArgs(args: string[]): void {
    if (args.length !== 2) {
        console.error("Usage: aegis protect <file>");
        process.exit(1);
    }
}

async function runApply(): Promise<void> {
    await runWorker([
        "apply",
        process.cwd(),
    ]);
}

async function runStatus(): Promise<void> {
    const sessionPath = findActiveSession();

    if (sessionPath === null) {
        console.log("Aegis");
        console.log("");
        console.log("No active session.");
        console.log("");
        console.log("Start with:");
        console.log("");
        console.log("  aegis protect <file>");
        return;
    }

    console.log("Aegis");
    console.log("");
    console.log(`Active session: ${sessionPath}`);
}

function findActiveSession(): string | null {
    const aegisDirectory = path.join(
        process.cwd(),
        ".aegis",
        "sessions"
    );

    return findReadySession(aegisDirectory);
}

function findReadySession(directory: string): string | null {
    // Temporary implementation.
    //
    // Session discovery will be moved into the worker once
    // the public CLI flow is stable.
    //
    // For now, the CLI will not guess between multiple sessions.

    return null;
}

function runWorker(
    args: string[],
    workingDirectory?: string
): Promise<void> {
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
                cwd: workingDirectory ?? repoRoot,
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

function printUsage(): void {
    console.log(`
Aegis
Privacy infrastructure for AI-assisted software development.

Usage:

  aegis protect <file>     Protect code before sending it to an LLM
  aegis apply              Apply safe LLM changes back to your code
  aegis status             Show the current Aegis session
  aegis help               Show this help

Examples:

  aegis protect src/AuthService.cs
  aegis apply
  aegis status
`);
}