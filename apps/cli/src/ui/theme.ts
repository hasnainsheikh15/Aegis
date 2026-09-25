/**
 * Aegis TUI — Design tokens.
 *
 * Warp-inspired: single accent color, structured
 * dim/bright contrast, minimal palette.
 */

export const colors = {
    // Primary text
    bright:  "#E4E4E7",    // zinc-200
    normal:  "#A1A1AA",    // zinc-400
    dim:     "#71717A",    // zinc-500
    faint:   "#52525B",    // zinc-600

    // Accent — warm amber, single brand color
    accent:  "#F59E0B",    // amber-500

    // Semantic — used sparingly
    ok:      "#34D399",    // emerald-400
    warn:    "#FBBF24",    // amber-400
    err:     "#FB7185",    // rose-400
    hint:    "#7DD3FC",    // sky-300
} as const;
