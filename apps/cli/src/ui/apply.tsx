import React from "react";
import { render, Box, Text } from "ink";
import Spinner from "ink-spinner";
import { colors } from "./theme.js";
import { Sep } from "./components.js";

export interface ApplyFileResult {
    name: string;
    status: "applied" | "skipped" | "review";
    reason?: string;
}

export interface ApplyResult {
    success: boolean;
    sessionId: string;
    files: ApplyFileResult[];
    message?: string;
}

function ApplyingScreen(): React.ReactElement {
    return (
        <Box paddingTop={1}>
            <Text color={colors.accent}><Spinner type="dots" /></Text>
            <Text color={colors.dim}> applying changes</Text>
        </Box>
    );
}

function ApplyCompleteScreen({ result }: { result: ApplyResult }): React.ReactElement {
    const applied = result.files.filter(f => f.status === "applied").length;
    const review = result.files.filter(f => f.status === "review").length;

    return (
        <Box flexDirection="column" paddingTop={1} paddingBottom={1}>
            <Box>
                <Text color={colors.accent} bold>aegis</Text>
                <Text color={colors.dim}>  apply</Text>
            </Box>

            <Sep />

            <Box marginTop={1} marginLeft={2}>
                <Text color={result.success ? colors.ok : colors.warn}>
                    {result.success ? "done  " : "review  "}
                </Text>
                <Text color={colors.normal}>{result.sessionId}</Text>
            </Box>

            {result.files.length > 0 && (
                <Box flexDirection="column" marginTop={1} marginLeft={2}>
                    {result.files.map((file) => {
                        const prefix =
                            file.status === "applied" ? "+" :
                            file.status === "review"  ? "!" : "~";
                        const color =
                            file.status === "applied" ? colors.ok :
                            file.status === "review"  ? colors.warn : colors.faint;

                        return (
                            <Box key={file.name} marginLeft={2}>
                                <Text color={color}>{prefix} </Text>
                                <Text color={colors.normal}>{file.name}</Text>
                                {file.reason && (
                                    <Text color={colors.faint}> {file.reason}</Text>
                                )}
                            </Box>
                        );
                    })}
                </Box>
            )}

            {(applied > 0 || review > 0) && (
                <Box marginTop={1} marginLeft={4}>
                    {applied > 0 && <Text color={colors.ok}>{applied} applied  </Text>}
                    {review > 0 && <Text color={colors.warn}>{review} needs review</Text>}
                </Box>
            )}

            {result.message && !result.files.length && (
                <Box marginTop={1} marginLeft={2}>
                    <Text color={colors.dim}>{result.message}</Text>
                </Box>
            )}
        </Box>
    );
}

function ApplyErrorScreen({ message }: { message: string }): React.ReactElement {
    return (
        <Box flexDirection="column" paddingTop={1} paddingBottom={1}>
            <Box>
                <Text color={colors.err}>error  </Text>
                <Text color={colors.normal}>{message}</Text>
            </Box>
        </Box>
    );
}

export function renderApplying(): { unmount: () => void } {
    const instance = render(<ApplyingScreen />);
    return { unmount: () => instance.unmount() };
}

export function renderApplyComplete(result: ApplyResult): void {
    render(<ApplyCompleteScreen result={result} />);
}

export function renderApplyError(message: string): void {
    render(<ApplyErrorScreen message={message} />);
}
