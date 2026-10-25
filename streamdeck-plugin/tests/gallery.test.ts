// Générateur de la galerie de touches (`npm run gallery`) : sans effet dans les tests normaux.
import { mkdirSync, writeFileSync } from "node:fs";
import path from "node:path";
import { it } from "vitest";

import { stringsFor, type Locale } from "../src/i18n/strings.js";
import type { StateMessage } from "../src/protocol.js";
import { altitudeFace, speedFace, statusFace, throttleFace, type FaceContext } from "../src/rendering/faces.js";
import { renderFaceSvg } from "../src/rendering/renderFace.js";
import type { Availability } from "../src/state/Store.js";

function flight(over: Partial<StateMessage> = {}): StateMessage {
	return {
		v: 1, type: "state", seq: 1, status: "connected", gameState: "FlightView", vessel: { name: "Kerbal X" },
		altitude: { seaLevel: 564_518, surface: 561_207 },
		speed: { mode: "orbit", surface: 292.5, orbital: 2_326.9, target: 325.4 },
		throttle: 0.5, sas: { enabled: true, mode: "StabilityAssist" }, rcs: { enabled: false }, ...over,
	};
}

const ctx = (availability: Availability, state: StateMessage | undefined, locale: Locale = "fr"): FaceContext => ({
	availability, state, strings: stringsFor(locale), locale,
});

const output = process.env["GALLERY_OUT"];

it.skipIf(output === undefined)("génère la galerie des touches", () => {
	const scenarios: Array<[string, FaceContext]> = [
		["En vol (orbite)", ctx("ready", flight())],
		["Décollage", ctx("ready", flight({ altitude: { seaLevel: 85.4, surface: 12.3 }, speed: { mode: "surface", surface: 7.8, orbital: 8.1, target: null }, throttle: 1 }))],
		["Atmosphère", ctx("ready", flight({ altitude: { seaLevel: 12_400.5, surface: 12_380 }, speed: { mode: "surface", surface: 1_245.3, orbital: 1_500, target: null }, throttle: 0.73 }))],
		["Lointain", ctx("ready", flight({ altitude: { seaLevel: 11_400_000, surface: 11_390_000 }, speed: { mode: "orbit", surface: 0, orbital: 10_500, target: null }, throttle: 0 }))],
		["Non contrôlable", ctx("notControllable", flight({ status: "notControllable" }))],
		["Aucun véhicule", ctx("noVessel", { v: 1, type: "state", seq: 1, status: "noVessel" })],
		["Hors ligne", ctx("offline", undefined)],
		["Anglais", ctx("ready", flight(), "en")],
		["Anglais, hors ligne", ctx("offline", undefined, "en")],
	];

	// Les touches physiques font 72 px : la galerie les montre à cette taille (1x) et agrandies (2x).
	const key = (svg: string, size: number) => `<div class="k" style="width:${size}px;height:${size}px">${svg.replace("<svg ", '<svg style="width:100%;height:100%" ')}</div>`;
	const rows = scenarios.map(([name, c]) => {
		const faces = [altitudeFace(c, "surface"), altitudeFace(c, "sea"), speedFace(c), throttleFace(c), statusFace(c)].map(renderFaceSvg);
		return `<div class="r"><div class="n">${name}</div><div class="g">${faces.map((f) => key(f, 72)).join("")}</div><div class="g">${faces.map((f) => key(f, 120)).join("")}</div></div>`;
	});

	const html = `<!doctype html><meta charset="utf-8"><title>Touches StreamPlug</title>
<style>body{background:#1b1d22;color:#ddd;font:14px 'Segoe UI',sans-serif;margin:16px}
.r{display:flex;align-items:center;gap:18px;margin-bottom:12px}.n{width:140px}.g{display:flex;gap:6px}.k{border:1px solid #555;border-radius:6px;overflow:hidden}</style>
<h3>Touches StreamPlug (taille réelle à gauche, agrandie à droite)</h3>${rows.join("")}`;

	mkdirSync(path.dirname(output as string), { recursive: true });
	writeFileSync(output as string, html);
});
