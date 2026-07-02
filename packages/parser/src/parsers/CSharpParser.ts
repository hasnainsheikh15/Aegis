import type { PIRPackage } from "../../../shared/src/index.js";
import type { IParser } from "../IParser.js";

export class CSharpParser implements IParser {
    supports(language: string): boolean {
        return language.toLocaleLowerCase() === "csharp";
    }

    async parse(filePath: string): Promise<PIRPackage> {
        throw new Error("Method not implemented");
        
    }
} 