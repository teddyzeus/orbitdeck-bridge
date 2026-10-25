/** Lit un réglage numérique venu de l'interface (souvent une chaîne) ; valeur par défaut si absent ou hors bornes. */
export function numberSetting(value: unknown, fallback: number, min: number, max: number): number {
	const parsed = typeof value === "number" ? value : typeof value === "string" && value.trim() !== "" ? Number(value) : Number.NaN;
	return Number.isFinite(parsed) && parsed >= min && parsed <= max ? Math.round(parsed) : fallback;
}
