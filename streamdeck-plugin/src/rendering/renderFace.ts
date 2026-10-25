/**
 * Dessin des touches en SVG. Une touche est décrite par un `KeyFace` (données déjà mises en forme),
 * le rendu ne connaît ni le jeu ni le protocole.
 */

/** Teinte de la valeur principale. */
export type Tone = "normal" | "dim" | "accent" | "ok" | "warn" | "alert";

export interface KeyFace {
	/** Petit libellé en haut de la touche. */
	label: string;
	/** Valeur principale ; peut contenir des retours à la ligne (affichage sur plusieurs lignes). */
	value: string;
	/** Unité sous la valeur. */
	unit?: string;
	tone: Tone;
	/** Jauge horizontale en bas de la touche, de 0 à 1 (remplace l'unité). */
	bar?: number;
	/** Pastille d'état à côté du libellé. */
	dot?: Tone;
}

/** Coordonnées dans un repère 144 × 144 (2 × la taille 72 × 72 des touches : net sur les écrans haute densité). */
const SIZE = 144;

const COLORS: Record<Tone, string> = {
	normal: "#f3f4f6",
	dim: "#6b7280",
	accent: "#38bdf8",
	ok: "#34d399",
	warn: "#fbbf24",
	alert: "#f87171",
};

const BACKGROUND = "#0f1218";
const MUTED = "#9aa4b2";
const TRACK = "#2a303c";
const FONT = "'Segoe UI', 'Helvetica Neue', Arial, sans-serif";

function escapeXml(text: string): string {
	return text.replace(/&/g, "&amp;").replace(/</g, "&lt;").replace(/>/g, "&gt;").replace(/"/g, "&quot;");
}

/** Taille de police de la valeur, réduite quand le texte est long pour rester dans la touche. */
export function valueFontSize(longestLineLength: number, lineCount: number): number {
	if (lineCount > 1) {
		return longestLineLength > 9 ? 22 : longestLineLength > 6 ? 26 : 30;
	}

	if (longestLineLength <= 3) {
		return 60;
	}

	if (longestLineLength === 4) {
		return 52;
	}

	if (longestLineLength === 5) {
		return 44;
	}

	return longestLineLength === 6 ? 38 : 32;
}

export function renderFaceSvg(face: KeyFace): string {
	const lines = face.value.split("\n");
	const longest = Math.max(...lines.map((line) => [...line].length));
	const fontSize = valueFontSize(longest, lines.length);
	const hasBar = face.bar !== undefined;
	const valueColor = COLORS[face.tone];

	// Centre vertical de la zone de valeur : un peu plus haut quand une unité ou une jauge occupe le bas.
	const centerY = face.unit || hasBar ? 82 : 90;
	const lineHeight = fontSize * 1.12;
	const firstBaseline = centerY - ((lines.length - 1) * lineHeight) / 2 + fontSize * 0.34;

	const valueText = lines
		.map((line, index) => {
			const y = Math.round((firstBaseline + index * lineHeight) * 10) / 10;
			return `<text x="72" y="${y}" font-size="${fontSize}" font-weight="700" fill="${valueColor}" text-anchor="middle">${escapeXml(line)}</text>`;
		})
		.join("");

	const dot = face.dot ? `<circle cx="18" cy="24" r="7" fill="${COLORS[face.dot]}"/>` : "";
	const label = face.label
		? `<text x="72" y="31" font-size="21" font-weight="600" fill="${MUTED}" text-anchor="middle">${escapeXml(face.label)}</text>`
		: "";

	let footer = "";
	if (hasBar) {
		const fraction = Math.min(1, Math.max(0, face.bar ?? 0));
		const fillWidth = Math.round(112 * fraction * 10) / 10;
		footer =
			`<rect x="16" y="120" width="112" height="12" rx="6" fill="${TRACK}"/>` +
			(fillWidth > 0 ? `<rect x="16" y="120" width="${Math.max(fillWidth, 12)}" height="12" rx="6" fill="${COLORS.accent}"/>` : "");
	} else if (face.unit) {
		footer = `<text x="72" y="124" font-size="24" font-weight="600" fill="${MUTED}" text-anchor="middle">${escapeXml(face.unit)}</text>`;
	}

	return (
		`<svg xmlns="http://www.w3.org/2000/svg" width="${SIZE}" height="${SIZE}" viewBox="0 0 ${SIZE} ${SIZE}" font-family="${FONT}">` +
		`<rect width="${SIZE}" height="${SIZE}" fill="${BACKGROUND}"/>` +
		dot +
		label +
		valueText +
		footer +
		`</svg>`
	);
}

/** SVG prêt à être passé à `setImage` de Stream Deck. */
export function renderFace(face: KeyFace): string {
	return `data:image/svg+xml;charset=utf8,${encodeURIComponent(renderFaceSvg(face))}`;
}
