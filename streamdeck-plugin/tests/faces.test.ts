import { describe, expect, it } from "vitest";

import { stringsFor, type Locale } from "../src/i18n/strings.js";
import type { StateMessage } from "../src/protocol.js";
import { altitudeFace, nextAltitudeMode, nextSpeedMode, speedFace, statusFace, throttleFace, type FaceContext } from "../src/rendering/faces.js";
import { renderFace, renderFaceSvg, valueFontSize } from "../src/rendering/renderFace.js";
import type { Availability } from "../src/state/Store.js";

function flight(overrides: Partial<StateMessage> = {}): StateMessage {
	return {
		v: 1,
		type: "state",
		seq: 1,
		status: "connected",
		gameState: "FlightView",
		vessel: { name: "Kerbal X" },
		altitude: { seaLevel: 12_400.5, surface: 523.4 },
		speed: { mode: "surface", surface: 245.3, orbital: 2_300.1, target: null },
		throttle: 0.5,
		sas: { enabled: true, mode: "StabilityAssist" },
		rcs: { enabled: false },
		...overrides,
	};
}

function context(availability: Availability, state: StateMessage | undefined, locale: Locale = "fr"): FaceContext {
	return { availability, state, strings: stringsFor(locale), locale };
}

const ready = (overrides?: Partial<StateMessage>, locale?: Locale) => context("ready", flight(overrides), locale);

describe("altitudeFace", () => {
	it("affiche l'altitude au-dessus du sol par défaut", () => {
		expect(altitudeFace(ready(), "surface")).toMatchObject({ label: "ALT. SOL", value: "523", unit: "m", tone: "normal" });
	});

	it("affiche l'altitude au-dessus de la mer", () => {
		expect(altitudeFace(ready(), "sea")).toMatchObject({ label: "ALT. MER", value: "12,4", unit: "km" });
	});

	it("traduit le libellé en anglais", () => {
		expect(altitudeFace(ready(undefined, "en"), "sea").label).toBe("ALT SEA");
	});

	it("affiche un tiret sans connexion", () => {
		expect(altitudeFace(context("offline", undefined), "surface")).toMatchObject({ value: "—", tone: "dim" });
	});

	it("affiche un tiret sans véhicule", () => {
		expect(altitudeFace(context("noVessel", { v: 1, type: "state", seq: 1, status: "noVessel" }), "surface")).toMatchObject({ value: "—" });
	});

	it("passe en jaune quand le véhicule n'est pas contrôlable", () => {
		const face = altitudeFace(context("notControllable", flight({ status: "notControllable" })), "surface");
		expect(face).toMatchObject({ value: "523", tone: "warn" });
	});
});

describe("speedFace", () => {
	it("suit le type de vitesse du jeu", () => {
		expect(speedFace(ready())).toMatchObject({ label: "VIT. SOL", value: "245", unit: "m/s" });
		expect(speedFace(ready({ speed: { mode: "orbit", surface: 245.3, orbital: 2_300.1, target: null } }))).toMatchObject({
			label: "VIT. ORBITE",
			value: "2300",
		});
		expect(speedFace(ready({ speed: { mode: "target", surface: 245.3, orbital: 2_300.1, target: 5.5 } }))).toMatchObject({
			label: "VIT. CIBLE",
			value: "5,5",
		});
	});

	it("affiche un tiret pour une vitesse cible sans cible", () => {
		expect(speedFace(ready({ speed: { mode: "target", surface: 1, orbital: 2, target: null } }))).toMatchObject({ value: "—" });
	});

	it("affiche un tiret sans connexion", () => {
		expect(speedFace(context("offline", undefined))).toMatchObject({ value: "—", tone: "dim" });
	});
});

describe("throttleFace", () => {
	it("affiche le pourcentage et la jauge", () => {
		expect(throttleFace(ready())).toMatchObject({ label: "POUSSÉE", value: "50%", bar: 0.5, tone: "normal" });
		expect(throttleFace(ready({ throttle: 1 }))).toMatchObject({ value: "100%", bar: 1 });
		expect(throttleFace(ready({ throttle: 0 }))).toMatchObject({ value: "0%", bar: 0 });
	});

	it("borne la jauge", () => {
		expect(throttleFace(ready({ throttle: 3 })).bar).toBe(1);
		expect(throttleFace(ready({ throttle: -1 })).bar).toBe(0);
	});

	it("affiche un tiret sans connexion ni véhicule", () => {
		expect(throttleFace(context("offline", undefined))).toMatchObject({ value: "—", bar: 0, tone: "dim" });
		expect(throttleFace(context("noVessel", { v: 1, type: "state", seq: 1, status: "noVessel" }))).toMatchObject({ value: "—" });
	});
});

describe("statusFace", () => {
	it("indique le jeu en ligne avec le nom du véhicule", () => {
		expect(statusFace(ready())).toMatchObject({ value: "EN LIGNE", unit: "Kerbal X", tone: "ok", dot: "ok" });
	});

	it("raccourcit les noms de véhicule trop longs", () => {
		const face = statusFace(ready({ vessel: { name: "Un vaisseau au nom beaucoup trop long" } }));
		expect([...(face.unit ?? "")].length).toBeLessThanOrEqual(11);
		expect(face.unit).toContain("…");
	});

	it.each([
		["offline", "JEU NON\nLANCÉ", "alert"],
		["noVessel", "AUCUN\nVÉHICULE", "warn"],
		["notControllable", "NON\nCONTRÔLABLE", "warn"],
	] as const)("état %s", (availability, value, dot) => {
		expect(statusFace(context(availability, undefined))).toMatchObject({ value, dot });
	});

	it("est traduit en anglais", () => {
		expect(statusFace(context("offline", undefined, "en")).value).toBe("GAME NOT\nRUNNING");
	});
});

