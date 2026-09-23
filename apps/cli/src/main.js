import { spawn } from "node:child_process";
const args = process.argv.slice(2);
if (args.length === 0) {
    printUsage();
    process.exit(1);
}
const command = args[0];
switch (command) {
    case "sanitize":
        await runWorker(args);
        break;
    case "import":
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
function runWorker(args) {
    return new Promise((resolve, reject) => {
        const worker = spawn("dotnet", [
            "run",
            "--project",
            "apps/roslyn-worker/RoslynWorker.csproj",
            "--",
            ...args,
        ], {
            stdio: "inherit",
            shell: true,
        });
        worker.on("error", reject);
        worker.on("close", (code) => {
            if (code === 0) {
                resolve();
                return;
            }
            reject(new Error(`Aegis worker exited with code ${code ?? "unknown"}.`));
        });
    });
}
function printUsage() {
    console.log(`
Aegis
Privacy infrastructure for AI-assisted software development.

Usage:

  aegis sanitize <project-folder> <file-path> <start> <length>

      Create a sanitized Aegis session.

  aegis import <session.json>

      Import a modified sanitized source and apply safe changes.

  aegis help

      Show this help message.

Examples:

  aegis sanitize ./samples/sampleProject ./samples/sampleProject/Program.cs 194 24

  aegis import ./samples/sampleProject/.aegis/sessions/<session-id>/session.json
`);
}
//# sourceMappingURL=main.js.map