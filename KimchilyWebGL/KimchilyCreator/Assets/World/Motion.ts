/** Imported helpers are checked and included with the world behaviour. */
export function sampleHeight(elapsed: number): number {
    return Math.sin(elapsed * 2) * 0.12;
}
