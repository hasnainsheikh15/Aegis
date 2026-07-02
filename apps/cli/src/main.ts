import { Indexer } from "../../../packages/indexer/src/Indexer.js";
import { CSharpParser } from "../../../packages/parser/src/parsers/CSharpParser.js";

const indexer = new Indexer([
    new CSharpParser(),
]);

await indexer.index("./samples/SampleProject");