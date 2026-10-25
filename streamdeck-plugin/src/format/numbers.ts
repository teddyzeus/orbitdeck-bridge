import type { Locale } from "../i18n/strings.js";

/** Valeur prête à afficher : nombre déjà mis en forme, et unité adaptée à son ordre de grandeur. */
export interface Quantity {
	value: string;
	unit: string;
}

const EM_DASH = "—";

/** Quantité absente (pas de donnée, valeur non finie). */
export const NO_QUANTITY: Quantity = { value: EM_DASH, unit: "" };

function withLocale(text: string, locale: Locale): string {
	return locale === "fr" ? text.replace(".", ",") : text;
}

/** Évite d'afficher « -0 » ou « -0.0 » pour des valeurs qui s'arrondissent à zéro. */
function signed(original: number, text: string): string {
	const isZero = Number(text) === 0;
	return original < 0 && !isZero ? `-${text}` : text;
}

/** Une décimale sous 100, aucune au-delà (« 99.9 », « 100 »), y compris quand l'arrondi franchit 100. */
function compact(value: number): string {
	const oneDecimal = value.toFixed(1);
	return Number(oneDecimal) >= 100 ? value.toFixed(0) : oneDecimal;
}

function quantity(original: number, text: string, unit: string, locale: Locale): Quantity {
	return { value: withLocale(signed(original, text), locale), unit };
}

/**
 * Altitude ou distance en mètres → m, km, Mm ou Gm, au plus 5 caractères pour tenir sur une touche.
 * Précision conservée en mètres jusqu'à 9 999 m (atterrissage). Les seuils portent sur la valeur arrondie :
 * 9 999,6 m s'affiche « 10.0 km », jamais « 10000 m ».
 */
export function formatDistance(meters: number | null | undefined, locale: Locale): Quantity {
	if (meters === null || meters === undefined || !Number.isFinite(meters)) {
		return NO_QUANTITY;
	}

	const abs = Math.abs(meters);

	if (Math.round(abs) < 10_000) {
		return quantity(meters, abs.toFixed(0), "m", locale);
	}

	if (Math.round(abs / 1e3) < 10_000) {
		return quantity(meters, compact(abs / 1e3), "km", locale);
	}

	if (Math.round(abs / 1e6) < 10_000) {
		return quantity(meters, compact(abs / 1e6), "Mm", locale);
	}

	return quantity(meters, compact(abs / 1e9), "Gm", locale);
}

/** Vitesse en m/s → m/s (une décimale sous 100), puis km/s à partir de 10 000 m/s. */
export function formatSpeed(metersPerSecond: number | null | undefined, locale: Locale): Quantity {
	if (metersPerSecond === null || metersPerSecond === undefined || !Number.isFinite(metersPerSecond)) {
		return NO_QUANTITY;
	}

	const abs = Math.abs(metersPerSecond);

	if (abs < 99.95) {
		return quantity(metersPerSecond, abs.toFixed(1), "m/s", locale);
	}

	if (Math.round(abs) < 10_000) {
		return quantity(metersPerSecond, abs.toFixed(0), "m/s", locale);
	}

	return quantity(metersPerSecond, (abs / 1e3).toFixed(2), "km/s", locale);
}

/** Fraction de 0 à 1 → pourcentage entier (« 50 », unité « % »). */
export function formatPercent(fraction: number | null | undefined): Quantity {
	if (fraction === null || fraction === undefined || !Number.isFinite(fraction)) {
		return NO_QUANTITY;
	}

	const clamped = Math.min(1, Math.max(0, fraction));
	return { value: String(Math.round(clamped * 100)), unit: "%" };
}
