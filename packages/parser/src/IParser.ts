import type { PIRPackage } from "../../shared/src/index.js";
export interface IParser {

    supports(language: string): boolean;
    parse(filePath: string): Promise<PIRPackage>
}