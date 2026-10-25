export const DEFAULT_PORT = 8765;
export const MIN_PORT = 1024;
export const MAX_PORT = 65535;

/** Réglages communs à toutes les touches du plugin. */
export type GlobalSettings = {
	/** Port du mod StreamPlug (`streamplug-config.json` côté jeu). Saisi comme texte dans l'interface. */
	port?: number | string;
};

/** Port saisi par l'utilisateur ; valeur par défaut si vide, illisible ou hors des bornes acceptées par le mod. */
export function portFrom(settings: GlobalSettings | undefined): number {
	const raw = settings?.port;
	const port = typeof raw === "string" ? (raw.trim() === "" ? Number.NaN : Number(raw.trim())) : raw;
	return typeof port === "number" && Number.isInteger(port) && port >= MIN_PORT && port <= MAX_PORT ? port : DEFAULT_PORT;
}

/** Le mod n'écoute que sur la boucle locale. */
export function urlFor(settings: GlobalSettings | undefined): string {
	return `ws://127.0.0.1:${portFrom(settings)}/`;
}
