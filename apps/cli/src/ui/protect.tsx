import React from "react";
import { render, Box, Text } from "ink";
import Spinner from "ink-spinner";
import { colors } from "./theme.js";
import { Sep } from "./components.js";

export interface ProtectResultFile {
    name: string;
}

export interface ProtectResult {
    sessionId: string;
    sessionPath: string;
    sanitizedDir: string;
    readmePath: string;
    files: ProtectResultFile[];
    sensitiveCount: number;
}

function ProtectingScreen({ filePath }: { filePath: string }): React.ReactElement {
    return (
        <Box paddingTop={1}>
            <Text color={colors.accent}><Spinner type="dots" /></Text>
            <Text color={colors.dim}> protecting </Text>
            <Text color={colors.bright} bold>{filePath}</Text>
        </Box>
    );
}

function ProtectCompleteScreen({ result }: { result: ProtectResult }): React.ReactElement {
    return (
        <Box flexDirection="column" paddingTop={1} paddingBottom={1}>
            <Box>
                <Text color={colors.accent} bold>aegis</Text>
                <Text color={colors.dim}>  protect</Text>
            </Box>

            <Sep />

            <Box marginTop={1} marginLeft={2}>
                <Text color={colors.ok}>done  </Text>
                <Text color={colors.normal}>session created</Text>
            </Box>

            <Box flexDirection="column" marginTop={1} marginLeft={2}>
                <Box>
                    <Box width={12}><Text color={colors.dim}>session</Text></Box>
                    <Text color={colors.bright}>{result.sessionId}</Text>
                </Box>
                <Box>
                    <Box width={12}><Text color={colors.dim}>sanitized</Text></Box>
                    <Text color={colors.faint}>{result.sanitizedDir}</Text>
                </Box>
                {result.sensitiveCount > 0 && (
                    <Box>
                        <Box width={12}><Text color={colors.dim}>replaced</Text></Box>
                        <Text color={colors.warn}>{result.sensitiveCount} sensitive values</Text>
                    </Box>
                )}
            </Box>

            {result.files.length > 0 && (
                <Box flexDirection="column" marginTop={1} marginLeft={2}>
                    {result.files.map((file) => (
                        <Box key={file.name} marginLeft={2}>
                            <Text color={colors.ok}>+ </Text>
                            <Text color={colors.normal}>{file.name}</Text>
                        </Box>
                    ))}
                </Box>
            )}

            <Sep />

            <Box marginTop={1} marginLeft={2}>
                <Text color={colors.dim}>next: review sanitized files, then  </Text>
                <Text color={colors.hint}>aegis apply</Text>
            </Box>
        </Box>
    );
}

export function renderProtecting(filePath: string): { unmount: () => void } {
    const instance = render(<ProtectingScreen filePath={filePath} />);
    return { unmount: () => instance.unmount() };
}

export function renderProtectComplete(result: ProtectResult): void {
    render(<ProtectCompleteScreen result={result} />);
}
