export type Locale = "fr" | "en";

export interface Strings {
	altitudeSea: string;
	altitudeSurface: string;
	speedSurface: string;
	speedOrbit: string;
	speedTarget: string;
	throttle: string;
	statusTitle: string;
	statusOnline: string;
	statusOffline: string;
	statusNoVessel: string;
	statusNotControllable: string;
}

const fr: Strings = {
	altitudeSea: "ALT. MER",
	altitudeSurface: "ALT. SOL",
	speedSurface: "VIT. SOL",
	speedOrbit: "VIT. ORBITE",
	speedTarget: "VIT. CIBLE",
	throttle: "POUSSÉE",
	statusTitle: "KSP 2",
	statusOnline: "EN LIGNE",
	statusOffline: "JEU NON\nLANCÉ",
	statusNoVessel: "AUCUN\nVÉHICULE",
	statusNotControllable: "NON\nCONTRÔLABLE",
};

const en: Strings = {
	altitudeSea: "ALT SEA",
	altitudeSurface: "ALT SURF",
	speedSurface: "SPD SURF",
	speedOrbit: "SPD ORBIT",
	speedTarget: "SPD TARGET",
	throttle: "THROTTLE",
	statusTitle: "KSP 2",
	statusOnline: "ONLINE",
	statusOffline: "GAME NOT\nRUNNING",
	statusNoVessel: "NO\nVESSEL",
	statusNotControllable: "NOT\nCONTROLLABLE",
};

const catalog: Record<Locale, Strings> = { fr, en };

/** Langue de l'interface Stream Deck (`fr`, `en`, `fr-FR`…) ramenée à une langue gérée ; anglais par défaut. */
export function resolveLocale(language: string | undefined): Locale {
	const base = language?.toLowerCase().split(/[-_]/)[0];
	return base === "fr" ? "fr" : "en";
}

export function stringsFor(locale: Locale): Strings {
	return catalog[locale];
}
