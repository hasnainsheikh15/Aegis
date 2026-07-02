import type { PIRNode } from "./node.js";
import type { PIRRelationship } from "./relationships.js";

export interface PIRPackage {
    nodes: PIRNode[];
    relationships: PIRRelationship[];
}