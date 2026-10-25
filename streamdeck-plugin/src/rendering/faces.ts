import { formatDistance, formatPercent, formatSpeed, NO_QUANTITY, type Quantity } from "../format/numbers.js";
import type { Locale, Strings } from "../i18n/strings.js";
import type { SpeedMode, StateMessage } from "../protocol.js";
import type { Availability } from "../state/Store.js";
import type { KeyFace, Tone } from "./renderFace.js";

/** Tout ce dont une touche a besoin pour se dessiner. */
export interface FaceContext {
	availability: Availability;
	/** Dernier état du jeu ; `undefined` hors connexion. */
	state: StateMessage | undefined;
	strings: Strings;
	locale: Locale;
}

export type AltitudeMode = "surface" | "sea";

const SPEED_CYCLE: readonly SpeedMode[] = ["surface", "orbit", "target"];

/** Les valeurs sont affichées en jaune quand le véhicule ne peut pas être piloté (pas de pilote, contrôle insuffisant). */
function dataTone(availability: Availability): Tone {
	switch (availability) {
		case "ready":
			return "normal";
		case "notControllable":
			return "warn";
		default:
			return "dim";
	}
}

function hasData(ctx: FaceContext): boolean {
	return (ctx.availability === "ready" || ctx.availability === "notControllable") && ctx.state !== undefined;
}

function quantityFace(label: string, quantity: Quantity, ctx: FaceContext): KeyFace {
	const shown = hasData(ctx) ? quantity : NO_QUANTITY;
	return { label, value: shown.value, unit: shown.unit || undefined, tone: shown.unit ? dataTone(ctx.availability) : "dim" };
}

export function altitudeFace(ctx: FaceContext, mode: AltitudeMode): KeyFace {
	const altitude = ctx.state?.altitude;
	const meters = mode === "sea" ? altitude?.seaLevel : altitude?.surface;
	const label = mode === "sea" ? ctx.strings.altitudeSea : ctx.strings.altitudeSurface;

	return quantityFace(label, formatDistance(meters, ctx.locale), ctx);
}

export function speedFace(ctx: FaceContext): KeyFace {
	const speed = ctx.state?.speed;
	const mode: SpeedMode = speed?.mode ?? "surface";

	const label = mode === "orbit" ? ctx.strings.speedOrbit : mode === "target" ? ctx.strings.speedTarget : ctx.strings.speedSurface;
	const metersPerSecond = mode === "orbit" ? speed?.orbital : mode === "target" ? speed?.target : speed?.surface;

	return quantityFace(label, formatSpeed(metersPerSecond, ctx.locale), ctx);
}

export function throttleFace(ctx: FaceContext): KeyFace {
	const throttle = ctx.state?.throttle;
	const available = hasData(ctx) && throttle !== undefined;
	const quantity = available ? formatPercent(throttle) : NO_QUANTITY;

	return {
		label: ctx.strings.throttle,
		value: quantity.value === NO_QUANTITY.value ? quantity.value : `${quantity.value}%`,
		tone: available ? dataTone(ctx.availability) : "dim",
		bar: available ? Math.min(1, Math.max(0, throttle ?? 0)) : 0,
	};
}

/** Nom du véhicule raccourci pour tenir sous la valeur. */
function shortName(name: string | null | undefined): string | undefined {
	if (!name) {
		return undefined;
	}

	const chars = [...name];
	return chars.length > 11 ? `${chars.slice(0, 10).join("")}…` : name;
}

export function statusFace(ctx: FaceContext): KeyFace {
	switch (ctx.availability) {
		case "ready":
			return {
				label: ctx.strings.statusTitle,
				value: ctx.strings.statusOnline,
				unit: shortName(ctx.state?.vessel?.name),
				tone: "ok",
				dot: "ok",
			};
		case "notControllable":
			return { label: ctx.strings.statusTitle, value: ctx.strings.statusNotControllable, tone: "warn", dot: "warn" };
		case "noVessel":
			return { label: ctx.strings.statusTitle, value: ctx.strings.statusNoVessel, tone: "warn", dot: "warn" };
		default:
			return { label: ctx.strings.statusTitle, value: ctx.strings.statusOffline, tone: "dim", dot: "alert" };
	}
}

export function nextAltitudeMode(mode: AltitudeMode): AltitudeMode {
	return mode === "surface" ? "sea" : "surface";
}

/**
 * Type de vitesse à demander au jeu à l'appui suivant : surface → orbite → cible → surface.
 * La cible est sautée quand le jeu n'en a pas (il refuserait la commande).
 */
export function nextSpeedMode(state: StateMessage | undefined): SpeedMode | undefined {
	const speed = state?.speed;
	if (speed === undefined) {
		return undefined;
	}

	const hasTarget = speed.target !== null && speed.target !== undefined;
	const start = SPEED_CYCLE.indexOf(speed.mode);

	for (let step = 1; step <= SPEED_CYCLE.length; step++) {
		const candidate = SPEED_CYCLE[(start + step) % SPEED_CYCLE.length];
		if (candidate !== undefined && (candidate !== "target" || hasTarget)) {
			return candidate;
		}
	}

	return undefined;
}
