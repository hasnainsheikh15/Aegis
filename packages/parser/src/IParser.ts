import type { PIRPackage } from "../../shared/src/index.js";
export interface IParser {
    readonly language : string;
    supports(language: string): boolean;
    parse(filePath: string): Promise<PIRPackage>
}