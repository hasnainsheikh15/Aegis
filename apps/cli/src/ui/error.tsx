import React from "react";
import { render, Box, Text } from "ink";
import { colors } from "./theme.js";

function UnknownCommandScreen({ command }: { command: string }): React.ReactElement {
    return (
        <Box flexDirection="column" paddingTop={1} paddingBottom={1}>
            <Box>
                <Text color={colors.err}>error  </Text>
                <Text color={colors.normal}>unknown command '</Text>
                <Text color={colors.bright}>{command}</Text>
                <Text color={colors.normal}>'</Text>
            </Box>
            <Box marginTop={1} marginLeft={2}>
                <Text color={colors.dim}>run  </Text>
                <Text color={colors.hint}>aegis help</Text>
                <Text color={colors.dim}>  for usage</Text>
            </Box>
        </Box>
    );
}

function ErrorScreen({ title, message, hint }: {
    title: string;
    message: string;
    hint?: string | undefined;
}): React.ReactElement {
    return (
        <Box flexDirection="column" paddingTop={1} paddingBottom={1}>
            <Box>
                <Text color={colors.err}>error  </Text>
                <Text color={colors.normal}>{title}</Text>
            </Box>
            <Box marginLeft={2}>
                <Text color={colors.dim}>{message}</Text>
            </Box>
            {hint && (
                <Box marginTop={1} marginLeft={2}>
                    <Text color={colors.hint}>{hint}</Text>
                </Box>
            )}
        </Box>
    );
}

export function renderUnknownCommand(command: string): void {
    render(<UnknownCommandScreen command={command} />);
}

export function renderError(title: string, message: string, hint?: string): void {
    render(<ErrorScreen title={title} message={message} hint={hint} />);
}