describe("modes", () => {
	it("bascule l'altitude entre sol et mer", () => {
		expect(nextAltitudeMode("surface")).toBe("sea");
		expect(nextAltitudeMode("sea")).toBe("surface");
	});

	it("parcourt surface → orbite → cible → surface quand une cible existe", () => {
		const withTarget = (mode: "surface" | "orbit" | "target") => flight({ speed: { mode, surface: 1, orbital: 2, target: 3 } });
		expect(nextSpeedMode(withTarget("surface"))).toBe("orbit");
		expect(nextSpeedMode(withTarget("orbit"))).toBe("target");
		expect(nextSpeedMode(withTarget("target"))).toBe("surface");
	});

	it("saute la cible quand il n'y en a pas", () => {
		const noTarget = (mode: "surface" | "orbit" | "target") => flight({ speed: { mode, surface: 1, orbital: 2, target: null } });
		expect(nextSpeedMode(noTarget("surface"))).toBe("orbit");
		expect(nextSpeedMode(noTarget("orbit"))).toBe("surface");
		expect(nextSpeedMode(noTarget("target"))).toBe("surface");
	});

	it("ne propose rien sans données de vitesse", () => {
		expect(nextSpeedMode(undefined)).toBeUndefined();
		expect(nextSpeedMode({ v: 1, type: "state", seq: 1, status: "noVessel" })).toBeUndefined();
	});
});

describe("rendu SVG", () => {
	it("produit un SVG valide contenant le libellé, la valeur et l'unité", () => {
		const svg = renderFaceSvg({ label: "ALT. SOL", value: "523", unit: "m", tone: "normal" });
		expect(svg.startsWith("<svg")).toBe(true);
		expect(svg.endsWith("</svg>")).toBe(true);
		expect(svg).toContain("ALT. SOL");
		expect(svg).toContain(">523<");
		expect(svg).toContain(">m<");
	});

	it("échappe les caractères spéciaux (nom de véhicule hostile)", () => {
		const svg = renderFaceSvg({ label: "<script>", value: 'a&b"c', tone: "normal" });
		expect(svg).not.toContain("<script>");
		expect(svg).toContain("&lt;script&gt;");
		expect(svg).toContain("a&amp;b&quot;c");
	});

	it("dessine une ligne par retour à la ligne", () => {
		const svg = renderFaceSvg({ label: "KSP 2", value: "JEU NON\nLANCÉ", tone: "dim" });
		expect(svg).toContain(">JEU NON<");
		expect(svg).toContain(">LANCÉ<");
	});

	it("dessine la jauge proportionnellement", () => {
		const half = renderFaceSvg({ label: "", value: "50%", tone: "normal", bar: 0.5 });
		const full = renderFaceSvg({ label: "", value: "100%", tone: "normal", bar: 1 });
		expect(half).toContain('width="56"');
		expect(full).toContain('width="112"');
	});

	it("dessine une jauge vide sans remplissage", () => {
		const svg = renderFaceSvg({ label: "", value: "0%", tone: "normal", bar: 0 });
		expect(svg.match(/<rect/g)?.length).toBe(2); // fond + piste, sans remplissage
	});

	it("est une URL de données utilisable par Stream Deck", () => {
		const uri = renderFace({ label: "A", value: "1", tone: "ok" });
		expect(uri.startsWith("data:image/svg+xml;charset=utf8,")).toBe(true);
		expect(decodeURIComponent(uri.split(",").slice(1).join(","))).toContain("<svg");
	});

	it("est déterministe (même face → même image) : permet de ne redessiner que ce qui change", () => {
		const face = { label: "ALT", value: "12,4", unit: "km", tone: "normal" } as const;
		expect(renderFace(face)).toBe(renderFace({ ...face }));
	});

	it("réduit la taille de la valeur quand elle est longue", () => {
		expect(valueFontSize(3, 1)).toBeGreaterThan(valueFontSize(5, 1));
		expect(valueFontSize(5, 1)).toBeGreaterThan(valueFontSize(7, 1));
		expect(valueFontSize(8, 2)).toBeLessThan(valueFontSize(3, 1));
	});

	it("rend toutes les faces des quatre touches pour tous les états sans erreur", () => {
		const contexts = [
			context("offline", undefined),
			context("noVessel", { v: 1, type: "state", seq: 1, status: "noVessel" }),
			context("notControllable", flight({ status: "notControllable" })),
			ready(),
		];

		for (const ctx of contexts) {
			for (const face of [altitudeFace(ctx, "surface"), altitudeFace(ctx, "sea"), speedFace(ctx), throttleFace(ctx), statusFace(ctx)]) {
				expect(renderFaceSvg(face)).toContain("<svg");
			}
		}
	});
});
