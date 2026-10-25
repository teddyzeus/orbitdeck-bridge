import { describe, expect, it } from "vitest";

import { formatDistance, formatPercent, formatSpeed, NO_QUANTITY } from "../src/format/numbers.js";
import { resolveLocale, stringsFor } from "../src/i18n/strings.js";

describe("formatDistance", () => {
	it.each([
		[0, "0", "m"],
		[523.4, "523", "m"],
		[999.6, "1000", "m"],
		[9_999.4, "9999", "m"],
		[9_999.6, "10.0", "km"],
		[10_000, "10.0", "km"],
		[12_380.1, "12.4", "km"],
		[99_949, "99.9", "km"],
		[99_960, "100", "km"],
		[100_000, "100", "km"],
		[564_518, "565", "km"],
		[9_999_400, "9999", "km"],
		[9_999_600, "10.0", "Mm"],
		[11_400_000, "11.4", "Mm"],
		[99_999_999, "100", "Mm"],
		[1_200_000_000, "1200", "Mm"],
		[9_999_600_000, "10.0", "Gm"],
		[12_345_678_901, "12.3", "Gm"],
		[4e10, "40.0", "Gm"],
	])("%d m → %s %s", (meters, value, unit) => {
		expect(formatDistance(meters, "en")).toEqual({ value, unit });
	});

	it("utilise la virgule décimale en français", () => {
		expect(formatDistance(12_380.1, "fr")).toEqual({ value: "12,4", unit: "km" });
		expect(formatDistance(12_380.1, "en")).toEqual({ value: "12.4", unit: "km" });
	});

	it("garde le signe des altitudes négatives", () => {
		expect(formatDistance(-45.2, "en")).toEqual({ value: "-45", unit: "m" });
		expect(formatDistance(-12_000, "en")).toEqual({ value: "-12.0", unit: "km" });
	});

	it("n'affiche jamais « -0 »", () => {
		expect(formatDistance(-0.3, "en")).toEqual({ value: "0", unit: "m" });
	});

	it.each([null, undefined, Number.NaN, Number.POSITIVE_INFINITY])("valeur absente (%s) → tiret", (value) => {
		expect(formatDistance(value, "en")).toEqual(NO_QUANTITY);
	});

	it("tient en 5 caractères au plus pour les ordres de grandeur du jeu", () => {
		for (const meters of [0, 1, 99, 999, 9_999, 10_000, 55_555, 99_999, 123_456, 9_999_999, 12_345_678, 99_999_999, 1.2e9, 4e10]) {
			expect(formatDistance(meters, "fr").value.length).toBeLessThanOrEqual(5);
		}
	});
});

describe("formatSpeed", () => {
	it.each([
		[0, "0.0", "m/s"],
		[45.26, "45.3", "m/s"],
		[99.94, "99.9", "m/s"],
		[99.96, "100", "m/s"],
		[100, "100", "m/s"],
		[245.3, "245", "m/s"],
		[2_300.4, "2300", "m/s"],
		[9_999.4, "9999", "m/s"],
		[10_000, "10.00", "km/s"],
		[12_345, "12.35", "km/s"],
	])("%d m/s → %s %s", (speed, value, unit) => {
		expect(formatSpeed(speed, "en")).toEqual({ value, unit });
	});

	it("utilise la virgule décimale en français", () => {
		expect(formatSpeed(45.26, "fr").value).toBe("45,3");
	});

	it("gère les vitesses négatives (vitesse verticale)", () => {
		expect(formatSpeed(-12.34, "en")).toEqual({ value: "-12.3", unit: "m/s" });
	});

	it.each([null, undefined, Number.NaN])("valeur absente (%s) → tiret", (value) => {
		expect(formatSpeed(value, "en")).toEqual(NO_QUANTITY);
	});
});

describe("formatPercent", () => {
	it.each([
		[0, "0"],
		[0.5, "50"],
		[0.004, "0"],
		[0.995, "100"],
		[1, "100"],
		[1.7, "100"],
		[-0.3, "0"],
	])("%d → %s %", (fraction, value) => {
		expect(formatPercent(fraction)).toEqual({ value, unit: "%" });
	});

	it("valeur absente → tiret", () => {
		expect(formatPercent(undefined)).toEqual(NO_QUANTITY);
		expect(formatPercent(Number.NaN)).toEqual(NO_QUANTITY);
	});
});

describe("langue", () => {
	it.each([
		["fr", "fr"],
		["fr-FR", "fr"],
		["FR_ca", "fr"],
		["en", "en"],
		["de", "en"],
		["", "en"],
		[undefined, "en"],
	])("%s → %s", (language, expected) => {
		expect(resolveLocale(language)).toBe(expected);
	});

	it("les deux langues ont exactement les mêmes clés", () => {
		expect(Object.keys(stringsFor("fr")).sort()).toEqual(Object.keys(stringsFor("en")).sort());
	});
});
