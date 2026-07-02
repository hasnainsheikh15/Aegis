import { readdir } from "node:fs/promises";
import type { IParser } from "../../parser/src/index.js";
import { join } from "node:path";
export class Indexer {
    // private readonly parsers : IParser[];

    // constructor(parsers : IParser[]) {
    //     this.parsers = parsers;
    // }

    // short form of the above code which is called parameter property 

    constructor(
        private readonly parsers: IParser[]
    ) { }

    private getLanguage(filePath: string): string | null {
        if (filePath.endsWith(".cs")) return "csharp";
        return null;
    }
    public async index(projectPath: string): Promise<void> {
        console.log(`Indexing the project : ${projectPath}`);

        const entries = await readdir(projectPath);

        console.log("\nfound");

        for (const entry of entries) {

            const filePath = join(projectPath, entry);

            const language = this.getLanguage(filePath);

            if (!language) continue;

            // console.log("Language:", language);
            // console.log("Available parsers:", this.parsers);

            const parser = this.parsers.find((parser) => {
                const result = parser.supports(language);
                //             console.log(
                //     `${parser.language}.supports("${language}") => ${result}`
                // );
                        return result; // we need to return true here in order to allow find to claiim that it has done his job ;
            })

            if (!parser) {
                console.warn(`No parser found for ${language}`);
                continue;
            }
            console.log(`Found ${language} file ${filePath}`);
            console.log(`Parsing  : ${filePath}`);

            await parser.parse(filePath);



        }
    }
} 