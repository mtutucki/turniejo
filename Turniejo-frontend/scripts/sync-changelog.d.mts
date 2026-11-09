import type { Release } from "../src/features/whats-new/releases"

export const CHANGELOG_PATH: string
export const RELEASES_PATH: string
export function parseChangelog(markdown: string): Release[]
export function renderReleases(markdown: string): string
