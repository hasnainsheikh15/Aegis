import React from "react";
import { Box, Text } from "ink";
import { colors } from "./theme.js";

export function Blank(): React.ReactElement {
    return <Box><Text>{" "}</Text></Box>;
}

export function Sep(): React.ReactElement {
    return (
        <Box>
            <Text color={colors.faint}>{"  ─────────────────────────────────────────────"}</Text>
        </Box>
    );
}
