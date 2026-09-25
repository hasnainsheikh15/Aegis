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
    case "sanitize":
        validateSanitizeArgs(args);
        await runWorker(args);
        break;

    case "import":
        validateImportArgs(args);
        await runWorker(args);
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

function validateSanitizeArgs(args: string[]): void {
    // Human-friendly mode:
    // aegis sanitize <project-folder> <file-path>
    if (args.length === 3) {
        return;
    }

    // Precise mode:
    // aegis sanitize <project-folder> <file-path> <start> <length>
    if (args.length !== 5) {
        console.error(
            "Invalid arguments for sanitize."
        );
        console.error("");
        console.error(
            "Usage: aegis sanitize <project-folder> <file-path> [<start> <length>]"
        );
        process.exit(1);
    }

    const start = Number(args[3]);
    const length = Number(args[4]);

    if (!Number.isInteger(start) || start < 0) {
        console.error(
            "Selection start must be a non-negative integer."
        );
        process.exit(1);
    }

    if (!Number.isInteger(length) || length < 0) {
        console.error(
            "Selection length must be a non-negative integer."
        );
        process.exit(1);
    }
}

function validateImportArgs(args: string[]): void {
    if (args.length !== 2) {
        console.error(
            "Invalid arguments for import."
        );
        console.error("");
        console.error(
            "Usage: aegis import <session.json>"
        );
        process.exit(1);
    }
}

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

function printUsage(): void {
    console.log(`
Aegis
Privacy infrastructure for AI-assisted software development.

Usage:

  aegis sanitize <project-folder> <file-path> [<start> <length>]

      Create a sanitized Aegis session.

      Without <start> and <length>, Aegis will
      ask you to select the source lines interactively.

  aegis import <session.json>

      Import a modified sanitized source and apply safe changes.

  aegis help

      Show this help message.

Examples:

  Interactive selection:

    aegis sanitize ./samples/sampleProject ./samples/sampleProject/Program.cs

  Precise selection:

    aegis sanitize ./samples/sampleProject ./samples/sampleProject/Program.cs 194 30

  Import:

    aegis import ./samples/sampleProject/.aegis/sessions/<session-id>/session.json
`);
}