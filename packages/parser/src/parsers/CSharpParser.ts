import { readFile } from "node:fs/promises";
import type { PIRPackage } from "../../../shared/src/index.js";
import type { IParser } from "../IParser.js";

export class CSharpParser implements IParser {
    readonly language: string = "csharp";

    supports(language: string): boolean {
        return language.toLocaleLowerCase() === "csharp";
    }

    async parse(filePath: string): Promise<PIRPackage> {

        const sourceCode = await readFile(filePath, "utf-8")
        console.log("\n========== SOURCE CODE ==========\n");
        console.log(sourceCode);
        console.log("\n=================================\n");

        return {
            nodes : [],
            relationships : []
        };
    }
} 