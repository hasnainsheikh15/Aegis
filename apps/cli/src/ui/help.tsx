import React from "react";
import { render, Box, Text } from "ink";
import { colors } from "./theme.js";
import { Sep } from "./components.js";

function HelpScreen(): React.ReactElement {
    return (
        <Box flexDirection="column" paddingTop={1} paddingBottom={1}>
            <Box>
                <Text color={colors.accent} bold>aegis</Text>
                <Text color={colors.dim}>  privacy infrastructure for AI-assisted development</Text>
            </Box>

            <Sep />

            <Box flexDirection="column" marginTop={1} marginLeft={2}>
                <Box>
                    <Box width={30}><Text color={colors.bright}>protect {"<file>"}</Text></Box>
                    <Text color={colors.dim}>Protect code before sending to an LLM</Text>
                </Box>
                <Box>
                    <Box width={30}><Text color={colors.bright}>apply</Text></Box>
                    <Text color={colors.dim}>Apply LLM changes back to your code</Text>
                </Box>
                <Box>
                    <Box width={30}><Text color={colors.bright}>status</Text></Box>
                    <Text color={colors.dim}>Show current session</Text>
                </Box>
                <Box>
                    <Box width={30}><Text color={colors.bright}>help</Text></Box>
                    <Text color={colors.dim}>Show this help</Text>
                </Box>
            </Box>

            <Sep />

            <Box flexDirection="column" marginTop={1} marginLeft={2}>
                <Text color={colors.faint}>$ </Text>
                <Box>
                    <Text color={colors.faint}>  </Text>
                    <Text color={colors.normal}>aegis protect src/AuthService.cs</Text>
                </Box>
                <Box>
                    <Text color={colors.faint}>  </Text>
                    <Text color={colors.normal}>aegis apply</Text>
                </Box>
            </Box>
        </Box>
    );
}

export function renderHelp(): void {
    render(<HelpScreen />);
}
