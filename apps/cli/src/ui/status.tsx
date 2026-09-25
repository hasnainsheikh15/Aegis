import React from "react";
import { render, Box, Text } from "ink";
import { colors } from "./theme.js";
import { Sep } from "./components.js";

export interface SessionInfo {
    sessionId: string;
    sessionPath: string;
    status: "Ready" | "Consumed" | string;
    files: Array<{
        name: string;
        status: "protected" | "modified" | "unchanged";
    }>;
}

function NoSessionScreen(): React.ReactElement {
    return (
        <Box flexDirection="column" paddingTop={1} paddingBottom={1}>
            <Box>
                <Text color={colors.accent} bold>aegis</Text>
                <Text color={colors.dim}>  status</Text>
            </Box>

            <Sep />

            <Box marginTop={1} marginLeft={2}>
                <Text color={colors.normal}>No active session.</Text>
            </Box>

            <Box marginTop={1} marginLeft={2}>
                <Text color={colors.dim}>Get started with  </Text>
                <Text color={colors.hint}>aegis protect {"<file>"}</Text>
            </Box>
        </Box>
    );
}

function ActiveSessionScreen({ session }: { session: SessionInfo }): React.ReactElement {
    return (
        <Box flexDirection="column" paddingTop={1} paddingBottom={1}>
            <Box>
                <Text color={colors.accent} bold>aegis</Text>
                <Text color={colors.dim}>  status</Text>
            </Box>

            <Sep />

            <Box flexDirection="column" marginTop={1} marginLeft={2}>
                <Box>
                    <Box width={12}><Text color={colors.dim}>session</Text></Box>
                    <Text color={colors.bright}>{session.sessionId}</Text>
                </Box>
                <Box>
                    <Box width={12}><Text color={colors.dim}>status</Text></Box>
                    <Text color={session.status === "Ready" ? colors.ok : colors.dim}>
                        {session.status.toLowerCase()}
                    </Text>
                </Box>
                <Box>
                    <Box width={12}><Text color={colors.dim}>path</Text></Box>
                    <Text color={colors.faint}>{session.sessionPath}</Text>
                </Box>
            </Box>

            {session.files.length > 0 && (
                <Box flexDirection="column" marginTop={1} marginLeft={2}>
                    <Text color={colors.dim}>files</Text>
                    {session.files.map((file) => (
                        <Box key={file.name} marginLeft={2}>
                            <Text color={colors.ok}>+ </Text>
                            <Text color={colors.normal}>{file.name}</Text>
                        </Box>
                    ))}
                </Box>
            )}
        </Box>
    );
}

export function renderNoSession(): void {
    render(<NoSessionScreen />);
}

export function renderActiveSession(session: SessionInfo): void {
    render(<ActiveSessionScreen session={session} />);
}
